/*
 * Configuration.cs --
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
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using ScriptOps = Licensing.Components.Private.CertificateScriptOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

using FileAndOrStreamDataList = System.Collections.Generic.List<
    Licensing.Components.Private.FileAndOrStreamData>;

using LoadPair = Eagle._Components.Public.MutableAnyPair<
    Eagle._Containers.Public.ResultList, bool>;

using ResultDictionary = System.Collections.Generic.Dictionary<
    string, Eagle._Components.Public.Result>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods used to locate, gather, and load the
    /// licensing configuration scripts for the Harpy plugin, drawing values
    /// from environment variables, the application domain, and configuration
    /// files on disk.
    /// </summary>
    [ObjectId("5c527fed-7346-4e94-89f0-e4668a48172e")]
    internal static class Configuration
    {
        /// <summary>
        /// Determines whether any debugger appears to be present, by
        /// checking for a forced configuration override, a native (Windows)
        /// debugger, and an attached managed debugger.
        /// </summary>
        /// <returns>
        /// Non-zero if a debugger is detected or forcibly enabled;
        /// otherwise, zero.
        /// </returns>
        public static bool IsThereAnyDebugger() /* CORE */
        {
            if (Utility.DoesEnvironmentVariableExist(
                    Constants.ForceDebuggerConfigurationEnvVarName))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    "IsThereAnyDebugger: Forcibly enabled.",
                    typeof(Configuration).Name,
                    TracePriority.MediumLow);
#endif

                return true;
            }

#if NATIVE && WINDOWS
            if (Utility.IsDebuggerPresent())
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    "IsThereAnyDebugger: Native present.",
                    typeof(Configuration).Name,
                    TracePriority.MediumLow);
#endif

                return true;
            }
#endif

            if (Debugger.IsAttached)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    "IsThereAnyDebugger: Managed attached.",
                    typeof(Configuration).Name,
                    TracePriority.MediumLow);
#endif

                return true;
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(
                "IsThereAnyDebugger: Nothing detected.",
                typeof(Configuration).Name,
                TracePriority.Lowest);
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the configured debugger suffix to the specified value,
        /// unless that value is null or empty.
        /// </summary>
        /// <param name="value">
        /// The value to which the debugger suffix should be appended.
        /// </param>
        /// <returns>
        /// The value with the debugger suffix appended, or the original
        /// value if it was null or empty.
        /// </returns>
        private static string MaybeWithDebuggerSuffix( /* CORE */
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            return String.Format(
                Constants.DebuggerSuffixFormat, value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configuration directory associated with the specified
        /// configuration, falling back to the directory derived from the
        /// current assembly when no configuration is available.
        /// </summary>
        /// <param name="configuration">
        /// The configuration whose directory should be used, if any.
        /// </param>
        /// <returns>
        /// The configuration directory, or null if it cannot be determined.
        /// </returns>
        public static string GetDirectory( /* CORE */
            IConfiguration configuration /* in */
            )
        {
            if (configuration != null)
                return configuration.GetConfigurationDirectory();

            return GetDirectory(CertificateAssemblyOps.GetDirectory());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configuration directory relative to the specified base
        /// directory, honoring any environment variable override and
        /// verifying that the resulting directory exists.
        /// </summary>
        /// <param name="baseDirectory">
        /// The base directory used to locate the configuration directory.
        /// </param>
        /// <returns>
        /// The configuration directory if it exists; otherwise, null.
        /// </returns>
        public static string GetDirectory( /* CORE */
            string baseDirectory /* in */
            )
        {
            string directory = GetVariable(
                Constants.ConfigurationDirectoryEnvVarName);

            if (!String.IsNullOrEmpty(directory))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetDirectory: overridden {0}",
                    Utility.FormatWrapOrNull(directory)),
                    typeof(Configuration).Name,
                    TracePriority.MediumHigh);
#endif
            }
            else
            {
                directory = Path.Combine(baseDirectory,
                    Constants.ConfigurationsDirectoryName);

                if (!Directory.Exists(directory))
                    directory = baseDirectory;
            }

            if (Directory.Exists(directory))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetDirectory: using {0}",
                    Utility.FormatWrapOrNull(directory)),
                    typeof(Configuration).Name,
                    TracePriority.Medium);
#endif

                return directory;
            }
            else
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetDirectory: missing {0}",
                    Utility.FormatWrapOrNull(directory)),
                    typeof(Configuration).Name,
                    TracePriority.MediumHigh);
#endif

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the public-only key pairs associated with the specified
        /// assembly, used to verify configuration script signatures.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to obtain the key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly from which to obtain the key pairs.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the public-only key pairs that were found.
        /// </param>
        /// <param name="keyUsage">
        /// Upon success, receives the key usage associated with the key
        /// pairs, if any.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode GetKeyPairs(
            Assembly assembly,                  /* in */
            AssemblyName assemblyName,          /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref string keyUsage,                /* out */
            ref Result error                    /* out */
            )
        {
            IEnumerable<IKeyPair> localKeyPairs = null;
            string localKeyUsage;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                    assembly, null, false, ref localKeyPairs,
                    ref error) == ReturnCode.Ok)
            {
                localKeyUsage = KeyUsage.Source;
            }
            else
            {
                return ReturnCode.Error;
            }
#else
            if (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPairs,
                    ref error) == ReturnCode.Ok)
            {
                localKeyUsage = null;
            }
            else
            {
                return ReturnCode.Error;
            }
#endif

            keyPairs = localKeyPairs;
            keyUsage = localKeyUsage;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the fallback simple name (e.g. "Harpy") used when building
        /// configuration variable and file names.
        /// </summary>
        /// <returns>
        /// The fallback simple name.
        /// </returns>
        private static string GetFallbackName() /* CORE */
        {
            return CertificateAssemblyOps.GetFallbackSimpleName();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the simple name (e.g. "Harpy.Basic") of the current
        /// assembly, used when building configuration variable and file
        /// names.
        /// </summary>
        /// <returns>
        /// The simple name of the current assembly.
        /// </returns>
        private static string GetSimpleName() /* CORE */
        {
            return CertificateAssemblyOps.MustGetSimpleName();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified variable name already begins
        /// with the fallback name or the simple name prefix.
        /// </summary>
        /// <param name="variable">
        /// The variable name to check for a known name prefix.
        /// </param>
        /// <returns>
        /// Non-zero if the variable name begins with a known prefix;
        /// otherwise, zero.
        /// </returns>
        private static bool HasAnyNamePrefix( /* CORE */
            string variable /* in */
            )
        {
            if (String.IsNullOrEmpty(variable))
                return false;

            string fallbackName = GetFallbackName();

            if (!String.IsNullOrEmpty(fallbackName) &&
                DataOps.StringStartsWith(variable, fallbackName))
            {
                return true;
            }

            string simpleName = GetSimpleName();

            if (!String.IsNullOrEmpty(simpleName) &&
                !DataOps.StringEquals(simpleName, fallbackName) &&
                DataOps.StringStartsWith(variable, simpleName))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the fallback-name and simple-name prefixed forms of the
        /// specified variable name, when those names are available.
        /// </summary>
        /// <param name="variable">
        /// The variable name for which to create prefixed forms.
        /// </param>
        /// <param name="prefixedVariable1">
        /// Receives the variable name prefixed with the fallback name, or
        /// null if it is unavailable.
        /// </param>
        /// <param name="prefixedVariable2">
        /// Receives the variable name prefixed with the simple name, or null
        /// if it is unavailable or identical to the fallback name.
        /// </param>
        private static void MaybeCreatePrefixedNames( /* CORE */
            string variable,              /* in */
            out string prefixedVariable1, /* out */
            out string prefixedVariable2  /* out */
            )
        {
            if (!String.IsNullOrEmpty(variable))
            {
                string fallbackName = GetFallbackName();

                if (!String.IsNullOrEmpty(fallbackName))
                {
                    prefixedVariable1 = String.Format(
                        Constants.ConfigurationVariableFormat1,
                        fallbackName, variable);
                }
                else
                {
                    prefixedVariable1 = null;
                }

                string simpleName = GetSimpleName();

                if (!String.IsNullOrEmpty(simpleName) &&
                    !DataOps.StringEquals(simpleName, fallbackName))
                {
                    prefixedVariable2 = String.Format(
                        Constants.ConfigurationVariableFormat1,
                        simpleName, variable);
                }
                else
                {
                    prefixedVariable2 = null;
                }
            }
            else
            {
                prefixedVariable1 = null;
                prefixedVariable2 = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified configuration variable exists,
        /// checking any prefixed forms within the active interpreter (when
        /// testing) and the process environment.
        /// </summary>
        /// <param name="variable">
        /// The name of the configuration variable to check.
        /// </param>
        /// <returns>
        /// Non-zero if the variable exists; otherwise, zero.
        /// </returns>
        public static bool DoesVariableExist( /* CORE */
            string variable /* in */
            )
        {
#if TEST
            EvaluateClientData clientData = null;
            Interpreter interpreter = Interpreter.GetActive();

            if (interpreter != null)
                clientData = ScriptOps.GetClientData(interpreter);

            string value; /* REUSED */
#endif

            if (!HasAnyNamePrefix(variable))
            {
                string prefixedVariable1;
                string prefixedVariable2;

                MaybeCreatePrefixedNames(
                    variable, out prefixedVariable1,
                    out prefixedVariable2);

                if (prefixedVariable1 != null)
                {
#if TEST
                    if ((clientData != null) &&
                        clientData.TryGetConfiguration(
                            prefixedVariable1, out value) &&
                        (value != null))
                    {
                        return true;
                    }
#endif

                    if (Utility.DoesEnvironmentVariableExist(
                            prefixedVariable1))
                    {
                        return true;
                    }
                }

                if (prefixedVariable2 != null)
                {
#if TEST
                    if ((clientData != null) &&
                        clientData.TryGetConfiguration(
                            prefixedVariable2, out value) &&
                        (value != null))
                    {
                        return true;
                    }
#endif

                    if (Utility.DoesEnvironmentVariableExist(
                            prefixedVariable2))
                    {
                        return true;
                    }
                }
            }

#if TEST
            if ((clientData != null) && clientData.TryGetConfiguration(
                    variable, out value) && (value != null))
            {
                return true;
            }
#endif

            if (Utility.DoesEnvironmentVariableExist(variable))
                return true;

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the first configuration variable, from the
        /// specified set of variable names, that has a non-empty value.
        /// </summary>
        /// <param name="variables">
        /// The candidate variable names to check, in order of preference.
        /// </param>
        /// <returns>
        /// The value of the first variable with a non-empty value, or null
        /// if none was found.
        /// </returns>
        private static string GetAnyVariable( /* CORE */
            params string[] variables /* in */
            )
        {
            if (variables != null)
            {
                foreach (string variable in variables)
                {
                    string value = GetVariable(variable);

                    if (!String.IsNullOrEmpty(value))
                        return value;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the specified variable from the data associated
        /// with the current application domain.
        /// </summary>
        /// <param name="variable">
        /// The name of the variable to query from the application domain.
        /// </param>
        /// <returns>
        /// The application domain data value for the variable, or null if it
        /// is not present.
        /// </returns>
        private static string GetVariableViaAppDomain( /* CORE */
            string variable /* in */
            )
        {
            if (variable != null)
            {
                AppDomain appDomain = AppDomain.CurrentDomain;

                if (appDomain != null)
                    return appDomain.GetData(variable) as string;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the specified configuration variable and uses
        /// it to update the supplied flags enumeration value of type
        /// <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">
        /// The flags enumeration type being parsed and updated.
        /// </typeparam>
        /// <param name="interpreter">
        /// The interpreter used when parsing the flags enumeration value.
        /// </param>
        /// <param name="variable">
        /// The name of the configuration variable to query.
        /// </param>
        /// <param name="oldValue">
        /// The existing flags enumeration value to be combined with the
        /// value parsed from the variable.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The updated flags enumeration value, the original value when the
        /// variable is not set, or null if parsing failed.
        /// </returns>
        public static object GetVariable<T>( /* CORE */
            Interpreter interpreter, /* in */
            string variable,         /* in */
            T oldValue,              /* in */
            ref Result error         /* out */
            )
#if false
            where T : Enum /* HACK: Not 100% backwards compatible. */
#endif
        {
            string newValue = GetVariable(variable);

            if (newValue == null)
                return oldValue;

            object enumValue = Utility.TryParseFlagsEnum(
                interpreter, typeof(T), Utility.GetStringFromObject(
                oldValue), newValue, interpreter.CultureInfo, true,
                true, true, ref error);

            if (!(enumValue is T))
                return null;

            return (T)enumValue;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the specified configuration variable, checking
        /// any prefixed forms within the active interpreter (when testing),
        /// the application domain, and the process environment.
        /// </summary>
        /// <param name="variable">
        /// The name of the configuration variable to query.
        /// </param>
        /// <returns>
        /// The value of the variable, or null if it was not found.
        /// </returns>
        public static string GetVariable( /* CORE */
            string variable /* in */
            )
        {
            string value; /* REUSED */

#if TEST
            EvaluateClientData clientData = null;
            Interpreter interpreter = Interpreter.GetActive();

            if (interpreter != null)
                clientData = ScriptOps.GetClientData(interpreter);
#endif

            if (!HasAnyNamePrefix(variable))
            {
                string prefixedVariable1;
                string prefixedVariable2;

                MaybeCreatePrefixedNames(
                    variable, out prefixedVariable1,
                    out prefixedVariable2);

                if (prefixedVariable1 != null)
                {
#if TEST
                    if ((clientData != null) &&
                        clientData.TryGetConfiguration(
                            prefixedVariable1, out value) &&
                        (value != null))
                    {
                        return value;
                    }
#endif

                    value = GetVariableViaAppDomain(
                        prefixedVariable1);

                    if (value != null)
                        return value;

                    value = Utility.GetEnvironmentVariable(
                        prefixedVariable1);

                    if (value != null)
                        return value;
                }

                if (prefixedVariable2 != null)
                {
#if TEST
                    if ((clientData != null) &&
                        clientData.TryGetConfiguration(
                            prefixedVariable2, out value) &&
                        (value != null))
                    {
                        return value;
                    }
#endif

                    value = GetVariableViaAppDomain(
                        prefixedVariable2);

                    if (value != null)
                        return value;

                    value = Utility.GetEnvironmentVariable(
                        prefixedVariable2);

                    if (value != null)
                        return value;
                }
            }

#if TEST
            if ((clientData != null) && clientData.TryGetConfiguration(
                    variable, out value) && (value != null))
            {
                return value;
            }
#endif

            value = GetVariableViaAppDomain(variable);

            if (value != null)
                return value;

            value = Utility.GetEnvironmentVariable(variable);

            if (value != null)
                return value;

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the specified environment variable to the given value, first
        /// unsetting any prefixed forms so that the new value takes
        /// precedence.
        /// </summary>
        /// <param name="variable">
        /// The name of the environment variable to set.
        /// </param>
        /// <param name="value">
        /// The value to assign to the environment variable.
        /// </param>
        public static void SetVariable( /* CORE */
            string variable, /* in */
            string value     /* in */
            )
        {
            //
            // HACK: Since the caller (obviously?) wants to override the
            //       value of this environment variable, make sure that
            //       any prefixed versions of it are gone.
            //
            if (!HasAnyNamePrefix(variable))
            {
                string prefixedVariable1;
                string prefixedVariable2;

                MaybeCreatePrefixedNames(
                    variable, out prefixedVariable1, out prefixedVariable2);

                if (prefixedVariable1 != null)
                    Utility.UnsetEnvironmentVariable(prefixedVariable1);

                if (prefixedVariable2 != null)
                    Utility.UnsetEnvironmentVariable(prefixedVariable2);
            }

            Utility.SetEnvironmentVariable(variable, value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the specified environment variable, along with any
        /// prefixed forms of it.
        /// </summary>
        /// <param name="variable">
        /// The name of the environment variable to unset.
        /// </param>
        public static void UnsetVariable( /* CORE */
            string variable /* in */
            )
        {
            //
            // HACK: Since the caller (obviously?) wants to unset this
            //       environment variable, make sure that any prefixed
            //       versions of it are unset as well.
            //
            if (!HasAnyNamePrefix(variable))
            {
                string prefixedVariable1;
                string prefixedVariable2;

                MaybeCreatePrefixedNames(
                    variable, out prefixedVariable1, out prefixedVariable2);

                if (prefixedVariable1 != null)
                    Utility.UnsetEnvironmentVariable(prefixedVariable1);

                if (prefixedVariable2 != null)
                    Utility.UnsetEnvironmentVariable(prefixedVariable2);
            }

            Utility.UnsetEnvironmentVariable(variable);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: This method should be called from the GetTimeout
        //          method overloads of the CertificateSharedOps class.
        //          Please do not call this method from elsewhere.
        //
        /// <summary>
        /// Gets the configured timeout value, falling back to the network
        /// timeout configured for the core library when available.
        /// </summary>
        /// <returns>
        /// The configured timeout value, or null if none was configured.
        /// </returns>
        public static int? GetTimeout() /* CORE */
        {
            //
            // HACK: Attempt to fallback to the "network" timeout
            //       configured for the core library, if any.  This
            //       has been hard-coded to assume the timeout type
            //       is for the network.
            //
            string stringValue = GetAnyVariable(
                Constants.TimeoutEnvVarName
#if NETWORK
                , EnvVars.NetworkTimeout
#endif
            );

            if (!String.IsNullOrEmpty(stringValue))
            {
                int intValue = 0;

                if (Value.GetInteger2(
                        stringValue, ValueFlags.AnyInteger,
                        null, ref intValue) == ReturnCode.Ok)
                {
                    return intValue;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the specified sandbox token with the configuration
        /// associated with the plugin data, when a new interpreter was
        /// created, and disables disposal of that interpreter so that it can
        /// be reused.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to keep track of, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose configuration tracks the sandbox token.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the sandbox token.
        /// </param>
        /// <param name="createdInterpreter">
        /// Non-zero if the interpreter was newly created for this purpose.
        /// </param>
        /// <param name="primaryToken">
        /// Receives a value indicating whether the token is the primary
        /// sandbox token.
        /// </param>
        /// <returns>
        /// Non-zero if the sandbox token was newly tracked; otherwise, zero.
        /// </returns>
        public static bool MaybeKeepTrackOfSandboxToken( /* CORE */
            ulong? token,            /* in */
            IPluginData pluginData,  /* in */
            Interpreter interpreter, /* in */
            bool createdInterpreter, /* in */
            out bool primaryToken    /* out */
            )
        {
            primaryToken = false;

            if (token != null)
            {
                IConfiguration configuration = pluginData as IConfiguration;

                if (configuration != null)
                {
                    primaryToken = configuration.IsPrimarySandboxToken(
                        (ulong)token);

                    if (createdInterpreter &&
                        configuration.AddSandboxToken((ulong)token))
                    {
                        if (interpreter != null)
                        {
                            interpreter.SetDisposalEnabled(
                                false, false); /* throw */
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the first non-empty file name from the specified set of
        /// candidate file names.
        /// </summary>
        /// <param name="fileNames">
        /// The candidate file names to consider, in order of preference.
        /// </param>
        /// <returns>
        /// The first non-empty file name, or null if none was found.
        /// </returns>
        private static string GetFileName( /* CORE */
            params string[] fileNames /* in */
            )
        {
            if (fileNames != null)
            {
                foreach (string fileName in fileNames)
                {
                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    return fileName;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the unique set of simple names used when locating
        /// configuration files, including the simple name, the fallback
        /// name, and the default package name.
        /// </summary>
        /// <returns>
        /// The unique set of simple names.
        /// </returns>
        private static IEnumerable<string> GetSimpleNames() /* CORE */
        {
            StringList simpleNames = new StringList();

            simpleNames.Add(GetSimpleName());   /* "Harpy.Basic", etc. */
            simpleNames.Add(GetFallbackName()); /* "Harpy" */

            simpleNames.Add(Utility.GetPackageName(
                PackageType.Default, false));   /* "Eagle", etc. */

            return Utility.GetUniqueElements(simpleNames);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the set of configuration file extensions to consider, which
        /// may include the plain and encrypted script extensions depending
        /// on the active configuration.
        /// </summary>
        /// <returns>
        /// The set of configuration file extensions.
        /// </returns>
        private static IEnumerable<string> GetFileExtensions() /* CORE */
        {
            StringList extensions = new StringList();

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            if (!DoesVariableExist(
                    Constants.HarpyEncryptedConfigurationsOnlyEnvVarName))
#endif
            {
                extensions.Add(FileExtension.Script); /* ".eagle" */
            }

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            extensions.Add(FileExtension.EncryptedScript); /* ".eeagle" */
#endif

            return extensions;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the prioritized list of candidate configuration file names
        /// for the specified simple name, optionally incorporating the
        /// plugin type, machine identifier, variant name, and local user,
        /// machine, and domain names, as well as a fallback name and
        /// debugger-specific variants.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request.  This parameter is
        /// not currently used.
        /// </param>
        /// <param name="simpleName">
        /// The simple name used as the base for the candidate file names.
        /// This value is required.
        /// </param>
        /// <param name="pluginType">
        /// The optional plugin type whose name is incorporated into the
        /// candidate file names.
        /// </param>
        /// <param name="machineId">
        /// The optional unique identifier for the current machine.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name incorporated into the candidate file
        /// names.
        /// </param>
        /// <param name="directory">
        /// The directory used to fully qualify the candidate file names.
        /// This value is required.
        /// </param>
        /// <param name="fileExtension">
        /// The optional file extension appended to the candidate file names.
        /// </param>
        /// <param name="noFallback">
        /// Non-zero to omit candidate file names based on the fallback name.
        /// </param>
        /// <param name="debugger">
        /// Non-zero to also include debugger-specific candidate file names.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The list of candidate configuration file names, or null if a
        /// required value was missing.
        /// </returns>
        private static IEnumerable<string> GetFileNames( /* CORE */
            Interpreter interpreter, /* in: NOT USED */
            string simpleName,       /* in */
            Type pluginType,         /* in: OPTIONAL */
            Guid? machineId,         /* in: OPTIONAL */
            string variantName,      /* in: OPTIONAL */
            string directory,        /* in */
            string fileExtension,    /* in: OPTIONAL */
            bool noFallback,         /* in */
            bool debugger,           /* in */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: The simple name is required in order to build any of
            //       the file names.  If it is not valid, fail now.  This
            //       value is used by the caller to allow configurations
            //       to vary between the different SKUs.
            //
            if (String.IsNullOrEmpty(simpleName))
            {
                error = "invalid simple name";
                return null;
            }

            //
            // NOTE: Since this method is designed to always return fully
            //       qualified file names, we must fail if the caller has
            //       not specified a directory name.
            //
            if (String.IsNullOrEmpty(directory))
            {
                error = "invalid directory";
                return null;
            }

            //
            // NOTE: First, grab the fallback name for the current (Harpy)
            //       assembly unless we are forbidden from doing so by the
            //       caller.
            //
            string fallbackName = null;

            if (!noFallback)
            {
                fallbackName = GetFallbackName();

                if (String.IsNullOrEmpty(fallbackName))
                {
                    error = "invalid fallback name";
                    return null;
                }
            }

            //
            // NOTE: The returned list (of file names) should contain
            //       at least the following, in order of priority, as
            //       listed here if <type> and <variant> are not null:
            //
            //       "<simple>.<type>.v1.<machineId>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<user>.<machine>.<domain>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<user>.<machine>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<user>.<domain>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<user>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<machine>.<domain>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<machine>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<domain>.<variant>.<extension>"
            //       "<simple>.<type>.v1.<variant>.<extension>"
            //       "<fallback>.<type>.v1.<variant>.<extension>"
            //
            //       -OR- if <type> is not null:
            //
            //       "<simple>.<type>.v1.<machineId>.<extension>"
            //       "<simple>.<type>.v1.<user>.<machine>.<domain>.<extension>"
            //       "<simple>.<type>.v1.<user>.<machine>.<extension>"
            //       "<simple>.<type>.v1.<user>.<domain>.<extension>"
            //       "<simple>.<type>.v1.<user>.<extension>"
            //       "<simple>.<type>.v1.<machine>.<domain>.<extension>"
            //       "<simple>.<type>.v1.<machine>.<extension>"
            //       "<simple>.<type>.v1.<domain>.<extension>"
            //       "<simple>.<type>.v1.<extension>"
            //       "<fallback>.<type>.v1.<extension>"
            //
            //       -OR- if <variant> is not null:
            //
            //       "<simple>.v1.<machineId>.<variant>.<extension>"
            //       "<simple>.v1.<user>.<machine>.<domain>.<variant>.<extension>"
            //       "<simple>.v1.<user>.<machine>.<variant>.<extension>"
            //       "<simple>.v1.<user>.<domain>.<variant>.<extension>"
            //       "<simple>.v1.<user>.<variant>.<extension>"
            //       "<simple>.v1.<machine>.<domain>.<variant>.<extension>"
            //       "<simple>.v1.<machine>.<variant>.<extension>"
            //       "<simple>.v1.<domain>.<variant>.<extension>"
            //       "<simple>.v1.<variant>.<extension>"
            //       "<fallback>.v1.<variant>.<extension>"
            //
            //       -OR- if <type> and <variant> are null:
            //
            //       "<simple>.v1.<machineId>.<extension>"
            //       "<simple>.v1.<user>.<machine>.<domain>.<extension>"
            //       "<simple>.v1.<user>.<machine>.<extension>"
            //       "<simple>.v1.<user>.<domain>.<extension>"
            //       "<simple>.v1.<user>.<extension>"
            //       "<simple>.v1.<machine>.<domain>.<extension>"
            //       "<simple>.v1.<machine>.<extension>"
            //       "<simple>.v1.<domain>.<extension>"
            //       "<simple>.v1.<extension>"
            //       "<fallback>.v1.<extension>"
            //
            StringList fileNames = new StringList();

            //
            // NOTE: Next, grab the plugin type name to use when building
            //       the candidate file names, if any.
            //
            string pluginTypeName = (pluginType != null) ?
                pluginType.ToString() : null;

            ///////////////////////////////////////////////////////////////////

            if (machineId != null)
            {
                if (!String.IsNullOrEmpty(pluginTypeName))
                {
                    if (!String.IsNullOrEmpty(variantName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat14,
                                    simpleName, MaybeWithDebuggerSuffix(
                                    pluginTypeName), machineId, variantName,
                                    fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat14,
                                simpleName, pluginTypeName, machineId,
                                variantName, fileExtension)));
                    }
                    else
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat6,
                                    simpleName, MaybeWithDebuggerSuffix(
                                    pluginTypeName), machineId,
                                    fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat6,
                                simpleName, pluginTypeName, machineId,
                                fileExtension)));
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(variantName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat10,
                                    MaybeWithDebuggerSuffix(simpleName),
                                    machineId, variantName,
                                    fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat10,
                                simpleName, machineId, variantName,
                                fileExtension)));
                    }
                    else
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat2,
                                    MaybeWithDebuggerSuffix(simpleName),
                                    machineId, fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat2,
                                simpleName, machineId, fileExtension)));
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Next, grab the user name, machine name, and domain
            //       name, if available.  They are used in an order that
            //       makes sense in terms of "override" semantics.
            //
            string userName;
            string machineName;
            string domainName;

            if (Utility.GetLocalNames(
                    true, true, out userName, out machineName,
                    out domainName) != null)
            {
                if (!String.IsNullOrEmpty(userName) &&
                    !String.IsNullOrEmpty(machineName) &&
                    !String.IsNullOrEmpty(domainName) &&
                    !DataOps.StringEqualsNoCase(userName, machineName) &&
                    !DataOps.StringEqualsNoCase(userName, domainName) &&
                    !DataOps.StringEqualsNoCase(machineName, domainName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat16,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, machineName,
                                        domainName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat16,
                                    simpleName, pluginTypeName, userName,
                                    machineName, domainName, variantName,
                                    fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat8,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, machineName,
                                        domainName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat8,
                                    simpleName, pluginTypeName, userName,
                                    machineName, domainName, fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat12,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, machineName, domainName,
                                        variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat12,
                                    simpleName, userName, machineName,
                                    domainName, variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat4,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, machineName, domainName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat4,
                                    simpleName, userName, machineName,
                                    domainName, fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(userName) &&
                    !String.IsNullOrEmpty(machineName) &&
                    !DataOps.StringEqualsNoCase(userName, machineName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat15,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, machineName,
                                        variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat15,
                                    simpleName, pluginTypeName, userName,
                                    machineName, variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat7,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, machineName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat7,
                                    simpleName, pluginTypeName, userName,
                                    machineName, fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat11,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, machineName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat11,
                                    simpleName, userName, machineName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat3,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, machineName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat3,
                                    simpleName, userName, machineName,
                                    fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(userName) &&
                    !String.IsNullOrEmpty(domainName) &&
                    !DataOps.StringEqualsNoCase(userName, domainName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat15,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, domainName,
                                        variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat15,
                                    simpleName, pluginTypeName, userName,
                                    domainName, variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat7,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, domainName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat7,
                                    simpleName, pluginTypeName, userName,
                                    domainName, fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat11,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, domainName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat11,
                                    simpleName, userName, domainName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat3,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, domainName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat3,
                                    simpleName, userName, domainName,
                                    fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(userName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat14,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat14,
                                    simpleName, pluginTypeName, userName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat6,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), userName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat6,
                                    simpleName, pluginTypeName, userName,
                                    fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat10,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat10,
                                    simpleName, userName, variantName,
                                    fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat2,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        userName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat2,
                                    simpleName, userName, fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(machineName) &&
                    !String.IsNullOrEmpty(domainName) &&
                    !DataOps.StringEqualsNoCase(machineName, domainName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat15,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), machineName, domainName,
                                        variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat15,
                                    simpleName, pluginTypeName, machineName,
                                    domainName, variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat7,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), machineName, domainName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat7,
                                    simpleName, pluginTypeName, machineName,
                                    domainName, fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat11,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        machineName, domainName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat11,
                                    simpleName, machineName, domainName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat3,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        machineName, domainName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat3,
                                    simpleName, machineName, domainName,
                                    fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(machineName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat14,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), machineName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat14,
                                    simpleName, pluginTypeName, machineName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat6,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), machineName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat6,
                                    simpleName, pluginTypeName, machineName,
                                    fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat10,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        machineName, variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat10,
                                    simpleName, machineName, variantName,
                                    fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat2,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        machineName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat2,
                                    simpleName, machineName, fileExtension)));
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (!String.IsNullOrEmpty(domainName))
                {
                    if (!String.IsNullOrEmpty(pluginTypeName))
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat14,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), domainName, variantName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat14,
                                    simpleName, pluginTypeName, domainName,
                                    variantName, fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat6,
                                        simpleName, MaybeWithDebuggerSuffix(
                                        pluginTypeName), domainName,
                                        fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat6,
                                    simpleName, pluginTypeName, domainName,
                                    fileExtension)));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(variantName))
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat10,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        domainName, variantName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat10,
                                    simpleName, domainName, variantName,
                                    fileExtension)));
                        }
                        else
                        {
                            if (debugger)
                            {
                                fileNames.Add(Path.Combine(
                                    directory, String.Format(
                                        Constants.ConfigurationFileNameFormat2,
                                        MaybeWithDebuggerSuffix(simpleName),
                                        domainName, fileExtension)));
                            }

                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat2,
                                    simpleName, domainName, fileExtension)));
                        }
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!String.IsNullOrEmpty(pluginTypeName))
            {
                if (!String.IsNullOrEmpty(variantName))
                {
                    if (debugger)
                    {
                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat13,
                                simpleName, MaybeWithDebuggerSuffix(
                                pluginTypeName), variantName,
                                fileExtension)));
                    }

                    fileNames.Add(Path.Combine(
                        directory, String.Format(
                            Constants.ConfigurationFileNameFormat13,
                            simpleName, pluginTypeName, variantName,
                            fileExtension)));

                    if (!String.IsNullOrEmpty(fallbackName) &&
                        !DataOps.StringEquals(simpleName, fallbackName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat13,
                                    fallbackName, MaybeWithDebuggerSuffix(
                                    pluginTypeName), variantName,
                                    fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat13,
                                fallbackName, pluginTypeName, variantName,
                                fileExtension)));
                    }
                }
                else
                {
                    if (debugger)
                    {
                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat5,
                                simpleName, MaybeWithDebuggerSuffix(
                                pluginTypeName), fileExtension)));
                    }

                    fileNames.Add(Path.Combine(
                        directory, String.Format(
                            Constants.ConfigurationFileNameFormat5,
                            simpleName, pluginTypeName, fileExtension)));

                    if (!String.IsNullOrEmpty(fallbackName) &&
                        !DataOps.StringEquals(simpleName, fallbackName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat5,
                                    fallbackName, MaybeWithDebuggerSuffix(
                                    pluginTypeName), fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat5,
                                fallbackName, pluginTypeName, fileExtension)));
                    }
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(variantName))
                {
                    if (debugger)
                    {
                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat9,
                                MaybeWithDebuggerSuffix(simpleName),
                                variantName, fileExtension)));
                    }

                    fileNames.Add(Path.Combine(
                        directory, String.Format(
                            Constants.ConfigurationFileNameFormat9,
                            simpleName, variantName, fileExtension)));

                    if (!String.IsNullOrEmpty(fallbackName) &&
                        !DataOps.StringEquals(simpleName, fallbackName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat9,
                                    MaybeWithDebuggerSuffix(fallbackName),
                                    variantName, fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat9,
                                fallbackName, variantName, fileExtension)));
                    }
                }
                else
                {
                    if (debugger)
                    {
                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat1,
                                MaybeWithDebuggerSuffix(simpleName),
                                fileExtension)));
                    }

                    fileNames.Add(Path.Combine(
                        directory, String.Format(
                            Constants.ConfigurationFileNameFormat1,
                            simpleName, fileExtension)));

                    if (!String.IsNullOrEmpty(fallbackName) &&
                        !DataOps.StringEquals(simpleName, fallbackName))
                    {
                        if (debugger)
                        {
                            fileNames.Add(Path.Combine(
                                directory, String.Format(
                                    Constants.ConfigurationFileNameFormat1,
                                    MaybeWithDebuggerSuffix(fallbackName),
                                    fileExtension)));
                        }

                        fileNames.Add(Path.Combine(
                            directory, String.Format(
                                Constants.ConfigurationFileNameFormat1,
                                fallbackName, fileExtension)));
                    }
                }
            }

            return fileNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the explicit configuration file names specified via the
        /// environment to the supplied list of file names.
        /// </summary>
        /// <param name="fileNames">
        /// The list to which the environment-specified file names are added.
        /// </param>
        /// <returns>
        /// The number of file names that were added.
        /// </returns>
        private static int AddEnvironmentFileNames( /* CORE */
            StringList fileNames /* in */
            )
        {
            int result = 0;

            if (fileNames == null)
                return result;

            string value = GetVariable(
                Constants.ConfigurationFileNamesEnvVarName);

            if (value == null)
                return result;

            StringList localFileNames = null;
            Result error = null;

            if (Parser.SplitList(
                    null, value, 0, Length.Invalid,
                    true, ref localFileNames,
                    ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "AddEnvironmentFileNames: error = {0}",
                    Utility.FormatWrapOrNull(error)),
                    typeof(Configuration).Name,
                    TracePriority.MediumHigh);
#endif

                return result;
            }

            foreach (string localFileName in localFileNames)
            {
                if (String.IsNullOrEmpty(localFileName))
                    continue;

                fileNames.Add(localFileName);
                result++;
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the configuration files in the specified directory whose
        /// names match the file patterns specified via the environment to
        /// the supplied list of file names.
        /// </summary>
        /// <param name="directory">
        /// The directory to search for matching configuration files.
        /// </param>
        /// <param name="fileNames">
        /// The list to which the matching file names are added.
        /// </param>
        /// <returns>
        /// The number of file names that were added.
        /// </returns>
        private static int AddEnvironmentFilePatterns( /* CORE */
            string directory,    /* in */
            StringList fileNames /* in */
            )
        {
            int result = 0;

            if (fileNames == null)
                return result;

            string value = GetVariable(
                Constants.ConfigurationFilePatternsEnvVarName);

            if (value == null)
                return result;

            StringList patterns = null;
            Result error = null;

            if (Parser.SplitList(
                    null, value, 0, Length.Invalid, true,
                    ref patterns, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "AddEnvironmentFilePatterns: error = {0}",
                    Utility.FormatWrapOrNull(error)),
                    typeof(Configuration).Name,
                    TracePriority.MediumHigh);
#endif

                return result;
            }

            string[] localFileNames = Directory.GetFiles(
                directory, Characters.Asterisk.ToString(),
                SearchOption.TopDirectoryOnly); /* throw */

            if ((localFileNames == null) ||
                (localFileNames.Length == 0))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    "AddEnvironmentFilePatterns: no files?",
                    typeof(Configuration).Name,
                    TracePriority.MediumHigh);
#endif

                return result;
            }

            Array.Sort(localFileNames); /* O(N) */

            foreach (string localFileName in localFileNames)
            {
                //
                // HACK: Skip over null / empty file names,
                //       though they should never actually
                //       be seen here.
                //
                if (String.IsNullOrEmpty(localFileName))
                    continue;

                //
                // HACK: There is not much point in pattern
                //       matching against the directory name
                //       since that is specified separately.
                //
                string localFileNameOnly = Path.GetFileName(
                    localFileName);

                //
                // HACK: The pattern is IGNORED if the file
                //       does not have a file extension that
                //       is recognized by this module, i.e.
                //       a configuration file extension.
                //
                if (!MatchFileExtension(localFileNameOnly))
                    continue;

                //
                // NOTE: Loop through the list of patterns.
                //       Stop if the file name matches any
                //       of them -AND- then add it to the
                //       final list.
                //
                bool match = false;

                foreach (string pattern in patterns)
                {
                    string localPattern = pattern;

                    if (String.IsNullOrEmpty(localPattern))
                        localPattern = null;

                    if ((localPattern != null) &&
                        !Parser.StringMatch(null,
                            localFileNameOnly, 0,
                            localPattern, 0, false))
                    {
                        continue;
                    }

                    match = true;
                    break;
                }

                if (match)
                {
                    fileNames.Add(localFileName);
                    result++;
                }
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name has an extension that
        /// is recognized as a configuration file extension.
        /// </summary>
        /// <param name="fileName">
        /// The file name whose extension is checked.
        /// </param>
        /// <returns>
        /// Non-zero if the file name has a recognized configuration file
        /// extension; otherwise, zero.
        /// </returns>
        private static bool MatchFileExtension( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            IEnumerable<string> fileExtensions = GetFileExtensions();

            if (fileExtensions == null)
                return false;

            string fileExtension = Path.GetExtension(fileName);

            foreach (string localFileExtension in fileExtensions)
            {
                if (DataOps.PathStringEquals(
                        fileExtension, localFileExtension))
                {
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /* CANNOT RETURN NULL */
        /// <summary>
        /// Builds the prioritized list of environment variable names used to
        /// locate a configuration file name, script text, and signature text
        /// for the specified index, optionally incorporating the plugin type
        /// and variant name.
        /// </summary>
        /// <param name="pluginType">
        /// The optional plugin type whose name is incorporated into the
        /// environment variable names.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name incorporated into the environment
        /// variable names.
        /// </param>
        /// <param name="index">
        /// The index incorporated into the environment variable names.
        /// </param>
        /// <returns>
        /// The list of candidate environment variable names.
        /// </returns>
        private static IList<string> GetEnvVarNames( /* CORE */
            Type pluginType,    /* in: OPTIONAL */
            string variantName, /* in: OPTIONAL */
            int index           /* in */
            )
        {
            //
            // NOTE: The returned list (of environment variable names)
            //       should contain at least the following, in order of
            //       priority, as listed here if <type> and <variant>
            //       are not null:
            //
            //       "<FileNameEnvVarPrefix><index>_<type>_<variant>"
            //       "<ScriptTextEnvVarPrefix><index>_<type>_<variant>"
            //       "<SignatureTextEnvVarPrefix><index>_<type>_<variant>"
            //
            //       -OR- if <type> is not null:
            //
            //       "<FileNameEnvVarPrefix><index>_<type>"
            //       "<ScriptTextEnvVarPrefix><index>_<type>"
            //       "<SignatureTextEnvVarPrefix><index>_<type>"
            //
            //       -OR- if <variant> is not null:
            //
            //       "<FileNameEnvVarPrefix><index>_<variant>"
            //       "<ScriptTextEnvVarPrefix><index>_<variant>"
            //       "<SignatureTextEnvVarPrefix><index>_<variant>"
            //
            //       -OR- if <type> and <variant> are null:
            //
            //       "<FileNameEnvVarPrefix><index>"
            //       "<ScriptTextEnvVarPrefix><index>"
            //       "<SignatureTextEnvVarPrefix><index>"
            //
            StringList envVarNames = new StringList();

            if (pluginType != null)
            {
                string pluginTypeName = (pluginType != null) ?
                    pluginType.ToString() : null;

                if (!String.IsNullOrEmpty(variantName))
                {
                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat3,
                        Constants.ConfigurationFileNameEnvVarName,
                        index, pluginTypeName, variantName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat3,
                        Constants.ConfigurationScriptTextEnvVarName,
                        index, pluginTypeName, variantName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat3,
                        Constants.ConfigurationSignatureTextEnvVarName,
                        index, pluginTypeName, variantName));
                }
                else
                {
                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationFileNameEnvVarName,
                        index, pluginTypeName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationScriptTextEnvVarName,
                        index, pluginTypeName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationSignatureTextEnvVarName,
                        index, pluginTypeName));
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(variantName))
                {
                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationFileNameEnvVarName,
                        index, variantName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationScriptTextEnvVarName,
                        index, variantName));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat2,
                        Constants.ConfigurationSignatureTextEnvVarName,
                        index, variantName));
                }
                else
                {
                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat1,
                        Constants.ConfigurationFileNameEnvVarName,
                        index));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat1,
                        Constants.ConfigurationScriptTextEnvVarName,
                        index));

                    envVarNames.Add(String.Format(
                        Constants.ConfigurationIndexFormat1,
                        Constants.ConfigurationSignatureTextEnvVarName,
                        index));
                }
            }

            return envVarNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Changes the extension of the specified script file name to the
        /// encrypted or unencrypted script extension, based on the requested
        /// state.
        /// </summary>
        /// <param name="encrypted">
        /// Non-zero to use the encrypted script extension, zero to use the
        /// unencrypted script extension, or null to leave the file name
        /// unchanged.
        /// </param>
        /// <param name="fileName">
        /// The script file name to mutate in place.
        /// </param>
        private static void MutateScriptFileName( /* CORE */
            bool? encrypted,    /* in: OPTIONAL */
            ref string fileName /* in, out */
            )
        {
            if (encrypted == null)
                return;

            if (String.IsNullOrEmpty(fileName))
                return;

            if ((bool)encrypted)
            {
                fileName = Path.Combine(Path.GetDirectoryName(
                    fileName), String.Format("{0}{1}",
                    Path.GetFileNameWithoutExtension(fileName),
                    FileExtension.EncryptedScript));
            }
            else
            {
                fileName = Path.Combine(Path.GetDirectoryName(
                    fileName), String.Format("{0}{1}",
                    Path.GetFileNameWithoutExtension(fileName),
                    FileExtension.Script));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines, as a unique-string callback, whether the specified
        /// script file name should be skipped because it was already seen or
        /// because an equivalent file with the opposite (un)encrypted
        /// extension already exists.
        /// </summary>
        /// <param name="collection">
        /// The collection of items being processed.  This parameter is not
        /// currently used.
        /// </param>
        /// <param name="dictionary">
        /// The optional dictionary of items that have already been seen.
        /// </param>
        /// <param name="keyItem">
        /// The candidate script file name to evaluate.
        /// </param>
        /// <returns>
        /// Non-zero to skip the item, or null to defer the decision to the
        /// caller.
        /// </returns>
        private static bool? HaveAnyScriptFileName( /* CORE */
            ICollection<string> collection,         /* in: NOT USED */
            IDictionary<string, string> dictionary, /* in: OPTIONAL */
            string keyItem                          /* in */
            ) /* Eagle._Components.Public.Delegates.UniqueStringCallback<T> */
        {
            if ((dictionary != null) && (keyItem != null))
            {
                //
                // NOTE: Has this exact item name already been seen by
                //       our caller (i.e. GetUniqueElements)?  In that
                //       case, just skip it.
                //
                if (dictionary.ContainsKey(keyItem))
                    return true;

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                //
                // BUGFIX: Skip over any item name that looks like a URI
                //         and/or does not exist on the file system.
                //
                if (!Utility.IsRemoteUri(keyItem) && File.Exists(keyItem))
                {
                    //
                    // NOTE: Next, assume the item name is actually a local
                    //       file name, then calculate a new file name that
                    //       should be almost identical, with the exception
                    //       that it should have the opposite (un)encrypted
                    //       file extension as the original item name.
                    //
                    bool encrypted = SharedOps.IsEncryptedFileName(keyItem);
                    string localKeyItem = keyItem;

                    /* NO RESULT */
                    MutateScriptFileName(!encrypted, ref localKeyItem);

                    //
                    // BUGFIX: Next, if there exists an (un)encrypted file
                    //         with exactly the same name, except the file
                    //         extension, we MAY want to skip it.  For the
                    //         original Beta 55 assembly, no extra checks
                    //         were performed here to see if either of the
                    //         files actually existed.  This meant that no
                    //         encrypted file names could ever (?) survive
                    //         because all the unencrypted file names came
                    //         first in the original list.
                    //
                    // NOTE: Only have our caller skip adding the original
                    //       item name if both item names are "valid", not
                    //       remote URIs, exist on the local file system,
                    //       and do not point to the same underlying file
                    //       on the local file system.  Some of the checks
                    //       mentioned are performed by the code blocks
                    //       containing this code block.
                    //
                    if ((localKeyItem != null) &&
                        dictionary.ContainsKey(localKeyItem) &&
                        !Utility.IsRemoteUri(localKeyItem) &&
                        File.Exists(localKeyItem) &&
                        !Utility.IsSameFile(keyItem, localKeyItem))
                    {
                        return true;
                    }
                }
#endif
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method cannot currently "fail"; however, its return
        //       code should be checked anyway.
        //
        /// <summary>
        /// Gathers configuration script text and signature text directly
        /// from the process environment for the specified plugin type and
        /// variant name, adding each to the supplied list.
        /// </summary>
        /// <param name="pluginType">
        /// The optional plugin type used when building the environment
        /// variable names.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name used when building the environment
        /// variable names.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when reading the script text.
        /// </param>
        /// <param name="debugger">
        /// Indicates whether a debugger was detected.  This parameter is not
        /// currently used.
        /// </param>
        /// <param name="configurations">
        /// The list to which the gathered configuration data is added.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while gathering the configuration
        /// data.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode GatherAllFromEnvironment( /* CORE */
            Type pluginType,                            /* in: OPTIONAL */
            string variantName,                         /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            bool debugger,                              /* in: NOT USED */
            ref FileAndOrStreamDataList configurations, /* in, out */
            ref ResultList errors                       /* in, out */
            )
        {
            int minimumIndex = Constants.ConfigurationMinimumIndex;
            int maximumIndex = Constants.ConfigurationMaximumIndex;

            for (int index0 = minimumIndex; index0 <= maximumIndex; index0++)
            {
                StringList envVarNames = new StringList();

                envVarNames.AddRange(
                    GetEnvVarNames(pluginType, variantName, index0));

                envVarNames.AddRange(
                    GetEnvVarNames(pluginType, null, index0));

                envVarNames.AddRange(
                    GetEnvVarNames(null, variantName, index0));

                envVarNames.AddRange(
                    GetEnvVarNames(null, null, index0));

                int count1 = envVarNames.Count;

                if ((count1 % 3) != 0)
                    continue;

                for (int index1 = 0; index1 < count1; index1 += 3)
                {
                    FileAndOrStreamData textData = null;

                    ScriptOps.MaybeGetStreamFrom(
                        GetFileName(GetVariable(
                            envVarNames[index1 + 0]),
                            envVarNames[index1 + 1]),
                        GetVariable(envVarNames[index1 + 1]),
                        GetVariable(envVarNames[index1 + 2]),
                        encoding, ref textData, ref errors);

                    if (textData != null)
                    {
                        if (configurations == null)
                            configurations = new FileAndOrStreamDataList();

                        configurations.Add(textData);
                    }
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gathers the candidate configuration file names found in the
        /// specified directory for all simple names and file extensions,
        /// adding them to the supplied list.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used when determining the machine
        /// identifier.
        /// </param>
        /// <param name="pluginType">
        /// The optional plugin type incorporated into the candidate file
        /// names.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name incorporated into the candidate file
        /// names.
        /// </param>
        /// <param name="directory">
        /// The directory in which to look for configuration files.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when determining the machine identifier.
        /// </param>
        /// <param name="debugger">
        /// Non-zero to also include debugger-specific candidate file names.
        /// </param>
        /// <param name="fileNames">
        /// The list to which the candidate file names are added.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while gathering the file names.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode GatherAllFromDirectory( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            Type pluginType,          /* in: OPTIONAL */
            string variantName,       /* in: OPTIONAL */
            string directory,         /* in */
            CultureInfo cultureInfo,  /* in */
            bool debugger,            /* in */
            ref StringList fileNames, /* in, out */
            ref ResultList errors     /* in, out */
            )
        {
            Guid? machineId = null;

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            //
            // NOTE: Next, grab the "unique identifier" for the current
            //       machine.  This may not actually be unique; however,
            //       we do not really care about that.
            //
            machineId = CertificatePolicyOps.GetMachineId(
                interpreter, null, cultureInfo);
#endif

            ///////////////////////////////////////////////////////////////////

            IEnumerable<string> simpleNames = GetSimpleNames();

            if (simpleNames == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("simple names unavailable");

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            IEnumerable<string> fileExtensions = GetFileExtensions();

            if (fileExtensions == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("file extensions unavailable");

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            IEnumerable<string> localFileNames; /* REUSED */
            Result localError; /* REUSED */

            foreach (string simpleName in simpleNames)
            {
                foreach (string fileExtension in fileExtensions)
                {
                    if ((pluginType != null) &&
                        !String.IsNullOrEmpty(variantName))
                    {
                        localError = null;

                        localFileNames = GetFileNames(
                            interpreter, simpleName, pluginType,
                            machineId, variantName, directory,
                            fileExtension, false, debugger,
                            ref localError);

                        if (localFileNames == null)
                        {
                            if (localError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(localError);
                            }

                            return ReturnCode.Error;
                        }

                        if (fileNames == null)
                            fileNames = new StringList();

                        fileNames.AddRange(localFileNames);
                    }

                    ///////////////////////////////////////////////////////////

                    if (!String.IsNullOrEmpty(variantName))
                    {
                        localError = null;

                        localFileNames = GetFileNames(
                            interpreter, simpleName, null,
                            machineId, variantName, directory,
                            fileExtension, false, debugger,
                            ref localError);

                        if (localFileNames == null)
                        {
                            if (localError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(localError);
                            }

                            return ReturnCode.Error;
                        }

                        if (fileNames == null)
                            fileNames = new StringList();

                        fileNames.AddRange(localFileNames);
                    }

                    ///////////////////////////////////////////////////////////

                    if (pluginType != null)
                    {
                        localError = null;

                        localFileNames = GetFileNames(
                            interpreter, simpleName, pluginType,
                            machineId, null, directory,
                            fileExtension, false, debugger,
                            ref localError);

                        if (localFileNames == null)
                        {
                            if (localError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(localError);
                            }

                            return ReturnCode.Error;
                        }

                        if (fileNames == null)
                            fileNames = new StringList();

                        fileNames.AddRange(localFileNames);
                    }

                    ///////////////////////////////////////////////////////////

                    localError = null;

                    localFileNames = GetFileNames(
                        interpreter, simpleName, null,
                        machineId, null, directory,
                        fileExtension, false, debugger,
                        ref localError);

                    if (localFileNames == null)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }

                        return ReturnCode.Error;
                    }

                    if (fileNames == null)
                        fileNames = new StringList();

                    fileNames.AddRange(localFileNames);
                }
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gathers configuration data from the environment, the explicitly
        /// listed files, the matching files in the directory, and the
        /// optional epilogue, then reads each into the supplied list of
        /// configurations.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="assembly">
        /// The optional assembly used when reading embedded resources.
        /// </param>
        /// <param name="pluginType">
        /// The optional plugin type used when locating configuration data.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name used when locating configuration data.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when reading configuration data.
        /// </param>
        /// <param name="directory">
        /// The directory in which to look for configuration files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when locating configuration data.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout used when reading remote configuration data.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to permit reading configuration data from remote URIs.
        /// </param>
        /// <param name="environmentOnly">
        /// Non-zero to gather configuration data from the environment only.
        /// </param>
        /// <param name="skipEmbedded">
        /// Non-zero to skip reading embedded configuration data.
        /// </param>
        /// <param name="configurations">
        /// The list to which the gathered configuration data is added.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while gathering or reading the
        /// configuration data.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode MaybeGatherAndReadAll( /* CORE */
            Interpreter interpreter,                    /* in: OPTIONAL */
            Assembly assembly,                          /* in: OPTIONAL */
            Type pluginType,                            /* in: OPTIONAL */
            string variantName,                         /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            string directory,                           /* in */
            CultureInfo cultureInfo,                    /* in: OPTIONAL */
            int? timeout,                               /* in: OPTIONAL */
            bool allowRemoteUri,                        /* in */
            bool environmentOnly,                       /* in */
            bool skipEmbedded,                          /* in */
            ref FileAndOrStreamDataList configurations, /* in, out */
            ref ResultList errors                       /* in, out */
            )
        {
            ///////////////////////////////////////////////////////////////////
            // PHASE #0: Detect things from the environment, e.g. are we being
            //           run from within a debugger?
            ///////////////////////////////////////////////////////////////////

            //
            // HACK: If any debugger appears to be attached, set the flag;
            //       this may result in an additional configuration file
            //       being found and loaded FOR EACH of the file names that
            //       are listed (just) below.
            //
            bool debugger = IsThereAnyDebugger();

            ///////////////////////////////////////////////////////////////////
            // PHASE #1: Attempt to read script block and its signature from
            //           the process environment directly.
            ///////////////////////////////////////////////////////////////////

            if (GatherAllFromEnvironment(
                    pluginType, variantName, encoding, debugger,
                    ref configurations, ref errors) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (environmentOnly)
                return ReturnCode.Ok;

            ///////////////////////////////////////////////////////////////////
            // PHASE #2: Attempt to query list of explicit configuration files
            //           to be loaded.
            ///////////////////////////////////////////////////////////////////

            StringList fileNames = new StringList();

            AddEnvironmentFileNames(fileNames);

            if (directory != null)
                AddEnvironmentFilePatterns(directory, fileNames);

            ///////////////////////////////////////////////////////////////////
            // PHASE #3: Attempt to build list of implicit configuration files
            //           to be loaded.
            ///////////////////////////////////////////////////////////////////

            if (!DoesVariableExist(
                    Constants.ConfigurationOverrideOnlyEnvVarName) &&
                (directory != null) && (GatherAllFromDirectory(
                    interpreter, pluginType, variantName,
                    directory, cultureInfo, debugger,
                    ref fileNames, ref errors) != ReturnCode.Ok))
            {
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////
            // PHASE #4: Attempt to get the (optional) configuration "epilogue"
            //           file and/or stream.
            ///////////////////////////////////////////////////////////////////

            string epilogueFileName = GetVariable(
                Constants.ConfigurationEpilogueFileNameEnvVarName);

            FileAndOrStreamData epilogueFileData = null;
            FileAndOrStreamData epilogueStreamData = null;

            if (!String.IsNullOrEmpty(epilogueFileName))
            {
                /* NO RESULT */
                ScriptOps.ReadFileAndOrStream(
                    interpreter, assembly, encoding, epilogueFileName,
                    timeout, allowRemoteUri, skipEmbedded,
                    ref epilogueFileData, ref epilogueStreamData,
                    ref errors);
            }

            ///////////////////////////////////////////////////////////////////
            // PHASE #5: Attempt to read data from all configuration files and
            //           streams.
            ///////////////////////////////////////////////////////////////////

            //
            // BUGFIX: Why is this environment variable check here?  Because,
            //         the Beta 55 code had a bug here causing configuration
            //         files to be skipped.  Now, even though the underlying
            //         bug has been fixed, this environment variable has been
            //         added as a backup safety mechanism.
            //
            bool unique;

            if (!DoesVariableExist(
                    Constants.ConfigurationNoUniqueFileNamesEnvVarName))
            {
                fileNames = Utility.GetUniqueElements(fileNames,
                    HaveAnyScriptFileName);

                unique = true;
            }
            else
            {
                unique = false;
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "MaybeGatherAndReadAll: {0}FILE NAMES: {1}",
                unique ? "UNIQUE " : "ALL ",
                DataOps.FormatList(fileNames)),
                typeof(Configuration).Name,
                TracePriority.Medium);
#endif

            foreach (string fileName in fileNames)
            {
                FileAndOrStreamData fileData = null;
                FileAndOrStreamData streamData = null;

                /* NO RESULT */
                ScriptOps.ReadFileAndOrStream(
                    interpreter, assembly, encoding,
                    fileName, timeout, allowRemoteUri,
                    skipEmbedded, ref fileData,
                    ref streamData, ref errors);

                if (fileData != null)
                {
                    if (configurations == null)
                        configurations = new FileAndOrStreamDataList();

                    configurations.Add(fileData);

                    if (epilogueFileData != null)
                        configurations.Add(epilogueFileData);

                    if (epilogueStreamData != null)
                        configurations.Add(epilogueStreamData);
                }

                if (streamData != null)
                {
                    if (configurations == null)
                        configurations = new FileAndOrStreamDataList();

                    configurations.Add(streamData);

                    if (epilogueFileData != null)
                        configurations.Add(epilogueFileData);

                    if (epilogueStreamData != null)
                        configurations.Add(epilogueStreamData);
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter settings file name configured via the
        /// environment for the specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to format the settings environment variable
        /// name.
        /// </param>
        /// <returns>
        /// The configured settings file name, or null if none was
        /// configured.
        /// </returns>
        private static string GetSettingsFileNameViaEnvironment( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            return GetVariable(
                DataOps.FormatWithPluginData(pluginData,
                Constants.ConfigurationInterpreterSettingsEnvVarFormat,
                Constants.ConfigurationInterpreterSettingsEnvVarName));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter settings file name configured via the
        /// environment, trying both the specified plugin data and the
        /// unqualified form.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to locate the settings file name.
        /// </param>
        /// <returns>
        /// The configured settings file name, or null if none was
        /// configured.
        /// </returns>
        private static string GetSettingsFileNameCallback( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            foreach (IPluginData localPluginData in new IPluginData[] {
                    pluginData, null
                })
            {
                string result = GetSettingsFileNameViaEnvironment(
                    localPluginData);

                if (!String.IsNullOrEmpty(result))
                    return result;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes any open streams contained in the specified list of
        /// configurations, then clears the list and resets it to null.
        /// </summary>
        /// <param name="configurations">
        /// The list of configurations whose streams are closed and which is
        /// then reset to null.
        /// </param>
        private static void CloseStreamsAndReset( /* CORE */
            ref FileAndOrStreamDataList configurations /* in, out */
            )
        {
            if (configurations == null)
                return;

            foreach (FileAndOrStreamData data in configurations)
            {
                Stream stream = data.Stream;

                if (stream == null)
                    continue;

                try
                {
                    stream.Close(); /* throw */
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(Configuration).Name,
                        TracePriority.Highest);
#endif
                }
                finally
                {
                    data.Stream = stream = null;
                }
            }

            configurations.Clear();
            configurations = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gathers and evaluates all applicable configuration scripts, using
        /// a sandboxed client data context, then records the file names that
        /// succeeded or failed with the plugin configuration.
        /// </summary>
        /// <param name="sandboxToken">
        /// The sandbox token identifying the interpreter used to evaluate
        /// the configuration scripts.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the configuration scripts.
        /// </param>
        /// <param name="anyClientData">
        /// The client data to attach to the configuration evaluation
        /// context.
        /// </param>
        /// <param name="assembly">
        /// The optional assembly used when reading embedded configuration
        /// data.
        /// </param>
        /// <param name="plugin">
        /// The optional plugin whose configuration is being loaded.
        /// </param>
        /// <param name="pluginType">
        /// The optional plugin type used when locating configuration data.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name used when locating configuration data.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used when verifying
        /// script signatures.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when verifying script signatures.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when reading configuration data.
        /// </param>
        /// <param name="directory">
        /// The directory in which to look for configuration files.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify configuration script signatures.
        /// </param>
        /// <param name="keyName">
        /// The optional key name used when verifying script signatures.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name used when verifying script signatures.
        /// </param>
        /// <param name="keyUsage">
        /// The optional key usage associated with the key pairs.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when evaluating the configuration
        /// scripts.
        /// </param>
        /// <param name="configurationPhase">
        /// The phase in which the configuration is being loaded.
        /// </param>
        /// <param name="trustFlags">
        /// The trust flags applied when evaluating the configuration
        /// scripts.
        /// </param>
        /// <param name="policyType">
        /// The optional policy type applied to the configuration scripts.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy applied to the configuration
        /// scripts.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout used when reading remote configuration data.
        /// </param>
        /// <param name="untrusted">
        /// Non-zero to evaluate the configuration scripts in an untrusted
        /// manner.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to permit reading configuration data from remote URIs.
        /// </param>
        /// <param name="environmentOnly">
        /// Non-zero to gather configuration data from the environment only.
        /// </param>
        /// <param name="skipEmbedded">
        /// Non-zero to skip reading embedded configuration data.
        /// </param>
        /// <param name="forceCommands">
        /// Non-zero to force the addition of the configuration commands.
        /// </param>
        /// <param name="swapCommands">
        /// Non-zero to swap the configuration commands.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to permit local policies within the configuration
        /// scripts.
        /// </param>
        /// <param name="failOnError">
        /// Non-zero to fail immediately when an error is encountered.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop processing further scripts when an error is
        /// encountered.
        /// </param>
        /// <param name="doNotTrack">
        /// Non-zero to skip recording the succeeded and failed file names
        /// with the plugin configuration.
        /// </param>
        /// <param name="results">
        /// Receives the per-script results, including any errors that were
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode LoadAll( /* CORE */
            ulong? sandboxToken,                   /* in */
            Interpreter interpreter,               /* in */
            IAnyClientData anyClientData,          /* in */
            Assembly assembly,                     /* in: OPTIONAL */
            IPlugin plugin,                        /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string hashAlgorithmName,              /* in: OPTIONAL */
            byte[] hashKey,                        /* in: OPTIONAL */
            Encoding encoding,                     /* in */
            string directory,                      /* in */
            IEnumerable<IKeyPair> keyPairs,        /* in */
            string keyName,                        /* in: OPTIONAL */
            string keyRingName,                    /* in: OPTIONAL */
            string keyUsage,                       /* in: OPTIONAL */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            TrustFlags trustFlags,                 /* in */
            PolicyType? policyType,                /* in: OPTIONAL */
            ExecutionPolicy? policy,               /* in: OPTIONAL */
            int? timeout,                          /* in: OPTIONAL */
            bool untrusted,                        /* in */
            bool allowRemoteUri,                   /* in */
            bool environmentOnly,                  /* in */
            bool skipEmbedded,                     /* in */
            bool forceCommands,                    /* in */
            bool swapCommands,                     /* in */
            bool allowLocalPolicy,                 /* in */
            bool failOnError,                      /* in */
            bool stopOnError,                      /* in */
            bool doNotTrack,                       /* in */
            ref ResultList results                 /* in, out */
            )
        {
            FileAndOrStreamDataList configurations = null;

            try
            {
                ResultList localResults = null;

                if (MaybeGatherAndReadAll(
                        interpreter, assembly, pluginType,
                        variantName, encoding, directory,
                        cultureInfo, timeout, allowRemoteUri,
                        environmentOnly, skipEmbedded,
                        ref configurations,
                        ref localResults) != ReturnCode.Ok)
                {
                    results = localResults;
                    return ReturnCode.Error;
                }

                if (configurations == null)
                    return ReturnCode.Ok;

                ResultDictionary okFileNames = null;
                ResultDictionary errorFileNames = null;

                using (EvaluateClientData clientData = new EvaluateClientData(
                        cultureInfo, null, null, Guid.Empty, null, null,
                        Constants.LoadAllContextName, new SharedEventWaitHandle(
                            false, EventResetMode.ManualReset), sandboxToken,
                        new LongList(), GetSettingsFileNameCallback, null,
                        interpreter, plugin, pluginType, null, null,
                        variantName, hashAlgorithmName, hashKey, encoding,
                        null, null, directory, null, null, keyPairs, null,
                        keyName, keyRingName, null, null, keyUsage,
                        configurationPhase, trustFlags, policyType, policy,
                        timeout, 0, untrusted, allowRemoteUri, true, true,
                        forceCommands, swapCommands, false, allowLocalPolicy,
                        true, failOnError, false))
                {
                    try
                    {
                        /* IGNORED */
                        clientData.AttachTo(anyClientData);

                    retry:

                        foreach (FileAndOrStreamData data in configurations)
                        {
                            if (data == null)
                                continue;

                            /* NO RESULT */
                            EvaluateClientData.ForNewScript(clientData, data);

                            //
                            // BUGFIX: The required version was ALWAYS intended to be
                            //         checked on a per-file basis.
                            //
                            /* IGNORED */
                            EvaluateClientData.ResetRequiredVersion(clientData, true);

                            ReturnCode code;
                            Result localResult = null;

#if TEST
                            IClientData savedClientData = null;

                            ScriptOps.BeginClientData(
                                interpreter, clientData, ref savedClientData);

                            try
                            {
#endif
#if DEBUG || FORCE_TRACE
                                try
                                {
#endif
                                    if (clientData.Stream != null)
                                    {
                                        code = ScriptOps.EvaluateStream(
                                            clientData, ref localResult);
                                    }
                                    else
                                    {
                                        code = ScriptOps.EvaluateFile(
                                            clientData, ref localResult);
                                    }
#if DEBUG || FORCE_TRACE
                                }
                                finally
                                {
                                    //
                                    // HACK: Update the hash value for this
                                    //       file / stream based on the one
                                    //       calculated during the signature
                                    //       verification process within the
                                    //       Evaluate*() methods.  This is
                                    //       only included when tracing is
                                    //       enabled because it is only used
                                    //       for tracing.
                                    //
                                    data.HashValue = clientData.HashValue;
                                }
#endif
#if TEST
                            }
                            finally
                            {
                                ScriptOps.EndClientData(
                                    interpreter, ref savedClientData);
                            }
#endif

#if DEBUG || FORCE_TRACE
                            bool success = ((code == ReturnCode.Ok) ||
                                (code == ReturnCode.Break));

                            CertificateTraceOps.DebugTrace(String.Format(
                                "LoadAll: {0} = {1} ({2}), plugin = {3}, " +
                                "signature = {4}, code = {5}, result = {6}",
                                (data.Stream != null) ? "stream" : "fileName",
                                Utility.FormatWrapOrNull(data.FileName),
                                Utility.FormatMaybeNull(
                                    DataOps.FormatHexadecimal(
                                        data.HashValue, false)),
                                Utility.FormatWrapOrNull(plugin),
                                Utility.FormatWrapOrNull(true, true,
                                    DataOps.FormatSignatureLine(
                                        data.Signature)),
                                Utility.FormatWrapOrNull(code),
                                Utility.FormatWrapOrNull(
                                    true, success, localResult)),
                                typeof(Configuration).Name,
                                success ?
                                    TracePriority.Medium :
                                    TracePriority.MediumHigh);
#endif

                            if (localResult != null)
                            {
                                if (results == null)
                                    results = new ResultList();

                                results.Add(StringList.MakeList(
                                    code, data.FileName, localResult));
                            }

                            //
                            // HACK: This code must assume that the error message
                            //       used with the [fatalError] command is still
                            //       available at this point, i.e. there was not
                            //       something else in the way of preserving it,
                            //       e.g. [catch], [evaluateWithoutError], etc;
                            //       otherwise, the "FatalError" property would
                            //       need to be changed to a Result object -AND-
                            //       treated as tripped when set to non-null.
                            //
                            if ((clientData != null) && clientData.FatalError)
                            {
#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.DebugTrace(String.Format(
                                    "LoadAll: FATAL ERROR HIT IN {0} {1} " +
                                    "({2}) {3}: {4} {5}",
                                    (data.Stream != null) ? "STREAM" : "FILE",
                                    Utility.FormatWrapOrNull(data.FileName),
                                    Utility.FormatMaybeNull(
                                        DataOps.FormatHexadecimal(
                                            data.HashValue, false)),
                                    Utility.FormatWrapOrNull(plugin),
                                    Utility.FormatWrapOrNull(code),
                                    Utility.FormatWrapOrNull(localResult)),
                                    typeof(Configuration).Name,
                                    TracePriority.Higher);
#endif

                                return ReturnCode.Error;
                            }

                            if ((code == ReturnCode.Ok) ||
                                (code == ReturnCode.Break))
                            {
                                if (data.FileName != null)
                                {
                                    if (okFileNames == null)
                                        okFileNames = new ResultDictionary();

                                    if (localResult != null)
                                        localResult.ReturnCode = code;

                                    okFileNames[data.FileName] = localResult;
                                }

                                if (code == ReturnCode.Break)
                                {
#if DEBUG || FORCE_TRACE
                                    CertificateTraceOps.DebugTrace(String.Format(
                                        "LoadAll: BREAK HIT IN {0} {1} " +
                                        "({2}) {3}: {4} {5}",
                                        (data.Stream != null) ? "STREAM" : "FILE",
                                        Utility.FormatWrapOrNull(data.FileName),
                                        Utility.FormatMaybeNull(
                                            DataOps.FormatHexadecimal(
                                                data.HashValue, false)),
                                        Utility.FormatWrapOrNull(plugin),
                                        Utility.FormatWrapOrNull(code),
                                        Utility.FormatWrapOrNull(localResult)),
                                        typeof(Configuration).Name,
                                        TracePriority.MediumHigh);
#endif

                                    break;
                                }
                            }
                            else
                            {
                                if (data.FileName != null)
                                {
                                    if (errorFileNames == null)
                                        errorFileNames = new ResultDictionary();

                                    if (localResult != null)
                                        localResult.ReturnCode = code;

                                    errorFileNames[data.FileName] = localResult;
                                }

                                if (clientData.FailOnError)
                                    return ReturnCode.Error;

                                if (stopOnError)
                                    break;

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.DebugTrace(String.Format(
                                    "LoadAll: NORMAL ERROR HIT IN {0} {1} " +
                                    "({2}) {3}: {4} {5}",
                                    (data.Stream != null) ? "STREAM" : "FILE",
                                    Utility.FormatWrapOrNull(data.FileName),
                                    Utility.FormatMaybeNull(
                                        DataOps.FormatHexadecimal(
                                            data.HashValue, false)),
                                    Utility.FormatWrapOrNull(plugin),
                                    Utility.FormatWrapOrNull(code),
                                    Utility.FormatWrapOrNull(localResult)),
                                    typeof(Configuration).Name,
                                    TracePriority.High);
#endif
                            }
                        }

                        //
                        // HACK: Check new configuration scripts queued via
                        //       [queueScript] commands within the evaluated
                        //       configuration scripts.
                        //
                        FileAndOrStreamDataList queue = clientData.DequeueScripts();

                        if (queue != null)
                        {
                            CloseStreamsAndReset(ref configurations);
                            configurations = queue;

                            goto retry;
                        }

                        return ReturnCode.Ok;
                    }
                    finally
                    {
                        if (!doNotTrack)
                        {
                            IConfiguration configuration = plugin as IConfiguration;

                            if (configuration != null)
                            {
                                /* IGNORED */
                                configuration.ClearConfigurationFileNames();

                                /* IGNORED */
                                configuration.AddConfigurationOkFileNames(
                                    interpreter, okFileNames);

                                /* IGNORED */
                                configuration.AddConfigurationErrorFileNames(
                                    interpreter, errorFileNames);
                            }
                        }
                    }
                }
            }
            finally
            {
                CloseStreamsAndReset(ref configurations);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads all applicable configuration scripts unless configuration
        /// loading has been disabled, optionally within an isolated
        /// interpreter and optionally on a queued (asynchronous) work item.
        /// </summary>
        /// <param name="sandboxToken">
        /// The sandbox token identifying the interpreter used to evaluate
        /// the configuration scripts.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the configuration scripts.
        /// </param>
        /// <param name="anyClientData">
        /// The client data to attach to the configuration evaluation
        /// context.
        /// </param>
        /// <param name="assembly">
        /// The optional assembly used when reading embedded configuration
        /// data.
        /// </param>
        /// <param name="plugin">
        /// The optional plugin whose configuration is being loaded.
        /// </param>
        /// <param name="pluginType">
        /// The optional plugin type used when locating configuration data.
        /// </param>
        /// <param name="variantName">
        /// The optional variant name used when locating configuration data.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used when verifying
        /// script signatures.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when verifying script signatures.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when reading configuration data.
        /// </param>
        /// <param name="directory">
        /// The directory in which to look for configuration files.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify configuration script signatures.
        /// </param>
        /// <param name="keyName">
        /// The optional key name used when verifying script signatures.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name used when verifying script signatures.
        /// </param>
        /// <param name="keyUsage">
        /// The optional key usage associated with the key pairs.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when evaluating the configuration
        /// scripts.
        /// </param>
        /// <param name="configurationPhase">
        /// The phase in which the configuration is being loaded.
        /// </param>
        /// <param name="trustFlags">
        /// The trust flags applied when evaluating the configuration
        /// scripts.
        /// </param>
        /// <param name="policyType">
        /// The optional policy type applied to the configuration scripts.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy applied to the configuration
        /// scripts.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout used when reading remote configuration data.
        /// </param>
        /// <param name="untrusted">
        /// Non-zero to evaluate the configuration scripts in an untrusted
        /// manner.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to permit reading configuration data from remote URIs.
        /// </param>
        /// <param name="environmentOnly">
        /// Non-zero to gather configuration data from the environment only.
        /// </param>
        /// <param name="skipEmbedded">
        /// Non-zero to skip reading embedded configuration data.
        /// </param>
        /// <param name="forceCommands">
        /// Non-zero to force the addition of the configuration commands.
        /// </param>
        /// <param name="swapCommands">
        /// Non-zero to swap the configuration commands.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to permit local policies within the configuration
        /// scripts.
        /// </param>
        /// <param name="failOnError">
        /// Non-zero to fail immediately when an error is encountered.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop processing further scripts when an error is
        /// encountered.
        /// </param>
        /// <param name="doNotTrack">
        /// Non-zero to skip recording the succeeded and failed file names
        /// with the plugin configuration.
        /// </param>
        /// <param name="results">
        /// Receives the per-script results, including any errors that were
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode MaybeLoadAll( /* CORE */
            ulong? sandboxToken,                   /* in */
            Interpreter interpreter,               /* in */
            IAnyClientData anyClientData,          /* in */
            Assembly assembly,                     /* in: OPTIONAL */
            IPlugin plugin,                        /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string hashAlgorithmName,              /* in: OPTIONAL */
            byte[] hashKey,                        /* in: OPTIONAL */
            Encoding encoding,                     /* in */
            string directory,                      /* in */
            IEnumerable<IKeyPair> keyPairs,        /* in */
            string keyName,                        /* in: OPTIONAL */
            string keyRingName,                    /* in: OPTIONAL */
            string keyUsage,                       /* in: OPTIONAL */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            TrustFlags trustFlags,                 /* in */
            PolicyType? policyType,                /* in: OPTIONAL */
            ExecutionPolicy? policy,               /* in: OPTIONAL */
            int? timeout,                          /* in: OPTIONAL */
            bool untrusted,                        /* in */
            bool allowRemoteUri,                   /* in */
            bool environmentOnly,                  /* in */
            bool skipEmbedded,                     /* in */
            bool forceCommands,                    /* in */
            bool swapCommands,                     /* in */
            bool allowLocalPolicy,                 /* in */
            bool failOnError,                      /* in */
            bool stopOnError,                      /* in */
            bool doNotTrack,                       /* in */
            ref ResultList results                 /* in, out */
            )
        {
            ReturnCode code = ReturnCode.Ok;

            if (DoesVariableExist(
                    Constants.NoConfigurationEnvVarName))
            {
                return code;
            }

            ParameterizedThreadStart callback =
                    new ParameterizedThreadStart(delegate(object obj)
            {
                LoadPair innerPair = obj as LoadPair;
                ResultList innerResults;
                bool asynchronous;

                if (innerPair != null)
                {
                    innerResults = innerPair.X;
                    asynchronous = innerPair.Y;
                }
                else
                {
                    innerResults = null;
                    asynchronous = false;
                }

                bool createdInterpreter = false;
                Interpreter localInterpreter = null;

                try
                {
                    if (DoesVariableExist(
                            Constants.IsolatedConfigurationEnvVarName))
                    {
                        //
                        // HACK: Yes, this is potentially quite
                        //       expensive; however, it may be
                        //       necessary when the interpreter
                        //       passed by the caller is missing
                        //       important commands.
                        //
                        // HACK: If the sandbox (interpreter) token
                        //       (for the plugin) specified by the
                        //       caller is null, a new interpreter
                        //       will be created (and disposed) to
                        //       evaluate the configuration file;
                        //       otherwise, the existing sandbox
                        //       interpreter (for the plugin) may
                        //       be reused.  This design decision
                        //       may be revisited in the future.
                        //       It should be noted here that this
                        //       interpreter, when created with a
                        //       sandbox token, will also be used
                        //       by contained [evaluateInSandbox]
                        //       commands.
                        //
                        Result createResult = null;

                        localInterpreter = ScriptOps.CreateInterpreter(
                            sandboxToken, GetSettingsFileNameCallback,
                            null, interpreter, plugin, hashAlgorithmName,
                            hashKey, encoding, keyPairs, keyUsage,
                            cultureInfo, timeout, allowRemoteUri,
                            ref createdInterpreter, ref createResult);

                        if (localInterpreter == null)
                        {
                            if (createResult != null)
                            {
                                if (innerResults == null)
                                    innerResults = new ResultList();

                                innerResults.Add(createResult);
                            }

                            code = ReturnCode.Error;
                            return;
                        }

                        bool primaryToken; /* NOT USED */

                        /* IGNORED */
                        MaybeKeepTrackOfSandboxToken(
                            sandboxToken, plugin, localInterpreter,
                            createdInterpreter, out primaryToken);
                    }
                    else
                    {
                        localInterpreter = interpreter;
                    }

                    if (LoadAll(
                            sandboxToken, localInterpreter,
                            anyClientData, assembly,
                            plugin, pluginType, variantName,
                            hashAlgorithmName, hashKey,
                            encoding, directory, keyPairs,
                            keyName, keyRingName, keyUsage,
                            cultureInfo, configurationPhase,
                            trustFlags, policyType, policy,
                            timeout, untrusted, allowRemoteUri,
                            environmentOnly, skipEmbedded,
                            forceCommands, swapCommands,
                            allowLocalPolicy, failOnError,
                            stopOnError, doNotTrack,
                            ref innerResults) == ReturnCode.Ok)
                    {
                        code = ReturnCode.Ok;
                    }
                    else
                    {
                        code = ReturnCode.Error;
                    }
                }
                finally
                {
                    //
                    // BUGFIX: Apparently, without this assignment, the
                    //         underlying (outer) ResultList will not be
                    //         changed.
                    //
                    innerPair.X = innerResults;

                    if (createdInterpreter && (localInterpreter != null))
                    {
                        /* IGNORED */
                        Utility.TryDisposeObjectOrComplain<Interpreter>(
                            interpreter, ref localInterpreter);

                        localInterpreter = null;
                    }

                    if (asynchronous && (code != ReturnCode.Ok))
                        Utility.Complain(interpreter, code, innerResults);
                }
            });

            LoadPair outerPair;
            ResultList outerResults = new ResultList();

            if (DoesVariableExist(
                    Constants.AsynchronousConfigurationEnvVarName))
            {
                outerPair = new LoadPair(true, outerResults, true);

                if (results == null)
                    results = new ResultList();

                if (Engine.QueueWorkItem(interpreter,
                        callback, outerPair, QueueFlags.Default))
                {
                    results.Add("queued configuration work item");

                    return ReturnCode.Ok;
                }
                else
                {
                    results.Add(
                        "failed to queue configuration work item");

                    return ReturnCode.Error;
                }
            }
            else
            {
                outerPair = new LoadPair(true, outerResults, false);

                callback(outerPair);

                if (results == null)
                    results = new ResultList();

                if (!Object.ReferenceEquals(outerResults, outerPair.X))
                    outerResults = outerPair.X;

                if (outerResults != null)
                    results.AddRange(outerResults);

                return code;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the configuration for the specified plugin or
        /// configuration, resolving the directory, key pairs, and various
        /// options from the environment, unless configuration loading has
        /// been disabled.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to evaluate the configuration
        /// scripts.
        /// </param>
        /// <param name="assembly">
        /// The optional assembly used when reading embedded configuration
        /// data.
        /// </param>
        /// <param name="assemblyName">
        /// The optional name of the assembly used when obtaining key pairs.
        /// </param>
        /// <param name="plugin">
        /// The optional plugin whose configuration is being loaded.
        /// </param>
        /// <param name="configuration">
        /// The optional configuration providing the directory and key pairs.
        /// </param>
        /// <param name="baseDirectory">
        /// The optional base directory used to locate the configuration
        /// directory.
        /// </param>
        /// <param name="anyClientData">
        /// The client data to attach to the configuration evaluation
        /// context.
        /// </param>
        /// <param name="configurationPhase">
        /// The phase in which the configuration is being loaded.
        /// </param>
        /// <param name="policyType">
        /// The optional policy type applied to the configuration scripts.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy applied to the configuration
        /// scripts.
        /// </param>
        /// <param name="keyName">
        /// The key name used when verifying script signatures.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name used when verifying script signatures.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout used when reading remote configuration data.
        /// </param>
        /// <param name="force">
        /// Non-zero to load the configuration even when configuration
        /// loading has been disabled via the environment.
        /// </param>
        /// <param name="doNotTrack">
        /// Non-zero to skip recording the succeeded and failed file names
        /// with the plugin configuration.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the overall results or the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode MaybeLoadFor( /* CORE */
            Interpreter interpreter,               /* in: OPTIONAL */
            Assembly assembly,                     /* in: OPTIONAL */
            AssemblyName assemblyName,             /* in: OPTIONAL */
            IPlugin plugin,                        /* in: OPTIONAL */
            IConfiguration configuration,          /* in: OPTIONAL */
            string baseDirectory,                  /* in: OPTIONAL */
            IAnyClientData anyClientData,          /* in */
            ConfigurationPhase configurationPhase, /* in */
            PolicyType? policyType,                /* in: OPTIONAL */
            ExecutionPolicy? policy,               /* in: OPTIONAL */
            string keyName,                        /* in */
            string keyRingName,                    /* in */
            int? timeout,                          /* in: OPTIONAL */
            bool force,                            /* in */
            bool doNotTrack,                       /* in */
            ref Result result                      /* out */
            )
        {
            //
            // HACK: Pre-check the "NoConfiguration" environment
            //       variable here to avoid doing some potentially
            //       expensive operations, e.g. key pair lookups.
            //
            if (!force && DoesVariableExist(
                    Constants.NoConfigurationEnvVarName))
            {
                return ReturnCode.Ok;
            }

            CultureInfo cultureInfo;
            bool disposed;

            DataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                result = "interpreter is disposed";
                return ReturnCode.Error;
            }

            //
            // HACK: *DEEP* Also, permit just the configuration of
            //       this plugin type to be skipped.
            //
            Type pluginType = null;

            if (plugin != null)
            {
                pluginType = plugin.GetType();

                if (!force && (pluginType != null) &&
                    DoesVariableExist(String.Format(
                        "{0}_{1}", Constants.NoConfigurationEnvVarName,
                        pluginType)))
                {
                    return ReturnCode.Ok;
                }
            }

            string directory;

            if (baseDirectory != null)
                directory = GetDirectory(baseDirectory);
            else if (configuration != null)
                directory = configuration.GetConfigurationDirectory();
            else
                directory = null;

            IEnumerable<IKeyPair> keyPairs = null;
            string keyUsage = null;

            if (configuration != null)
            {
                if (configuration.GetConfigurationKeyPairs(
                        ref keyPairs, ref keyUsage,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                //
                // HACK: *SECURITY* The assembly is hard-coded
                //       here (to Harpy) because the loaded key
                //       pairs will be given all key usage flags
                //       and we cannot permit non-Harpy keys to
                //       have those usage flags.
                //
                if (GetKeyPairs( /* OK */
                        CertificateAssemblyOps.GetObject(),
                        CertificateAssemblyOps.GetName(),
                        ref keyPairs, ref keyUsage,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            bool environmentOnly = false;

            if (DoesVariableExist(
                    Constants.ConfigurationEnvironmentOnlyEnvVarName))
            {
                environmentOnly = true;
            }

            bool skipEmbedded = false;

            if (DoesVariableExist(
                    Constants.ConfigurationSkipEmbeddedEnvVarName))
            {
                skipEmbedded = true;
            }

            bool allowLocalPolicy;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            allowLocalPolicy = Constants.ChangeLocalPolicies;
#else
            allowLocalPolicy = false;
#endif

            bool failOnError = false;

            if (DoesVariableExist(
                    Constants.ConfigurationFailOnErrorEnvVarName))
            {
                failOnError = true;
            }

            bool forceCommands = false;

            if (DoesVariableExist(
                    Constants.ConfigurationForceCommandsEnvVarName))
            {
                forceCommands = true;
            }

            bool swapCommands = false;

            if (DoesVariableExist(
                    Constants.ConfigurationSwapCommandsEnvVarName))
            {
                swapCommands = true;
            }

            ulong sandboxToken;

            if (configuration != null)
                sandboxToken = configuration.GetPrimarySandboxToken();
            else
                sandboxToken = CertificateSandboxState.GetPrimaryToken();

            ResultList results = null;

            if (MaybeLoadAll(
                    sandboxToken, interpreter, anyClientData,
                    assembly, plugin, pluginType,
                    CertificateAssemblyOps.GetConfiguration(),
                    SharedOps.GetHashAlgorithm(
                        null, null, null,
                        HashAlgorithmType.ScriptUse),
                    null, DataOps.GetDefaultEncoding(),
                    directory, keyPairs, keyName, keyRingName,
                    keyUsage, cultureInfo, configurationPhase,
                    Constants.ConfigurationTrustFlags, policyType,
                    policy, timeout, false, true, environmentOnly,
                    skipEmbedded, forceCommands, swapCommands,
                    allowLocalPolicy, failOnError, false,
                    doNotTrack, ref results) == ReturnCode.Ok)
            {
                result = results;
                return ReturnCode.Ok;
            }
            else
            {
                result = results;
                return ReturnCode.Error;
            }
        }
    }
}
