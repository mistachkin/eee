/*
 * WebLicenseOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Public;
using Kapok.Components.Shared;

#if LICENSING || SECURITY
using Licensing.Sdk.Private;
#endif

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides license-certificate discovery, configuration, and verification
    /// for the server, including the per-thread certificate state and the
    /// Harpy SDK security configuration for sandboxed interpreters.
    /// </summary>
    [ObjectId("4b1d9512-d7f1-4b01-aea5-f127926cccfc")]
    internal static class WebLicenseOps
    {
        #region Private Constants
        //
        // NOTE: This is the environment variable name to use when setting
        //       up the license certificate file name for the Harpy and/or
        //       Badge plugins.
        //
        /// <summary>
        /// The setting names searched to locate the plugin certificate file.
        /// </summary>
        private static readonly string[] PluginCertificateEnvVarNames = {
            "Override_Harpy_Certificate", "Override_Badge_Certificate"
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the prefix string used when attempting to locate
        //       a valid looking license certificate for the Harpy and/or
        //       Badge plugins.  The settings "HarpyCertificateM" to
        //       "HarpyCertificateN" will be checked, in order, until one
        //       that looks valid is found, where M in the minimum index
        //       and N is the maximum search index.  This is used as the
        //       "page name" value when attempting to fetch the associated
        //       configuration settings; however, this is not a page name.
        //
        /// <summary>
        /// The setting-name prefix for the plugin certificate.
        /// </summary>
        private static readonly string PluginCertificatePrefix =
            "PluginCertificate";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the prefix string used when attempting to locate
        //       a valid looking license certificate for the Kapok server
        //       itself.  The settings "ServerCertificateM" to
        //       "ServerCertificateN" will be checked, in order, until one
        //       that looks valid is found, where M in the minimum index
        //       and N is the maximum search index.  This is used as the
        //       "page name" value when attempting to fetch the associated
        //       configuration settings; however, this is not a page name.
        //
        /// <summary>
        /// The setting-name prefix for the server certificate.
        /// </summary>
        private static readonly string ServerCertificatePrefix =
            "ServerCertificate";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the "legacy" fallback prefix string that may be used
        //       when attempting to locate a license certificate for the Kapok
        //       server itself.  It should not be used in new applications; it
        //       is being retained for backward compatibility only.  This is
        //       used as the "page name" value when attempting to fetch the
        //       associated configuration settings; however, this is not a
        //       page name.
        //
        /// <summary>
        /// The legacy setting-name prefix for the certificate.
        /// </summary>
        private static readonly string LegacyCertificatePrefix =
            "Certificate";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
#if LICENSING
        //
        // NOTE: This is used to synchronize access to the private static
        //       data in this file.
        //
        /// <summary>
        /// The object used to synchronize access to the certificate state.
        /// </summary>
        private static readonly object certificateSyncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The certificate file name currently in use.
        //
#if MONO_BUILD
#pragma warning disable 414
#endif
        /// <summary>
        /// The certificate file name currently in use.
        /// </summary>
        private static string certificateFileName;
#if MONO_BUILD
#pragma warning restore 414
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The certificate currently in use.
        //
#if MONO_BUILD
#pragma warning disable 414
#endif
        /// <summary>
        /// The certificate currently in use.
        /// </summary>
        private static object certificate;
#if MONO_BUILD
#pragma warning restore 414
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Licensing Support Helper Methods
        /// <summary>
        /// Verifies the server license certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during verification.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when verifying in an isolated context.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode VerifyCertificate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            bool isolated,           /* in */
            ref Result error         /* out */
            )
        {
#if LICENSING
            ReturnCode code;
            string localCertificateFileName = null;
            object localCertificate = null;
            Result localResult = null;

            code = LicenseOps.VerifyCertificate(
                interpreter, WebGlobalState.GetAssembly(),
                WebGlobalState.GetAssemblyName(),
                WebGlobalState.GetPlugin(), null, null, null, null,
                null, null, null, null, null, null, null, false, false,
                false, true, isolated || LicenseOps.UseIsolated(
                    typeof(Enterprise)), null, null,
                new AnyClientData(clientData, false),
                ref localCertificateFileName, ref localCertificate,
                ref localResult);

            if (code == ReturnCode.Ok)
            {
                lock (certificateSyncRoot)
                {
                    certificateFileName = localCertificateFileName;
                    certificate = localCertificate;
                }
            }
            else
            {
                error = localResult;
            }

            return code;
#else
            return ReturnCode.Ok;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied file name could be a certificate
        /// file.
        /// </summary>
        /// <param name="fileName">
        /// The file name to test.
        /// </param>
        /// <returns>
        /// Non-zero when the file could be a certificate; otherwise, zero.
        /// </returns>
        private static bool CouldBeCertificateFile(
            string fileName /* in */
            )
        {
            try
            {
                if (String.IsNullOrEmpty(fileName))
                    return false;

                if (!File.Exists(fileName))
                    return false;

                string text = File.ReadAllText(fileName); /* throw */

#if XML
                if (!Utility.LooksLikeXmlDocument(text))
                    return false;
#endif

                return true;
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(WebLicenseOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Discovers and configures the license certificates for the request.
        /// </summary>
        /// <param name="type">
        /// The provision type being configured.
        /// </param>
        /// <param name="securityFlags">
        /// The security flags governing configuration.
        /// </param>
        /// <param name="pluginCertificateFileName">
        /// On output, receives the resolved plugin certificate file name.
        /// </param>
        /// <param name="serverClientData">
        /// The server caller data.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool ConfigureCertificates(
            Type type,                            /* in: OPTIONAL */
            SecurityFlags securityFlags,          /* in */
            ref string pluginCertificateFileName, /* out */
            ref IClientData serverClientData      /* out */
            )
        {
            int[] counts = { 0, 0 };

            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.FileName;

            bool noSearch = WebSettingsOps.HasFlags(
                dataType, SettingDataType.NoSearch, true);

            int minimumIndex;
            int maximumIndex;

            WebSettingsOps.InitializeIndexes(Index.Invalid,
                noSearch, out minimumIndex, out maximumIndex);

            string serverCertificateFileName = null;

            if (ConfigureCertificate(
                    type, ServerCertificatePrefix, minimumIndex,
                    maximumIndex, dataType, ref serverCertificateFileName,
                    ref serverClientData))
            {
                counts[0]++;

                if (HasFlags(
                        securityFlags, SecurityFlags.TraceCertificates, true))
                {
                    Utility.DebugTrace(String.Format(
                        "Selected server license certificate from file {0}.",
                        Utility.FormatWrapOrNull(serverCertificateFileName)),
                        typeof(WebLicenseOps).Name, TracePriority.Medium |
                            TracePriority.FromPlugin);
                }
            }
            else
            {
                //
                // HACK: *COMPAT* If a specific license certificate for the
                //       server is not found, fallback to using the legacy
                //       license certificate prefix.
                //
                if (ConfigureCertificate(
                        type, LegacyCertificatePrefix, minimumIndex,
                        maximumIndex, dataType, ref serverCertificateFileName,
                        ref serverClientData))
                {
                    counts[0]++;

                    if (HasFlags(
                            securityFlags, SecurityFlags.TraceCertificates, true))
                    {
                        Utility.DebugTrace(String.Format(
                            "Selected legacy license certificate from file {0}.",
                            Utility.FormatWrapOrNull(serverCertificateFileName)),
                            typeof(WebLicenseOps).Name, TracePriority.Medium |
                                TracePriority.FromPlugin);
                    }
                }
                else
                {
                    Utility.DebugTrace(
                        "No server or legacy license certificate found.",
                        typeof(WebLicenseOps).Name, TracePriority.MediumHigh |
                            TracePriority.FromPlugin);
                }
            }

            IClientData pluginClientData = null; /* NOT USED */

            if (ConfigureCertificate(
                    type, PluginCertificatePrefix, minimumIndex,
                    maximumIndex, dataType, ref pluginCertificateFileName,
                    ref pluginClientData))
            {
                string[] envVarNames = PluginCertificateEnvVarNames;

                if ((pluginCertificateFileName != null) &&
                    (envVarNames != null))
                {
                    foreach (string envVarName in envVarNames)
                    {
                        if (String.IsNullOrEmpty(envVarName))
                            continue;

                        if (Utility.SetEnvironmentVariable(
                                envVarName, pluginCertificateFileName))
                        {
                            counts[1]++;
                        }
                    }
                }

                if (HasFlags(
                        securityFlags, SecurityFlags.TraceCertificates, true))
                {
                    Utility.DebugTrace(String.Format(
                        "Selected plugin license certificate from file {0}.",
                        Utility.FormatWrapOrNull(pluginCertificateFileName)),
                        typeof(WebLicenseOps).Name, TracePriority.Medium |
                            TracePriority.FromPlugin);
                }
            }
            else
            {
                Utility.DebugTrace(
                    "No plugin license certificate found.",
                    typeof(WebLicenseOps).Name, TracePriority.MediumHigh |
                        TracePriority.FromPlugin);
            }

            return (counts[0] > 0) && (counts[1] > 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures a single license certificate, resolving its file name
        /// from the page settings.
        /// </summary>
        /// <param name="type">
        /// The provision type being configured.
        /// </param>
        /// <param name="pageName">
        /// The page name used to look up settings.
        /// </param>
        /// <param name="minimumIndex">
        /// The minimum indexed-search index.
        /// </param>
        /// <param name="maximumIndex">
        /// The maximum indexed-search index.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the setting.
        /// </param>
        /// <param name="certificateFileName">
        /// On output, receives the resolved certificate file name.
        /// </param>
        /// <param name="clientData">
        /// The caller data.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool ConfigureCertificate(
            Type type,                      /* in: NOT USED */
            string pageName,                /* in */
            int minimumIndex,               /* in */
            int maximumIndex,               /* in */
            SettingDataType dataType,       /* in */
            ref string certificateFileName, /* out */
            ref IClientData clientData      /* out */
            )
        {
            //
            // HACK: This is needed so that Harpy can locate a valid license
            //       certificate for the Kapok server itself as well as for
            //       the Harpy package used in the evaluated license renewal
            //       (and other) scripts.
            //
            // HACK: The "pageName" used here is not really a page name; it
            //       is used as the page name for consistency with handling
            //       of other configuration settings with one or two parts.
            //
            if (String.IsNullOrEmpty(pageName))
                return false;

            string fileName = WebSettingsOps.GetPage(
                pageName, null, dataType);

            if (CouldBeCertificateFile(fileName))
            {
                certificateFileName = fileName;

                clientData = new ClientData(StringList.MakeList(
                    fileName));

                return true;
            }

            for (int index = minimumIndex; index <= maximumIndex; index++)
            {
                fileName = WebSettingsOps.GetPage(
                    pageName, index.ToString(), dataType);

                if (CouldBeCertificateFile(fileName))
                {
                    certificateFileName = fileName;

                    clientData = new ClientData(
                        StringList.MakeList(fileName));

                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied security flags contain the given
        /// flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        public static bool HasFlags(
            SecurityFlags flags,    /* in */
            SecurityFlags hasFlags, /* in */
            bool all                /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != SecurityFlags.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the Harpy/Badge SDK security for the interpreter using
        /// the resolved certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to configure.
        /// </param>
        /// <param name="certificateFileName">
        /// The certificate file name to use.
        /// </param>
        /// <param name="flags">
        /// The security flags governing configuration.
        /// </param>
        /// <param name="force">
        /// Non-zero to force reconfiguration.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when configuring in an isolated context.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ConfigureSecurity(
            Interpreter interpreter,    /* in */
            string certificateFileName, /* in */
            SecurityFlags flags,        /* in */
            bool force,                 /* in */
            ref bool isolated,          /* out */
            ref Result error            /* out */
            )
        {
#if SECURITY
            if (force || Security.CanLoad())
            {
                ReturnCode code;
                Result result = null;

                //
                // NOTE: The caller really needs to know if the Harpy plugin
                //       was loaded in isolated mode.
                //
                isolated = HasFlags(flags, SecurityFlags.UseIsolation, true);

                //
                // NOTE: Before loading any of the Harpy plugins, enable
                //       the necessary "security" using the Harpy SDK.
                //
                code = Security.Enable(
                    interpreter, isolated, null, certificateFileName,
                    ref result);

                if (code != ReturnCode.Ok)
                {
                    error = result;
                    return code;
                }

                //
                // NOTE: Some plugins cannot work properly with full isolation
                //       enabled (e.g. due to use of private classes, methods,
                //       etc).
                //
                if (HasFlags(flags, SecurityFlags.DisableIsolation, true))
                {
                    code = Security.DisableIsolation(interpreter, ref result);

                    if (code != ReturnCode.Ok)
                    {
                        error = result;
                        return code;
                    }
                }
            }
#endif

            return ReturnCode.Ok;
        }
        #endregion
    }
}
