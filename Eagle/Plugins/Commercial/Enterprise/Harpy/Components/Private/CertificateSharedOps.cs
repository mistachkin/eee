/*
 * CertificateSharedOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Components.Private.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using FlagOps = Licensing.Components.Private.CertificateFlagOps;
using LicenseState = Licensing.Components.Private.CertificateLicenseState;
using PathOps = Licensing.Components.Private.CertificatePathOps;
using RevocationOps = Licensing.Components.Private.CertificateRevocationOps;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using _PublicKeyToken = Eagle._Constants.PublicKeyToken;

#if NETWORK
using NetworkOps = Licensing.Components.Private.CertificateNetworkOps;
using NetworkState = Licensing.Components.Private.CertificateNetworkState;
using TimeOps = Licensing.Components.Private.CertificateTimeOps;
#endif

#if NETWORK && CERTIFICATE_RENEWAL
using RenewalOps = Licensing.Components.Private.CertificateRenewalOps;
#endif

#if CERTIFICATE_PLUGIN
using IsolatedState = Licensing.Components.Private.CertificateIsolatedState;
#endif

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
using PolicyOps = Licensing.Components.Private.CertificatePolicyOps;
#endif

using KeyUsageTriplet = Eagle._Components.Public.AnyTriplet<
    string, bool, bool>;

#if !NET_STANDARD_20
using RSAProvider = System.Security.Cryptography.RSACryptoServiceProvider;
using DSAProvider = System.Security.Cryptography.DSACryptoServiceProvider;
#else
using RSAProvider = System.Security.Cryptography.RSA;
using DSAProvider = System.Security.Cryptography.DSA;
#endif

#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
using BigCrypto;
#endif

using Utility = Eagle._Components.Public.Utility;
using VersionRange = Eagle._Components.Public.Pair<System.Version>;

using InterpreterPair = Eagle._Interfaces.Public.IAnyPair<
    Eagle._Components.Public.Interpreter, Eagle._Interfaces.Public.IClientData>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides shared helper operations used throughout the Harpy
    /// licensing certificate subsystem, including data hashing,
    /// signature verification, key usage checking, embedded resource
    /// access, time-server selection, and storage management.
    /// </summary>
    [ObjectId("34282b4c-0774-42bb-9dad-b0ec661c3d69")]
    internal static partial class CertificateSharedOps
    {
        #region Private Data
        //
        // NOTE: This is the number of times an RSA provider has been
        //       created by this class.
        //
        /// <summary>
        /// The number of times an RSA provider has been created by this
        /// class.
        /// </summary>
        private static long rsaProviderCount = 0;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the number of times an DSA provider has been
        //       created by this class.
        //
        /// <summary>
        /// The number of times a DSA provider has been created by this
        /// class.
        /// </summary>
        private static long dsaProviderCount = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        /// <summary>
        /// Determines whether any other application domains have been
        /// created or unloaded within the current process.
        /// </summary>
        /// <returns>
        /// Non-zero if other application domains have been created or
        /// unloaded.
        /// </returns>
        public static bool HaveOtherAppDomains() /* CORE */
        {
            long createCount = 0;
            long unloadCount = 0;

            /* NO RESULT */
            Utility.GetAppDomainCounts(
                false, ref createCount, ref unloadCount);

            return (createCount > 0) || (unloadCount > 0);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified command belongs to the Harpy
        /// plugin, based on the file name of its owning plugin.
        /// </summary>
        /// <param name="command">
        /// The command whose owning plugin is to be examined.
        /// </param>
        /// <returns>
        /// Non-zero if the command belongs to the Harpy plugin.
        /// </returns>
        public static bool IsCommandForPlugin( /* CORE */
            ICommand command /* in */
            )
        {
            if (command == null)
                return false;

            IPlugin plugin = command.Plugin;

            if (plugin == null)
                return false;

            return AssemblyOps.MatchFileName(plugin.FileName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs any one-time setup needed for the core library state,
        /// e.g. configuring the maximum number of web retries.
        /// </summary>
        public static void SetupForCoreLibraryState() /* CORE */
        {
#if NETWORK
            /* NO RESULT */
            SetupMaximumRetriesForWeb();
#endif
        }

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        /// <summary>
        /// Configures the maximum number of retries used for web requests,
        /// optionally overriding it from the process environment.
        /// </summary>
        private static void SetupMaximumRetriesForWeb() /* CORE */
        {
            int? newValue = NetworkState.GetMaximumRetries();

            string stringValue = Configuration.GetVariable(
                Constants.HarpyWebMaximumRetriesEnvVarName);

            if (!String.IsNullOrEmpty(stringValue))
            {
                int intValue = 0;

                if (Value.GetInteger2(
                        stringValue, ValueFlags.AnyInteger,
                        null, ref intValue) == ReturnCode.Ok)
                {
                    newValue = intValue;
                }
                else
                {
                    return;
                }
            }

            if (newValue != null)
            {
                int oldValue = Utility.SetWebMaximumRetries(
                    (int)newValue);

#if DEBUG || FORCE_TRACE
                TraceOps.DebugTrace(String.Format(
                    "SetupMaximumRetriesForWeb: " +
                    "oldValue = {0}, newValue = {1}",
                    oldValue, newValue),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow);
#endif
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Obfuscates (or de-obfuscates) the specified key bytes in place
        /// using the supplied mixing value.
        /// </summary>
        /// <param name="mix">
        /// The mixing value used to obfuscate the key bytes.
        /// </param>
        /// <param name="key">
        /// The key bytes to be obfuscated; upon return, contains the
        /// obfuscated result.
        /// </param>
        public static void ObfuscateKey( /* CORE */
            long mix,      /* in */
            ref byte[] key /* in, out */
            )
        {
            if (key == null)
                return;

            int keyLength = key.Length;

            if (keyLength == 0)
                return;

            if ((mix & byte.MaxValue) == 0)
                mix = (~mix & ~Constants.ObfuscateBitMask);

            byte[] mixKey = new byte[keyLength];

            for (int index = 0; index < keyLength; index++)
            {
                mixKey[index] = (byte)((key[index] ^
                    (mix & (Constants.ObfuscateBitMask << index))) &
                    byte.MaxValue);
            }

            key = mixKey;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to extract the embedded process identifier from the
        /// file description of the managed executable for the current
        /// process.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when parsing; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="pluginFlags">
        /// The plugin flags controlling verification and trust
        /// requirements.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The extracted process identifier, or null if it could not be
        /// obtained.
        /// </returns>
        public static Guid? TryExtractProcessId( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PluginFlags pluginFlags, /* in */
            ref Result error         /* out */
            )
        {
            string fileName = Utility.GetManagedExecutableName();

            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid process file name";
                return null;
            }

            if (!File.Exists(fileName))
            {
                error = String.Format(
                    "process file {0} does not exist",
                    Utility.FormatWrapOrNull(fileName));

                return null;
            }

            if (Utility.HasFlags(
                    pluginFlags, PluginFlags.VerifiedOnly, true) &&
                !Utility.IsFileStrongNameVerified(fileName))
            {
                error = String.Format(
                    "process file {0} is not verified",
                    Utility.FormatWrapOrNull(fileName));

                return null;
            }

            if (Utility.HasFlags(
                    pluginFlags, PluginFlags.TrustedOnly, true) &&
                !Utility.IsFileTrusted(interpreter, fileName))
            {
                error = String.Format(
                    "process file {0} is not trusted",
                    Utility.FormatWrapOrNull(fileName));

                return null;
            }

            FileVersionInfo versionInfo;

            try
            {
                versionInfo = FileVersionInfo.GetVersionInfo(
                    fileName); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }

            if (versionInfo == null)
                return null;

            string description = versionInfo.FileDescription;

            if (String.IsNullOrEmpty(description))
            {
                error = "invalid file description";
                return null;
            }

            Regex regEx = Constants.ProcessRegEx;

            if (regEx == null)
            {
                error = "missing pattern for process identifier";
                return null;
            }

            string idString;
            Match match = regEx.Match(description);

            if ((match != null) && match.Success)
            {
                try
                {
                    idString = match.Groups[1].Value; /* throw */
                }
                catch (Exception e)
                {
                    error = e;
                    return null;
                }
            }
            else
            {
                error = "missing embedded process identifier";
                return null;
            }

            Guid id = Guid.Empty;

            if (Value.GetGuid(idString,
                    cultureInfo, ref id, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return id;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the application domain associated with the specified
        /// plugin, falling back to the current application domain when
        /// necessary.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to query for its application domain; this
        /// parameter is optional and may be null.
        /// </param>
        /// <returns>
        /// The application domain for the plugin, or the current one.
        /// </returns>
        public static AppDomain GetAppDomainFromPlugin( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            AppDomain appDomain = null;

            if (pluginData != null)
                appDomain = pluginData.AppDomain;

            AppDomain currentAppDomain = AppDomain.CurrentDomain;

            if ((appDomain == null) && (currentAppDomain != null))
                appDomain = currentAppDomain;

#if DEBUG || FORCE_TRACE
            if (!Utility.IsCurrentAppDomain(appDomain))
            {
                string pluginName = (pluginData != null) ?
                    pluginData.Name : null;

                TraceOps.DebugTrace(String.Format(
                    "GetAppDomainFromPlugin: plugin {0} " +
                    "appDomain {1} does not match current {2}",
                    Utility.FormatWrapOrNull(pluginName),
                    Utility.FormatAppDomainId(appDomain, true),
                    Utility.FormatAppDomainId(currentAppDomain, true)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }
#endif

            return appDomain;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the Harpy plugin associated with the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the plugin.
        /// </param>
        /// <returns>
        /// The plugin, if found; otherwise, null.
        /// </returns>
        public static IPlugin GetPlugin( /* CORE */
            Interpreter interpreter /* in */
            )
        {
            Result error = null;

            return GetPlugin(interpreter, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the Harpy plugin associated with the specified interpreter,
        /// matching it by its enterprise public key token.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the plugin.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The plugin, if found; otherwise, null.
        /// </returns>
        private static IPlugin GetPlugin( /* CORE */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            IPlugin plugin; /* REUSED */

            if (interpreter == null)
                interpreter = Interpreter.GetAny();

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && LICENSING
            plugin = CertificatePluginState.GetPending(
                interpreter, Utility.GetCurrentThreadId(),
                ref error);

            if (plugin != null)
                return plugin;
#endif

            if (interpreter != null)
            {
                byte[] publicKeyToken =
                    Constants.EnterprisePublicKeyTokenBytes;

                if (publicKeyToken != null)
                {
                    plugin = interpreter.FindPlugin(
                        null, MatchMode.None, null, null,
                        publicKeyToken, false, ref error);

                    if (plugin != null)
                        return plugin;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER || (NETWORK && CERTIFICATE_RENEWAL)
        /// <summary>
        /// Gets the certificate storage directory for the specified plugin,
        /// optionally creating it.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to derive the directory name; this
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the directory if it does not exist.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while locating the directory.
        /// </param>
        /// <returns>
        /// The directory path, or null if none could be found.
        /// </returns>
        public static string GetDirectory(
            IPluginData pluginData, /* in: OPTIONAL */
            bool create,            /* in */
            ref ResultList errors   /* in, out */
            )
        {
            return GetDirectory(PathOps.GetPluginName(
                pluginData, PluginNameFlags.Directory), create,
                ref errors);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate storage directory for the named plugin by
        /// searching a series of well-known environment variables,
        /// optionally creating it.
        /// </summary>
        /// <param name="pluginName">
        /// The plugin name used as a subdirectory; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the directory if it does not exist.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while locating the directory.
        /// </param>
        /// <returns>
        /// The directory path, or null if none could be found.
        /// </returns>
        private static string GetDirectory(
            string pluginName,    /* in: OPTIONAL */
            bool create,          /* in */
            ref ResultList errors /* in, out */
            )
        {
            //
            // NOTE: Were there any errors leftover from previous
            //       calls?
            //
            int errorCount = (errors != null) ? errors.Count : 0;

            //
            // TODO: In the future, consider making this list of
            //       searched environment variables configurable.
            //
            foreach (string envVarName in new string[] {
                    EnvVars.XdgStateHome, EnvVars.XdgDataHome,
                    EnvVars.XdgConfigHome, EnvVars.Home,
                    EnvVars.UserProfile
                })
            {
                if (envVarName == null)
                    continue;

                string directory = Configuration.GetVariable(
                    envVarName);

                if (String.IsNullOrEmpty(directory))
                {
                    if (errorCount == 0)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(String.Format(
                            "no value from environment variable {0}",
                            Utility.FormatWrapOrNull(envVarName)));
                    }

                    continue;
                }

                string subDirectory = Path.Combine(
                    directory, Constants.DefaultDirectoryName);

                if (pluginName != null)
                {
                    subDirectory = Path.Combine(
                        subDirectory, pluginName);
                }

                if (create)
                {
                    if (Directory.Exists(subDirectory))
                    {
                        return subDirectory;
                    }
                    else
                    {
                        try
                        {
                            Directory.CreateDirectory(
                                subDirectory); /* throw */

                            return subDirectory;
                        }
                        catch (Exception e)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(e);
                        }
                    }
                }
                else if (Directory.Exists(subDirectory))
                {
                    return subDirectory;
                }
                else
                {
                    Result error = String.Format(
                        "plugin {0} subdirectory {1} does not exist",
                        Utility.FormatWrapOrNull(pluginName),
                        Utility.FormatWrapOrNull(subDirectory));

                    if ((errors == null) || !DataOps.StringEquals(
                            errors[errors.Count - 1], error))
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(error);
                    }
                }
            }

            if (errorCount == 0)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Insert(0, "no usable certificate directory found");
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the fully qualified file name corresponding to the
        /// specified hash value within the certificate storage directory
        /// for a plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to derive the directory; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="hashValue">
        /// The hash value used to form the file name.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the containing directory if needed.
        /// </param>
        /// <returns>
        /// The file name, or null if it could not be determined.
        /// </returns>
        public static string GetHashFileName(
            IPluginData pluginData, /* in: OPTIONAL */
            byte[] hashValue,       /* in */
            bool create             /* in */
            )
        {
            Result error; /* REUSED */

            if (hashValue == null)
            {
                error = "invalid hash value";
                goto fail;
            }

            string fileNameOnly = DataOps.FormatHashFileNameOnly(
                hashValue);

            ResultList errors = null;

            foreach (IPluginData localPluginData in
                    new IPluginData[] { pluginData, null })
            {
                string pluginName = PathOps.GetPluginName(
                    localPluginData, PluginNameFlags.Directory);

                string directory = GetDirectory(
                    pluginName, create, ref errors);

                if (directory == null)
                    continue;

                string fileName = Path.Combine(directory, fileNameOnly);

                if (!create && !File.Exists(fileName))
                {
                    Result localError = String.Format(
                        "plugin {0} file {1} does not exist",
                        Utility.FormatWrapOrNull(pluginName),
                        Utility.FormatWrapOrNull(fileName));

                    if ((errors == null) || !DataOps.StringEquals(
                            errors[errors.Count - 1], localError))
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    continue;
                }

                return fileName;
            }

            error = errors;

        fail:

#if DEBUG || FORCE_TRACE
            TraceOps.DebugTrace(String.Format(
                "GetHashFileName: pluginData = {0}, " +
                "hashValue = {1}, create = {2}, " +
                "error = {3}", Utility.FormatWrapOrNull(
                    (pluginData != null) ? pluginData.Name : null),
                DataOps.FormatHexadecimal(hashValue),
                create, Utility.FormatWrapOrNull(error)),
                typeof(CertificateSharedOps).Name,
                TracePriority.MediumLow);
#endif

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the first key pair from the specified sequence whose public
        /// key token matches the one provided.
        /// </summary>
        /// <param name="keyPairs">
        /// The sequence of key pairs to search.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to match against.
        /// </param>
        /// <returns>
        /// The matching key pair, or null if none was found.
        /// </returns>
        public static IKeyPair GetKeyPairByPublicKeyToken( /* CORE */
            IEnumerable<IKeyPair> keyPairs, /* in */
            byte[] publicKeyToken           /* in */
            )
        {
            if ((keyPairs == null) || (publicKeyToken == null))
                return null;

            foreach (IKeyPair keyPair in keyPairs)
            {
                if (keyPair == null)
                    continue;

                byte[] localPublicKeyToken = keyPair.PublicKeyToken;

                if (localPublicKeyToken == null)
                    continue;

                if (DataOps.MatchPublicKeyToken(
                        localPublicKeyToken, publicKeyToken))
                {
                    return keyPair;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a short string representation of the specified
        /// certificate, suitable for use in diagnostic messages.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to format; this parameter is optional and may
        /// be null.
        /// </param>
        /// <returns>
        /// A wrapped string containing the certificate identifier.
        /// </returns>
        public static string ToString( /* CORE */
            ICertificate certificate /* in */
            )
        {
            //
            // NOTE: Avoid returning too much information about the
            //       certificate here as this method is used mainly
            //       for debugging purposes.
            //
            return Utility.FormatWrapOrNull((certificate != null) ?
                certificate.Id.ToString(Constants.DefaultGuidFormat) :
                null);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Adjusts the specified options for use with an isolated plugin,
        /// when isolated plugin support is available.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data associated with the options.
        /// </param>
        /// <param name="options">
        /// The options to be adjusted.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enable strict processing.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode FixupOptions(
            IPluginData pluginData,   /* in */
            OptionDictionary options, /* in */
            bool strict,              /* in: EXEMPT */
            ref Result error          /* out */
            )
        {
            #region Needs Isolated Plugin Support
#if ISOLATED_PLUGINS
            return Utility.FixupOptions(
                pluginData, options, strict, ref error);
#else
            return ReturnCode.Ok;
#endif
            #endregion
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether plugins should be treated as isolated, based
        /// on an override from the process environment.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <returns>
        /// Non-zero or zero per the environment override, or null if no
        /// override is present.
        /// </returns>
        private static bool? ShouldTreatAsIsolated( /* CORE */
            Interpreter interpreter /* in: OPTIONAL */
            )
        {
            string value = Configuration.GetVariable(
                Constants.TreatAsIsolatedEnvVarName);

            if (value != null)
            {
                CultureInfo cultureInfo;

                /* NO RESULT */
                DataOps.SafeGetCultureInfo(
                    interpreter, out cultureInfo);

                bool? boolValue = null;
                Result error = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref boolValue, ref error) != ReturnCode.Ok)
                {
                    //
                    // HACK: This was an override and it failed;
                    //       so, complain loudly.
                    //
                    Utility.Complain(
                        interpreter, ReturnCode.Error, error);

                    return null;
                }

                return boolValue;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified plugin should be treated as
        /// residing in a separate application domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to examine; this parameter is optional and may
        /// be null.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin should be treated as cross-application
        /// domain.
        /// </returns>
        public static bool IsCrossAppDomain( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in: OPTIONAL */
            )
        {
            bool? treatAsIsolated = ShouldTreatAsIsolated(interpreter);

            if (treatAsIsolated != null)
                return (bool)treatAsIsolated;

            return (interpreter != null) ?
                Utility.IsCrossAppDomain(
                    interpreter, pluginData, !Utility.IsDotNetCore()) :
                Utility.IsCrossAppDomain(
                    pluginData, !Utility.IsDotNetCore());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name has a recognized
        /// script file extension.
        /// </summary>
        /// <param name="fileName">
        /// The file name to examine.
        /// </param>
        /// <returns>
        /// Non-zero if the file name appears to be a script file.
        /// </returns>
        public static bool IsScriptFileName( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            string fileExtension = Path.GetExtension(fileName);

            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.Script))
            {
                return true;
            }

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            //
            // TODO: Should the #ifdef around this check be removed?
            //       The question is do we want to return true here
            //       even if the code to handle an encrypted script
            //       file is not present in this assembly?
            //
            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.EncryptedScript))
            {
                return true;
            }
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name has a recognized
        /// assembly (library or executable) file extension.
        /// </summary>
        /// <param name="fileName">
        /// The file name to examine.
        /// </param>
        /// <returns>
        /// Non-zero if the file name appears to be an assembly file.
        /// </returns>
        public static bool IsAssemblyFileName( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            string fileExtension = Path.GetExtension(fileName);

            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.Library))
            {
                return true;
            }

            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.Executable))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name has a recognized
        /// signature file extension and, if so, extracts the base file
        /// name.
        /// </summary>
        /// <param name="fileName">
        /// The file name to examine.
        /// </param>
        /// <param name="baseFileName">
        /// Upon return, receives the file name with the signature
        /// extension removed.
        /// </param>
        /// <returns>
        /// Non-zero if the file name appears to be a signature file.
        /// </returns>
        public static bool IsSignatureFileName( /* CORE */
            string fileName,        /* in */
            ref string baseFileName /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            string haveFileExtension = Path.GetExtension(
                fileName);

            if (String.IsNullOrEmpty(haveFileExtension))
                return false;

            string wantFileExtension; /* REUSED */
            int fileNameLength = fileName.Length;

            wantFileExtension = FileExtension.Signature;

            if (wantFileExtension != null)
            {
                if (DataOps.PathStringEquals(
                        haveFileExtension, wantFileExtension))
                {
                    baseFileName = fileName.Substring(0,
                        fileNameLength - wantFileExtension.Length);

                    return true;
                }
            }

            wantFileExtension = FileExtension.Base64Signature;

            if (wantFileExtension != null)
            {
                if (DataOps.PathStringEquals(
                        haveFileExtension, wantFileExtension))
                {
                    baseFileName = fileName.Substring(0,
                        fileNameLength - wantFileExtension.Length);

                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name has a recognized
        /// encrypted markup or script file extension.
        /// </summary>
        /// <param name="fileName">
        /// The file name to examine.
        /// </param>
        /// <returns>
        /// Non-zero if the file name appears to be an encrypted file.
        /// </returns>
        public static bool IsEncryptedFileName( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            string fileExtension = Path.GetExtension(fileName);

            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.EncryptedMarkup))
            {
                return true;
            }

            if (DataOps.PathStringEquals(
                    fileExtension, FileExtension.EncryptedScript))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the enterprise public key token to require, unless any
        /// resource public key is permitted.
        /// </summary>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to permit any resource public key, which causes null
        /// to be returned.
        /// </param>
        /// <returns>
        /// The enterprise public key token bytes, or null.
        /// </returns>
        private static byte[] MaybeGetPublicKeyToken( /* CORE */
            bool anyResourcePublicKey /* in */
            )
        {
            return anyResourcePublicKey ?
                null : Constants.EnterprisePublicKeyTokenBytes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies an assembly contained in the specified byte array by
        /// writing it to a temporary file and validating its strong name.
        /// </summary>
        /// <param name="fileName">
        /// The original file name of the assembly.
        /// </param>
        /// <param name="bytes">
        /// The raw bytes of the assembly to verify.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to require; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the assembly was verified successfully.
        /// </returns>
        private static bool VerifyAssemblyFromBytes( /* CORE */
            string fileName,       /* in */
            byte[] bytes,          /* in */
            byte[] publicKeyToken, /* in: OPTIONAL */
            ref Result error       /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid assembly file name";
                return false;
            }

            if (bytes == null)
            {
                error = "invalid assembly bytes";
                return false;
            }

            string temporaryDirectory = null;

            try
            {
                temporaryDirectory = Utility.GetUniquePath(
                    null, Utility.GetTempPath(null), null,
                    null, ref error);

                if (temporaryDirectory == null)
                    return false;

                Directory.CreateDirectory(
                    temporaryDirectory); /* throw */

                string temporaryFileName = Path.Combine(
                    temporaryDirectory, fileName);

                File.WriteAllBytes(
                    temporaryFileName, bytes); /* throw */

                return VerifyAssemblyFromFile(
                    temporaryFileName, publicKeyToken, ref error);
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                if (temporaryDirectory != null)
                {
                    /* IGNORED */
                    Utility.CleanupDirectory(temporaryDirectory,
                        new string[] { fileName }, true);
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the assembly in the specified file by validating its
        /// strong name against the supplied public key token.
        /// </summary>
        /// <param name="fileName">
        /// The file name of the assembly to verify.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to require; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the assembly was verified successfully.
        /// </returns>
        private static bool VerifyAssemblyFromFile( /* CORE */
            string fileName,       /* in */
            byte[] publicKeyToken, /* in: OPTIONAL */
            ref Result error       /* out */
            )
        {
            return (Utility.VerifyAssemblyFromFile(fileName,
                publicKeyToken, null, ref error) == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the data for the specified file, which may be a local file
        /// or a remote URI, returning it as text or raw bytes as
        /// appropriate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="fileName">
        /// The file name or remote URI to read.
        /// </param>
        /// <param name="timeout">
        /// The optional download timeout, in milliseconds.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to permit reading from a remote URI.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to permit any resource public key.
        /// </param>
        /// <param name="raw">
        /// Non-zero to return the data as raw bytes.
        /// </param>
        /// <param name="useResource">
        /// On input, whether assembly resource use is permitted; on
        /// return, whether the data should be treated as a resource.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The file data as a string or byte array, or null on failure.
        /// </returns>
        public static object GetDataFromFile( /* CORE */
            Interpreter interpreter,   /* in: OPTIONAL */
            Encoding encoding,         /* in: OPTIONAL */
            string fileName,           /* in */
            int? timeout,              /* in: OPTIONAL */
            bool allowRemoteUri,       /* in */
            bool anyResourcePublicKey, /* in */
            bool raw,                  /* in */
            ref bool useResource,      /* in, out */
            ref Result error           /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid file name";
                return null;
            }

            bool remoteUri = Utility.IsRemoteUri(fileName); /* EXEMPT */

            if (remoteUri)
            {
                if (!allowRemoteUri)
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Skipping file name {0}, it is a remote URI...",
                        Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    error = "remote uri not allowed";
                    return null;
                }
            }
            else
            {
                if (!LicenseState.HaveCachedFile(fileName) &&
                    !File.Exists(fileName))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Skipping file name {0}, it does not exist...",
                        Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    error = "file does not exist";
                    return null;
                }
            }

            bool isAssembly = IsAssemblyFileName(fileName);

            if (isAssembly && !useResource)
            {
                error = "assembly resource use forbidden";
                return null;
            }

            try
            {
                byte[] publicKeyToken = MaybeGetPublicKeyToken(
                    anyResourcePublicKey);

                if (remoteUri)
                {
#if NETWORK
                    bool wasCached;
                    object data;

                    wasCached = LicenseState.TryGetCachedFile(
                        fileName, out data);

                    if (wasCached && (data == null))
                    {
#if DEBUG || FORCE_TRACE
                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                            "Skipping file name {0}, invalid cached data...",
                            Utility.FormatWrapOrNull(fileName)),
                            typeof(CertificateSharedOps).Name,
                            TracePriority.Lower, 0);
#endif

                        error = "invalid cached data";
                        return null;
                    }

                    if (!wasCached)
                    {
#if TEST
                        if (Utility.SetWebSecurityProtocol(
                                false, ref error) != ReturnCode.Ok)
                        {
                            return null;
                        }
#endif

                        data = NetworkOps.DownloadData(
                            interpreter, fileName, timeout, isAssembly ||
                            ((encoding == null) && raw), ref error);

                        if (data == null)
                            return null;
                    }

                    if (isAssembly)
                    {
                        if (!VerifyAssemblyFromBytes(
                                Path.GetFileName(fileName), data as byte[],
                                publicKeyToken, ref error))
                        {
#if DEBUG || FORCE_TRACE
                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                "Skipping file name {0}, bad assembly...",
                                Utility.FormatWrapOrNull(fileName)),
                                typeof(CertificateSharedOps).Name,
                                TracePriority.Lower, 0);
#endif

                            return null;
                        }

                        useResource = true; /* REDUNDANT */
                        return data;
                    }
                    else if ((encoding != null) && raw)
                    {
                        //
                        // TODO: The WebClient class uses the Encoding
                        //       property to download the string and
                        //       then we use our encoding parameter
                        //       (which could be the same or different
                        //       from the default Encoding property
                        //       value of the WebClient)?
                        //
                        useResource = false;
                        return encoding.GetBytes((string)data); /* throw */
                    }
                    else
                    {
                        useResource = false;
                        return data;
                    }
#else
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Skipping file name {0}, no network support...",
                        Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "not implemented";
                    return null;
#endif
                }
                else
                {
                    if (isAssembly || raw)
                    {
                        if (!isAssembly && (encoding != null))
                        {
                            useResource = false;

                            string text;

                            if (LicenseState.TryGetCachedTextFile(
                                    fileName, out text))
                            {
                                //
                                // BUGBUG: Assumes that the text was
                                //         originally read using the
                                //         same encoding specified by
                                //         the caller.
                                //
                                return encoding.GetBytes(text); /* throw */
                            }
                            else
                            {
                                //
                                // TODO: ReadAllText uses its encoding
                                //       argument and then we use it
                                //       again to get the underlying
                                //       bytes for the string?
                                //
                                return encoding.GetBytes(File.ReadAllText(
                                    fileName, encoding)); /* throw */
                            }
                        }
                        else
                        {
                            byte[] bytes;

                            if (!LicenseState.TryGetCachedBinaryFile(
                                    fileName, out bytes))
                            {
                                bytes = File.ReadAllBytes(fileName); /* throw */
                            }

                            if (isAssembly && !VerifyAssemblyFromBytes(
                                    Path.GetFileName(fileName), bytes,
                                    publicKeyToken, ref error))
                            {
#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Skipping file name {0}, bad assembly...",
                                    Utility.FormatWrapOrNull(fileName)),
                                    typeof(CertificateSharedOps).Name,
                                    TracePriority.Lower, 0);
#endif

                                return null;
                            }

                            useResource = isAssembly;
                            return bytes;
                        }
                    }
                    else
                    {
                        useResource = false;

                        string text;

                        if (LicenseState.TryGetCachedTextFile(
                                fileName, out text))
                        {
                            return text;
                        }
                        else
                        {
                            return File.ReadAllText(fileName); /* throw */
                        }
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Skipping file name {0}, could not get data: {1}",
                    Utility.FormatWrapOrNull(fileName),
                    Utility.FormatTraceException(e)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines which hash algorithm name should be used, considering
        /// the command, script, remote, certificate, environment, and
        /// system defaults in priority order.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The caller-specified hash algorithm name; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs that may be relevant; this parameter is optional
        /// and is not used.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose hash algorithm may be used; this
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="hashAlgorithmType">
        /// The kind of hash algorithm use being requested.
        /// </param>
        /// <returns>
        /// The selected hash algorithm name, or null if none is available.
        /// </returns>
        public static string GetHashAlgorithm( /* CORE */
            string hashAlgorithmName,           /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,     /* in: OPTIONAL, NOT USED */
            ICertificate certificate,           /* in: OPTIONAL */
            HashAlgorithmType hashAlgorithmType /* in */
            )
        {
            string value;

            //
            // NOTE: First, if this hash algorithm name was explicitly set by
            //       a script command, always use it.  Nothing else matters,
            //       if the script selects a hash algorithm name that is not
            //       compatible with a remote server, an error will be raised
            //       and the script will have to deal with it.
            //
            if (HasFlags(hashAlgorithmType, HashAlgorithmType.CommandUse, true))
            {
                value = hashAlgorithmName;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the command ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            //
            // NOTE: Next, if this hash algorithm is for use when reading a
            //       local script file, always use the default script hash
            //       algorithm in this case.
            //
            if (HasFlags(hashAlgorithmType, HashAlgorithmType.ScriptUse, true))
            {
                value = Constants.ScriptHashAlgorithmName;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the script ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            //
            // NOTE: If this hash algorithm is for use with a (remote) server,
            //       we cannot simply use whatever algorithm is configured for
            //       a local certificate.  Always use the default remote hash
            //       algorithm in this case.
            //
            if (HasFlags(hashAlgorithmType, HashAlgorithmType.RemoteUse, true))
            {
                value = Constants.RemoteHashAlgorithmName;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the remote ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            //
            // NOTE: Otherwise, at this point, if the hash algorithm is purely
            //       optional, just stop now (and maybe return null).  Do not
            //       attempt to fallback on any certificate or system defaults.
            //
            if (HasFlags(hashAlgorithmType, HashAlgorithmType.OptionalUse, true))
            {
                value = Constants.OptionalHashAlgorithmName;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the optional ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            //
            // NOTE: Otherwise, if the certificate has a hash algorithm set,
            //       we MUST use it; otherwise, it will most likely fail to
            //       verify (i.e. unless it happens to use the default hash
            //       algorithm).
            //
            if (certificate != null)
            {
                value = certificate.HashAlgorithm;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the certificate ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            //
            // NOTE: Otherwise, if a hash algorithm was explicitly specified
            //       by the caller, use it.
            //
            value = hashAlgorithmName;

            if (!String.IsNullOrEmpty(value))
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Using the specified ({0}) hash algorithm {1}...",
                    Utility.FormatWrapOrNull(hashAlgorithmType),
                    Utility.FormatWrapOrNull(value)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.Lower, 0);
#endif

                return value;
            }

            //
            // NOTE: Otherwise, attempt to query the hash algorithm to use
            //       from the process environment.
            //
            value = Configuration.GetVariable(
                Constants.HashAlgorithmEnvVarName);

            if (!String.IsNullOrEmpty(value))
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Using the environment ({0}) hash algorithm {1}...",
                    Utility.FormatWrapOrNull(hashAlgorithmType),
                    Utility.FormatWrapOrNull(value)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.Lower, 0);
#endif

                return value;
            }

            //
            // NOTE: Finally, fallback to a system default hash algorithm,
            //       based on the legacy flag.
            //
            if (HasFlags(hashAlgorithmType, HashAlgorithmType.Legacy, true))
            {
                value = Constants.LegacyHashAlgorithmName;

                if (!String.IsNullOrEmpty(value))
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Using the legacy ({0}) hash algorithm {1}...",
                        Utility.FormatWrapOrNull(hashAlgorithmType),
                        Utility.FormatWrapOrNull(value)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.Lower, 0);
#endif

                    return value;
                }
            }

            value = Constants.LocalHashAlgorithmName;

            if (!String.IsNullOrEmpty(value))
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Using the local ({0}) hash algorithm {1}...",
                    Utility.FormatWrapOrNull(hashAlgorithmType),
                    Utility.FormatWrapOrNull(value)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.Lower, 0);
#endif

                return value;
            }

#if DEBUG || FORCE_TRACE
            TraceOps.MaybeLogAndDebugTrace(
                "No configured hash algorithm is available.",
                typeof(CertificateSharedOps).Name,
                TracePriority.Low, 0);
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of RSA providers that have been created by this
        /// class.
        /// </summary>
        /// <returns>
        /// The current RSA provider creation count.
        /// </returns>
        public static long GetRsaProviderCount()
        {
            return Interlocked.CompareExchange(
                ref rsaProviderCount, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of DSA providers that have been created by this
        /// class.
        /// </summary>
        /// <returns>
        /// The current DSA provider creation count.
        /// </returns>
        public static long GetDsaProviderCount()
        {
            return Interlocked.CompareExchange(
                ref dsaProviderCount, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new RSA provider instance appropriate for the current
        /// runtime.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new RSA provider, or null on failure.
        /// </returns>
        public static RSA CreateRsaProvider( /* CORE */
            ref Result error /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            DateTime start = Utility.GetUtcNow();
#endif

            RSA provider = null;

            try
            {
#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
                if (BigRSACryptoServiceProvider.IsEnabled())
                {
                    provider = new BigRSACryptoServiceProvider();

                    if (provider != null)
                        Interlocked.Increment(ref rsaProviderCount);

                    return provider;
                }
#endif

#if !NET_STANDARD_20
                //
                // BUGBUG: This line is a work-around for MS KB Q322371: "CSP
                //         for this implementation could not be acquired"
                //         CryptographicException error during instantiation.
                //
                RSAProvider.UseMachineKeyStore = true;

                provider = new RSAProvider();
#elif OPEN_SSL
                provider = new RSAOpenSsl();
#else
                provider = RSAProvider.Create();
#endif

                if (provider != null)
                    Interlocked.Increment(ref rsaProviderCount);

                return provider;
            }
            catch (Exception e)
            {
                error = e;
            }
#if DEBUG || FORCE_TRACE
            finally
            {
                DateTime stop = Utility.GetUtcNow();

                TraceOps.DebugTrace(String.Format(
                    "CreateRsaProvider(1, {0}): Took {1} milliseconds.",
                    DataOps.MaybeNullOrEmpty(provider),
                    stop.Subtract(start).TotalMilliseconds),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new DSA provider instance appropriate for the current
        /// runtime.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new DSA provider, or null on failure.
        /// </returns>
        public static DSA CreateDsaProvider( /* CORE */
            ref Result error /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            DateTime start = Utility.GetUtcNow();
#endif

            DSA provider = null;

            try
            {
#if !NET_STANDARD_20
                //
                // BUGBUG: This line is a work-around for MS KB Q322371: "CSP
                //         for this implementation could not be acquired"
                //         CryptographicException error during instantiation.
                //
                DSAProvider.UseMachineKeyStore = true;

                provider = new DSAProvider();
#elif OPEN_SSL
                provider = new DSAOpenSsl();
#else
                provider = DSAProvider.Create();
#endif

                if (provider != null)
                    Interlocked.Increment(ref dsaProviderCount);

                return provider;
            }
            catch (Exception e)
            {
                error = e;
            }
#if DEBUG || FORCE_TRACE
            finally
            {
                DateTime stop = Utility.GetUtcNow();

                TraceOps.DebugTrace(String.Format(
                    "CreateDsaProvider(1, {0}): Took {1} milliseconds.",
                    DataOps.MaybeNullOrEmpty(provider),
                    stop.Subtract(start).TotalMilliseconds),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Creates a new RSA provider instance using the specified key size
        /// and CSP parameters.
        /// </summary>
        /// <param name="keySize">
        /// The key size, in bits; zero requests the default size.
        /// </param>
        /// <param name="parameters">
        /// The CSP parameters to use when creating the provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new RSA provider, or null on failure.
        /// </returns>
        public static RSA CreateRsaProvider(
            int keySize,       /* in */
            object parameters, /* in */
            ref Result error   /* out */
            )
        {
            if (parameters == null)
            {
                error = "invalid RSA parameters";
                return null;
            }

            CspParameters cspParameters = parameters as CspParameters;

            if (cspParameters == null)
            {
                error = "invalid CSP parameters";
                return null;
            }

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            DateTime start = Utility.GetUtcNow();
#endif

            RSA provider = null;

            try
            {
#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
                if (BigRSACryptoServiceProvider.IsEnabled())
                {
                    if (keySize == 0)
                        keySize = Constants.DefaultRsaKeySize;

                    provider = new BigRSACryptoServiceProvider(
                        keySize, cspParameters);

                    if (provider != null)
                        Interlocked.Increment(ref rsaProviderCount);

                    return provider;
                }
#endif

#if !NET_STANDARD_20
                //
                // BUGBUG: This line is a work-around for MS KB Q322371: "CSP
                //         for this implementation could not be acquired"
                //         CryptographicException error during instantiation.
                //
                RSAProvider.UseMachineKeyStore = true;

                //
                // HACK: Apparently, Mono is not capable of dealing with key
                //       sizes of zero.  When using the .NET Framework, zero
                //       means "use the default value".
                //
                if ((keySize == 0) && Utility.IsMono())
                    keySize = Constants.DefaultRsaKeySize;

                provider = new RSAProvider(keySize, cspParameters);
#elif OPEN_SSL
                provider = new RSAOpenSsl(keySize);
#else
                provider = RSAProvider.Create();

                if (provider != null)
                    provider.KeySize = keySize;
#endif

                if (provider != null)
                    Interlocked.Increment(ref rsaProviderCount);

                return provider;
            }
            catch (Exception e)
            {
                error = e;
            }
#if DEBUG || FORCE_TRACE
            finally
            {
                DateTime stop = Utility.GetUtcNow();

                TraceOps.DebugTrace(String.Format(
                    "CreateRsaProvider(3, {0}): Took {1} milliseconds.",
                    DataOps.MaybeNullOrEmpty(provider),
                    stop.Subtract(start).TotalMilliseconds),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new DSA provider instance using the specified key size
        /// and CSP parameters.
        /// </summary>
        /// <param name="keySize">
        /// The key size, in bits; zero requests the default size.
        /// </param>
        /// <param name="parameters">
        /// The CSP parameters to use when creating the provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new DSA provider, or null on failure.
        /// </returns>
        public static DSA CreateDsaProvider(
            int keySize,       /* in */
            object parameters, /* in */
            ref Result error   /* out */
            )
        {
            if (parameters == null)
            {
                error = "invalid DSA parameters";
                return null;
            }

            CspParameters cspParameters = parameters as CspParameters;

            if (cspParameters == null)
            {
                error = "invalid CSP parameters";
                return null;
            }

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            DateTime start = Utility.GetUtcNow();
#endif

            DSA provider = null;

            try
            {
#if !NET_STANDARD_20
                //
                // BUGBUG: This line is a work-around for MS KB Q322371: "CSP
                //         for this implementation could not be acquired"
                //         CryptographicException error during instantiation.
                //
                DSAProvider.UseMachineKeyStore = true;

                //
                // HACK: Apparently, Mono is not capable of dealing with key
                //       sizes of zero.  When using the .NET Framework, zero
                //       means "use the default value".
                //
                if ((keySize == 0) && Utility.IsMono())
                    keySize = Constants.DefaultDsaKeySize;

                provider = new DSAProvider(keySize, cspParameters);
#elif OPEN_SSL
                provider = new DSAOpenSsl(keySize);
#else
                provider = DSAProvider.Create();

                if (provider != null)
                    provider.KeySize = keySize;
#endif

                if (provider != null)
                    Interlocked.Increment(ref dsaProviderCount);

                return provider;
            }
            catch (Exception e)
            {
                error = e;
            }
#if DEBUG || FORCE_TRACE
            finally
            {
                DateTime stop = Utility.GetUtcNow();

                TraceOps.DebugTrace(String.Format(
                    "CreateDsaProvider(3, {0}): Took {1} milliseconds.",
                    DataOps.MaybeNullOrEmpty(provider),
                    stop.Subtract(start).TotalMilliseconds),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }
#endif

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and initializes a hash algorithm with the specified
        /// name, optionally applying a keyed hash key.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The key to use for a keyed hash algorithm; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new hash algorithm, or null on failure.
        /// </returns>
        private static HashAlgorithm CreateHashAlgorithm( /* CORE */
            string hashAlgorithmName, /* in: OPTIONAL */
            byte[] hashKey,           /* in: OPTIONAL */
            ref Result error          /* out */
            )
        {
            HashAlgorithm hashAlgorithm = Utility.CreateHashAlgorithm(
                hashAlgorithmName, ref error);

            if (hashAlgorithm != null)
            {
                hashAlgorithm.Initialize();

                if (hashKey != null)
                {
                    KeyedHashAlgorithm keyedHashAlgorithm =
                        hashAlgorithm as KeyedHashAlgorithm;

                    if (keyedHashAlgorithm != null)
                        keyedHashAlgorithm.Key = hashKey;
                }
            }

            return hashAlgorithm;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified certificate.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to hash.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included in the hash.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode Hash( /* CORE */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in */
            Encoding encoding,                          /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.Full, ref list);

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash certificate success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "encoding = {4}, localByteLength = {5}, " +
                    "localBytes = {6}, hashBytes = {7}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    Utility.FormatWrapOrNull(encoding),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified byte array.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="value">
        /// The bytes to hash; this parameter is optional and may be null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashBytes( /* CORE */
            string hashAlgorithmName, /* in: OPTIONAL */
            byte[] hashKey,           /* in: OPTIONAL */
            byte[] value,             /* in: OPTIONAL */
            ref byte[] hashBytes,     /* out */
            ref Result error          /* out */
            )
        {
            try
            {
                using (HashAlgorithm hashAlgorithm = CreateHashAlgorithm(
                        hashAlgorithmName, hashKey, ref error))
                {
                    if (hashAlgorithm != null)
                    {
                        hashBytes = hashAlgorithm.ComputeHash(value);
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }

#if DEBUG || FORCE_TRACE
                int valueLength = (value != null) ?
                    value.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash bytes success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "valueLength = {2}, value = {3}, " +
                    "hashBytes = {4}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    valueLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(value)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// supplied byte array.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="value">
        /// The additional bytes to hash; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode HashBytes(
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            byte[] value,                               /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.Bytes, ref list);

                if (value != null)
                    list.AddRange(value);

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int valueLength = (value != null) ?
                    value.Length : Length.Invalid;

                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash certificate and bytes success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "valueLength = {4}, value = {5}, " +
                    "localByteLength = {6}, localBytes = {7}, " +
                    "hashBytes = {8}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    valueLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(value)),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Low);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified string value.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="value">
        /// The string to hash; this parameter is optional and may be null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashString( /* CORE */
            string hashAlgorithmName, /* in: OPTIONAL */
            byte[] hashKey,           /* in: OPTIONAL */
            Encoding encoding,        /* in: OPTIONAL */
            string value,             /* in: OPTIONAL */
            ref byte[] hashBytes,     /* out */
            ref Result error          /* out */
            )
        {
            try
            {
                ByteList list = new ByteList();

                if (!String.IsNullOrEmpty(value))
                {
                    if (encoding != null)
                    {
                        list.MaybeAddRange(encoding.GetBytes(value));
                    }
                    else
                    {
                        list.MaybeAddRange(
                            DataOps.GetRawBytes(value));
                    }
                }

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int valueLength = (value != null) ?
                    value.Length : Length.Invalid;

                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash string success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "encoding = {2}, valueLength = {3}, value = {4}, " +
                    "localByteLength = {5}, localBytes = {6}, " +
                    "hashBytes = {7}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    Utility.FormatWrapOrNull(encoding),
                    valueLength, Utility.FormatWrapOrNull(true, true,
                        value),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// supplied string value.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="value">
        /// The string to hash; this parameter is optional and may be null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashString( /* CORE */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            string value,                               /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.String, ref list);

                if (!String.IsNullOrEmpty(value))
                {
                    if (encoding != null)
                    {
                        list.MaybeAddRange(encoding.GetBytes(value));
                    }
                    else
                    {
                        list.MaybeAddRange(
                            DataOps.GetRawBytes(value));
                    }
                }

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int valueLength = (value != null) ?
                    value.Length : Length.Invalid;

                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash certificate and string success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "encoding = {4}, valueLength = {5}, value = {6}, " +
                    "localByteLength = {7}, localBytes = {8}, " +
                    "hashBytes = {9}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    Utility.FormatWrapOrNull(encoding),
                    valueLength, Utility.FormatWrapOrNull(true, true,
                        value),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// supplied string value and byte list.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="value">
        /// The string to hash; this parameter is optional and may be null.
        /// </param>
        /// <param name="bytes">
        /// The additional bytes to hash; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashStringAndBytes( /* CORE */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            string value,                               /* in: OPTIONAL */
            ByteList bytes,                             /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.String, ref list);

                if (!String.IsNullOrEmpty(value))
                {
                    if (encoding != null)
                    {
                        list.MaybeAddRange(encoding.GetBytes(value));
                    }
                    else
                    {
                        list.MaybeAddRange(
                            DataOps.GetRawBytes(value));
                    }
                }

                if (bytes != null)
                {
                    list.Add((byte)Characters.EndOfFile);
                    list.AddRange(bytes);
                }

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int valueLength = (value != null) ?
                    value.Length : Length.Invalid;

                int byteCount = (bytes != null) ?
                    bytes.Count : Count.Invalid;

                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash certificate and string and bytes success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "encoding = {4}, valueLength = {5}, value = {6}, " +
                    "byteCount = {7}, bytes = {8}, " +
                    "localByteLength = {9}, localBytes = {10}, " +
                    "hashBytes = {11}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    Utility.FormatWrapOrNull(encoding),
                    valueLength, Utility.FormatWrapOrNull(true, true,
                        value),
                    byteCount, Utility.FormatWrapOrNull(true, true, bytes),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the specified RSA signature over the supplied hash
        /// bytes using the public parameters of the given key pair.
        /// </summary>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to verify.
        /// </param>
        /// <param name="keyPair">
        /// The RSA key pair whose public key verifies the signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode VerifyHashRsa( /* CORE */
            byte[] hashBytes,         /* in */
            string hashAlgorithmName, /* in */
            byte[] signature,         /* in */
            IKeyPair keyPair,         /* in */
            ref Result result         /* out */
            )
        {
            if (hashBytes == null)
            {
                result = "invalid byte array";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(hashAlgorithmName))
            {
                result = "invalid hash algorithm name";
                return ReturnCode.Error;
            }

            if (signature == null)
            {
                result = "invalid signature";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            RsaKeyPair localKeyPair = keyPair as RsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an RSA key pair";
                return ReturnCode.Error;
            }

            RSAParameters parameters = localKeyPair.ToPublicParameters();

#if DEBUG
            RsaKeyFile.MaybeDumpVerifyParameters(
                "VerifyHashRsa", parameters, TracePriority.Highest);
#endif

            Result localError = null;

            using (RSA rsa = CreateRsaProvider(ref localError))
            {
                if (rsa != null)
                {
#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
                    BigRSACryptoServiceProvider bigRsa =
                        rsa as BigRSACryptoServiceProvider;

                    if (bigRsa != null)
                    {
                        bigRsa.ImportParameters(parameters);

                        if (bigRsa.VerifyHash(
                                hashBytes, signature, new HashAlgorithmName(
                                hashAlgorithmName), RSASignaturePadding.Pkcs1))
                        {
                            result = OperationStatus.VerifiedOk;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            result = null;
                            return ReturnCode.Error;
                        }
                    }
#endif

                    RSAProvider provider = rsa as RSAProvider;

                    if (provider != null)
                    {
                        provider.ImportParameters(parameters);

#if !NET_STANDARD_20
                        if (provider.VerifyHash(
                                hashBytes, CryptoConfig.MapNameToOID(
                                hashAlgorithmName), signature))
#else
                        if (provider.VerifyHash(
                                hashBytes, signature, new HashAlgorithmName(
                                hashAlgorithmName), RSASignaturePadding.Pkcs1))
#endif
                        {
                            result = OperationStatus.VerifiedOk;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            result = null;
                            return ReturnCode.Error;
                        }
                    }

                    result = String.Format(
                        "RSA provider is not based on " +
                        "{0} -OR- its use is not enabled",
                        typeof(RSAProvider));

                    return ReturnCode.Error;
                }
                else if (localError != null)
                {
                    result = localError;
                    return ReturnCode.Error;
                }
                else
                {
                    result = String.Format(
                        "RSA provider is not based on {0}",
                        typeof(RSA));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the specified DSA signature over the supplied hash
        /// bytes using the public parameters of the given key pair.
        /// </summary>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to verify.
        /// </param>
        /// <param name="keyPair">
        /// The DSA key pair whose public key verifies the signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode VerifyHashDsa( /* CORE */
            byte[] hashBytes,         /* in */
            string hashAlgorithmName, /* in */
            byte[] signature,         /* in */
            IKeyPair keyPair,         /* in */
            ref Result result         /* out */
            )
        {
            if (hashBytes == null)
            {
                result = "invalid byte array";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(hashAlgorithmName))
            {
                result = "invalid hash algorithm name";
                return ReturnCode.Error;
            }

            if (signature == null)
            {
                result = "invalid signature";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            DsaKeyPair localKeyPair = keyPair as DsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an DSA key pair";
                return ReturnCode.Error;
            }

            DSAParameters parameters = localKeyPair.ToPublicParameters();

#if DEBUG
            DsaKeyFile.MaybeDumpVerifyParameters(
                "VerifyHashDsa", parameters, TracePriority.Highest);
#endif

            Result localError = null;

            using (DSA dsa = CreateDsaProvider(ref localError))
            {
                if (dsa != null)
                {
                    DSAProvider provider = dsa as DSAProvider;

                    if (provider != null)
                    {
#if NET_STANDARD_20 || NET_STANDARD_21
                        //
                        // HACK: Apparently, if these DSAParameters fields are
                        //       not nulled out for .NET Core (Windows only?),
                        //       WindowsCryptographicException will be thrown
                        //       after NCryptImportKey fails from inside the
                        //       ImportKeyBlob method.
                        //
                        if (Utility.IsWindowsOperatingSystem())
                        {
                            parameters.Seed = null;
                            parameters.Counter = 0;
                        }
#endif

                        provider.ImportParameters(parameters);

#if NET_STANDARD_20 || NET_STANDARD_21
                        //
                        // BUGBUG: *SECURITY* This is really insecure because
                        //         it ignores the hash algorithm name used by
                        //         the caller and always uses SHA1, which is
                        //         fairly weak.
                        //
                        if (provider.VerifySignature(hashBytes, signature))
                        {
                            result = OperationStatus.VerifiedOk;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            result = null;
                            return ReturnCode.Error;
                        }
#else
                        //
                        // HACK: Apparently, Mono only supports the literal
                        //       string "SHA1" here.  Anything other string
                        //       will cause an exception.
                        //
                        if (Utility.IsMono())
                        {
                            if (provider.VerifyHash(
                                    hashBytes, hashAlgorithmName, signature))
                            {
                                result = OperationStatus.VerifiedOk;
                                return ReturnCode.Ok;
                            }
                            else
                            {
                                result = null;
                                return ReturnCode.Error;
                            }
                        }
                        else
                        {
                            if (provider.VerifyHash(
                                    hashBytes, CryptoConfig.MapNameToOID(
                                    hashAlgorithmName), signature))
                            {
                                result = OperationStatus.VerifiedOk;
                                return ReturnCode.Ok;
                            }
                            else
                            {
                                result = null;
                                return ReturnCode.Error;
                            }
                        }
#endif
                    }

                    result = String.Format(
                        "DSA provider is not based on " +
                        "{0} -OR- its use is not enabled",
                        typeof(DSAProvider));

                    return ReturnCode.Error;
                }
                else if (localError != null)
                {
                    result = localError;
                    return ReturnCode.Error;
                }
                else
                {
                    result = String.Format(
                        "DSA provider is not based on {0}",
                        typeof(DSAProvider));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the timeout to use, in milliseconds, based on the
        /// interpreter and timeout type, falling back to the configured
        /// default.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="timeoutType">
        /// The kind of timeout being requested; this parameter is optional
        /// and may be null.
        /// </param>
        /// <returns>
        /// The timeout, in milliseconds, or null if none is configured.
        /// </returns>
        public static int? GetTimeout( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            TimeoutType? timeoutType /* in: OPTIONAL */
            )
        {
            int? timeout = GetTimeoutFrom(
                interpreter, null, timeoutType);

            if (timeout != null)
                return timeout;

            return Configuration.GetTimeout();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the timeout to use from the currently active interpreter,
        /// without removing it from the active stack.
        /// </summary>
        /// <param name="timeoutType">
        /// The kind of timeout being requested; this parameter is optional
        /// and may be null.
        /// </param>
        /// <returns>
        /// The timeout, in milliseconds, or null if unavailable.
        /// </returns>
        private static int? PeekAndGetTimeout( /* CORE */
            TimeoutType? timeoutType /* in: OPTIONAL */
            )
        {
            InterpreterPair anyPair =
                Utility.PeekActiveInterpreter();

            if (anyPair == null)
                return null;

            return GetTimeoutFrom(
                anyPair.X, anyPair.Y as EvaluateClientData,
                timeoutType);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the timeout to use from the supplied client data or
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data that may carry a timeout; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="timeoutType">
        /// The kind of timeout being requested; this parameter is optional
        /// and may be null.
        /// </param>
        /// <returns>
        /// The timeout, in milliseconds, or null if unavailable.
        /// </returns>
        private static int? GetTimeoutFrom( /* CORE */
            Interpreter interpreter,       /* in: OPTIONAL */
            EvaluateClientData clientData, /* in: OPTIONAL */
            TimeoutType? timeoutType       /* in: OPTIONAL */
            )
        {
            int? timeout; /* REUSED */

            if (clientData != null)
            {
                timeout = clientData.Timeout;

                if (timeout != null)
                    return timeout;
            }

            if (interpreter != null)
            {
                TimeoutType localTimeoutType = (timeoutType != null) ?
                    (TimeoutType)timeoutType : TimeoutType.Network;

                Result error = null; /* NOT USED */

                timeout = interpreter.GetTimeout(
                    localTimeoutType, ref error);

                if (timeout != null)
                    return timeout;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies a signature over the supplied hash bytes, dispatching
        /// to the RSA or DSA verifier based on the key pair type.
        /// </summary>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to verify.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyHash( /* CORE */
            byte[] hashBytes,         /* in */
            string hashAlgorithmName, /* in */
            byte[] signature,         /* in */
            IKeyPair keyPair,         /* in */
            ref Result result         /* out */
            )
        {
            if (keyPair is RsaKeyPair)
            {
                return VerifyHashRsa(
                    hashBytes, hashAlgorithmName, signature,
                    keyPair, ref result);
            }

            if (keyPair is DsaKeyPair)
            {
                return VerifyHashDsa(
                    hashBytes, hashAlgorithmName, signature,
                    keyPair, ref result);
            }

            result = "unsupported key pair type";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies a signature over the supplied hash bytes against each
        /// of the given key pairs, returning the one that succeeds.
        /// </summary>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to verify.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to attempt verification with.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the
        /// signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyHash( /* CORE */
            byte[] hashBytes,               /* in */
            string hashAlgorithmName,       /* in */
            byte[] signature,               /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            ref IKeyPair keyPair,           /* out */
            ref Result result               /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (VerifyHash(
                        hashBytes, hashAlgorithmName, signature,
                        localKeyPair, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify hash";

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies a signature over the supplied hash bytes for the
        /// specified certificate and key pair, including public key token,
        /// revocation, and expiration checks.
        /// </summary>
        /// <param name="type">
        /// A descriptive name for the entity being verified, used in error
        /// messages.
        /// </param>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the certificate and key pair public key
        /// tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyHash( /* CORE */
            string type,              /* in */
            byte[] hashBytes,         /* in */
            string hashAlgorithmName, /* in */
            ICertificate certificate, /* in */
            IKeyPair keyPair,         /* in */
            bool matchPublicKeyToken, /* in */
            bool checkRevocation,     /* in */
            ref Result result         /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            byte[] publicKeyToken = null;

            if (CheckKeyPair(
                    keyPair, ref publicKeyToken,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // BUGFIX: Make sure the public key tokens match.
            //
            if (matchPublicKeyToken &&
                !DataOps.MatchPublicKeyToken(
                    certificate.Key, publicKeyToken))
            {
#if CERTIFICATE_PLUGIN
                //
                // REFACTOR: Make it (much?) easier to troubleshoot
                //           issues with missing public keys, etc.
                //
                string format = null;

                if (IsolatedState.GetIncludePublicKeyToken())
                    format = Constants.PublicKeyTokenMismatchFormat;

                if (format == null)
                    format = Constants.PublicKeyTokenMismatchError;

                if (format != null)
                {
                    result = String.Format(format,
                        DataOps.FormatPublicKeyToken(
                            certificate.Key, true, true),
                        DataOps.FormatPublicKeyToken(
                            publicKeyToken, true, true));
                }
                else
                {
                    result = null;
                }
#else
                result = Constants.PublicKeyTokenMismatchError;
#endif

                return ReturnCode.Error;
            }

            //
            // HACK: If a particular key pair is revoked, it cannot be used
            //       to verify anything, no matter what.
            //
            // HACK: *SECURITY* However, sometimes there may be a context
            //       where we cannot perform any remote server checks.
            //
            NetworkFlags networkFlags = Helpers.GetNetworkFlags(null);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            if (CertificateKeyPairState.GetForceNetwork())
                networkFlags |= NetworkFlags.ForceMask;
#endif

            //
            // HACK: There is not much point in checking revocation
            //       for the certificate itself here as that should
            //       be done by the (indirect?) caller(s).
            //
            networkFlags |= NetworkFlags.KeyPairOnly;

            //
            // HACK: Maybe invoke the fail-safe checking, which will
            //       perform an asynchronous forced remote check to
            //       determine if the certificate -OR- its signing
            //       key pair has been actively revoked.
            //
            Assembly assembly = AssemblyOps.GetObject();
            DateTime timeStamp = certificate.TimeStamp;
            int? timeout = PeekAndGetTimeout(null);

            RevocationOps.MaybePerformFailSafeChecks( /* OK */
                null, assembly, null, hashAlgorithmName, null, null,
                null, certificate, keyPair, null, timeStamp, timeout,
                networkFlags);

            if (checkRevocation && RevocationOps.IsRevoked( /* OK */
                    null, assembly, null, hashAlgorithmName, null,
                    null, null, keyPair, null, timeStamp, timeout,
                    networkFlags, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // HACK: If a particular key pair is expired, it cannot be
            //       used to verify anything.  This does not apply if
            //       a key pair has the "ExpireSignature" usage flag.
            //
            if (CheckKeyExpiration(keyPair,
                    certificate.TimeStamp, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (VerifyHash(
                    hashBytes, hashAlgorithmName, certificate.Signature,
                    keyPair, ref result) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                if (DataOps.MatchPublicKeyToken(
                        certificate.Key, publicKeyToken))
                {
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Failed to verify hash {0} using algorithm {1} " +
                        "with key pair {2}", Utility.FormatWrapOrNull(
                            DataOps.FormatHexadecimal(hashBytes)),
                        Utility.FormatWrapOrNull(hashAlgorithmName),
                        Utility.FormatWrapOrNull(keyPair)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.MediumHigh, 0);
                }
#endif

                //
                // NOTE: Set error message to indicate failure, using the
                //       signature type, if any, specified by the caller.
                //       This is only necessary if the lower level method
                //       did not already set the result.
                //
                if (result == null)
                {
                    result = String.Format(
                        "{0} signature could not be verified",
                        type).Trim();
                }

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies a signature over the supplied hash bytes for the
        /// specified certificate against each of the given key pairs,
        /// returning the one that succeeds.
        /// </summary>
        /// <param name="type">
        /// A descriptive name for the entity being verified, used in error
        /// messages.
        /// </param>
        /// <param name="hashBytes">
        /// The hash bytes that were signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the signature.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to attempt verification with.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the certificate and key pair public key
        /// tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the
        /// signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyHash( /* CORE */
            string type,                    /* in */
            byte[] hashBytes,               /* in */
            string hashAlgorithmName,       /* in */
            ICertificate certificate,       /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            bool matchPublicKeyToken,       /* in */
            bool checkRevocation,           /* in */
            ref IKeyPair keyPair,           /* out */
            ref Result result               /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (VerifyHash(
                        type, hashBytes, hashAlgorithmName,
                        certificate, localKeyPair,
                        matchPublicKeyToken, checkRevocation,
                        ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify hash";

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// contents of the supplied stream.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the stream as text; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="stream">
        /// The stream whose contents are to be hashed.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashStream(
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            Stream stream,                              /* in */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            if (stream == null)
            {
                error = "invalid stream";
                return ReturnCode.Error;
            }

            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.Stream, ref list);

                byte[] fileBytes;

                if (encoding != null)
                {
                    //
                    // NOTE: Treat the stream as text in the specified
                    //       encoding.
                    //
                    // TODO: Is this correct?
                    //
                    using (StreamReader streamReader = new StreamReader(
                            stream, encoding))
                    {
                        //
                        // TODO: ReadToEnd uses the encoding and then
                        //       we use it again to get the underlying
                        //       bytes for the string?
                        //
                        fileBytes = encoding.GetBytes(
                            streamReader.ReadToEnd()); /* throw */
                    }
                }
                else
                {
                    //
                    // NOTE: Treat the stream as a binary blob.
                    //
                    using (BinaryReader binaryReader = new BinaryReader(
                            stream))
                    {
                        fileBytes = binaryReader.ReadBytes(
                            (int)stream.Length); /* throw */
                    }
                }

                list.AddRange(fileBytes);

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash stream success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "encoding = {4}, stream = {5}, " +
                    "localByteLength = {6}, localBytes = {7}, " +
                    "hashBytes = {8}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    Utility.FormatWrapOrNull(encoding),
                    Utility.FormatWrapOrNull(stream),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified stream and verifies its
        /// signature against the supplied certificate and key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="stream">
        /// The stream whose contents are to be verified.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyStream(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            Stream stream,                              /* in */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = HashStream(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, stream,
                    ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = VerifyHash(
                        "stream", hashBytes, hashAlgorithmName,
                        certificate, keyPair, matchPublicKeyToken,
                        checkRevocation, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Computes the hash of the specified string and verifies its
        /// signature against the supplied certificate and key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="value">
        /// The string whose signature is to be verified; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyString(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            string value,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, value,
                    ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = VerifyHash(
                        "string", hashBytes, hashAlgorithmName,
                        certificate, keyPair, matchPublicKeyToken,
                        checkRevocation, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// contents of the named file, reading the file data first.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="fileName">
        /// The file name or remote URI whose contents are to be hashed.
        /// </param>
        /// <param name="timeout">
        /// The optional download timeout, in milliseconds.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            string fileName,                            /* in */
            int? timeout,                               /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            byte[] fileBytes;
            bool useResource = false; /* NOT USED */

            fileBytes = GetDataFromFile(
                null, encoding, fileName, timeout, true, true,
                true, ref useResource, ref error) as byte[];

            if (fileBytes == null)
                return ReturnCode.Error;

            return HashFile(
                hashAlgorithmName, hashKey, certificate,
                certificateHashFlags, encoding, fileName,
                fileBytes, timeout, ref hashBytes, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified certificate combined with the
        /// supplied file bytes.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to include in the hash; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="fileName">
        /// The file name associated with the supplied bytes.
        /// </param>
        /// <param name="fileBytes">
        /// The file contents to hash.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds.
        /// </param>
        /// <param name="hashBytes">
        /// Upon return, receives the computed hash bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HashFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            string fileName,                            /* in */
            byte[] fileBytes,                           /* in */
            int? timeout,                               /* in: OPTIONAL */
            ref byte[] hashBytes,                       /* out */
            ref Result error                            /* out */
            )
        {
            try
            {
                ByteList list = new ByteList();

                Certificate.AddToHash(
                    certificate, (encoding != null) ?
                        encoding : DataOps.GetRawEncoding(),
                    (certificateHashFlags != null) ?
                        (CertificateHashFlags)certificateHashFlags :
                        CertificateHashFlags.File, ref list);

                if (fileBytes != null)
                    list.AddRange(fileBytes);

                byte[] localBytes = list.ToArray();

                if (HashBytes(
                        hashAlgorithmName, hashKey, localBytes,
                        ref hashBytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                int localByteLength = (localBytes != null) ?
                    localBytes.Length : Length.Invalid;

                TraceOps.DebugTrace(String.Format(
                    "Hash file success, " +
                    "hashAlgorithmName = {0}, hashKey = {1}, " +
                    "certificate = {2}, certificateHashFlags = {3}, " +
                    "encoding = {4}, fileName = {5}, " +
                    "localByteLength = {6}, localBytes = {7}, " +
                    "hashBytes = {8}",
                    Utility.FormatWrapOrNull(hashAlgorithmName),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatHexadecimal(hashKey)),
                    ToString(certificate),
                    Utility.FormatWrapOrNull(certificateHashFlags),
                    Utility.FormatWrapOrNull(encoding),
                    Utility.FormatWrapOrNull(fileName),
                    localByteLength, Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(localBytes)),
                    Utility.FormatWrapOrNull(true, true,
                        DataOps.FormatHexadecimal(hashBytes))),
                    typeof(CertificateSharedOps).Name, TracePriority.Lower);
#endif

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;

                TraceOps.DebugTrace(String.Format(
                    "Hash file failure, fileName = {0}, error = {1}",
                    Utility.FormatWrapOrNull(fileName),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh);
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Computes the hash of the specified certificate, string, and byte
        /// list, then verifies its signature against the supplied key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="value">
        /// The string to include; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="bytes">
        /// The additional bytes to include; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyStringAndBytes(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            string value,                               /* in: OPTIONAL */
            ByteList bytes,                             /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = HashStringAndBytes(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, value,
                    bytes, ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = VerifyHash(
                        "stringAndBytes", hashBytes, hashAlgorithmName,
                        certificate, keyPair, matchPublicKeyToken,
                        checkRevocation, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified file and verifies its
        /// signature against the supplied certificate and key pairs.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to attempt verification with.
        /// </param>
        /// <param name="fileName">
        /// The file name or remote URI whose signature is to be verified.
        /// </param>
        /// <param name="timeout">
        /// The optional download timeout, in milliseconds.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the
        /// signature.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string fileName,                            /* in */
            int? timeout,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = HashFile(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, fileName,
                    timeout, ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = VerifyHash(
                        "file", hashBytes, hashAlgorithmName,
                        certificate, keyPairs, matchPublicKeyToken,
                        checkRevocation, ref keyPair, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Computes the hash of the specified file and verifies its
        /// signature against the supplied certificate and key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature is to be verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key verifies the signature.
        /// </param>
        /// <param name="fileName">
        /// The file name or remote URI whose signature is to be verified.
        /// </param>
        /// <param name="timeout">
        /// The optional download timeout, in milliseconds.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key tokens to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the key pair has been revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the verification status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode VerifyFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            string fileName,                            /* in */
            int? timeout,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = HashFile(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, fileName,
                    timeout, ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = VerifyHash(
                        "file", hashBytes, hashAlgorithmName,
                        certificate, keyPair, matchPublicKeyToken,
                        checkRevocation, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            CertificateHashFlags flags,    /* in */
            CertificateHashFlags hasFlags, /* in */
            bool all                       /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != CertificateHashFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            ConfigurationFileFlags flags,    /* in */
            ConfigurationFileFlags hasFlags, /* in */
            bool all                         /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != ConfigurationFileFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            FileNameFlags flags,    /* in */
            FileNameFlags hasFlags, /* in */
            bool all                /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != FileNameFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            EntityType flags,    /* in */
            EntityType hasFlags, /* in */
            bool all             /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != EntityType.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            HashAlgorithmType flags,    /* in */
            HashAlgorithmType hasFlags, /* in */
            bool all                    /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != HashAlgorithmType.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && NETWORK && WEB
        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            InstallFlags flags,    /* in */
            InstallFlags hasFlags, /* in */
            bool all               /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != InstallFlags.None);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            KeyFileFormat flags,    /* in */
            KeyFileFormat hasFlags, /* in */
            bool all                /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != KeyFileFormat.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            LicenseType flags,    /* in */
            LicenseType hasFlags, /* in */
            bool all              /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != LicenseType.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            PolicyTraceFlags flags,    /* in */
            PolicyTraceFlags hasFlags, /* in */
            bool all                   /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != PolicyTraceFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            RecordResultType flags,    /* in */
            RecordResultType hasFlags, /* in */
            bool all                   /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != RecordResultType.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags(
            ResetFlags flags,    /* in */
            ResetFlags hasFlags, /* in */
            bool all             /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != ResetFlags.None);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            NetworkFlags flags,    /* in */
            NetworkFlags hasFlags, /* in */
            bool all               /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != NetworkFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            NotCommandFlags flags,    /* in */
            NotCommandFlags hasFlags, /* in */
            bool all                  /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != NotCommandFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            RestrictionFlags flags,    /* in */
            RestrictionFlags hasFlags, /* in */
            bool all                   /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != RestrictionFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            ConfigurationPhase flags,    /* in */
            ConfigurationPhase hasFlags, /* in */
            bool all                     /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != ConfigurationPhase.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            EnableSecurityFlags flags,    /* in */
            EnableSecurityFlags hasFlags, /* in */
            bool all                      /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != EnableSecurityFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            EvaluateCommandFlags flags,    /* in */
            EvaluateCommandFlags hasFlags, /* in */
            bool all                       /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != EvaluateCommandFlags.None);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags(
            ShellFlags flags,    /* in */
            ShellFlags hasFlags, /* in */
            bool all             /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != ShellFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags(
            ShellFlags? flags,   /* in */
            ShellFlags hasFlags, /* in */
            bool all             /* in */
            )
        {
            ShellFlags localFlags = (flags != null) ?
                (ShellFlags)flags : ShellFlags.None;

            return HasFlags(localFlags, hasFlags, all);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            StatusFlags flags,    /* in */
            StatusFlags hasFlags, /* in */
            bool all              /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != StatusFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags( /* CORE */
            UriType flags,    /* in */
            UriType hasFlags, /* in */
            bool all          /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != UriType.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Determines whether the <paramref name="flags" /> value contains
        /// the <paramref name="hasFlags" /> flag bits.
        /// </summary>
        /// <param name="flags">
        /// The flags value to be checked.
        /// </param>
        /// <param name="hasFlags">
        /// The flag bits to look for within <paramref name="flags" />.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// bits are present; otherwise, any matching bit is sufficient.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flag bits are present.
        /// </returns>
        public static bool HasFlags(
            FlagRuleType flags,    /* in */
            FlagRuleType hasFlags, /* in */
            bool all               /* in */
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != FlagRuleType.None);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the entity type of the specified certificate
        /// satisfies the required and forbidden entity type constraints.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose entity type is to be checked.
        /// </param>
        /// <param name="hasEntityType">
        /// The entity type bits that must be present.
        /// </param>
        /// <param name="notHasEntityType">
        /// The entity type bits that must not be present.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required bits.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden bits before failing.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchEntityType( /* CORE */
            ICertificate certificate,    /* in */
            EntityType hasEntityType,    /* in */
            EntityType notHasEntityType, /* in */
            bool hasAll,                 /* in */
            bool notHasAll,              /* in */
            ref Result result            /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            return MatchEntityType(
                certificate.EntityType, hasEntityType,
                notHasEntityType, hasAll, notHasAll,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified entity type satisfies the
        /// required and forbidden entity type constraints.
        /// </summary>
        /// <param name="entityType">
        /// The entity type to be checked.
        /// </param>
        /// <param name="hasEntityType">
        /// The entity type bits that must be present.
        /// </param>
        /// <param name="notHasEntityType">
        /// The entity type bits that must not be present.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required bits.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden bits before failing.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchEntityType( /* CORE */
            EntityType entityType,       /* in */
            EntityType hasEntityType,    /* in */
            EntityType notHasEntityType, /* in */
            bool hasAll,                 /* in */
            bool notHasAll,              /* in */
            ref Result result            /* out */
            )
        {
            if ((hasEntityType != EntityType.None) &&
                !HasFlags(entityType, hasEntityType, hasAll))
            {
                result = String.Format(
                    "missing {0} of entity type: {1}",
                    hasAll ? "some" : "all", hasEntityType);

                return ReturnCode.Error;
            }

            if ((notHasEntityType != EntityType.None) &&
                HasFlags(entityType, notHasEntityType, notHasAll))
            {
                result = String.Format(
                    "found {0} of entity type: {1}",
                    notHasAll ? "all" : "some", notHasEntityType);

                return ReturnCode.Error;
            }

            result = OperationStatus.TypeOk;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the subject matching options from the specified
        /// execution policy, falling back to the configured default.
        /// </summary>
        /// <param name="policy">
        /// The execution policy to examine; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="usePrefix">
        /// Upon return, indicates whether prefix subject matching is
        /// enabled.
        /// </param>
        /// <param name="useSimpleName">
        /// Upon return, indicates whether simple name matching is enabled.
        /// </param>
        public static void ExtractSubjectExecutionPolicy( /* CORE */
            ExecutionPolicy? policy, /* in */
            out bool usePrefix,      /* out */
            out bool useSimpleName   /* out */
            )
        {
            ExecutionPolicy localPolicy;

            if (policy != null)
            {
                localPolicy = (ExecutionPolicy)policy;
            }
            else
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                //
                // NOTE: Fallback on the currently configured global "other"
                //       execution policy.
                //
                localPolicy = PolicyOps.GetPolicy(PolicyType.Other);
#else
                localPolicy = Constants.DefaultOtherExecutionPolicy;
#endif
            }

            usePrefix = Utility.HasFlags(localPolicy,
                ExecutionPolicy.MatchSubjectPrefix, true);

            useSimpleName = Utility.HasFlags(localPolicy,
                ExecutionPolicy.MatchSubjectSimpleName, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the subject of the specified X.509
        /// certificate matches the given subject, using the matching
        /// options from the supplied execution policy.
        /// </summary>
        /// <param name="x509Certificate2">
        /// The X.509 certificate whose subject is to be matched.
        /// </param>
        /// <param name="subject">
        /// The subject to match against.
        /// </param>
        /// <param name="policy">
        /// The execution policy supplying the matching options; this
        /// parameter is optional and may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the subject matches.
        /// </returns>
        private static bool MatchSubject( /* CORE */
            X509Certificate2 x509Certificate2, /* in */
            string subject,                    /* in */
            ExecutionPolicy? policy            /* in: OPTIONAL, May be null. */
            )
        {
            bool usePrefix;
            bool useSimpleName;

            ExtractSubjectExecutionPolicy(
                policy, out usePrefix, out useSimpleName);

            return MatchSubject(
                x509Certificate2, subject, usePrefix, useSimpleName);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method was "stolen" from the SecurityOps class in the
        //       updater tool and then modified to fit with the conventions
        //       of this class.
        //
        /// <summary>
        /// Determines whether the subject of the specified X.509
        /// certificate matches the given subject, with optional prefix and
        /// simple name matching.
        /// </summary>
        /// <param name="x509Certificate2">
        /// The X.509 certificate whose subject is to be matched.
        /// </param>
        /// <param name="subject">
        /// The subject to match against.
        /// </param>
        /// <param name="usePrefix">
        /// Non-zero to allow the subject to match a prefix of the
        /// certificate subject.
        /// </param>
        /// <param name="useSimpleName">
        /// Non-zero to also match against the certificate simple name.
        /// </param>
        /// <returns>
        /// Non-zero if the subject matches.
        /// </returns>
        private static bool MatchSubject( /* CORE */
            X509Certificate2 x509Certificate2, /* in */
            string subject,                    /* in */
            bool usePrefix,                    /* in */
            bool useSimpleName                 /* in */
            )
        {
            //
            // NOTE: No matching can be done on an invalid certificate.
            //
            if (x509Certificate2 == null)
                return false;

            //
            // NOTE: Reject matching a subject of null instead of treating
            //       it as "always match" here, even in non-strict mode.
            //
            if (subject == null)
                return false;

            //
            // NOTE: Grab the certificate subject as we may need it multiple
            //       times.
            //
            string localSubject = x509Certificate2.Subject;

            //
            // NOTE: Does the certificate subject match the specified subject
            //       exactly?
            //
            if (DataOps.StringEquals(localSubject, subject))
                return true;

            //
            // NOTE: Does the specified subject, with an added space, match
            //       the start of the certificate subject exactly?
            //
            if (usePrefix && DataOps.StringStartsWith(
                    localSubject, subject + Characters.Space))
            {
                return true;
            }

            //
            // NOTE: If simple name matching is disabled, return false now.
            //
            if (!useSimpleName)
                return false;

            //
            // NOTE: Grab the certificate simple name as we may need it
            //       multiple times.
            //
            string localSimpleName = x509Certificate2.GetNameInfo(
                X509NameType.SimpleName, false);

            //
            // NOTE: Does the certificate simple name match the specified
            //       subject exactly?
            //
            if (DataOps.StringEquals(localSimpleName, subject))
                return true;

            //
            // NOTE: Does the specified subject, with an added space, match
            //       the start of the certificate simple name exactly?
            //
            if (usePrefix && DataOps.StringStartsWith(
                    localSimpleName, subject + Characters.Space))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the Authenticode subject of the specified
        /// assembly matches the vendor of the given certificate.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose Authenticode subject is to be matched.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose vendor is to be matched.
        /// </param>
        /// <param name="policy">
        /// The execution policy supplying the matching options; this
        /// parameter is optional and may be null.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode MatchSubject( /* CORE */
            Assembly assembly,        /* in */
            ICertificate certificate, /* in */
            ExecutionPolicy? policy   /* in: OPTIONAL, May be null. */
            )
        {
            Result result = null; /* NOT USED */

            return MatchSubject(
                assembly, certificate, policy, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the Authenticode subject of the specified
        /// assembly matches the vendor of the given certificate, honoring
        /// the relevant certificate feature flags.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose Authenticode subject is to be matched.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose vendor is to be matched.
        /// </param>
        /// <param name="policy">
        /// The execution policy supplying the matching options; this
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchSubject( /* CORE */
            Assembly assembly,        /* in */
            ICertificate certificate, /* in */
            ExecutionPolicy? policy,  /* in: OPTIONAL, May be null. */
            ref Result result         /* out */
            )
        {
            if (assembly == null)
            {
                result = "invalid assembly";
                return ReturnCode.Error;
            }

            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            try
            {
                //
                // NOTE: Currently, only makes sure that vendor of the
                //       certificate matches the Authenticode subject.
                //       This may be enhanced later with more checking.
                //
                long flagsKey = Utility.DefaultAttributeFlagsKey();
                X509Certificate2 x509Certificate2 = null;

                if (Utility.GetAssemblyCertificate2(
                        assembly, false, ref x509Certificate2,
                        ref result) == ReturnCode.Ok)
                {
                    if (x509Certificate2 != null)
                    {
                        //
                        // NOTE: Match the X509 certificate subject
                        //       against the certificate vendor.  An
                        //       exact (culture-insensitive and
                        //       case-sensitive) match is required
                        //       here.
                        //
                        if (MatchSubject(
                                x509Certificate2, certificate.Vendor,
                                policy))
                        {
                            result = OperationStatus.SignatureOk;
                            return ReturnCode.Ok;
                        }
                        else if (MatchFlags(
                                certificate, FlagType.Feature, flagsKey,
                                Features.NoSubjectOrAll, null, false,
                                false, true) == ReturnCode.Ok)
                        {
                            result = OperationStatus.SignatureMismatch;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            result = "unauthorized certificate vendor";
                        }
                    }
                    else if (MatchFlags(
                            certificate, FlagType.Feature, flagsKey,
                            Features.NoTrustedOrAll, null, false,
                            false, true) == ReturnCode.Ok)
                    {
                        result = OperationStatus.SignatureMissing;
                        return ReturnCode.Ok;
                    }
                    else
                    {
                        //
                        // NOTE: No Authenticode signature on the
                        //       assembly.  In debug mode this is
                        //       allowed.  In release mode this is
                        //       an error.
                        //
#if DEBUG
                        result = OperationStatus.SignatureSkipped;
                        return ReturnCode.Ok;
#else
                        result = "assembly is missing signature";
#endif
                    }
                }
                else if (MatchFlags(
                        certificate, FlagType.Feature, flagsKey,
                        Features.NoTrustedOrAll, null, false,
                        false, true) == ReturnCode.Ok)
                {
                    result = OperationStatus.SignatureError;
                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the flags string of the specified type from the given
        /// certificate.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to query.
        /// </param>
        /// <param name="type">
        /// The kind of flags to retrieve.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The flags string, or null on failure.
        /// </returns>
        public static string GetFlags( /* CORE */
            ICertificate certificate, /* in */
            FlagType type,            /* in */
            ref Result error          /* out */
            )
        {
            if (certificate != null)
            {
                string result = null;

                switch (type)
                {
                    case FlagType.Feature:
                        {
                            result = certificate.Features;

                            if (result == null)
                                result = String.Empty;

                            break;
                        }
                    case FlagType.Restriction:
                        {
                            result = certificate.Restrictions;

                            if (result == null)
                                result = String.Empty;

                            break;
                        }
                    default:
                        {
                            error = String.Format(
                                "unknown flag type {0}",
                                type);

                            break;
                        }
                }

                return result;
            }
            else
            {
                error = "invalid certificate";
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified flags text satisfies the
        /// required and forbidden flag constraints.
        /// </summary>
        /// <param name="text">
        /// The flags text to be parsed and checked.
        /// </param>
        /// <param name="type">
        /// The kind of flags being checked.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must not be present.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required flags.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden flags before failing.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enable strict flag parsing.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchFlags( /* CORE */
            string text,        /* in */
            FlagType type,      /* in */
            long key,           /* in */
            string hasFlags,    /* in */
            string notHasFlags, /* in */
            bool hasAll,        /* in */
            bool notHasAll,     /* in */
            bool strict         /* in: EXEMPT */
            )
        {
            Result result = null;

            return MatchFlags(
                text, type, key, hasFlags, notHasFlags, hasAll, notHasAll,
                strict, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified flags text satisfies the
        /// required and forbidden flag constraints, returning a detailed
        /// result.
        /// </summary>
        /// <param name="text">
        /// The flags text to be parsed and checked.
        /// </param>
        /// <param name="type">
        /// The kind of flags being checked.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must not be present; this parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required flags.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden flags before failing.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enable strict flag parsing.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchFlags( /* CORE */
            string text,        /* in */
            FlagType type,      /* in */
            long key,           /* in */
            string hasFlags,    /* in: OPTIONAL */
            string notHasFlags, /* in: OPTIONAL */
            bool hasAll,        /* in */
            bool notHasAll,     /* in */
            bool strict,        /* in: EXEMPT */
            ref Result result   /* out */
            )
        {
            Result error = null;

            IDictionary<long, string> flags = Utility.ParseAttributeFlags(
                text, true, true, true, ref error);

            if (flags == null)
            {
                result = String.Format(
                    "cannot parse {0} flags: {1}", type,
                    Utility.FormatWrapOrNull(error));

                return ReturnCode.Error;
            }

            if ((hasFlags != null) && !Utility.HaveAttributeFlags(
                    flags, key, hasFlags, hasAll, strict))
            {
                result = String.Format(
                    "missing {0} of the {1} flags {2} with key {3}",
                    hasAll ? "some" : "all", type,
                    Utility.FormatWrapOrNull(hasFlags), key);

                return ReturnCode.Error;
            }

            if ((notHasFlags != null) && Utility.HaveAttributeFlags(
                    flags, key, notHasFlags, notHasAll, strict))
            {
                result = String.Format(
                    "found {0} of the {1} flags {2} with key {3}",
                    notHasAll ? "all" : "some", type,
                    Utility.FormatWrapOrNull(notHasFlags), key);

                return ReturnCode.Error;
            }

            result = OperationStatus.FlagOk;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the flags of the specified type on the given
        /// certificate satisfy the required and forbidden constraints.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose flags are to be checked.
        /// </param>
        /// <param name="type">
        /// The kind of flags being checked.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must not be present.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required flags.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden flags before failing.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enable strict flag parsing.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchFlags( /* CORE */
            ICertificate certificate, /* in */
            FlagType type,            /* in */
            long key,                 /* in */
            string hasFlags,          /* in */
            string notHasFlags,       /* in */
            bool hasAll,              /* in */
            bool notHasAll,           /* in */
            bool strict               /* in: EXEMPT */
            )
        {
            Result result = null;

            return MatchFlags(
                certificate, type, key, hasFlags, notHasFlags, hasAll,
                notHasAll, strict, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the flags of the specified type on the given
        /// certificate satisfy the required and forbidden constraints,
        /// returning a detailed result.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose flags are to be checked.
        /// </param>
        /// <param name="type">
        /// The kind of flags being checked.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must not be present.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the required flags.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require all of the forbidden flags before failing.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enable strict flag parsing.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchFlags( /* CORE */
            ICertificate certificate, /* in */
            FlagType type,            /* in */
            long key,                 /* in */
            string hasFlags,          /* in */
            string notHasFlags,       /* in */
            bool hasAll,              /* in */
            bool notHasAll,           /* in */
            bool strict,              /* in: EXEMPT */
            ref Result result         /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            Result error = null;
            string text = GetFlags(certificate, type, ref error);

            if (text == null)
            {
                result = String.Format(
                    "invalid {0} flags: {1}", type,
                    Utility.FormatWrapOrNull(error));

                return ReturnCode.Error;
            }

            return MatchFlags(
                text, type, key, hasFlags, notHasFlags, hasAll, notHasAll,
                strict, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified entity type to its default required key usage
        /// flags.
        /// </summary>
        /// <param name="entityType">
        /// The entity type to map.
        /// </param>
        /// <param name="hasFlags">
        /// Upon return, receives the required key usage flags.
        /// </param>
        /// <param name="hasAll">
        /// Upon return, indicates whether all of the flags are required.
        /// </param>
        /// <param name="mayNeedRootKeyUsage">
        /// Upon return, indicates whether root key usage may be required.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if a key usage mapping was determined.
        /// </returns>
        public static bool DefaultEntityTypeToKeyUsage( /* CORE */
            EntityType entityType,        /* in */
            ref string hasFlags,          /* out */
            ref bool hasAll,              /* out */
            ref bool mayNeedRootKeyUsage, /* out */
            ref Result error              /* out */
            )
        {
            //
            // NOTE: *SPECIAL* For license certificate signing only, there are
            //       multiple valid entity types; therefore, a bitmask is used.
            //       The following checks are necessary here:
            //
            //       1. One (or more) license entity types are present.
            //
            //       2. Zero non-license entity types are present.
            //
            if (HasFlags(entityType, EntityType.LicenseTypeMask, false) &&
                !HasFlags(entityType, ~EntityType.LicenseTypeMask, false))
            {
                hasFlags = KeyUsage.License; /* Individual Flag */
                hasAll = true; /* Logical -AND- */
                mayNeedRootKeyUsage = false; /* No Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.Script" bitmask.
            //
            if (entityType == EntityType.Script)
            {
                hasFlags = KeyUsage.Script; /* Individual Flag */
                hasAll = true; /* Logical -AND- */
                mayNeedRootKeyUsage = false; /* No Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.String" bitmask.
            //
            if (entityType == EntityType.String)
            {
                hasFlags = KeyUsage.String; /* Individual Flag */
                hasAll = true; /* Logical -AND- */
                mayNeedRootKeyUsage = false; /* No Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.File" bitmask.
            //
            if (entityType == EntityType.File)
            {
                hasFlags = KeyUsage.File; /* Individual Flag */
                hasAll = true; /* Logical -AND- */
                mayNeedRootKeyUsage = false; /* No Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.KeyRing" bitmask.
            //
            if (entityType == EntityType.KeyRing)
            {
                hasFlags = KeyUsage.KeyRing; /* Composite Flag */
                hasAll = false; /* Logical -OR- */
                mayNeedRootKeyUsage = true; /* Maybe Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.List" bitmask.
            //
            if (entityType == EntityType.List)
            {
                hasFlags = KeyUsage.RemoteList; /* Composite Flag */
                hasAll = false; /* Logical -OR- */
                mayNeedRootKeyUsage = true; /* Maybe Root Key Usage */

                return true;
            }

            //
            // HACK: For now, perform an exact match here instead of checking
            //       the "EntityType.Time" bitmask.
            //
            if (entityType == EntityType.Time)
            {
                hasFlags = KeyUsage.RemoteTime; /* Composite Flag */
                hasAll = false; /* Logical -OR- */
                mayNeedRootKeyUsage = true; /* Maybe Root Key Usage */

                return true;
            }

            //
            // NOTE: At this point, while the entity type itself may be valid
            //       there is no well-known key usage for it.  So, fail.
            //
            hasFlags = KeyUsage.Invalid; /* No Flag */
            hasAll = false; /* Logical -OR- */
            mayNeedRootKeyUsage = false; /* No Root Key Usage */

            error = String.Format(
                "unsupported entity type {0} for key usage",
                Utility.FormatWrapOrNull(entityType));

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified entity type to its custom required key usage
        /// flags, when a custom mapping has been registered.
        /// </summary>
        /// <param name="entityType">
        /// The entity type to map.
        /// </param>
        /// <param name="hasFlags">
        /// Upon return, receives the required key usage flags.
        /// </param>
        /// <param name="hasAll">
        /// Upon return, indicates whether all of the flags are required.
        /// </param>
        /// <param name="mayNeedRootKeyUsage">
        /// Upon return, indicates whether root key usage may be required.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if a key usage mapping was determined.
        /// </returns>
        private static bool CustomEntityTypeToKeyUsage( /* CORE */
            EntityType entityType,        /* in */
            ref string hasFlags,          /* out */
            ref bool hasAll,              /* out */
            ref bool mayNeedRootKeyUsage, /* out */
            ref Result error              /* out */
            )
        {
            KeyUsageTriplet anyTriplet;

            if (CertificateGlobalState.TryGetKeyUsage(
                    entityType, out anyTriplet))
            {
                if (anyTriplet != null)
                {
                    hasFlags = anyTriplet.X;
                    hasAll = anyTriplet.Y;
                    mayNeedRootKeyUsage = anyTriplet.Z;

                    return true;
                }
                else
                {
                    error = String.Format(
                        "forbidden entity type {0} for key usage",
                        Utility.FormatWrapOrNull(entityType));
                }
            }
            else
            {
                error = String.Format(
                    "missing entity type {0} for key usage",
                    Utility.FormatWrapOrNull(entityType));
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified policy type to its corresponding entity type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to map.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The corresponding entity type, or null on failure.
        /// </returns>
        public static EntityType? PolicyTypeToEntityType( /* CORE */
            PolicyType policyType, /* in */
            ref Result error       /* out */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                case PolicyType.File:
                case PolicyType.Stream:
                    {
                        return EntityType.Script;
                    }
                case PolicyType.License:
                    {
                        //
                        // HACK: Using the "Principle
                        //       of Least Powerful"
                        //       option here.
                        //
                        return EntityType.Individual;
                    }
                case PolicyType.KeyPair:
                    {
                        //
                        // HACK: Technically, this may
                        //       not actually be an exact
                        //       match; however, it makes
                        //       the most logical sense.
                        //
                        return EntityType.KeyRing;
                    }
                default:
                    {
                        error = String.Format(
                            "no entity type for policy type {0}",
                            policyType);

                        return null;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified entity type to its required key usage flags,
        /// preferring a custom mapping over the default one.
        /// </summary>
        /// <param name="entityType">
        /// The entity type to map.
        /// </param>
        /// <param name="hasFlags">
        /// Upon return, receives the required key usage flags.
        /// </param>
        /// <param name="hasAll">
        /// Upon return, indicates whether all of the flags are required.
        /// </param>
        /// <param name="mayNeedRootKeyUsage">
        /// Upon return, indicates whether root key usage may be required.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if a key usage mapping was determined.
        /// </returns>
        public static bool EntityTypeToKeyUsage( /* CORE */
            EntityType entityType,        /* in */
            ref string hasFlags,          /* out */
            ref bool hasAll,              /* out */
            ref bool mayNeedRootKeyUsage, /* out */
            ref Result error              /* out */
            )
        {
            Result localError; /* REUSED */
            ResultList errors = null;

            if (CertificateSdkMode.IsEnabled())
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("custom key usage is forbidden");
            }
            else
            {
                localError = null;

                if (CustomEntityTypeToKeyUsage(
                        entityType, ref hasFlags, ref hasAll,
                        ref mayNeedRootKeyUsage, ref localError))
                {
                    return true;
                }
                else
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }
                }
            }

            localError = null;

            if (DefaultEntityTypeToKeyUsage(
                    entityType, ref hasFlags, ref hasAll,
                    ref mayNeedRootKeyUsage, ref localError))
            {
                return true;
            }
            else
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            error = errors;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates the specified key pair and extracts its public key
        /// token.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to validate.
        /// </param>
        /// <param name="publicKeyToken">
        /// Upon success, receives the public key token of the key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckKeyPair( /* CORE */
            IKeyPair keyPair,          /* in */
            ref byte[] publicKeyToken, /* out */
            ref Result error           /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            if (!keyPair.HavePublicKey)
            {
                error = "public key is not present";
                return ReturnCode.Error;
            }

            byte[] localPublicKeyToken = keyPair.PublicKeyToken;

            if (localPublicKeyToken == null)
            {
                error = "invalid public key token";
                return ReturnCode.Error;
            }

            int length = localPublicKeyToken.Length;

            if (length <= 0)
            {
                error = "empty public key token";
                return ReturnCode.Error;
            }

#if DEBUG || FORCE_TRACE
            if (length != sizeof(ulong))
            {
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Public key token {0} has weird length {1}, should be {2}.",
                    DataOps.FormatPublicKeyToken(localPublicKeyToken, true, true),
                    length, sizeof(ulong)), typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh, 0);
            }
#endif

            publicKeyToken = localPublicKeyToken;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair has expired, honoring
        /// the expire-signature key usage flag relative to the supplied
        /// time stamp.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose expiration is to be checked.
        /// </param>
        /// <param name="timeStamp">
        /// The signing time stamp to compare against; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckKeyExpiration( /* CORE */
            IKeyPair keyPair,    /* in */
            DateTime? timeStamp, /* in: OPTIONAL */
            ref Result error     /* out */
            )
        {
            byte[] publicKeyToken = null;

            if (CheckKeyPair(
                    keyPair, ref publicKeyToken,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: If this value is null, the key pair NEVER expires.
            //
            DateTime? keyExpiration = keyPair.KeyExpiration;

            if (keyExpiration == null)
                return ReturnCode.Ok;

            //
            // NOTE: Otherwise, if the key expiration time stamp is before
            //       NOW, the key pair is expired.
            //
            DateTime now = DataOps.GetTimeStamp();

            if ((DateTime)keyExpiration < now)
            {
                //
                // HACK: Ok, the key itself is expired; however, that may
                //       only prevent it from being used to sign entities
                //       after that point-in-time, depending on its key
                //       usage flags, i.e. if the "ExpireSignature" key
                //       usage flag is set, all pre-existing signatures
                //       are still considered to be valid; however, the
                //       key cannot be used to sign anything after that
                //       point.
                //
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if (MatchFlags(
                        keyPair.KeyUsage, FlagType.KeyUsage, flagsKey,
                        KeyUsage.ExpireSignature, null, true, false,
                        true) == ReturnCode.Ok)
                {
                    if (timeStamp != null) /* ICertificateData.TimeStamp */
                    {
                        if ((DateTime)timeStamp <= (DateTime)keyExpiration)
                        {
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            error = String.Format(
                                "public key token {0} cannot sign after {1}",
                                Utility.FormatWrapOrNull(
                                    DataOps.FormatPublicKeyToken(
                                        publicKeyToken, true, true)),
                                Utility.FormatWrapOrNull(
                                    DataOps.FormatTimeStamp(
                                        (DateTime)keyExpiration)));

                            return ReturnCode.Error;
                        }
                    }
                }

                error = String.Format(
                    "public key token {0} is expired as of {1}",
                    Utility.FormatWrapOrNull(
                        DataOps.FormatPublicKeyToken(
                            publicKeyToken, true, true)),
                    Utility.FormatWrapOrNull(
                        DataOps.FormatTimeStamp(
                            (DateTime)keyExpiration)));

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair has root key usage.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="entityType">
        /// The entity type for which the key pair is being used.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode CheckForRootKeyUsage( /* CORE */
            IKeyPair keyPair,      /* in */
            EntityType entityType, /* in */
            ref Result error       /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();
            Result result = null;

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                    KeyUsage.Root, null, true, false, true,
                    ref result) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                TraceKeyUsageError(keyPair, entityType, result);
#endif

                error = result;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate matches the given
        /// key pair identifier and assembly subject.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose subject is to be matched.
        /// </param>
        /// <param name="certificate">
        /// The certificate to compare against.
        /// </param>
        /// <param name="keyPairId">
        /// The key pair identifier to match.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate matches the key pair identifier.
        /// </returns>
        private static bool MatchKeyIdentifier( /* CORE? */
            Assembly assembly,        /* in */
            ICertificate certificate, /* in */
            Guid keyPairId            /* in */
            )
        {
            if (certificate == null)
                return false;

            if (!keyPairId.Equals(certificate.Id))
                return false;

            if (MatchSubject(
                    assembly, certificate, null) != ReturnCode.Ok)
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any known certificate matches the identifier
        /// of the specified key pair and the given assembly subject.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose subject is to be matched.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose identifier is to be matched.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if a matching certificate was found.
        /// </returns>
        public static bool MatchKeyIdentifier( /* CORE? */
            Assembly assembly, /* in */
            IKeyPair keyPair,  /* in */
            ref Result error   /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return false;
            }

            IIdentifierBase identifierBase = keyPair as IIdentifierBase;

            if (identifierBase == null)
            {
                error = "key pair is not an identifier";
                return false;
            }

            Guid keyPairId = identifierBase.Id;

            foreach (ICertificate certificate in new ICertificate[] {
                    LicenseState.GetCertificate(keyPairId),
                    LicenseState.GetCertificate()
                })
            {
                if (MatchKeyIdentifier(
                        assembly, certificate, keyPairId))
                {
                    return true;
                }
            }

            error = String.Format(
                "no matching certificate for key pair {0}", keyPairId);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Applies the inheritable key usage restrictions of the specified
        /// key pair to the supplied key usage flags, for use by child key
        /// pairs.
        /// </summary>
        /// <param name="keyPair">
        /// The parent key pair whose restrictions are to be applied.
        /// </param>
        /// <param name="keyUsage">
        /// On input, the current key usage flags; on return, the
        /// restricted key usage flags.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the restrictions were applied successfully.
        /// </returns>
        public static bool RestrictKeyUsage( /* CORE? */
            IKeyPair keyPair,    /* in */
            ref string keyUsage, /* in, out */
            ref Result error     /* out */
            )
        {
            if (keyPair != null)
            {
                string localKeyUsage = keyUsage;
                string keyPairKeyUsage = keyPair.KeyUsage;
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                //
                // NOTE: Check if the key pair has the "LicenseeOnly" key
                //       usage (restriction) flag.  If so, apply both it
                //       and all the other key usage (restriction) flags
                //       to the resulting key usage flags, i.e. those to
                //       be applied to child key pairs.
                //
                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.LicenseeOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.LicenseeOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.LicenseeOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }

                    ////////////////////////////////////////////////////

                    if (!MatchKeyIdentifier(
                            AssemblyOps.GetObject(), keyPair, ref error))
                    {
                        return false;
                    }
                }

                //
                // NOTE: Check if the key pair has the "InheritOnly" key
                //       usage (restriction) flag.  If so, apply both it
                //       and all the other key usage (restriction) flags
                //       to the resulting key usage flags, i.e. those to
                //       be applied to child key pairs.
                //
                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.InheritOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.InheritOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.InheritOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }
                else
                {
                    //
                    // NOTE: The key pair does not have the "InheritOnly"
                    //       key usage (restriction) flag; therefore, do
                    //       nothing and return success.
                    //
                    return true;
                }

                //
                // TODO: If the list of key usage flags that are actually
                //       restrictions (e.g. end with "Only", etc) changes
                //       this code block must be updated as well.
                //
                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.KeyRingOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.KeyRingOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.KeyRingOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.DeveloperOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.DeveloperOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.DeveloperOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.TestOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.TestOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.TestOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.LimitedTimeOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.LimitedTimeOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.LimitedTimeOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.RelaxedLimitedTimeOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.RelaxedLimitedTimeOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.RelaxedLimitedTimeOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.OnlineOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.OnlineOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.OnlineOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                if (MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage,
                        flagsKey, null, KeyUsage.RelaxedOnlineOnly,
                        false, false, true) != ReturnCode.Ok)
                {
                    if (localKeyUsage == null)
                    {
                        localKeyUsage = KeyUsage.RelaxedOnlineOnly;
                    }
                    else if (!ChangeKeyUsage(
                            localKeyUsage, KeyUsage.RelaxedOnlineOnly,
                            ref localKeyUsage, ref error))
                    {
                        return false;
                    }
                }

                keyUsage = localKeyUsage;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Checks the specified script text against the active policy and
        /// returns whether it was approved.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="text">
        /// The script text to check.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckScriptAgainstPolicy( /* CORE? */
            Interpreter interpreter, /* in */
            string text,             /* in */
            ref Result result        /* out */
            )
        {
            IPolicyContext policyContext = null;
            Result localResult = null;

            if (CheckScriptAgainstPolicy(
                    interpreter, text, ref policyContext,
                    ref localResult) == ReturnCode.Ok)
            {
                if (policyContext != null)
                {
                    if (policyContext.IsApproved())
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "script approved by policy";

                        return ReturnCode.Ok;
                    }
                    else if (policyContext.IsDenied())
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "script denied by policy";
                    }
                    else
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "script not approved by policy";
                    }
                }
                else
                {
                    if (localResult != null)
                        result = localResult;
                    else
                        result = "missing policy context for script";
                }
            }
            else
            {
                if (localResult != null)
                    result = localResult;
                else
                    result = "could not check script against policy";
            }

            return ReturnCode.Error;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the specified script text against the active policy,
        /// returning the resulting policy context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="text">
        /// The script text to check.
        /// </param>
        /// <param name="policyContext">
        /// Upon success, receives the policy context produced by the
        /// check.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckScriptAgainstPolicy( /* CORE? */
            Interpreter interpreter,          /* in */
            string text,                      /* in */
            ref IPolicyContext policyContext, /* out */
            ref Result result                 /* out */
            )
        {
            IPlugin plugin = null;

            if (PolicyOps.GetPlugin(
                    interpreter, ref plugin, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IPolicyContext localPolicyContext = PolicyContext.Create(
                PolicyFlags.AfterFile, null, null, null, null,
                null, Constants.NoFileName, null, text, null,
                null, null, null, ClientData.Empty, interpreter,
                plugin, PolicyDecision.None);

            IClientData clientData = new ClientData(localPolicyContext);

            if (Policies.File.PolicyCallbackHelper(
                    interpreter, clientData, null, true,
                    ref result) == ReturnCode.Ok)
            {
                policyContext = localPolicyContext;
                return ReturnCode.Ok;
            }
            else
            {
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the specified script file against the active policy and
        /// returns whether it was approved.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The script file name to check.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckFileAgainstPolicy( /* CORE? */
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            ref Result result        /* out */
            )
        {
            IPolicyContext policyContext = null;
            Result localResult = null;

            if (CheckFileAgainstPolicy(
                    interpreter, encoding, fileName,
                    timeout, ref policyContext,
                    ref localResult) == ReturnCode.Ok)
            {
                if (policyContext != null)
                {
                    if (policyContext.IsApproved())
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "file approved by policy";

                        return ReturnCode.Ok;
                    }
                    else if (policyContext.IsDenied())
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "file denied by policy";
                    }
                    else
                    {
                        if (localResult != null)
                            result = localResult;
                        else
                            result = "file not approved by policy";
                    }
                }
                else
                {
                    if (localResult != null)
                        result = localResult;
                    else
                        result = "missing policy context for file";
                }
            }
            else
            {
                if (localResult != null)
                    result = localResult;
                else
                    result = "could not check file against policy";
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the specified script file against the active policy,
        /// returning the resulting policy context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The script file name to check.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds.
        /// </param>
        /// <param name="policyContext">
        /// Upon success, receives the policy context produced by the
        /// check.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckFileAgainstPolicy( /* CORE? */
            Interpreter interpreter,          /* in */
            Encoding encoding,                /* in */
            string fileName,                  /* in */
            int? timeout,                     /* in: OPTIONAL */
            ref IPolicyContext policyContext, /* out */
            ref Result result                 /* out */
            )
        {
            IPlugin plugin = null;

            if (PolicyOps.GetPlugin(
                    interpreter, ref plugin, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IClientData readScriptClientData = null;
            string text = null; /* NOT USED */

            if (Engine.ReadScriptFile(interpreter, fileName,
                    Constants.ReadEngineFlags, ref readScriptClientData,
                    ref text, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string originalText = Engine.GetReadScriptFileOriginalText(
                readScriptClientData);

            if (originalText == null)
            {
                result = "original script file text unavailable";
                return ReturnCode.Error;
            }

            Encoding policyEncoding = encoding;

            if (policyEncoding == null)
                policyEncoding = DataOps.GetDefaultEncoding();

            byte[] bytes;

            try
            {
                bytes = policyEncoding.GetBytes(originalText); /* throw */
            }
            catch (Exception e)
            {
                result = e;
                return ReturnCode.Error;
            }

            IPolicyContext localPolicyContext = PolicyContext.Create(
                PolicyFlags.AfterFile, null, null, null, null,
                null, fileName, bytes, originalText, policyEncoding,
                timeout, null, null, ClientData.Empty, interpreter,
                plugin, PolicyDecision.None);

            IClientData clientData = new ClientData(localPolicyContext);

            if (Policies.File.PolicyCallbackHelper(
                    interpreter, clientData, null, true,
                    ref result) == ReturnCode.Ok)
            {
                policyContext = localPolicyContext;
                return ReturnCode.Ok;
            }
            else
            {
                return ReturnCode.Error;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies a key usage change to the specified key usage flags.
        /// </summary>
        /// <param name="oldKeyUsage">
        /// The original key usage flags.
        /// </param>
        /// <param name="changeKeyUsage">
        /// The key usage flags to apply.
        /// </param>
        /// <param name="newKeyUsage">
        /// Upon success, receives the resulting key usage flags.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the change was applied successfully.
        /// </returns>
        public static bool ChangeKeyUsage( /* CORE */
            string oldKeyUsage,     /* in */
            string changeKeyUsage,  /* in */
            ref string newKeyUsage, /* out */
            ref Result error        /* out */
            )
        {
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            if (FlagOps.Change(
                    oldKeyUsage, changeKeyUsage,
                    flagsKey, true, false, true,
                    true, false, ref newKeyUsage,
                    ref error) == ReturnCode.Ok)
            {
                return true;
            }

#if DEBUG || FORCE_TRACE
            TraceOps.MaybeLogAndDebugTrace(String.Format(
                "Cannot change key usage {0} with {1}: {2}",
                Utility.FormatWrapOrNull(oldKeyUsage),
                Utility.FormatWrapOrNull(changeKeyUsage),
                Utility.FormatWrapOrNull(true, false, error)),
                typeof(CertificateSharedOps).Name,
                TracePriority.Higher, 0);
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a diagnostic trace message describing why the specified
        /// key pair could not be used for the given entity type.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair involved in the error.
        /// </param>
        /// <param name="entityType">
        /// The entity type the key pair was being used for.
        /// </param>
        /// <param name="error">
        /// The error to be traced.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceKeyUsageError( /* CORE */
            IKeyPair keyPair,      /* in */
            EntityType entityType, /* in */
            Result error           /* in */
            )
        {
            TraceOps.MaybeLogAndDebugTrace(String.Format(
                "Cannot use key pair {0} for {1}: {2}",
                Utility.FormatWrapOrNull((keyPair != null) ?
                    DataOps.FormatPublicKeyToken(
                        keyPair.PublicKeyToken, true, true) : null),
                entityType,
                Utility.FormatWrapOrNull(true, false, error)),
                typeof(CertificateSharedOps).Name,
                TracePriority.MediumLow, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair may be used for the
        /// given entity type.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="entityType">
        /// The entity type the key pair would be used for.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckKeyUsage( /* CORE */
            IKeyPair keyPair,     /* in */
            EntityType entityType /* in */
            )
        {
            Result error = null;

            return CheckKeyUsage(keyPair, entityType, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair may be used for the
        /// given entity type, returning a detailed error on failure.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="entityType">
        /// The entity type the key pair would be used for.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode CheckKeyUsage( /* CORE */
            IKeyPair keyPair,      /* in */
            EntityType entityType, /* in */
            ref Result error       /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();
            Result result; /* REUSED */

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.LicenseeOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                result = null;

                if (!MatchKeyIdentifier(
                        AssemblyOps.GetObject(), keyPair, ref result))
                {
                    result = Utility.MaybeCombineResults(
                        "key pair is for licensee use only", result);

#if DEBUG || FORCE_TRACE
                    TraceKeyUsageError(keyPair, entityType, result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.DeveloperOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                result = "key pair is for developer use only";

#if DEBUG || FORCE_TRACE
                TraceKeyUsageError(keyPair, entityType, result);
#endif

#if !DEBUG
                error = result;
                return ReturnCode.Error;
#endif
            }

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.TestOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                if (!CertificateTestMode.IsEnabled()
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
                        || !CertificateGlobalState.IsEnableTestModeOrAll()
#endif
                    )
                {
                    result = "key pair is for test use only";

#if DEBUG || FORCE_TRACE
                    TraceKeyUsageError(keyPair, entityType, result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, KeyUsage.Any, null, true,
                    false, true) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.KeyRingOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                if ((entityType != EntityType.KeyRing)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                        || !CertificatePolicyState.IsKeyRingPending()
#endif
                    )
                {
                    result = "key pair is for key ring use only";

#if DEBUG || FORCE_TRACE
                    TraceKeyUsageError(keyPair, entityType, result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }

            string entityTypeKeyUsage = null;
            bool entityTypeHasAll = false;
            bool entityTypeMayNeedRootKeyUsage = false;

            if (!EntityTypeToKeyUsage(entityType,
                    ref entityTypeKeyUsage, ref entityTypeHasAll,
                    ref entityTypeMayNeedRootKeyUsage, ref error))
            {
#if DEBUG || FORCE_TRACE
                TraceKeyUsageError(keyPair, entityType, error);
#endif

                return ReturnCode.Error;
            }

            result = null;

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                    entityTypeKeyUsage, null, entityTypeHasAll,
                    false, true, ref result) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                TraceKeyUsageError(keyPair, entityType, result);
#endif

                error = result;
                return ReturnCode.Error;
            }

            //
            // NOTE: *HACK* If an "Intermediate" key is being checked
            //       because it has been used to sign another key, it
            //       must have been signed by a "Root" key.  It should
            //       be noted here that this restriction is simply a
            //       policy and that this policy effectively prevents
            //       more than one layer of "Intermediate" keys from
            //       being used.  This policy is subject to change at
            //       any time.  If an "Intermediate" key also happens
            //       to be a "Root" key, this policy does not apply.
            //
            // HACK: Actually, the above is a bit too restrictive to
            //       be an absolute rule; therefore, permit an extra
            //       key usage flag (i.e. "Delegation") to bypass.
            //
            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, KeyUsage.Delegation, null,
                    true, false, true) == ReturnCode.Ok)
            {
                result = null;

                if (entityTypeMayNeedRootKeyUsage && MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                        KeyUsage.Intermediate, null, true, false,
                        true, ref result) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    TraceKeyUsageError(keyPair, entityType, result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }
            else
            {
                if (entityTypeMayNeedRootKeyUsage && MatchFlags(
                        keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                        KeyUsage.Intermediate, KeyUsage.Root, true,
                        false, true) == ReturnCode.Ok)
                {
                    if (CheckForRootKeyUsage(
                            keyPair.Parent as IKeyPair, entityType,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair is permitted to sign
        /// entities with an unlimited time duration.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode CanSignWithUnlimitedTime( /* CORE */
            IKeyPair keyPair, /* in */
            ref Result error  /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                    null, KeyUsage.LimitedTimeOnly, false, false,
                    true, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Cannot use key pair {0}: {1}",
                    Utility.FormatWrapOrNull(
                        DataOps.FormatPublicKeyToken(
                            keyPair.PublicKeyToken, true, true)),
                    Utility.FormatWrapOrNull(true, false, error)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair is permitted to renew
        /// limited-time entities.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode CanRenewWithLimitedTime( /* CORE */
            IKeyPair keyPair, /* in */
            ref Result error  /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                    KeyUsage.RelaxedLimitedTimeOnly, null, true,
                    false, true, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Cannot renew with key pair {0}: {1}",
                    Utility.FormatWrapOrNull(
                        DataOps.FormatPublicKeyToken(
                            keyPair.PublicKeyToken, true, true)),
                    Utility.FormatWrapOrNull(true, false, error)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair is permitted to
        /// convert entities to a limited time duration.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        private static ReturnCode CanConvertToLimitedTime( /* CORE */
            IKeyPair keyPair, /* in */
            ref Result error  /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            if (MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage, flagsKey,
                    KeyUsage.ConvertToLimitedTime, null, true,
                    false, true, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(String.Format(
                    "Cannot limit duration with key pair {0}: {1}",
                    Utility.FormatWrapOrNull(
                        DataOps.FormatPublicKeyToken(
                            keyPair.PublicKeyToken, true, true)),
                    Utility.FormatWrapOrNull(true, false, error)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the simple (short) name of the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to query.
        /// </param>
        /// <returns>
        /// The simple assembly name, or null if unavailable.
        /// </returns>
        private static string GetAssemblySimpleName( /* CORE */
            Assembly assembly /* in */
            )
        {
            if (assembly == null)
                return null;

            AssemblyName assemblyName = assembly.GetName();

            if (assemblyName == null)
                return null;

            return assemblyName.Name;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Gets the names of the embedded resources in the specified
        /// assembly that match the given pattern.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to enumerate.
        /// </param>
        /// <param name="pattern">
        /// The match pattern to apply to resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match without regard to case.
        /// </param>
        /// <param name="keysOnly">
        /// Non-zero to include only key file resources.
        /// </param>
        /// <returns>
        /// The matching resource names, or null on failure.
        /// </returns>
        public static IEnumerable<string> GetEmbeddedNames( /* CORE? */
            Assembly assembly, /* in */
            string pattern,    /* in */
            bool noCase,       /* in */
            bool keysOnly      /* in */
            )
        {
            Result error = null;

            return GetEmbeddedNames(
                assembly, pattern, noCase, keysOnly, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the names of the embedded resources in the specified
        /// assembly that match the given pattern, returning a detailed
        /// error on failure.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to enumerate.
        /// </param>
        /// <param name="pattern">
        /// The match pattern to apply to resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match without regard to case.
        /// </param>
        /// <param name="keysOnly">
        /// Non-zero to include only key file resources.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The matching resource names, or null on failure.
        /// </returns>
        public static IEnumerable<string> GetEmbeddedNames( /* CORE? */
            Assembly assembly, /* in */
            string pattern,    /* in */
            bool noCase,       /* in */
            bool keysOnly,     /* in */
            ref Result error   /* out */
            )
        {
            if (assembly == null)
            {
                error = "invalid assembly";
                return null;
            }

            try
            {
                StringList list = new StringList();

                foreach (string resourceName
                        in assembly.GetManifestResourceNames())
                {
                    if (String.IsNullOrEmpty(resourceName))
                        continue;

                    if (keysOnly)
                    {
                        string fileExtension = Path.GetExtension(
                            resourceName);

                        if ((Utility.CompareFileNames(fileExtension,
                                FileExtension.StrongNameKey) != 0) &&
                            (Utility.CompareFileNames(fileExtension,
                                FileExtension.PrivateKey) != 0) &&
                            (Utility.CompareFileNames(fileExtension,
                                FileExtension.DsaStrongNameKey) != 0) &&
                            (Utility.CompareFileNames(fileExtension,
                                FileExtension.DsaPrivateKey) != 0))
                        {
                            continue;
                        }
                    }

                    if ((pattern != null) && !Parser.StringMatch(
                            null, resourceName, 0, pattern, 0, noCase))
                    {
                        continue;
                    }

                    list.Add(resourceName);
                }

                return list;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Derives the embedded resource name corresponding to the
        /// specified markup file name.
        /// </summary>
        /// <param name="fileName">
        /// The file name to convert.
        /// </param>
        /// <returns>
        /// The corresponding embedded resource name.
        /// </returns>
        public static string ResourceNameFromFileName( /* CORE */
            string fileName /* in */
            )
        {
            string fileExtension = Path.GetExtension(fileName);

            if (!Utility.StringEquals(
                    fileExtension, FileExtension.Markup,
                    Utility.GetPathComparisonType()) &&
                !Utility.StringEquals(
                    fileExtension, FileExtension.EncryptedMarkup,
                    Utility.GetPathComparisonType()))
            {
                fileExtension = FileExtension.Markup;
            }

            return String.Format(
                "{0}{1}", Path.GetFileNameWithoutExtension(fileName),
                fileExtension);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the assembly contained in the specified bytes for
        /// reflection-only use.
        /// </summary>
        /// <param name="bytes">
        /// The raw bytes of the assembly to load.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The loaded assembly, or null on failure.
        /// </returns>
        private static Assembly GetAssembly( /* CORE */
            byte[] bytes,    /* in */
            ref Result error /* out */
            )
        {
            return CertificateAssemblyCache.ForReflectionOnly(
                bytes, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bytes of an embedded resource from the assembly
        /// contained in the specified bytes, trying the supplied resource
        /// name and any well-known fallbacks.
        /// </summary>
        /// <param name="fileName">
        /// The file name associated with the assembly bytes.
        /// </param>
        /// <param name="bytes">
        /// The raw bytes of the assembly.
        /// </param>
        /// <param name="resourceName">
        /// The resource name to look for; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to permit any resource public key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero to also try the well-known resource names for this
        /// assembly.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The resource bytes, or null on failure.
        /// </returns>
        public static byte[] GetEmbeddedBytes( /* CORE */
            string fileName,           /* in */
            byte[] bytes,              /* in */
            string resourceName,       /* in */
            bool anyResourcePublicKey, /* in */
            bool isForThisAssembly,    /* in */
            ref Result error           /* out */
            )
        {
            StringList resourceNames = new StringList();

            if (resourceName != null)
                resourceNames.Add(resourceName);

            if (isForThisAssembly)
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                resourceNames.Add(String.Format(
                    Constants.ThisAssemblyEmbeddedResourceFormat,
                    FileExtension.EncryptedMarkup));
#endif

                resourceNames.Add(String.Format(
                    Constants.ThisAssemblyEmbeddedResourceFormat,
                    FileExtension.Markup));
            }

            return GetEmbeddedBytes(
                fileName, bytes, resourceNames, anyResourcePublicKey,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bytes of the first matching embedded resource from the
        /// assembly contained in the specified bytes, after verifying its
        /// strong name.
        /// </summary>
        /// <param name="fileName">
        /// The file name associated with the assembly bytes.
        /// </param>
        /// <param name="bytes">
        /// The raw bytes of the assembly.
        /// </param>
        /// <param name="resourceNames">
        /// The resource names to look for, in order.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to permit any resource public key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The resource bytes, or null on failure.
        /// </returns>
        private static byte[] GetEmbeddedBytes( /* CORE */
            string fileName,                   /* in */
            byte[] bytes,                      /* in */
            IEnumerable<string> resourceNames, /* in */
            bool anyResourcePublicKey,         /* in */
            ref Result error                   /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid file name";
                return null;
            }

            if (bytes == null)
            {
                error = "invalid assembly bytes";
                return null;
            }

            if (resourceNames == null)
            {
                error = "invalid resource names";
                return null;
            }

            string temporaryDirectory = null;
            string fileNameOnly = null;

            try
            {
                fileNameOnly = Path.GetFileName(
                    fileName); /* throw */

                if (String.IsNullOrEmpty(fileNameOnly))
                {
                    error = "invalid file name only";
                    return null;
                }

                temporaryDirectory = Utility.GetUniquePath(
                    null, Utility.GetTempPath(null), null,
                    null, ref error);

                if (temporaryDirectory == null)
                    return null;

                Directory.CreateDirectory(
                    temporaryDirectory); /* throw */

                string temporaryFileName = Path.Combine(
                    temporaryDirectory, fileNameOnly);

                File.WriteAllBytes(
                    temporaryFileName, bytes); /* throw */

                byte[] publicKeyToken = MaybeGetPublicKeyToken(
                    anyResourcePublicKey);

                if (!VerifyAssemblyFromFile(
                        temporaryFileName, publicKeyToken,
                        ref error))
                {
                    return null;
                }

                //
                // HACK: The CoreCLR runtime has never really supported
                //       reflection-only loading of assemblies.  Please
                //       refer to the following StackOverflow question
                //       and the GitHub issue it mentions, here:
                //
                //       https://stackoverflow.com/questions/69093562
                //
                //                        -AND-
                //
                //       https://github.com/dotnet/runtime/issues/7273
                //
                Assembly assembly;

                if (Utility.IsDotNetCore())
                {
                    //
                    // NOTE: This requires that the assembly being
                    //       checked for the embedded resource has
                    //       already been loaded into the current
                    //       AppDomain.  This will attempt to find
                    //       it by its fully qualified file name.
                    //
                    assembly = Utility.FindAssemblyInAppDomain(
                        null, null, null, fileName, null, ref error);
                }
                else
                {
                    assembly = GetAssembly(bytes, ref error);
                }

                if (assembly == null)
                    return null;

                ResultList errors = null;

                foreach (string resourceName in resourceNames)
                {
                    if (String.IsNullOrEmpty(resourceName))
                        continue;

                    byte[] result;
                    Result localError = null;

                    result = GetEmbeddedBytes(
                        assembly, resourceName, ref localError);

                    if (result != null)
                        return result;

                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }
                }

                if (errors != null)
                    error = errors;
                else
                    error = "no matching resource found";

                return null;
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                if (temporaryDirectory != null)
                {
                    /* IGNORED */
                    Utility.CleanupDirectory(temporaryDirectory,
                        new string[] { fileNameOnly }, true);
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bytes of the named embedded resource from the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the resource to read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The resource bytes, or null on failure.
        /// </returns>
        public static byte[] GetEmbeddedBytes( /* CORE */
            Assembly assembly,   /* in */
            string resourceName, /* in */
            ref Result error     /* out */
            )
        {
            if (assembly == null)
                return null;

            Stream stream = GetStream(assembly, resourceName, ref error);

            if (stream == null)
                return null;

            try
            {
                using (BinaryReader binaryReader = new BinaryReader(stream))
                {
                    return binaryReader.ReadBytes(
                        (int)stream.Length); /* throw */
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the embedded resource stream described by the specified
        /// value, which may include an assembly name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="value">
        /// The list value naming the resource and optional assembly.
        /// </param>
        /// <param name="assembly">
        /// Upon success, receives the assembly containing the resource.
        /// </param>
        /// <param name="stream">
        /// Upon success, receives the resource stream.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode GetStream( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            string value,            /* in */
            ref Assembly assembly,   /* out */
            ref Stream stream,       /* out */
            ref Result error         /* out */
            )
        {
            StringList list = null;

            if (Parser.SplitList(
                    interpreter, value, 0, Length.Invalid, true,
                    ref list, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            Assembly localAssembly = AssemblyOps.GetObject();

            if (list.Count >= 2)
            {
                localAssembly = Utility.FindAssemblyInAppDomain(
                    interpreter, null, MatchMode.Glob, list[1],
                    false, null, ref error);

                if (localAssembly == null)
                    return ReturnCode.Error;
            }

            Stream localStream = GetStream(localAssembly, list[0],
                ref error);

            if (localStream == null)
                return ReturnCode.Error;

            assembly = localAssembly;
            stream = localStream;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the named embedded resource stream from the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the resource to open.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The resource stream, or null on failure.
        /// </returns>
        public static Stream GetStream( /* CORE */
            Assembly assembly,   /* in */
            string resourceName, /* in */
            ref Result error     /* out */
            )
        {
            if (assembly == null)
            {
                error = "invalid assembly";
                return null;
            }

            if (resourceName == null)
            {
                error = "invalid resource name";
                return null;
            }

            try
            {
                Stream stream = assembly.GetManifestResourceStream(
                    resourceName);

                if (stream != null)
                {
                    return stream;
                }
                else
                {
                    error = String.Format(
                        "missing manifest resource stream {0}",
                        Utility.FormatWrapOrNull(resourceName));
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the first matching embedded resource stream for the
        /// specified plugin, searching the plugin, assembly, and this
        /// assembly as appropriate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to search; this parameter is optional when a
        /// plugin is supplied.
        /// </param>
        /// <param name="plugin">
        /// The plugin to search; this parameter is optional when an
        /// assembly is supplied.
        /// </param>
        /// <param name="clientData">
        /// The client data context; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="bootstrapType">
        /// The bootstrap type used to derive candidate resource names.
        /// </param>
        /// <param name="policy">
        /// The execution policy controlling which assemblies are searched.
        /// </param>
        /// <param name="isForThisPlugin">
        /// Non-zero if the lookup is for this plugin.
        /// </param>
        /// <param name="resourceName">
        /// Upon success, receives the resource name that was found.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The resource stream, or null on failure.
        /// </returns>
        public static Stream GetStream( /* CORE */
            Interpreter interpreter,     /* in: OPTIONAL */
            Assembly assembly,           /* in: OPTIONAL With plugin. */
            IPlugin plugin,              /* in: OPTIONAL With assembly. */
            IClientData clientData,      /* in: OPTIONAL */
            BootstrapType bootstrapType, /* in */
            ExecutionPolicy policy,      /* in */
            bool isForThisPlugin,        /* in */
            ref string resourceName,     /* out */
            ref Result error             /* out */
            )
        {
            StringList resourceNames = null;

            if (PathOps.GetFileNamesOnly(
                    interpreter, assembly, plugin, clientData,
                    bootstrapType, isForThisPlugin,
                    ref resourceNames, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            IList<Assembly> assemblies = new List<Assembly>(3);

            if ((plugin != null) &&
                !Utility.IsCrossAppDomain(plugin))
            {
                Assembly pluginAssembly = plugin.Assembly;

                if ((pluginAssembly != null) &&
                    !Object.ReferenceEquals(pluginAssembly, assembly))
                {
                    assemblies.Add(pluginAssembly);
                }
            }

            if (assembly != null)
                assemblies.Add(assembly);

            if (!Utility.HasFlags(
                    policy, ExecutionPolicy.SkipThisStream, true))
            {
                /* IGNORED */
                AssemblyOps.MaybeAddObject(assemblies);
            }

            ResultList errors = null;

            foreach (string localResourceName in resourceNames)
            {
                if (localResourceName == null)
                    continue;

                foreach (Assembly localAssembly in assemblies)
                {
                    if (localAssembly == null)
                        continue;

                    Stream stream; /* REUSED */
                    Result localError = null; /* REUSED */

                    stream = GetStream(
                        localAssembly, localResourceName,
                        ref localError);

                    if (stream != null)
                    {
                        resourceName = localResourceName;
                        return stream;
                    }

                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    string localAssemblySimpleName = GetAssemblySimpleName(
                        localAssembly);

                    if (localAssemblySimpleName != null)
                    {
                        string fallbackResourceName = String.Format(
                            Constants.FallbackEmbeddedResourceFormat,
                            localAssemblySimpleName, localResourceName);

                        localError = null;

                        stream = GetStream(
                            localAssembly, fallbackResourceName,
                            ref localError);

                        if (stream != null)
                        {
                            resourceName = fallbackResourceName;
                            return stream;
                        }

                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }
                    }
                }
            }

            if (errors != null)
                error = errors;
            else
                error = "unable to get stream";

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the specified certificate has a well-known
        /// public key token.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to check.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate key is a well-known public key
        /// token.
        /// </returns>
        private static bool HasWellKnownPublicKeyToken( /* CORE? */
            ICertificate certificate /* in */
            )
        {
            if (certificate == null)
                return false;

            string reason = null; /* NOT USED */
            Result error = null; /* NOT USED */

            return IsWellKnownPublicKeyToken(
                certificate.Key, ref reason, ref error);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified public key token is one of the
        /// well-known tokens recognized by this class.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to check.
        /// </param>
        /// <param name="reason">
        /// Upon success, receives the reason the token is well-known.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the public key token is well-known.
        /// </returns>
        public static bool IsWellKnownPublicKeyToken( /* CORE */
            byte[] publicKeyToken, /* in */
            ref string reason,     /* out */
            ref Result error       /* out */
            )
        {
            if (publicKeyToken == null)
            {
                error = "invalid public key token";
                return false;
            }

            if (DataOps.MatchPublicKeyToken(
                    publicKeyToken, Constants.EnterprisePublicKeyTokenBytes))
            {
                reason = Constants.EnterpriseKeyReason;
                return true;
            }

#if DEMO_KEY_PAIRS || DEMO_EDITION
            //
            // TODO: Maybe remove this and simply rely on the Promotional
            //       ("P") feature flag?
            //
            if (CertificateDemoMode.IsEnabled() &&
                DataOps.MatchPublicKeyToken(
                    publicKeyToken, Constants.DemoPublicKeyTokenBytes))
            {
                reason = Constants.DemoKeyReason;
                return true;
            }
#endif

            error = String.Format(
                "public key token {0} is not well-known",
                DataOps.FormatPublicKeyToken(publicKeyToken, true, true));

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified public key token matches a
        /// known token and, if so, returns a descriptive tag.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to check.
        /// </param>
        /// <param name="tag">
        /// Upon return, receives a descriptive tag for the token.
        /// </param>
        /// <returns>
        /// Non-zero if the public key token matched a known token.
        /// </returns>
        public static bool IsWellKnownPublicKeyToken( /* CORE */
            byte[] publicKeyToken, /* in */
            ref string tag         /* out */
            )
        {
            if (publicKeyToken == null)
            {
                tag = "<null>";
                return false;
            }

            if (publicKeyToken.Length == 0)
            {
                tag = "<empty>";
                return false;
            }

            StringPair[] pairs = {
                new StringPair(_PublicKeyToken.Fast, "<fast>"),
                new StringPair(_PublicKeyToken.Strong, "<strong>"),
                new StringPair(_PublicKeyToken.Beta, "<beta>"),
                new StringPair(_PublicKeyToken.Security, "<security>"),
                new StringPair(_PublicKeyToken.TrustRoot, "<trustRoot>"),
                new StringPair(_PublicKeyToken.Class0, "<class0>"),
                new StringPair(_PublicKeyToken.Class1, "<class1>"),
                new StringPair(_PublicKeyToken.Class2, "<class2>"),
                new StringPair(_PublicKeyToken.Demo, "<demo>"),
                new StringPair(_PublicKeyToken.Mistachkin, "<mistachkin>"),
                new StringPair(_PublicKeyToken.Build, "<build>"),
                new StringPair(_PublicKeyToken.Test, "<test>")
            };

            foreach (StringPair pair in pairs)
            {
                if (pair == null)
                    continue;

                if (DataOps.MatchPublicKeyToken(publicKeyToken,
                    DataOps.ParsePublicKeyToken(pair.X)))
                {
                    tag = pair.Y;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a process-specific environment variable name from the
        /// specified parts and the current process identifier.
        /// </summary>
        /// <param name="part1">
        /// The first name part; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="part2">
        /// The second name part; this parameter is optional and may be
        /// null.
        /// </param>
        /// <returns>
        /// The constructed environment variable name.
        /// </returns>
        public static string GetEnvVarName( /* CORE */
            string part1, /* in: OPTIONAL */
            string part2  /* in: OPTIONAL */
            )
        {
            Process process = Process.GetCurrentProcess();

            return String.Format("__{0}_{1}_{2}", part1, part2,
                (process != null) ? process.Id : 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate requires
        /// activation.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to check.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the status or error.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate requires activation.
        /// </returns>
        public static bool NeedsActivation( /* CORE */
            ICertificate certificate, /* in */
            ref Result result         /* out */
            )
        {
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            return (MatchFlags(
                certificate, FlagType.Restriction, flagsKey,
                null, Restrictions.Activation, false, false,
                true, ref result) != ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        /// <summary>
        /// Gets the configured host name or address of the network time
        /// server to use, or an empty string to force the primary server.
        /// </summary>
        /// <param name="viaHttp">
        /// Non-zero to select an HTTP-based time server.
        /// </param>
        /// <param name="forcePrimary">
        /// Non-zero to force use of the primary server.
        /// </param>
        /// <returns>
        /// The host name or address, an empty string, or null.
        /// </returns>
        public static string GetTimeHostNameOrAddress( /* CORE */
            bool viaHttp,     /* in */
            bool forcePrimary /* in */
            )
        {
#if DEBUG || EXTRA_DIAGNOSTICS
            string value = Configuration.GetVariable(
                Constants.NetworkTimeUriEnvVarName);

            if (value != null)
                return value;

            value = Configuration.GetVariable(String.Format(
                Constants.NetworkTimeUriEnvVarFormat, viaHttp ?
                "Http" : "Ntp"));

            if (value != null)
                return value;

            if (Configuration.DoesVariableExist(
                    Constants.PrimaryNetworkTimeEnvVarName))
            {
                return String.Empty;
            }
#endif

            if (forcePrimary)
                return String.Empty;

#pragma warning disable 429
            return Constants.NetworkTimeForcePrimary ?
                String.Empty : null;
#pragma warning restore 429
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects a network time server host name or address from the
        /// caller-supplied value, the interpreter, or the default server
        /// list.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="defaultServers">
        /// The default time servers to fall back on; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="syncRoot">
        /// The lock object guarding access to the default servers.
        /// </param>
        /// <param name="callback">
        /// The callback used to obtain descriptive time strings.
        /// </param>
        /// <param name="hostNameOrAddress">
        /// On input, an optional preferred host; on return, the selected
        /// host name or address.
        /// </param>
        /// <param name="serverType">
        /// Upon return, receives a description of the selected server
        /// type; present only in diagnostic builds.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered during selection.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode SelectTimeHostNameOrAddress( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            string[] defaultServers,        /* in: OPTIONAL */
            object syncRoot,                /* in */
            GetTimeStringCallback callback, /* in */
            ref string hostNameOrAddress,   /* in, out: OPTIONAL */
#if DEBUG || FORCE_TRACE
            ref string serverType,          /* out */
#endif
            ref ResultList errors           /* in, out: OPTIONAL */
            )
        {
            if (callback == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid time string callback");
                return ReturnCode.Error;
            }

            string localHostNameOrAddress = hostNameOrAddress;
            ReturnCode code; /* REUSED */
            object value; /* REUSED */
            Result localError; /* REUSED */

            if (localHostNameOrAddress != null)
            {
                if (localHostNameOrAddress.Length == 0)
                {
                    if (syncRoot == null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add("invalid time static lock");
                        return ReturnCode.Error;
                    }

                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        if ((defaultServers == null) ||
                            (defaultServers.Length == 0))
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add("primary server unavailable");
                            return ReturnCode.Error;
                        }

                        localHostNameOrAddress = defaultServers[0];

                        if (localHostNameOrAddress == null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(
                                "invalid primary host name or address");

                            return ReturnCode.Error;
                        }
                    }

#if DEBUG || FORCE_TRACE
                    serverType = callback(TimeStringType.PrimaryServer);
#endif

                    hostNameOrAddress = localHostNameOrAddress;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    serverType = callback(TimeStringType.ManualServer);
#endif
                }

#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "SelectTimeHostNameOrAddress(1): " +
                        "hostNameOrAddress = {0}",
                        Utility.FormatWrapOrNull(
                            hostNameOrAddress)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return ReturnCode.Ok;
            }
            else
            {
                bool badServer = false;

                string[] servers = NetworkOps.TryGetTimeServers(
                    interpreter, true, ref badServer, ref errors);

                if (servers != null)
                {
                    value = null;
                    localError = null;

                    code = Utility.SelectRandomArrayValue(
                        interpreter, servers, ref value,
                        ref localError);

                    if (code != ReturnCode.Ok)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }

                        if (errors == null)
                        {
                            errors = new ResultList();

                            errors.Add(callback(
                                TimeStringType.UnknownError, 1));
                        }

                        return code;
                    }

                    localHostNameOrAddress = value as string;

                    if (localHostNameOrAddress == null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(
                            "invalid random interpreter host name or address");

                        return ReturnCode.Error;
                    }

#if DEBUG || FORCE_TRACE
                    serverType = callback(TimeStringType.InterpreterServer);
#endif
                }
                else if (badServer)
                {
                    //
                    // NOTE: The interpreter was valid -AND- it had a valid
                    //       list of time servers; however, none were good
                    //       (e.g. not signed correctly, etc).  Also, it is
                    //       possible that the list was valid -AND- empty.
                    //
                    return ReturnCode.Error;
                }

                //
                // NOTE: Did we manage to select a network time server via
                //       the interpreter?
                //
                if (localHostNameOrAddress == null)
                {
                    if (syncRoot == null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add("invalid time static lock");
                        return ReturnCode.Error;
                    }

                    value = null;
                    localError = null;

                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        code = Utility.SelectRandomArrayValue(
                            interpreter, defaultServers, ref value,
                            ref localError);
                    }

                    if (code != ReturnCode.Ok)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }

                        if (errors == null)
                        {
                            errors = new ResultList();

                            errors.Add(callback(
                                TimeStringType.UnknownError, 2));
                        }

                        return code;
                    }

                    localHostNameOrAddress = value as string;

                    if (localHostNameOrAddress == null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(
                            "invalid random default host name or address");

                        return ReturnCode.Error;
                    }

#if DEBUG || FORCE_TRACE
                    serverType = callback(TimeStringType.DefaultServer);
#endif
                }

                hostNameOrAddress = localHostNameOrAddress;

#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "SelectTimeHostNameOrAddress(2): " +
                        "hostNameOrAddress = {0}",
                        Utility.FormatWrapOrNull(
                            hostNameOrAddress)),
                    typeof(CertificateSharedOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return ReturnCode.Ok;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the string dictionary carried by the specified
        /// client data.
        /// </summary>
        /// <param name="clientData">
        /// The client data to query.
        /// </param>
        /// <param name="dictionary">
        /// Upon success, receives the string dictionary.
        /// </param>
        /// <returns>
        /// Non-zero if the dictionary was obtained.
        /// </returns>
        public static bool TryGetDictionary( /* CORE */
            IClientData clientData,         /* in */
            ref StringDictionary dictionary /* out */
            )
        {
            Result error = null;

            return TryGetDictionary(
                clientData, ref dictionary, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the string dictionary carried by the specified
        /// client data, returning a detailed error on failure.
        /// </summary>
        /// <param name="clientData">
        /// The client data to query.
        /// </param>
        /// <param name="dictionary">
        /// Upon success, receives the string dictionary.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the dictionary was obtained.
        /// </returns>
        private static bool TryGetDictionary( /* CORE */
            IClientData clientData,          /* in */
            ref StringDictionary dictionary, /* out */
            ref Result error                 /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid client data";
                return false;
            }

            IHaveStringDictionary haveStringDictionary =
                clientData as IHaveStringDictionary;

            if (haveStringDictionary == null)
            {
                error = "client data is of wrong type";
                return false;
            }

            dictionary = haveStringDictionary.Dictionary;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the root client data associated with the specified client
        /// data.
        /// </summary>
        /// <param name="clientData">
        /// The client data whose root is to be obtained.
        /// </param>
        /// <returns>
        /// The root client data, or the supplied client data itself.
        /// </returns>
        public static IClientData GetRootClientData( /* CORE */
            IClientData clientData /* in */
            )
        {
            if (clientData == null)
                return null;

            IAnyClientData anyClientData = clientData as IAnyClientData;

            if (anyClientData == null)
                return clientData;

            return anyClientData.Root;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified client data carries a value
        /// with the given name.
        /// </summary>
        /// <param name="clientData">
        /// The client data to query.
        /// </param>
        /// <param name="name">
        /// The name of the value to look for.
        /// </param>
        /// <returns>
        /// Non-zero if a value with the given name is present.
        /// </returns>
        public static bool TryHasDataValue( /* CORE */
            IClientData clientData, /* in */
            string name             /* in */
            )
        {
            object value = null; /* NOT USED */
            Result error = null; /* NOT USED */

            return TryGetDataValue(
                clientData, name, ref value, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the named value carried by the specified client
        /// data.
        /// </summary>
        /// <param name="clientData">
        /// The client data to query.
        /// </param>
        /// <param name="name">
        /// The name of the value to get.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the value was obtained.
        /// </returns>
        public static bool TryGetDataValue( /* CORE */
            IClientData clientData, /* in */
            string name,            /* in */
            ref object value,       /* out */
            ref Result error        /* out */
            )
        {
            if (name == null)
            {
                error = "invalid script client data name";
                return false;
            }

            IAnyDataBase anyDataBase = clientData as IAnyDataBase;

            if (anyDataBase == null)
            {
                error = "client data is of wrong type";
                return false;
            }

            Result localError = null;

            if (!anyDataBase.TryGetAny(
                    name, out value, ref localError))
            {
                error = String.Format(
                    "could not get script client data value {0}: {1}",
                    Utility.FormatWrapOrNull(name),
                    Utility.FormatWrapOrNull(localError));

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to set the named value carried by the specified client
        /// data.
        /// </summary>
        /// <param name="clientData">
        /// The client data to modify.
        /// </param>
        /// <param name="name">
        /// The name of the value to set.
        /// </param>
        /// <param name="value">
        /// The value to set.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the value was set.
        /// </returns>
        public static bool TrySetDataValue( /* CORE */
            IClientData clientData, /* in */
            string name,            /* in */
            object value,           /* in */
            ref Result error        /* out */
            )
        {
            if (name == null)
            {
                error = "invalid script client data name";
                return false;
            }

            IAnyDataBase anyDataBase = clientData as IAnyDataBase;

            if (anyDataBase == null)
            {
                error = "client data is of wrong type";
                return false;
            }

            Result localError = null;

            if (!anyDataBase.TrySetAny(
                    name, value, true, true, true, ref localError))
            {
                error = String.Format(
                    "could not set script client data value {0}: {1}",
                    Utility.FormatWrapOrNull(name),
                    Utility.FormatWrapOrNull(localError));

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to unset the named value carried by the specified
        /// client data.
        /// </summary>
        /// <param name="clientData">
        /// The client data to modify.
        /// </param>
        /// <param name="name">
        /// The name of the value to unset.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the value was unset.
        /// </returns>
        public static bool TryUnsetDataValue( /* CORE */
            IClientData clientData, /* in */
            string name,            /* in */
            ref Result error        /* out */
            )
        {
            if (name == null)
            {
                error = "invalid script client data name";
                return false;
            }

            IAnyDataBase anyDataBase = clientData as IAnyDataBase;

            if (anyDataBase == null)
            {
                error = "client data is of wrong type";
                return false;
            }

            Result localError = null;

            if (!anyDataBase.TryUnsetAny(name, ref localError))
            {
                error = String.Format(
                    "could not unset script client data value {0}: {1}",
                    Utility.FormatWrapOrNull(name),
                    Utility.FormatWrapOrNull(localError));

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs sanity checks on the install time of the specified
        /// plugin relative to its creation time and obsolescence window.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data being checked.
        /// </param>
        /// <param name="installed">
        /// The detected installation time.
        /// </param>
        /// <param name="now">
        /// The current time.
        /// </param>
        /// <param name="maximumDays">
        /// The maximum number of days before the plugin is considered
        /// obsolete.
        /// </param>
        /// <param name="requirePlugin">
        /// Non-zero to require valid plugin data.
        /// </param>
        /// <returns>
        /// Non-zero if the install time passes the sanity checks.
        /// </returns>
        private static bool CheckInstallTime( /* CORE */
            IPluginData pluginData, /* in */
            DateTime installed,     /* in */
            DateTime now,           /* in */
            long maximumDays,       /* in */
            bool requirePlugin      /* in */
            )
        {
            try
            {
                //
                // NOTE: Make sure that the plugin was installed at some time
                //       earlier than the current local time.  This check is
                //       universal (i.e. it does not require a plugin itself)
                //       because the install date/time is directly provided
                //       by the caller.
                //
                if (installed > now)
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                        "Plugin {0} was installed {1} in the future {2}.",
                        Utility.FormatWrapOrNull(pluginData),
                        DataOps.FormatNow(installed),
                        DataOps.FormatNow(now)),
                        typeof(CertificateSharedOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    return false;
                }

                if (pluginData != null)
                {
                    //
                    // NOTE: Attempt to grab and sanity check plugin creation
                    //       time.  If the DateTime property is not correctly
                    //       set this check will fail, by design.
                    //
                    DateTime? created = pluginData.DateTime;

                    if (created == null)
                    {
#if DEBUG || FORCE_TRACE
                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                            "Plugin {0} was never created.",
                            Utility.FormatWrapOrNull(pluginData)),
                            typeof(CertificateSharedOps).Name,
                            TracePriority.MediumHigh, 0);
#endif

                        return false;
                    }

                    //
                    // BUGBUG: This will normally NOT work when running from
                    //         the source tree.  The .NET build system always
                    //         creates the directory prior to compiling the
                    //         assembly.
                    //
                    // NOTE: Make sure the plugin creation time is before the
                    //       creation time of its containing directory.
                    //
                    if (installed < (DateTime)created)
                    {
#if DEBUG || FORCE_TRACE
                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                            "Plugin {0} was installed {1} before creation {2}.",
                            Utility.FormatWrapOrNull(pluginData),
                            DataOps.FormatInstalled(installed),
                            DataOps.FormatCreated(created)),
                            typeof(CertificateSharedOps).Name,
                            TracePriority.Medium, 0);
#endif

                        return false;
                    }

                    //
                    // NOTE: Make sure that the plugin was installed prior
                    //       to it being considered "obsolete", as measured
                    //       in days from when it was created.  This strongly
                    //       suggests trial certificates must be refreshed
                    //       periodically (e.g. for each release?) in order
                    //       to remain valid.
                    //
                    DateTime obsolete = ((DateTime)created).AddDays(
                        maximumDays);

                    if (installed > obsolete)
                    {
#if DEBUG || FORCE_TRACE
                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                            "Plugin {0} was installed {1} as obsolete {2}.",
                            Utility.FormatWrapOrNull(pluginData),
                            DataOps.FormatInstalled(installed),
                            DataOps.FormatObsolete(obsolete)),
                            typeof(CertificateSharedOps).Name,
                            TracePriority.Medium, 0);
#endif

                        return false;
                    }
                }
                else if (requirePlugin)
                {
                    return false;
                }

                return true;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(
                    e, typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh, 0);
#endif
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the estimated installation time of the specified plugin,
        /// based on the timestamps of its containing directory.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose installation time is to be determined.
        /// </param>
        /// <param name="utc">
        /// Non-zero to return the time in UTC.
        /// </param>
        /// <returns>
        /// The installation time, or null if it could not be determined.
        /// </returns>
        private static DateTime? GetInstallTime( /* CORE */
            IPlugin plugin, /* in */
            bool utc        /* in */
            )
        {
            try
            {
                if (plugin == null)
                    return null;

                string directory = PathOps.GetDirectory(
                    plugin);

                if (String.IsNullOrEmpty(directory))
                    return null;

                //
                // HACK: This assumes the creation and/or modified time
                //       for directory containing the plugin is a fairly
                //       reliable basis of when it was first "installed".
                //
                DateTime[] dateTimes = {
                    utc ? Directory.GetLastWriteTimeUtc(directory) :
                        Directory.GetLastWriteTime(directory),
                    utc ? Directory.GetCreationTimeUtc(directory) :
                        Directory.GetCreationTime(directory)
                };

                DateTime? installed = null;

                foreach (DateTime dateTime in dateTimes)
                {
                    //
                    // BUGBUG: Round up to the next whole second to
                    //         avoid issues on ASP.NET.
                    //
                    DateTime roundDateTime = new DateTime(
                        dateTime.Year, dateTime.Month,
                        dateTime.Day, dateTime.Hour,
                        dateTime.Minute, dateTime.Second,
                        0, dateTime.Kind).AddSeconds(1);

                    if ((installed == null) ||
                        (roundDateTime < (DateTime)installed))
                    {
                        installed = roundDateTime;
                    }
                }

                return installed;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                TraceOps.MaybeLogAndDebugTrace(
                    e, typeof(CertificateSharedOps).Name,
                    TracePriority.MediumHigh, 0);
#endif
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the difference between two times is within
        /// the specified maximum number of ticks.
        /// </summary>
        /// <param name="then">
        /// The earlier time.
        /// </param>
        /// <param name="now">
        /// The later time.
        /// </param>
        /// <param name="maximumTicks">
        /// The maximum allowed difference, in ticks.
        /// </param>
        /// <returns>
        /// Non-zero if the time difference is within the allowed range.
        /// </returns>
        private static bool IsTimeDifferenceOk( /* CORE */
            DateTime then,    /* in */
            DateTime now,     /* in */
            long maximumTicks /* in */
            )
        {
            TimeSpan difference;

            return IsTimeDifferenceOk(
                then, now, maximumTicks, out difference);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the difference between two times is within
        /// the specified maximum number of ticks, returning the difference.
        /// </summary>
        /// <param name="then">
        /// The earlier time.
        /// </param>
        /// <param name="now">
        /// The later time.
        /// </param>
        /// <param name="maximumTicks">
        /// The maximum allowed difference, in ticks.
        /// </param>
        /// <param name="difference">
        /// Upon return, receives the computed time difference.
        /// </param>
        /// <returns>
        /// Non-zero if the time difference is within the allowed range.
        /// </returns>
        private static bool IsTimeDifferenceOk( /* CORE */
            DateTime then,          /* in */
            DateTime now,           /* in */
            long maximumTicks,      /* in */
            out TimeSpan difference /* out */
            )
        {
            difference = now.Subtract(then);

            return Math.Abs(difference.Ticks) < maximumTicks;
        }

        ///////////////////////////////////////////////////////////////////////

#if (NETWORK && CERTIFICATE_RENEWAL) || (CERTIFICATE_PLUGIN && PLUGIN_COMMANDS)
        /// <summary>
        /// Determines whether the specified certificate has expired.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to query for metadata; this parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin used to determine the install time; this parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose expiration is to be checked.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify network time responses.
        /// </param>
        /// <param name="keyPair">
        /// The key pair associated with the certificate.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="installed">
        /// The simulated install time, when no plugin is available.
        /// </param>
        /// <param name="timeout">
        /// The optional network timeout, in milliseconds.
        /// </param>
        /// <param name="policyType">
        /// The policy type for which the check is being performed.
        /// </param>
        /// <param name="networkFlags">
        /// The flags controlling network time behavior.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the expiration status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode IsExpired(
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            ICertificate certificate,       /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            IKeyPair keyPair,               /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime? installed,            /* in */
            int? timeout,                   /* in */
            PolicyType policyType,          /* in */
            NetworkFlags networkFlags,      /* in */
            ref Result result               /* out */
            )
        {
            bool canRenew = false;

            return IsExpired(interpreter,
                assembly, plugin, certificate, keyPairs, keyPair,
                cultureInfo, installed, timeout, policyType,
                networkFlags, ref canRenew, ref result);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the remaining duration of the specified certificate
        /// relative to the supplied current time.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose remaining duration is to be computed.
        /// </param>
        /// <param name="now">
        /// The current time.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The remaining duration, an unlimited duration, or null if the
        /// certificate has already expired.
        /// </returns>
        public static TimeSpan? RemainingDuration( /* CORE */
            ICertificate certificate, /* in */
            DateTime now,             /* in */
            ref Result error          /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return null;
            }

            TimeSpan oldDuration = certificate.Duration;

            if (oldDuration.Ticks < 0)
                return oldDuration; /* UNLIMITED */

            DateTime created = certificate.TimeStamp;

            try
            {
                DateTime expired = created.Add(oldDuration); /* throw */

                if (now >= expired)
                {
                    error = String.Format(
                        "no remaining duration, already expired {0}",
                        DataOps.FormatExpired(expired));

                    return null;
                }

                return expired.Subtract(now); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to query the location and build time of the specified
        /// assembly, verifying its trust and strong name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to query.
        /// </param>
        /// <param name="noTrusted">
        /// Non-zero to skip the trust check.
        /// </param>
        /// <param name="location">
        /// Upon success, receives the assembly location.
        /// </param>
        /// <param name="built">
        /// Upon success, receives the assembly build time.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the metadata was obtained.
        /// </returns>
        private static bool MaybeQueryAssemblyMetadata( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            Assembly assembly,       /* in */
            bool noTrusted,          /* in */
            out string location,     /* out */
            out DateTime? built,     /* out */
            ref Result error         /* out */
            )
        {
            location = null;
            built = null;

            string localLocation = null;

            if (assembly != null)
            {
                try
                {
                    localLocation = assembly.Location; /* throw */
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        e, typeof(CertificateSharedOps).Name,
                        TracePriority.Highest, 0);
#endif
                }
            }

            if (localLocation == null)
            {
                error = "failed to query assembly location";
                return false;
            }

#if !DEBUG
            if (!noTrusted &&
                !Utility.IsFileTrusted(interpreter, localLocation))
            {
                error = String.Format(
                    "assembly location {0} not trusted",
                    Utility.FormatWrapOrNull(localLocation));

                return false;
            }
#endif

            if (!Utility.IsFileStrongNameVerified(localLocation))
            {
                error = String.Format(
                    "assembly location {0} not verified",
                    Utility.FormatWrapOrNull(localLocation));

                return false;
            }

            DateTime localBuilt = DateTime.MinValue;

            if (!Utility.GetPeFileDateTime(localLocation, ref localBuilt))
            {
                error = String.Format(
                    "assembly location {0} missing date/time",
                    Utility.FormatWrapOrNull(localLocation));

                return false;
            }

            location = localLocation;
            built = localBuilt;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the kind of the specified date/time to be either UTC or
        /// local, as requested.
        /// </summary>
        /// <param name="utc">
        /// Non-zero to convert to UTC; otherwise, convert to local time.
        /// </param>
        /// <param name="dateTime">
        /// On input, the date/time to adjust; on return, the adjusted
        /// value.
        /// </param>
        private static void MaybeAdjustDateTimeKind( /* CORE */
            bool utc,             /* in */
            ref DateTime dateTime /* in, out */
            )
        {
#pragma warning disable 162
            if (utc)
            {
                if (dateTime.Kind == DateTimeKind.Utc)
                    return;

                dateTime = dateTime.ToUniversalTime();
            }
            else
            {
                if (dateTime.Kind == DateTimeKind.Local)
                    return;

                dateTime = dateTime.ToLocalTime();
            }
#pragma warning restore 162
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate has expired,
        /// performing system clock and network time validation, and
        /// indicating whether renewal is possible.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to query for metadata; this parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin used to determine the install time; this parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose expiration is to be checked.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify network time responses.
        /// </param>
        /// <param name="keyPair">
        /// The key pair associated with the certificate.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="installed">
        /// The simulated install time, when no plugin is available.
        /// </param>
        /// <param name="timeout">
        /// The optional network timeout, in milliseconds.
        /// </param>
        /// <param name="policyType">
        /// The policy type for which the check is being performed.
        /// </param>
        /// <param name="networkFlags">
        /// The flags controlling network time behavior.
        /// </param>
        /// <param name="canRenew">
        /// Upon return, indicates whether the certificate may be renewed.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the expiration status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode IsExpired( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            ICertificate certificate,       /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            IKeyPair keyPair,               /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime? installed,            /* in */
            int? timeout,                   /* in */
            PolicyType policyType,          /* in */
            NetworkFlags networkFlags,      /* in */
            ref bool canRenew,              /* out */
            ref Result result               /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            //
            // NOTE: The following maps some semantic meanings onto the list of
            //       trace priorities created here:
            //
            //       1. "informPriority" -- Some extra useful troubleshooting
            //                              data.  These messages are lowest
            //                              priority and are generally only
            //                              seen for temporary certificates.
            //
            //       2. "warningPriority" -- Hit a condition that MAY lead to
            //                               a failure being returned shortly.
            //                               Then again, it may not.
            //
            //       3. "allowedPriority" -- Success being returned now in
            //                               spite of any previous errors.
            //
            //       4. "disallowedPriority" -- Failure is being returned now.
            //
            TracePriority informPriority = TracePriority.MediumLow;   /* DIAGNOSTIC */
            TracePriority warningPriority = TracePriority.MediumHigh; /* RARE (?) */
            TracePriority allowedPriority = TracePriority.High;       /* VERY-RARE (?) */
            TracePriority disallowedPriority = TracePriority.Higher;  /* FATAL */

            try
            {
                //
                // NOTE: When the appropriate environment variable is set,
                //       force all certificates to always expire now.
                //       This code is skipped when we are being called by
                //       the license certificate renewal subsystem -OR-
                //       when a license key ring is being loaded.
                //
                if (!HasFlags(
                        networkFlags, NetworkFlags.ViaRenewal, true) &&
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    !CertificateKeyRingState.IsLicensePending() &&
#if NETWORK && CERTIFICATE_RENEWAL
                    !CertificateKeyRingState.IsRenewalPending() &&
#endif
#endif
                    Configuration.DoesVariableExist(
                        Constants.AlwaysExpiresEnvVarName))
                {
                    canRenew = true; /* COMMON SENSE */
                    result = OperationStatus.AlwaysExpires;

                    return ReturnCode.Error;
                }

                long flagsKey = Utility.DefaultAttributeFlagsKey();
                bool localNoTrusted;

                if (!HasFlags(
                        networkFlags, NetworkFlags.Strict, true) &&
                    (MatchFlags(
                        certificate, FlagType.Feature, flagsKey,
                        Features.NoTrustedOrAll, null, false,
                        false, true) != ReturnCode.Ok))
                {
                    localNoTrusted = true;
                }
                else
                {
                    localNoTrusted = false;
                }

                string location;
                DateTime? built;
                Result metadataError = null;

                /* IGNORED */
                MaybeQueryAssemblyMetadata(
                    interpreter, assembly, localNoTrusted,
                    out location, out built, ref metadataError);

                int retries = 1;

            retry:

                //
                // NOTE: Start with the creation timestamp.  This value is
                //       quite likely to be in UTC.  Either way, convert it
                //       to be in the expected time zone before using it.
                //
                bool utc = Constants.IsTimeStampUtc;
                DateTime created = certificate.TimeStamp;

                /* NO RESULT */
                MaybeAdjustDateTimeKind(utc, ref created);

                //
                // NOTE: Is the duration valid?
                //
                TimeSpan duration = certificate.Duration;

                if (DataOps.IsNonZeroDuration(duration))
                {
                    //
                    // NOTE: How long is the certificate good for (in 100
                    //       nanosecond units)?
                    //
                    ResultList errors = null; /* REUSED */

                    if (DataOps.IsLimitedDuration(duration))
                    {
                        //
                        // NOTE: What time is it now locally?
                        //
                        DateTime now = DataOps.GetTimeStamp();

#if NETWORK
                        //
                        // NOTE: In the debug build configuration only,
                        //       when the appropriate environment variable
                        //       is set, skip checking the network time
                        //       (NTP) server in order to determine if the
                        //       system clock has been tampered with.
                        //
#if DEBUG || EXTRA_DIAGNOSTICS
                        if (!Configuration.DoesVariableExist(
                                Constants.NoNetworkTimeEnvVarName))
#endif
                        {
                            if (HasFlags(
                                    networkFlags, NetworkFlags.Force, true) ||
                                (MatchFlags(
                                    certificate, FlagType.Feature, flagsKey,
                                    Features.NoNetworkTimeOrAll, null,
                                    false, false, true) != ReturnCode.Ok) ||
                                (MatchFlags(
                                    certificate, FlagType.Restriction, flagsKey,
                                    null, Restrictions.ForceNetworkTime,
                                    false, false, true) != ReturnCode.Ok))
                            {
                                bool localStrictNetworkTime;

                                if (HasFlags(
                                        networkFlags, NetworkFlags.Strict, true) ||
                                    (MatchFlags(
                                        certificate, FlagType.Restriction,
                                        flagsKey, null,
                                        Restrictions.StrictNetworkTime,
                                        false, false, true) != ReturnCode.Ok))
                                {
                                    localStrictNetworkTime = true;
                                }
                                else
                                {
                                    localStrictNetworkTime = false;
                                }

                                bool localViaHttp;

                                if (HasFlags(
                                        networkFlags, NetworkFlags.ViaHttp, true) ||
                                    (MatchFlags(
                                        certificate, FlagType.Restriction,
                                        flagsKey, null,
                                        Restrictions.HttpNetworkTime,
                                        false, false, true) != ReturnCode.Ok))
                                {
                                    localViaHttp = true;
                                }
                                else
                                {
                                    localViaHttp = false;
                                }

                                //
                                // HACK: If the network subsystem has not (yet)
                                //       checked whether or not the necessary
                                //       hosts are actually accessible, skip
                                //       using them.
                                //
                                if (localViaHttp)
                                {
                                    if (!CertificateNetworkState.IsHttpsOk())
                                        goto skipNetwork;
                                }
                                else
                                {
                                    if (!CertificateNetworkState.IsNtpOk())
                                        goto skipNetwork;
                                }

                                //
                                // HACK: If applicable -AND- unless forbidden
                                //       from doing so, merge (all) available
                                //       (loaded) key pairs that could may be
                                //       used to verify a time-stamps from an
                                //       HTTP(S) server.
                                //
                                if (localViaHttp && !Configuration.DoesVariableExist(
                                        Constants.NoMergeKeyPairsForExpirationEnvVarName))
                                {
                                    /* NO RESULT */
                                    CertificateKeyPairOps.MergeAnyPublicOnlyOrTrace(
                                        interpreter, policyType, ref keyPairs);
                                }

                                //
                                // BUGFIX: The "errorOnTooFast" parameter here
                                //         should always be false; otherwise,
                                //         routine (repeat) license checks can
                                //         too easily fail.
                                //
                                string hostNameOrAddress = GetTimeHostNameOrAddress(
                                    localViaHttp, false);

                                DateTime network = DateTime.MinValue;
                                Result error = null;

                                if (NetworkOps.TryQueryTime(interpreter,
                                        hostNameOrAddress, keyPairs, cultureInfo,
                                        now, timeout, null, localViaHttp, false,
                                        false, Constants.NetworkTimeMustBeSigned,
                                        ref network, ref error) == ReturnCode.Ok)
                                {
                                    TimeSpan difference;

                                    if (!IsTimeDifferenceOk(network, now,
                                            Constants.NetworkTimeDifferenceMaximumTicks,
                                            out difference))
                                    {
#if DEBUG || FORCE_TRACE
                                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                                            "Local time difference is just too great: {0}",
                                            difference), typeof(CertificateSharedOps).Name,
                                            disallowedPriority, 0);
#endif

                                        canRenew = false; /* POLICY */
                                        result = OperationStatus.MaybeExpired;

                                        return ReturnCode.Error;
                                    }
                                }
                                else
                                {
                                    //
                                    // NOTE: If the relaxed network time feature is
                                    //       present in the certificate, just ignore
                                    //       this network time server error -UNLESS-
                                    //       the strict network time restriction is
                                    //       in place.
                                    //
                                    if (localStrictNetworkTime &&
                                        (MatchFlags(
                                            certificate, FlagType.Feature, flagsKey,
                                            Features.RelaxedNetworkTimeOrAll, null,
                                            false, false, true) != ReturnCode.Ok))
                                    {
#if DEBUG || FORCE_TRACE
                                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                                            "Could not check local time difference via {0}: {1}",
                                            Utility.FormatWrapOrNull(hostNameOrAddress),
                                            Utility.FormatWrapOrNull(error)),
                                            typeof(CertificateSharedOps).Name,
                                            disallowedPriority, 0);
#endif

                                        if (errors == null)
                                            errors = new ResultList();

                                        errors.Add(OperationStatus.UnknownExpired);

                                        if (error != null)
                                            errors.Add(error);

                                        canRenew = false; /* POLICY */
                                        result = errors;

                                        return ReturnCode.Error;
                                    }
                                }
                            }
                        }
#endif

#if NETWORK
                    skipNetwork:
#endif

                        //
                        // NOTE: Try to make sure the clock has not been set
                        //       backwards in time (at least too far).
                        //
                        bool createdBeforeNow = (now >= created);
                        TimeSpan createdDifference;

                        if (createdBeforeNow || IsTimeDifferenceOk(created, now,
                                Constants.CreatedTimeDifferenceMaximumTicks,
                                out createdDifference) ||
                            (MatchFlags(
                                certificate, FlagType.Feature, flagsKey,
                                Features.CreatedAnyTimeOrAll, null, false,
                                false, true) == ReturnCode.Ok))
                        {
                            //
                            // HACK: Should the timestamp for the certificate
                            //       be reset to the installation time?  This
                            //       is only done when the ExpiredFromInstall
                            //       flag is set.
                            //
                            bool resetTimeStamp = false;

                            //
                            // NOTE: What is the detected installation time of
                            //       the plugin, if any?
                            //
                            DateTime? localInstalled = null;

                            //
                            // NOTE: What point in time does this certificate
                            //       actually expire (UTC)?
                            //
                            DateTime expired;

                            if (MatchFlags(
                                    certificate, FlagType.Restriction,
                                    flagsKey, null,
                                    Restrictions.ExpiredFromInstall,
                                    false, false, true) != ReturnCode.Ok)
                            {
                                localInstalled = GetInstallTime(plugin, utc);

#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Certificate {0} for plugin {1} was installed {2}.",
                                    DebugOnlyOps.FormatCertificate(certificate),
                                    Utility.FormatWrapOrNull(plugin),
                                    DataOps.FormatInstalled(localInstalled)),
                                    typeof(CertificateSharedOps).Name,
                                    informPriority, 0);
#endif

                                bool requirePlugin = true;

#if DEBUG || EXTRA_DIAGNOSTICS
                                //
                                // HACK: Permit the plugin installation DateTime
                                //       to be specified (simulated?) by caller
                                //       when the plugin is invalid.
                                //
                                if ((localInstalled == null) && (plugin == null) &&
                                    (installed != null))
                                {
#if DEBUG || FORCE_TRACE
                                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                                        "Treating certificate {0} for plugin {1} as installed {2}.",
                                        DebugOnlyOps.FormatCertificate(certificate),
                                        Utility.FormatWrapOrNull(plugin),
                                        DataOps.FormatInstalled(installed)),
                                        typeof(CertificateSharedOps).Name,
                                        informPriority, 0);
#endif

                                    localInstalled = installed;
                                    requirePlugin = false;
                                }
#endif

                                if (localInstalled != null)
                                {
                                    //
                                    // HACK: The maximum number of days before a
                                    //       plugin is considered "obsolete" for
                                    //       trial purposes is one leap year plus
                                    //       the duration of the associated trial
                                    //       certificate.  Does this seem right?
                                    //
                                    long maximumDays = (long)duration.TotalDays;

                                    maximumDays += Constants.InstallTimeMaximumDays;

                                    //
                                    // HACK: Sanity check the installation time
                                    //       with the creation (compilation?)
                                    //       date of the assembly containing the
                                    //       plugin.
                                    //
                                    if (!CheckInstallTime(
                                            plugin, (DateTime)localInstalled,
                                            now, maximumDays, requirePlugin))
                                    {
                                        canRenew = false; /* POLICY */
                                        result = OperationStatus.MaybeInstalled;

                                        return ReturnCode.Error;
                                    }

                                    expired = ((DateTime)localInstalled).Add(
                                        duration);

                                    resetTimeStamp = true;
                                }
                                else
                                {
                                    canRenew = false; /* POLICY */
                                    result = OperationStatus.NotInstalled;

                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                expired = created.Add(duration);
                            }

                            //
                            // NOTE: Is the certificate expired?
                            //
                            if (now < expired)
                            {
                                if (resetTimeStamp && (localInstalled != null))
                                {
                                    //
                                    // HACK: Reset the timestamp for the certificate
                                    //       to the detected installation time; this
                                    //       is only done when the ExpiredFromInstall
                                    //       flag is set on the certificate.
                                    //
                                    certificate.TimeStamp = (DateTime)localInstalled;
                                }

#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Certificate {0} for plugin {1} will expire {2}.",
                                    DebugOnlyOps.FormatCertificate(certificate),
                                    Utility.FormatWrapOrNull(plugin),
                                    DataOps.FormatExpired(expired)),
                                    typeof(CertificateSharedOps).Name,
                                    allowedPriority, 0);
#endif

                                canRenew = false; /* SUPERFLUOUS */

                                result = createdBeforeNow ?
                                    OperationStatus.NotExpired :
                                    OperationStatus.NotYetValid;

                                return ReturnCode.Ok;
                            }
                            else
                            {
#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Certificate {0} for plugin {1} has expired {2}.",
                                    DebugOnlyOps.FormatCertificate(certificate),
                                    Utility.FormatWrapOrNull(plugin),
                                    DataOps.FormatExpired(expired)),
                                    typeof(CertificateSharedOps).Name,
                                    warningPriority, 0);
#endif

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                if (HasWellKnownPublicKeyToken(certificate) &&
                                    (MatchFlags(
                                        certificate, FlagType.Feature, flagsKey,
                                        Features.WellKnownNeverExpiredOrAll, null,
                                        false, false, true) == ReturnCode.Ok))
                                {
#if DEBUG || FORCE_TRACE
                                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                                        "Certificate {0} for plugin {1} has well-known key pair: {2}.",
                                        DebugOnlyOps.FormatCertificate(certificate),
                                        Utility.FormatWrapOrNull(plugin),
                                        Utility.FormatWrapOrNull(keyPair)),
                                        typeof(CertificateSharedOps).Name,
                                        allowedPriority, 0);
#endif

                                    canRenew = true; /* COMMON SENSE */
                                    result = OperationStatus.ExpiredWellKnown;

                                    return ReturnCode.Ok;
                                }
                                else
#endif
                                if (MatchFlags(
                                        certificate, FlagType.Feature, flagsKey,
                                        Features.UseVersionForExpirationOrAll, null,
                                        false, false, true) == ReturnCode.Ok)
                                {
                                    if (assembly != null)
                                    {
                                        if (location != null)
                                        {
                                            Version version1 = DataOps.NormalizeVersion(
                                                AssemblyOps.GetVersion(assembly));

                                            Version version2 = DataOps.NormalizeVersion(
                                                certificate.Version);

                                            if ((version1 != null) && (version2 != null) &&
                                                (Utility.VersionCompare(version1, version2) <= 0))
                                            {
#if DEBUG || FORCE_TRACE
                                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                    "Certificate {0} for plugin {1} version {2} versus {3} allowed.",
                                                    DebugOnlyOps.FormatCertificate(certificate),
                                                    Utility.FormatWrapOrNull(plugin),
                                                    Utility.FormatWrapOrNull(version1),
                                                    Utility.FormatWrapOrNull(version2)),
                                                    typeof(CertificateSharedOps).Name,
                                                    allowedPriority, 0);
#endif

                                                canRenew = true; /* COMMON SENSE */
                                                result = OperationStatus.ExpiredOldVersion;

                                                return ReturnCode.Ok;
                                            }
                                            else
                                            {
#if DEBUG || FORCE_TRACE
                                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                    "Certificate {0} for plugin {1} version {2} versus {3} disallowed.",
                                                    DebugOnlyOps.FormatCertificate(certificate),
                                                    Utility.FormatWrapOrNull(plugin),
                                                    Utility.FormatWrapOrNull(version1),
                                                    Utility.FormatWrapOrNull(version2)),
                                                    typeof(CertificateSharedOps).Name,
                                                    warningPriority, 0);
#endif

                                                VersionRange versionRange = CertificateVersionState.GetRange(
                                                    policyType, false);

                                                if (DataOps.IsVersionInRange(version1, versionRange))
                                                {
#if DEBUG || FORCE_TRACE
                                                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                        "Certificate {0} for plugin {1} version {2} versus range {3} allowed.",
                                                        DebugOnlyOps.FormatCertificate(certificate),
                                                        Utility.FormatWrapOrNull(plugin),
                                                        Utility.FormatWrapOrNull(version1),
                                                        Utility.FormatWrapOrNull(
                                                            DataOps.FormatVersionRange(versionRange))),
                                                        typeof(CertificateSharedOps).Name,
                                                        allowedPriority, 0);
#endif

                                                    canRenew = true; /* COMMON SENSE */
                                                    result = OperationStatus.ExpiredInRange;

                                                    return ReturnCode.Ok;
                                                }
                                                else
                                                {
#if DEBUG || FORCE_TRACE
                                                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                        "Certificate {0} for plugin {1} version {2} versus range {3} disallowed.",
                                                        DebugOnlyOps.FormatCertificate(certificate),
                                                        Utility.FormatWrapOrNull(plugin),
                                                        Utility.FormatWrapOrNull(version1),
                                                        Utility.FormatWrapOrNull(
                                                            DataOps.FormatVersionRange(versionRange))),
                                                        typeof(CertificateSharedOps).Name,
                                                        disallowedPriority, 0);
#endif

                                                    if (errors == null)
                                                        errors = new ResultList();

                                                    errors.Add("assembly version: newer disallowed");
                                                }
                                            }
                                        }
                                        else
                                        {
#if DEBUG || FORCE_TRACE
                                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                "Certificate {0} for plugin {1} assembly {2}: {3}",
                                                DebugOnlyOps.FormatCertificate(certificate),
                                                Utility.FormatWrapOrNull(plugin),
                                                Utility.FormatWrapOrNull(assembly),
                                                Utility.FormatMaybeNull(metadataError)),
                                                typeof(CertificateSharedOps).Name,
                                                disallowedPriority, 0);
#endif

                                            if (errors == null)
                                                errors = new ResultList();

                                            errors.Add(String.Format(
                                                "assembly version: {0}", metadataError));
                                        }
                                    }
                                }

                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(String.Format("certificate expired {0}",
                                    DataOps.FormatExpired(expired)));

                                canRenew = true; /* COMMON SENSE */
                                result = errors;
                            }
                        }
                        else
                        {
#if DEBUG || FORCE_TRACE
                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                "Created time difference is just too great: {0}",
                                createdDifference), typeof(CertificateSharedOps).Name,
                                disallowedPriority, 0);
#endif

                            //
                            // NOTE: The clock has [probably] been tampered with
                            //       -OR- the certificate was generated in advance
                            //       and is not yet valid; therefore, treat it as
                            //       expired as of now.
                            //
                            canRenew = false; /* POLICY */

                            result = String.Format("certificate not yet valid {0}",
                                DataOps.FormatExpired(now));
                        }
                    }
                    else // if (DataOps.IsUnlimitedDuration(duration))
                    {
                        //
                        // NOTE: Disallow this certificate from having an infinite
                        //       duration *IF* the key pair is not valid for that
                        //       usage.
                        //
                        Result keyUsageError = null;

                        if ((keyPair == null) ||
                            (keyPair.KeyUsage == null) ||
                            CanSignWithUnlimitedTime(keyPair,
                                ref keyUsageError) == ReturnCode.Ok)
                        {
                            //
                            // NOTE: If the duration is less than zero then
                            //       the certificate never expires.
                            //
                            canRenew = false; /* SUPERFLUOUS */
                            result = OperationStatus.NeverExpires;

                            return ReturnCode.Ok;
                        }
                        else
                        {
#if DEBUG || FORCE_TRACE
                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                "Certificate must expire with key pair: {0}",
                                Utility.FormatWrapOrNull(keyPair)),
                                typeof(CertificateSharedOps).Name,
                                warningPriority, 0);
#endif

                            if (keyUsageError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(keyUsageError);
                            }

                            Result keyConvertError = null;

                            if (CanConvertToLimitedTime(keyPair,
                                    ref keyConvertError) == ReturnCode.Ok)
                            {
                                TimeSpan? newDuration = CertificateTimeState.GetDurationOrDefault(
                                    policyType, false, false);

                                if (DataOps.IsLimitedDuration(newDuration, true))
                                {
                                    if (retries-- > 0)
                                    {
                                        bool canRetry = false; /* NOTHING CHANGED */

                                        if ((built != null) && ((DateTime)built < created))
                                        {
#if DEBUG || FORCE_TRACE
                                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                "Certificate time-stamp rewound to {0} with key pair: {1}",
                                                DataOps.FormatCreated(built),
                                                Utility.FormatWrapOrNull(keyPair)),
                                                typeof(CertificateSharedOps).Name,
                                                informPriority, 0);
#endif

                                            certificate.TimeStamp = (DateTime)built;
                                            canRetry = true; /* TIMESTAMP CHANGED */
                                        }

                                        if (newDuration != null)
                                        {
#if DEBUG || FORCE_TRACE
                                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                "Certificate duration limited to {0} with key pair: {1}",
                                                Utility.FormatWrapOrNull(newDuration),
                                                Utility.FormatWrapOrNull(keyPair)),
                                                typeof(CertificateSharedOps).Name,
                                                informPriority, 0);
#endif

                                            certificate.Duration = (TimeSpan)newDuration;
                                            canRetry = true; /* DURATION CHANGED */
                                        }

                                        if (canRetry)
                                        {
                                            goto retry;
                                        }
                                        else
                                        {
#if DEBUG || FORCE_TRACE
                                            TraceOps.MaybeLogAndDebugTrace(String.Format(
                                                "Certificate time-stamp {0} and duration {1} unchanged with key pair: {2}",
                                                DataOps.FormatCreated(built),
                                                Utility.FormatWrapOrNull(newDuration),
                                                Utility.FormatWrapOrNull(keyPair)),
                                                typeof(CertificateSharedOps).Name,
                                                disallowedPriority, 0);
#endif

                                            if (errors == null)
                                                errors = new ResultList();

                                            errors.Add("limited duration: certificate unchanged");
                                        }
                                    }
                                    else
                                    {
#if DEBUG || FORCE_TRACE
                                        TraceOps.MaybeLogAndDebugTrace(String.Format(
                                            "Certificate cannot limit duration to {0} with key pair: {1}",
                                            Utility.FormatWrapOrNull(newDuration),
                                            Utility.FormatWrapOrNull(keyPair)),
                                            typeof(CertificateSharedOps).Name,
                                            disallowedPriority, 0);
#endif

                                        if (errors == null)
                                            errors = new ResultList();

                                        errors.Add("limited duration: out-of-retries");
                                    }
                                }
                                else
                                {
#if DEBUG || FORCE_TRACE
                                    TraceOps.MaybeLogAndDebugTrace(String.Format(
                                        "Certificate cannot use unlimited duration {0} with key pair: {1}",
                                        Utility.FormatWrapOrNull(newDuration),
                                        Utility.FormatWrapOrNull(keyPair)),
                                        typeof(CertificateSharedOps).Name,
                                        disallowedPriority, 0);
#endif

                                    if (errors == null)
                                        errors = new ResultList();

                                    errors.Add("limited duration: new duration unusable");
                                }
                            }
                            else
                            {
#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Certificate cannot convert with key pair: {0}",
                                    Utility.FormatWrapOrNull(keyPair)),
                                    typeof(CertificateSharedOps).Name,
                                    warningPriority, 0);
#endif

                                if (keyConvertError != null)
                                {
                                    if (errors == null)
                                        errors = new ResultList();

                                    errors.Add(keyConvertError);
                                }
                            }

                            Result keyRenewError = null;

                            if (CanRenewWithLimitedTime(keyPair,
                                    ref keyRenewError) == ReturnCode.Ok)
                            {
                                canRenew = true; /* COMMON SENSE */
                            }
                            else
                            {
#if DEBUG || FORCE_TRACE
                                TraceOps.MaybeLogAndDebugTrace(String.Format(
                                    "Certificate cannot renew with key pair: {0}",
                                    Utility.FormatWrapOrNull(keyPair)),
                                    typeof(CertificateSharedOps).Name,
                                    disallowedPriority, 0);
#endif

                                if (keyRenewError != null)
                                {
                                    if (errors == null)
                                        errors = new ResultList();

                                    errors.Add(keyRenewError);
                                }

                                canRenew = false; /* POLICY */
                            }

                            result = errors;
                        }
                    }
                }
                else
                {
                    //
                    // NOTE: A duration of zero is invalid; therefore, just
                    //       treat it as expired from when it was originally
                    //       created.
                    //
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    if (HasWellKnownPublicKeyToken(certificate) &&
                        (MatchFlags(
                            certificate, FlagType.Feature, flagsKey,
                            Features.WellKnownNeverExpiredOrAll, null,
                            false, false, true) == ReturnCode.Ok))
                    {
                        canRenew = true; /* COMMON SENSE */
                        result = OperationStatus.ExpiredWellKnown;

                        return ReturnCode.Ok;
                    }
                    else
#endif
                    {
                        canRenew = true; /* COMMON SENSE */

                        result = String.Format("certificate expired {0}",
                            DataOps.FormatExpired(created));
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                //
                // HACK: In debug build, do not allow this certificate to
                //       be renewed as this failure mode should not really
                //       happen.
                //
                canRenew = false; /* POLICY */
#else
                canRenew = true;
#endif

                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Gets the registry manager for the specified plugin, optionally
        /// creating it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data for which to get the registry manager.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the registry manager if needed.
        /// </param>
        /// <returns>
        /// The registry manager, or null if unavailable.
        /// </returns>
        private static IRegistryManager GetRegistryManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in */
            bool create              /* in */
            )
        {
            return RegistryManager.GetRegistryManager(
                interpreter, pluginData, create);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the storage manager for the specified plugin, optionally
        /// creating it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data for which to get the storage manager.
        /// </param>
        /// <param name="storageType">
        /// The kind of storage to use.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Whether the storage manager must support security.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the storage manager if needed.
        /// </param>
        /// <returns>
        /// The storage manager, or null if unavailable.
        /// </returns>
        public static IStorageManager GetStorageManager( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            IPluginData pluginData,   /* in */
            StorageType? storageType, /* in */
            bool? mustHaveSecurity,   /* in */
            bool create               /* in */
            )
        {
#if !NET_STANDARD_20
            return StorageManager.GetStorageManager(
                interpreter, pluginData, GetRegistryManager(
                interpreter, pluginData, true), storageType,
                mustHaveSecurity, create);
#else
            return StorageManager.GetStorageManager(
                interpreter, pluginData, storageType,
                mustHaveSecurity, create);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Processes the use quantity for the specified certificate,
        /// decrementing the remaining number of uses recorded in storage.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this parameter is optional and
        /// may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the storage; this parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The optional keyed hash key; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose quantity is to be processed.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags controlling which parts of the certificate are
        /// included; this parameter is optional and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="storageType">
        /// The kind of storage to use.
        /// </param>
        /// <param name="protect">
        /// Non-zero to protect (encrypt) the stored quantity value.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to use per-machine storage.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode ProcessQuantity( /* CORE */
            Interpreter interpreter,                    /* in: OPTIONAL */
            IPluginData pluginData,                     /* in: OPTIONAL */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CultureInfo cultureInfo,                    /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            StorageType? storageType,                   /* in */
            bool protect,                               /* in */
            bool perMachine,                            /* in */
            ref Result error                            /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            //
            // NOTE: Unlimited quantity?  Return now.
            //
            long certificateQuantity = certificate.Quantity;

            if (certificateQuantity == Constants.QuantityUnlimited)
                return ReturnCode.Ok;

            //
            // NOTE: If the certificate is not specially flagged as
            //       having a "limited quantity", return now.
            //
            Result localResult = null; /* NOT USED */

            if (MatchFlags(
                    certificate, FlagType.Restriction,
                    Utility.DefaultAttributeFlagsKey(),
                    null, Restrictions.LimitedQuantity, false,
                    false, true, ref localResult) == ReturnCode.Ok)
            {
                if (!Configuration.DoesVariableExist(
                        Constants.AlwaysLimitedQuantityEnvVarName))
                {
                    return ReturnCode.Ok;
                }
            }

            string name = DataOps.FormatValueName(
                certificate, Constants.QuantityValueName);

            byte[] nameData = null;

            if (HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, name,
                    ref nameData, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IStorageManager storageManager = GetStorageManager(
                interpreter, pluginData, storageType, true, true);

            if (storageManager == null)
            {
                error = "storage manager not available";
                return ReturnCode.Error;
            }

            string valueName = DataOps.FormatHexadecimal(
                nameData);

            byte[] valueData = null;

            if (storageManager.ReadValue(
                    valueName, cultureInfo, perMachine, true,
                    ref valueData, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

#if NATIVE
            string description = null; /* NOT USED */
#endif

            if ((valueData == null) ||
                storageManager.IsDefaultValue(valueData))
            {
                //
                // NOTE: The registry setting has never been written before?
                //       Ok, start out with the certificate quantity.
                //
                valueData = BitConverter.GetBytes(certificateQuantity);
            }
#if NATIVE
            else if (protect)
            {
                //
                // NOTE: Decrypt the value read from the registry to obtain
                //       the quantity of uses remaining.
                //
                if (ProtectOps.UnprotectData(
                        certificate.Key, perMachine, false,
                        true, ref description, ref valueData,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
#endif

            //
            // NOTE: Convert the byte array value to an integer quantity.
            //
            long quantity = BitConverter.ToInt64(valueData, 0);

            //
            // NOTE: If the quantity is greater than zero, modify number
            //       of uses remaining.
            //
            if (quantity > 0)
            {
                valueData = BitConverter.GetBytes(quantity - 1);

#if NATIVE
                if (protect)
                {
                    description = DataOps.FormatId(
                        certificate.Id);

                    if (ProtectOps.ProtectData(
                            certificate.Key, perMachine, false,
                            true, description, ref valueData,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }
#endif

                if (storageManager.WriteValue(
                        valueName, cultureInfo, perMachine, true,
                        valueData, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            //
            // NOTE: Is there at least one use remaining?
            //
            if (quantity <= 0)
            {
                error = "certificate quantity met or exceeded";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Interpreter Support Methods
        /// <summary>
        /// Determines whether security has been enabled for the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode HasSecurityEnabled( /* CORE */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (!interpreter.SetSecurityWasEnabled(null))
            {
                error = "interpreter security not enabled";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Licensing Support Methods
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /// <summary>
        /// Gets the license certificate data associated with the specified
        /// plugin, if any.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <returns>
        /// The license certificate data, or null if unavailable.
        /// </returns>
        public static ILicenseCertificateData GetLicenseCertificateData(
            IPluginData pluginData /* in */
            )
        {
            //
            // NOTE: When compiled without LICENSING compile-time option,
            //       this will always return null.
            //
            return pluginData as ILicenseCertificateData;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the license plugin manager data associated with the
        /// specified plugin, if any.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <returns>
        /// The license plugin manager data, or null if unavailable.
        /// </returns>
        public static ILicensePluginManagerData GetLicensePluginManagerData(
            IPluginData pluginData /* in */
            )
        {
            //
            // NOTE: When compiled without LICENSING compile-time option,
            //       this will always return null.
            //
            return pluginData as ILicensePluginManagerData;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER || (CERTIFICATE_PLUGIN && CERTIFICATE_POLICY)
        //
        // HACK: This method is required when looking up items in the auxiliary
        //       data dictionary a plugin.  The "using" clauses at the top of
        //       this file establish several dictionary types; however, those
        //       type names are not used at runtime.  Furthermore, since the
        //       key name and key ring name are both the same underlying types,
        //       there is a conflict if only the type names are used.
        //
        /// <summary>
        /// Builds the auxiliary data dictionary key name for the specified
        /// property name and type.
        /// </summary>
        /// <param name="propertyName">
        /// The property name to incorporate; this parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="type">
        /// The type to incorporate into the name.
        /// </param>
        /// <returns>
        /// The auxiliary data key name, or null if the type is invalid.
        /// </returns>
        public static string GetNameForAuxiliaryData( /* CORE? */
            string propertyName, /* in */
            Type type            /* in */
            )
        {
            if (type == null)
                return null;

            return !String.IsNullOrEmpty(propertyName) ?
                String.Format("{0}-{1}", propertyName, type) :
                String.Format("{0}", type);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate associated with the specified plugin,
        /// checking license data, auxiliary data, and the cached license
        /// certificate.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <returns>
        /// The certificate, or null if none is associated.
        /// </returns>
        public static ICertificate GetViaPlugin(
            IPluginData pluginData /* in */
            )
        {
            if (pluginData != null)
            {
                ILicenseCertificateData licenseCertificateData =
                    GetLicenseCertificateData(pluginData);

                if (licenseCertificateData != null)
                {
                    ICertificate certificate =
                        licenseCertificateData.Certificate;

                    if (certificate != null)
                        return certificate;
                }

                ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

                if (auxiliaryData != null)
                {
                    string name = GetNameForAuxiliaryData(
                        null, typeof(ICertificate));

                    if (name != null)
                    {
                        object value;

                        if (auxiliaryData.TryGetValue(name, out value))
                        {
                            ICertificate certificate = value as ICertificate;

                            if (certificate != null)
                                return certificate;
                        }
                    }
                }

                //
                // HACK: Fallback to using the cached license certificate
                //       for the Harpy assembly, if the specified plugin
                //       corresponds to it.  This is extremely important
                //       now, because all "duplicate" attempts to verify
                //       the license certificate for the Harpy plugin are
                //       skipped (i.e. for performance).  Without this
                //       handling, all Harpy plugin instances except the
                //       first one would lack a license certificate and
                //       that would cause a lot of things to fail (e.g.
                //       anything that needs to check feature flags, etc).
                //
                if (AssemblyOps.MatchObjectOrName(pluginData))
                {
                    ICertificate certificate = LicenseState.GetCertificate();

                    if (certificate != null)
                        return certificate;
                }
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && LICENSING
        /// <summary>
        /// Associates the specified certificate with the given plugin via
        /// its auxiliary data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to modify.
        /// </param>
        /// <param name="certificate">
        /// The certificate to associate.
        /// </param>
        public static void SetViaPlugin(
            IPluginData pluginData,  /* in */
            ICertificate certificate /* in */
            )
        {
            if (pluginData != null)
            {
                ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

                if (auxiliaryData != null)
                {
                    string name = GetNameForAuxiliaryData(
                        null, typeof(ICertificate));

                    if (name != null)
                        auxiliaryData[name] = certificate;
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Gets the list of recognized URI type names.
        /// </summary>
        /// <param name="result">
        /// Upon success, receives the list of URI type names; otherwise,
        /// the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode GetUriTypes( /* CORE? */
            ref Result result /* out */
            )
        {
            try
            {
                string[] names = Enum.GetNames(typeof(UriType));

                if (names == null)
                {
                    result = "uri types unavailable";
                    return ReturnCode.Error;
                }

                StringList list = null;

                foreach (string name in names)
                {
                    if (String.IsNullOrEmpty(name))
                        continue;

                    //
                    // HACK: Skip the specific values that are
                    //       not actually types of URIs.
                    //
                    if (DataOps.StringEquals(name, "None") ||
                        DataOps.StringEquals(name, "Invalid") ||
                        DataOps.StringEquals(name, "Default"))
                    {
                        continue;
                    }

                    //
                    // HACK: Skip any name that starts with the
                    //       string "Use" because that means its
                    //       an auxiliary flag, not a URI type.
                    //
                    if (DataOps.StringStartsWith(name, "Use"))
                        continue;

                    //
                    // HACK: Skip any name that ends with the
                    //       string "Mask" because that means
                    //       it is a combination of URI types,
                    //       not a URI type itself.
                    //
                    if (DataOps.StringEndsWith(name, "Mask"))
                        continue;

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                }

                if (list != null)
                {
                    result = list;
                    return ReturnCode.Ok;
                }
                else
                {
                    result = "no matching uri types";
                    return ReturnCode.Error;
                }
            }
            catch (Exception e)
            {
                result = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the URI (or related value) of the specified type for the
        /// given plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="plugin">
        /// The plugin for which the URI is being requested.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use; this parameter is optional and may be
        /// null.
        /// </param>
        /// <param name="type">
        /// The type of URI to retrieve.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the URI value; otherwise, the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode GetUri( /* CORE? */
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            UriType type,            /* in */
            ref Result result        /* out */
            )
        {
            UriType baseType = (type & UriType.TypeMask);

#if NETWORK && ((XML && WEB) || DEBUG || EXTRA_DIAGNOSTICS)
            Uri uri; /* REUSED */
#endif

            switch (baseType)
            {
                case UriType.NtpBase: /* e.g. "time.mistachkin.net" */
                    {
#if NETWORK
                        string hostNameOrAddress = null;

                        if (NtpOps.SelectHostNameOrAddress(
                                interpreter, ref hostNameOrAddress,
                                ref result) == ReturnCode.Ok)
                        {
                            result = hostNameOrAddress;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.NtpRelative: /* Nonsensical */
                    {
                        result = "not implemented";
                        return ReturnCode.Error;
                    }
                case UriType.HttpTimeBase: /* e.g. "https://urn.to/r/get_time_07" */
                    {
#if NETWORK
                        string hostNameOrAddress = null;

                        if (TimeOps.SelectHostNameOrAddress(
                                interpreter, ref hostNameOrAddress,
                                ref result) == ReturnCode.Ok)
                        {
                            result = hostNameOrAddress;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.HttpTimeRelative: /* Unsupported */
                    {
                        result = "not implemented";
                        return ReturnCode.Error;
                    }
                case UriType.SecretBase: /* e.g. "https://urn.to/r/secret" */
                    {
#if XML && NETWORK && WEB && PLUGIN_COMMANDS
                        uri = SecretOps.GetUri(
                            null, cultureInfo, ref result);

                        if (uri != null)
                        {
                            result = uri;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.SecretRelative: /* Unsupported */
                    {
                        result = "not implemented";
                        return ReturnCode.Error;
                    }
                case UriType.AuthorityBase: /* e.g. "https://urn.to/r/authority" */
                    {                       /* e.g. "https://urn.to/r/renew_license_07" */
#if NETWORK && (DEBUG || EXTRA_DIAGNOSTICS)
                        ICertificate certificate = null;

                        if (HasFlags(
                                type, UriType.UseCertificate, true))
                        {
                            certificate = LicenseState.GetCertificate();
                        }

                        uri = null;

                        if (NetworkOps.GetAuthorityAndComponents(
                                interpreter, AssemblyOps.GetObject(),
                                plugin, certificate, cultureInfo,
                                ref uri, ref result) == ReturnCode.Ok)
                        {
                            result = uri;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.AuthorityRelative: /* Unsupported */
                    {
                        result = "not implemented";
                        return ReturnCode.Error;
                    }
                case UriType.RenewalBase: /* Authority */
                    {
                        goto case UriType.AuthorityBase;
                    }
                case UriType.RenewalRelative: /* e.g. "certificate/renew.cgi?" */
                    {
#if NETWORK && CERTIFICATE_RENEWAL
                        result = RenewalOps.GetRelativeUri();
                        return ReturnCode.Ok;
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.RevocationBase: /* Authority */
                    {
                        goto case UriType.AuthorityBase;
                    }
                case UriType.RevocationRelative: /* e.g. "certificate/revoked.cgi?" */
                    {
#if NETWORK
                        result = RevocationOps.GetRelativeUri();
                        return ReturnCode.Ok;
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.PingBase: /* Harpy Test Suite Only */
                case UriType.PingRelative: /* Harpy Test Suite Only */
                case UriType.SupportBase: /* Harpy Test Suite Only */
                case UriType.SupportRelative: /* Harpy Test Suite Only */
                case UriType.ScriptBase: /* Kapok Test Suite Only */
                case UriType.ScriptRelative: /* Kapok Test Suite Only */
                case UriType.StorageBase: /* Kapok Test Suite Only */
                case UriType.StorageRelative: /* Kapok Test Suite Only */
                    {
                        if (HasFlags(
                                type, UriType.UseVariable, true))
                        {
                            //
                            // HACK: These are always hard-coded to
                            //       be mapped to their associated
                            //       variable name and suffixed with
                            //       the literal string "Uri".
                            //
                            string typeString = DataOps.ToNameString(
                                baseType);

                            string[] varNames = {
                                String.Format(
                                    "{0}Uri", typeString),
                                String.Format(
                                    "env({0}Uri)", typeString)
                            };

                            Result localResult = null;
                            ResultList errors = null;

                            foreach (string varName in varNames)
                            {
                                if (String.IsNullOrEmpty(varName))
                                    continue;

                                Result value = null;
                                Result error = null;

                                if (interpreter.GetVariableValue(
                                        VariableFlags.GlobalOnly,
                                        varName, ref value,
                                        ref error) == ReturnCode.Ok)
                                {
                                    localResult = value;
                                    break;
                                }
                                else
                                {
                                    if (error != null)
                                    {
                                        if (errors == null)
                                            errors = new ResultList();

                                        errors.Add(error);
                                    }
                                }
                            }

                            if (localResult != null)
                            {
                                result = localResult;
                                return ReturnCode.Ok;
                            }
                            else
                            {
                                result = errors;
                                return ReturnCode.Error;
                            }
                        }
                        else
                        {
                            result = "cannot use script variable";
                            return ReturnCode.Error;
                        }
                    }
                case UriType.RequestBase: /* e.g. "https://urn.to/r/license" */
                case UriType.LicenseBase: /* SAME AS ABOVE */
                    {
#if XML && NETWORK && WEB
                        uri = Helpers.GetRequestBaseUri(
                            AssemblyOps.GetObject(), cultureInfo,
                            ref result);

                        if (uri != null)
                        {
                            result = uri;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.RequestRelative: /* e.g. "certificate/request.cgi?" */
                case UriType.LicenseRelative: /* SAME AS ABOVE */
                    {
#if XML && NETWORK && WEB
                        result = Helpers.GetRequestRelativeUri();
                        return ReturnCode.Ok;
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.ProvisionBase: /* e.g. "https://urn.to/r/provision" */
                    {
#if XML && NETWORK && WEB
                        uri = Helpers.GetProvisionBaseUri(
                            AssemblyOps.GetObject(), cultureInfo,
                            ref result);

                        if (uri != null)
                        {
                            result = uri;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.ProvisionRelative: /* e.g. "service/provision.cgi?" */
                    {
#if XML && NETWORK && WEB
                        result = Helpers.GetProvisionRelativeUri();
                        return ReturnCode.Ok;
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.TestBase: /* e.g. "https://urn.to/r/test" */
                    {
#if XML && NETWORK && WEB
                        uri = Helpers.GetTestBaseUri(
                            AssemblyOps.GetObject(), cultureInfo,
                            ref result);

                        if (uri != null)
                        {
                            result = uri;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.TestRelative: /* e.g. "test/page.cgi?" */
                    {
#if XML && NETWORK && WEB
                        result = Helpers.GetTestRelativeUri();
                        return ReturnCode.Ok;
#else
                        result = "not implemented";
                        return ReturnCode.Error;
#endif
                    }
                case UriType.LibraryBase: /* e.g. "https://urn.to/r/get_license_07" */
                    {
                        //
                        // NOTE: This type of URI is only ever
                        //       used from the script level via
                        //       the [requestLicenseCertificate]
                        //       script library helper.  So, we
                        //       have to evaluate a hard-coded
                        //       script to fetch it.
                        //
                        if (HasFlags(
                                type, UriType.UseLibrary, true))
                        {
                            return interpreter.EvaluateScript(
                                Constants.LibraryUriScript,
                                ref result);
                        }
                        else
                        {
                            result = "cannot use script library";
                            return ReturnCode.Error;
                        }
                    }
                case UriType.LibraryRelative: /* Unsupported */
                    {
                        result = "not implemented";
                        return ReturnCode.Error;
                    }
                default:
                    {
                        result = String.Format(
                            "unsupported uri type {0}",
                            Utility.FormatWrapOrNull(type));

                        return ReturnCode.Error;
                    }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// Removes any certificate association from the auxiliary data of
        /// the specified plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to modify.
        /// </param>
        public static void UnsetViaPlugin(
            IPluginData pluginData /* in */
            )
        {
            if (pluginData != null)
            {
                ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

                if (auxiliaryData != null)
                {
                    string name = GetNameForAuxiliaryData(
                        null, typeof(ICertificate));

                    if (name != null)
                    {
                        /* IGNORED */
                        auxiliaryData.Remove(name);
                    }
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && LICENSING
        /// <summary>
        /// Determines whether the agreement of the specified certificate
        /// matches one of the supplied required agreements.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose agreement is to be matched.
        /// </param>
        /// <param name="agreements">
        /// The set of acceptable agreements.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the match status or error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// code.
        /// </returns>
        public static ReturnCode MatchAgreement(
            ICertificate certificate,       /* in */
            UriDictionary<bool> agreements, /* in */
            ref Result result               /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            if (agreements == null)
            {
                result = "invalid agreement list";
                return ReturnCode.Error;
            }

            string certificateAgreement = (certificate.Agreement != null) ?
                certificate.Agreement.ToString() : null;

            foreach (KeyValuePair<Uri, bool> pair in agreements)
            {
                if (DataOps.StringEquals(
                        pair.Key.ToString(), certificateAgreement))
                {
                    result = OperationStatus.AgreementOk;
                    return ReturnCode.Ok;
                }
            }

            result = String.Format(
                "license agreement mismatch, must be {0}.",
                Utility.ListToEnglish(new List<Uri>(agreements.Keys),
                ", ", " ", "or ", Characters.QuotationMark.ToString(),
                Characters.QuotationMark.ToString()));

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the assembly name for the specified plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <returns>
        /// The assembly name, or null if unavailable.
        /// </returns>
        public static AssemblyName GetAssemblyName( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            if (pluginData == null)
                return null;

            AssemblyName assemblyName = pluginData.AssemblyName;

            if ((assemblyName == null) &&
                !Utility.IsCrossAppDomain(pluginData))
            {
                Assembly assembly = pluginData.Assembly;

                if (assembly != null)
                    assemblyName = assembly.GetName();
            }

            return assemblyName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether per-machine storage should be used, based on
        /// the supplied preference and administrator status.
        /// </summary>
        /// <param name="perMachine">
        /// The explicit per-machine preference; this parameter is optional
        /// and may be null.
        /// </param>
        /// <returns>
        /// Non-zero if per-machine storage should be used.
        /// </returns>
        public static bool ShouldUsePerMachine( /* CORE */
            bool? perMachine /* in */
            )
        {
            if (perMachine != null)
                return (bool)perMachine;

            //
            // TODO: Perhaps also check if the process has no interactive
            //       user -OR- is running in session zero?
            //
            return Utility.IsAdministrator();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the storage manager associated with the specified plugin,
        /// checking license data, client data, and auxiliary data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context; this parameter is not used.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <param name="initialize">
        /// Non-zero if the plugin is currently being constructed.
        /// </param>
        /// <returns>
        /// The storage manager, or null if none was found.
        /// </returns>
        public static IStorageManager FindStorageManager( /* CORE */
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData,  /* in */
            bool initialize          /* in */
            )
        {
            if (pluginData == null)
                return null;

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
            if (!initialize) /* NOTE: Are we constructing the plugin? */
            {
                ILicensePluginManagerData licensePluginManagerData =
                    GetLicensePluginManagerData(pluginData);

                if (licensePluginManagerData != null)
                {
                    IStorageManager storageManager =
                        licensePluginManagerData.StorageManager;

                    if (storageManager != null)
                        return storageManager;
                }
            }
#endif

            IClientData clientData = pluginData.ClientData;

            if (clientData != null)
            {
                object data = null;

                /* IGNORED */
                clientData = ClientData.UnwrapOrReturn(
                    clientData, ref data);

                IStorageManager storageManager =
                    data as IStorageManager;

                if (storageManager != null)
                    return storageManager;
            }

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData != null)
            {
                string name = typeof(IStorageManager).Name;
                object value;

                if (auxiliaryData.TryGetValue(name, out value))
                {
                    IStorageManager storageManager =
                        value as IStorageManager;

                    if (storageManager != null)
                        return storageManager;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Finds the registry manager associated with the specified plugin,
        /// checking license data, client data, and auxiliary data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context; this parameter is not used.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to query.
        /// </param>
        /// <param name="initialize">
        /// Non-zero if the plugin is currently being constructed.
        /// </param>
        /// <returns>
        /// The registry manager, or null if none was found.
        /// </returns>
        public static IRegistryManager FindRegistryManager( /* CORE */
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData,  /* in */
            bool initialize          /* in */
            )
        {
            if (pluginData == null)
                return null;

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
            if (!initialize) /* NOTE: Are we constructing the plugin? */
            {
                ILicensePluginManagerData licensePluginManagerData =
                    GetLicensePluginManagerData(pluginData);

                if (licensePluginManagerData != null)
                {
                    IRegistryManager registryManager =
                        licensePluginManagerData.RegistryManager;

                    if (registryManager != null)
                        return registryManager;
                }
            }
#endif

            IClientData clientData = pluginData.ClientData;

            if (clientData != null)
            {
                object data = null;

                /* IGNORED */
                clientData = ClientData.UnwrapOrReturn(
                    clientData, ref data);

                IRegistryManager registryManager =
                    data as IRegistryManager;

                if (registryManager != null)
                    return registryManager;
            }

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData != null)
            {
                string name = typeof(IRegistryManager).Name;
                object value;

                if (auxiliaryData.TryGetValue(name, out value))
                {
                    IRegistryManager registryManager =
                        value as IRegistryManager;

                    if (registryManager != null)
                        return registryManager;
                }
            }

            return null;
        }
#endif
        #endregion
    }
}
