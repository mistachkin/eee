/*
 * LicenseManager.cs --
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
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using _Utility = Eagle._Components.Public.Utility;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using GlobalState = Licensing.Components.Private.CertificateGlobalState;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;

using CertificateDictionary = System.Collections.Generic.IDictionary<
    string, string>;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Provides the public entry point for the Harpy licensing subsystem,
    /// implementing <see cref="ILicenseManager" /> to create, verify, renew,
    /// and query license certificates on behalf of plugins and interpreters.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("93c50ba3-5aec-4db6-a6e4-5d0960c8d236")]
    public sealed class LicenseManager :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        ILicenseManager
    {
        #region Public Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseManager" />
        /// class, setting up the certificate-related global state used by the
        /// licensing subsystem.
        /// </summary>
        public LicenseManager()
        {
            /* NO RESULT */
            CertificateSharedOps.SetupForCoreLibraryState();

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            KeyFile.InitializeKeyPairTypes(false);

            ///////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_POLICY && PLUGIN_COMMANDS
            //
            // HACK: This class should not "reset" any global
            //       state, only "initialize" it.
            //
            /* NO RESULT */
            // CertificateShellState.ResetFlags();
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            /* NO RESULT */
            CertificatePluginState.InitializeMappings(false);
#endif

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            CertificateTimeState.InitializeDurations(false);

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            CertificateVersionState.InitializeRanges(false);

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            SetupWellKnownConfigurationData();

            ///////////////////////////////////////////////////////////////////

#if NETWORK
#if DEBUG || EXTRA_DIAGNOSTICS
            if (!Configuration.DoesVariableExist(
                    Constants.NoNetworkTimeEnvVarName))
#endif
            {
                /* NO RESULT */
                CertificateNetworkOps.AsynchronousAccessChecks(
                    null, _Utility.GetUtcNow(), true);
            }
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Checks that the licensing prerequisites are met, optionally
        /// loading the manager plugin when one is required, before delegating
        /// to the certificate plugin verification logic.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin being checked, if any; this value may be null.
        /// </param>
        /// <param name="mustHaveManagerPlugin">
        /// Non-zero if a manager plugin must be present, causing one to be
        /// loaded when <paramref name="managerPlugin" /> is null.
        /// </param>
        /// <param name="managerPlugin">
        /// The manager plugin to use; may be populated by this method when it
        /// is initially null and a manager plugin is required.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode Check(
            Interpreter interpreter,    /* in: OPTIONAL */
            IPlugin plugin,             /* in: OPTIONAL */
            bool mustHaveManagerPlugin, /* in */
            ref IPlugin managerPlugin,  /* in, out */
            ref Result result           /* out */
            )
        {
            if (mustHaveManagerPlugin && (managerPlugin == null))
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                if (CertificatePolicyOps.GetOrLoadPlugin(
                        interpreter, ref managerPlugin,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
#else
                result = "no manager plugin available";
                return ReturnCode.Error;
#endif
            }

#if CERTIFICATE_PLUGIN
            return CertificatePluginOps.Check(
                interpreter, plugin, ref result);
#else
            return ReturnCode.Ok;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the well-known configuration data for the current
        /// application domain, or traces that the step was skipped depending
        /// on the active build configuration.
        /// </summary>
        private static void SetupWellKnownConfigurationData()
        {
#if OFFICIAL || DEBUG || FORCE_TRACE
            AppDomain appDomain = AppDomain.CurrentDomain;
#endif

#if OFFICIAL
            /* NO RESULT */
            WellKnownOps.SetupConfigurationData(appDomain);
#elif DEBUG || FORCE_TRACE
            TraceOps.NetworkDebugTrace(String.Format(
                "SetupWellKnownConfigurationData: Skipped setting up " +
                "well-known configuration data for {0}.",
                DataOps.FormatAppDomainId(appDomain, true, true)),
                typeof(LicenseManager).Name, TracePriority.High);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to create a certificate from the specified dictionary of
        /// name/value pairs.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the certificate field values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The newly created certificate, or null if it could not be created.
        /// </returns>
        private static ICertificate TryCreate(
            CertificateDictionary dictionary, /* in */
            ref Result error                  /* out */
            )
        {
#if CERTIFICATE_PLUGIN
            return Certificate.CreateFromDictionary(
                dictionary, ref error);
#else
            error = "not implemented";
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the license manager associated with the specified plugin
        /// data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose associated license manager is sought.
        /// </param>
        /// <returns>
        /// The license manager that was found, or null if none was found.
        /// </returns>
        private static ILicenseManager FindLicenseManager(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in */
            )
        {
#if CERTIFICATE_PLUGIN
            return CertificatePluginOps.FindLicenseManager(
                interpreter, pluginData, false);
#else
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether fail-safe mode should be skipped, based on
        /// whether a plugin is already associated with the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to examine.
        /// </param>
        /// <returns>
        /// Non-zero if fail-safe mode should be skipped; otherwise, zero.
        /// </returns>
        private static bool ShouldSkipFailSafe(
            Interpreter interpreter /* in */
            )
        {
            return CertificateSharedOps.GetPlugin(interpreter) != null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate associated with the specified license
        /// certificate data, importing it from its backing file when
        /// necessary.
        /// </summary>
        /// <param name="licenseCertificateData">
        /// The license certificate data describing the certificate to obtain.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to allow any resource public key when importing the
        /// certificate.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate when importing it.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate that was obtained, or null if it was unavailable.
        /// </returns>
        private static ICertificate GetCertificate(
            ILicenseCertificateData licenseCertificateData, /* in */
            bool anyResourcePublicKey,                      /* in */
            bool validate,                                  /* in */
            ref Result result                               /* out */
            )
        {
            if (licenseCertificateData == null)
                return null;

            ICertificate certificate = licenseCertificateData.Certificate;

            if (certificate != null)
                return certificate;

            string fileName = licenseCertificateData.CertificateFileName;

            if (String.IsNullOrEmpty(fileName))
                return null;

            Result localResult = null;

#if XML && SERIALIZATION
            if (CertificateXmlOps.Import(
                    fileName, anyResourcePublicKey, false, validate,
                    ref certificate, ref localResult) == ReturnCode.Ok)
            {
                return certificate;
            }
#else
            localResult = "not implemented";
#endif

            result = localResult;
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified certificate into a human-readable string
        /// containing its identifier and entity name.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to format; this value may be null.
        /// </param>
        /// <returns>
        /// A string describing the certificate.
        /// </returns>
        private static string FormatCertificate(
            ICertificate certificate /* in */
            )
        {
            if (certificate == null)
                return _Utility.FormatWrapOrNull(null);

            return String.Format("{0} - {1}",
                DataOps.FormatId(certificate.Id),
                _Utility.FormatWrapOrNull(certificate.EntityName));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the support URI declared by the specified assembly,
        /// optionally falling back to the assembly's general URI.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose support URI is sought.
        /// </param>
        /// <param name="fallback">
        /// Non-zero to fall back to the assembly's general URI when no
        /// support URI is present.
        /// </param>
        /// <returns>
        /// The support URI, or null if none was found.
        /// </returns>
        private static Uri GetSupport(
            Assembly assembly, /* in */
            bool fallback      /* in */
            )
        {
            Uri uri = _Utility.GetAssemblyUri(
                assembly, Constants.SupportUriName);

            if (uri != null)
                return uri;

            if (!fallback)
                return null;

            return _Utility.GetAssemblyUri(assembly);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseManagerData Members
        /// <summary>
        /// The callback used to select a certificate file name from a set of
        /// candidate file names.
        /// </summary>
        private ElementSelectionCallback fileNameCallback;
        /// <summary>
        /// Gets or sets the callback used to select a certificate file name
        /// from a set of candidate file names.
        /// </summary>
        public ElementSelectionCallback FileNameCallback
        {
            get { return fileNameCallback; }
            set { fileNameCallback = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to renew a certificate.
        /// </summary>
        private RenewCallback renewCallback;
        /// <summary>
        /// Gets or sets the callback used to renew a certificate.
        /// </summary>
        public RenewCallback RenewCallback
        {
            get { return renewCallback; }
            set { renewCallback = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseManager Members
        /// <summary>
        /// Gets the directory used to store certificates for the specified
        /// plugin, optionally creating it when it does not exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context; this value is not used.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose certificate directory is sought.
        /// </param>
        /// <param name="anyClientData">
        /// The extra client data; this value is not used.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the directory when it does not already exist.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate directory, or null if it could not be determined.
        /// </returns>
        public string GetCertificateDirectory(
            Interpreter interpreter,      /* in: NOT USED */
            IPluginData pluginData,       /* in */
            IAnyClientData anyClientData, /* in: NOT USED */
            bool create,                  /* in */
            ref Result error              /* out */
            )
        {
            string directory;
            ResultList errors = null;

            directory = CertificateSharedOps.GetDirectory(
                pluginData, create, ref errors);

            if (directory == null)
                error = errors;

            return directory;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects a certificate file name from the specified set of
        /// candidate file names using the default selection logic.
        /// </summary>
        /// <param name="fileNames">
        /// The candidate certificate file names to choose from.
        /// </param>
        /// <param name="anyClientData">
        /// The extra client data, if any; this value may be null.
        /// </param>
        /// <returns>
        /// The selected file name, or null if none was selected.
        /// </returns>
        public string SelectCertificateFileName(
            IEnumerable<string> fileNames, /* in */
            IAnyClientData anyClientData   /* in, OPTIONAL: May be null. */
            )
        {
#if CERTIFICATE_PLUGIN
            return CertificatePluginOps.DefaultFileNameCallback(
                fileNames, anyClientData);
#else
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Displays summary information about the certificate represented by
        /// the specified dictionary of name/value pairs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary containing the certificate field values.
        /// </param>
        /// <param name="result">
        /// Receives the resulting information on success or an error message
        /// on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode AboutCertificate(
            Interpreter interpreter,          /* in: OPTIONAL: May be null. */
            IPlugin plugin,                   /* in, OPTIONAL: May be null. */
            CertificateDictionary dictionary, /* in */
            ref Result result                 /* in, out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        ReturnCode code = Check(
                            interpreter, plugin, false, ref plugin,
                            ref result);

                        if (code == ReturnCode.Ok)
                        {
#if CERTIFICATE_PLUGIN
                            ICertificate certificate = TryCreate(
                                dictionary, ref result);

                            if (certificate != null)
                            {
                                code = CertificatePluginOps.About(
                                    interpreter, plugin, certificate,
                                    ref result);
                            }
                            else
                            {
                                code = ReturnCode.Error;
                            }
#else
                            result = "not implemented";
                            code = ReturnCode.Error;
#endif
                        }

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Displays summary information about the specified certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to describe.
        /// </param>
        /// <param name="result">
        /// Receives the resulting information on success or an error message
        /// on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode AboutCertificate(
            Interpreter interpreter,  /* in: OPTIONAL: May be null. */
            IPlugin plugin,           /* in, OPTIONAL: May be null. */
            ICertificate certificate, /* in */
            ref Result result         /* in, out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        ReturnCode code = Check(
                            interpreter, plugin, false, ref plugin,
                            ref result);

                        if (code == ReturnCode.Ok)
                        {
#if CERTIFICATE_PLUGIN
                            code = CertificatePluginOps.About(
                                interpreter, plugin, certificate,
                                ref result);
#else
                            result = "not implemented";
                            code = ReturnCode.Error;
#endif
                        }

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate that has the specified unique identifier.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="id">
        /// The unique identifier of the certificate to obtain.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the certificate that was found.
        /// </param>
        /// <param name="result">
        /// Receives status information on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode GetCertificate(
            Interpreter interpreter,      /* in: OPTIONAL: May be null. */
            IPlugin plugin,               /* in: OPTIONAL: May be null. */
            Guid id,                      /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        ReturnCode code = Check(
                            interpreter, plugin, false, ref plugin,
                            ref result);

                        if (code == ReturnCode.Ok)
                        {
                            if (Helpers.GetLicenseCertificate(
                                    id, ref certificate, ref result))
                            {
                                result = OperationStatus.FoundOk;
                                code = ReturnCode.Ok;
                            }
                            else
                            {
                                code = ReturnCode.Error;
                            }
                        }

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /* Licensing.Components.Public.Delegates.RenewCallback */
        /// <summary>
        /// Renews a license certificate, producing an updated certificate
        /// and, when applicable, writing it to a file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the certificate, if any; this value
        /// may be null.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly associated with the certificate.
        /// </param>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any; this value may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any; this value may be null.
        /// </param>
        /// <param name="hashValue">
        /// The hash value to use, if any; this value may be null.
        /// </param>
        /// <param name="encoding">
        /// The text encoding to use.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to use for the renewal operation.
        /// </param>
        /// <param name="anyClientData">
        /// The extra client data to use.
        /// </param>
        /// <param name="features">
        /// The features to request for the renewed certificate.
        /// </param>
        /// <param name="restrictions">
        /// The restrictions to apply to the renewed certificate.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use, if any; this value may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type to use, if any; this value may be null.
        /// </param>
        /// <param name="keyName">
        /// The key name to use, if any; this value may be null.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name to use, if any; this value may be null.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use; this value may be
        /// null.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the renewed certificate.
        /// </param>
        /// <param name="fileName">
        /// The certificate file name; may be updated to reflect the renewed
        /// certificate.
        /// </param>
        /// <param name="certificate">
        /// The certificate to renew; receives the renewed certificate.
        /// </param>
        /// <param name="result">
        /// Receives status information on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode RenewCertificate(
            Interpreter interpreter,      /* in: OPTIONAL: May be null. */
            Assembly assembly,            /* in, OPTIONAL: May be null. */
            AssemblyName assemblyName,    /* in */
            IPlugin plugin,               /* in, OPTIONAL: May be null. */
            string hashAlgorithmName,     /* in, OPTIONAL: May be null. */
            byte[] hashKey,               /* in, OPTIONAL: May be null. */
            byte[] hashValue,             /* in, OPTIONAL: May be null. */
            Encoding encoding,            /* in */
            object keyPairs,              /* in */
            IAnyClientData anyClientData, /* in */
            string features,              /* in */
            string restrictions,          /* in */
            ExecutionPolicy? policy,      /* in: OPTIONAL: May be null. */
            PolicyType? policyType,       /* in: OPTIONAL: May be null. */
            string keyName,               /* in: OPTIONAL: May be null. */
            string keyRingName,           /* in: OPTIONAL: May be null. */
            int? timeout,                 /* in: OPTIONAL: May be null. */
            bool embedded,                /* in */
            bool validate,                /* in */
            ref string fileName,          /* in, out */
            ref ICertificate certificate, /* in, out */
            ref Result result             /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        ReturnCode code = Check(
                            interpreter, plugin, false, ref plugin,
                            ref result);

#if NETWORK && CERTIFICATE_RENEWAL
                        if (code == ReturnCode.Ok)
                        {
#if CERTIFICATE_PLUGIN
                            //
                            // HACK: Always enable the "strict" license manager
                            //       parameter handling here.  It should only be
                            //       disabled when the license manager is being
                            //       called late-bound via the SDK.
                            //
                            CertificateIsolatedOps.MaybeFixupParameters(
                                true, ref assembly, ref plugin, ref encoding);
#endif

                            code = CertificateRenewalOps.Process(
                                interpreter, assembly, assemblyName, plugin,
                                hashAlgorithmName, hashKey, hashValue, encoding,
                                keyPairs as IEnumerable<IKeyPair>, anyClientData,
                                features, restrictions, policy, policyType,
                                keyName, keyRingName, timeout, embedded, validate,
                                ref fileName, ref certificate, ref result);
                        }
#endif

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the license certificate associated with the specified
        /// assembly and plugin, optionally renewing it when necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly whose certificate is to be verified.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly whose certificate is to be verified.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with the certificate.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any; this value may be
        /// null.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any; this value may be null.
        /// </param>
        /// <param name="encoding">
        /// The text encoding to use, if any; this value may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to use, if any; this value may be null.
        /// </param>
        /// <param name="features">
        /// The features to require, if any; this value may be null.
        /// </param>
        /// <param name="restrictions">
        /// The restrictions to enforce, if any; this value may be null.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use, if any; this value may be null.
        /// </param>
        /// <param name="keyName">
        /// The key name to use, if any; this value may be null.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name to use, if any; this value may be null.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use; this value may be
        /// null.
        /// </param>
        /// <param name="force">
        /// Non-zero to force verification.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate.
        /// </param>
        /// <param name="fileNameCallback">
        /// The callback used to select a certificate file name, if any; this
        /// value may be null.
        /// </param>
        /// <param name="renewCallback">
        /// The callback used to renew the certificate, if any; this value may
        /// be null.
        /// </param>
        /// <param name="anyClientData">
        /// The extra client data, if any; this value may be null.
        /// </param>
        /// <param name="fileName">
        /// The certificate file name; may be updated during verification.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the verified certificate.
        /// </param>
        /// <param name="result">
        /// Receives status information on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode VerifyCertificate(
            Interpreter interpreter,                   /* in, OPTIONAL: May be null. */
            Assembly assembly,                         /* in */
            AssemblyName assemblyName,                 /* in */
            IPlugin plugin,                            /* in */
            string hashAlgorithmName,                  /* in, OPTIONAL: May be null. */
            byte[] hashKey,                            /* in, OPTIONAL: May be null. */
            Encoding encoding,                         /* in, OPTIONAL: May be null. */
            object keyPairs,                           /* in, OPTIONAL: May be null. */
            string features,                           /* in, OPTIONAL: May be null. */
            string restrictions,                       /* in, OPTIONAL: May be null. */
            ExecutionPolicy? policy,                   /* in, OPTIONAL: May be null. */
            string keyName,                            /* in, OPTIONAL: May be null. */
            string keyRingName,                        /* in: OPTIONAL: May be null. */
            int? timeout,                              /* in, OPTIONAL: May be null. */
            bool force,                                /* in */
            bool embedded,                             /* in */
            bool validate,                             /* in */
            ElementSelectionCallback fileNameCallback, /* in, OPTIONAL: May be null. */
            RenewCallback renewCallback,               /* in, OPTIONAL: May be null. */
            IAnyClientData anyClientData,              /* in, OPTIONAL: May be null. */
            ref string fileName,                       /* in, out */
            ref ICertificate certificate,              /* out */
            ref Result result                          /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        ReturnCode code = Check(
                            interpreter, plugin, false, ref plugin,
                            ref result);

                        if (code == ReturnCode.Ok)
                        {
#if CERTIFICATE_PLUGIN && NETWORK && CERTIFICATE_RENEWAL
                            //
                            // HACK: Always enable the "strict" license manager
                            //       parameter handling here.  It should only be
                            //       disabled when the license manager is being
                            //       called late-bound via the SDK.
                            //
                            CertificateIsolatedOps.MaybeFixupParameters(
                                true, ref assembly, ref plugin, ref encoding,
                                ref renewCallback);
#endif

                            code = CertificateVerifyOps.LoadAndProcess(
                                interpreter, assembly, assemblyName, plugin,
                                hashAlgorithmName, hashKey, encoding,
                                keyPairs as IEnumerable<IKeyPair>, features,
                                restrictions, policy, keyName, keyRingName,
                                timeout, force, embedded, validate,
                                fileNameCallback, renewCallback, anyClientData,
                                ref fileName, ref certificate, ref result);
                        }

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Matches the flags of the certificate represented by the specified
        /// dictionary against the requested set of flags.
        /// </summary>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary containing the certificate field values.
        /// </param>
        /// <param name="type">
        /// The flag type, as a <see cref="FlagType" /> value, to match.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use when matching.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must be absent.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the
        /// <paramref name="hasFlags" /> flags are present.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require that all of the
        /// <paramref name="notHasFlags" /> flags are absent.
        /// </param>
        /// <param name="strict">
        /// Non-zero to perform strict matching.
        /// </param>
        /// <param name="result">
        /// Receives status information on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode MatchCertificateFlags(
            IPlugin plugin,                   /* in, OPTIONAL: May be null. */
            CertificateDictionary dictionary, /* in */
            int /* FlagType */ type,          /* in */
            long key,                         /* in */
            string hasFlags,                  /* in */
            string notHasFlags,               /* in */
            bool hasAll,                      /* in */
            bool notHasAll,                   /* in */
            bool strict,                      /* in */
            ref Result result                 /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(null) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(null);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    ReturnCode code = Check(
                        null, plugin, false, ref plugin, ref result);

                    if (code == ReturnCode.Ok)
                    {
                        ICertificate certificate = TryCreate(
                            dictionary, ref result);

                        if (certificate != null)
                        {
                            code = CertificateSharedOps.MatchFlags(
                                certificate, (FlagType)type, key,
                                hasFlags, notHasFlags, hasAll,
                                notHasAll, strict, ref result);
                        }
                        else
                        {
                            code = ReturnCode.Error;
                        }
                    }

                    return code;
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Matches the flags of the specified certificate against the
        /// requested set of flags.
        /// </summary>
        /// <param name="plugin">
        /// The plugin context, if any; this value may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose flags are to be matched.
        /// </param>
        /// <param name="type">
        /// The flag type, as a <see cref="FlagType" /> value, to match.
        /// </param>
        /// <param name="key">
        /// The attribute flags key to use when matching.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must be absent.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the
        /// <paramref name="hasFlags" /> flags are present.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require that all of the
        /// <paramref name="notHasFlags" /> flags are absent.
        /// </param>
        /// <param name="strict">
        /// Non-zero to perform strict matching.
        /// </param>
        /// <param name="result">
        /// Receives status information on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode MatchCertificateFlags(
            IPlugin plugin,           /* in, OPTIONAL: May be null. */
            ICertificate certificate, /* in */
            int /* FlagType */ type,  /* in */
            long key,                 /* in */
            string hasFlags,          /* in */
            string notHasFlags,       /* in */
            bool hasAll,              /* in */
            bool notHasAll,           /* in */
            bool strict,              /* in */
            ref Result result         /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(null) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(null);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    ReturnCode code = Check(
                        null, plugin, false, ref plugin, ref result);

                    if (code == ReturnCode.Ok)
                    {
                        code = CertificateSharedOps.MatchFlags(
                            certificate, (FlagType)type, key,
                            hasFlags, notHasFlags, hasAll,
                            notHasAll, strict, ref result);
                    }

                    return code;
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the signed script file associated with the specified
        /// plugin in the context of the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which to evaluate the script.
        /// </param>
        /// <param name="plugin">
        /// The plugin whose script file is to be evaluated.
        /// </param>
        /// <param name="variantName">
        /// The name of the script variant to evaluate.
        /// </param>
        /// <param name="anyClientData">
        /// The extra client data to make available during evaluation.
        /// </param>
        /// <param name="result">
        /// Receives the evaluation result on success or an error message on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode EvaluateFile(
            Interpreter interpreter,      /* in */
            IPlugin plugin,               /* in */
            string variantName,           /* in */
            IAnyClientData anyClientData, /* in */
            ref Result result             /* out */
            )
        {
            bool skipFailSafe;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            skipFailSafe = ShouldSkipFailSafe(interpreter) ||
                GlobalState.HaveExtraFeatures(
                    Features.SkipFailSafeModeOrAll, false);
#else
            skipFailSafe = ShouldSkipFailSafe(interpreter);
#endif

            if (!skipFailSafe)
                CertificateFailSafeMode.Enable();

            try
            {
                CertificateSdkMode.Enable();

                try
                {
                    _Utility.PushActiveInterpreter(interpreter);

                    try
                    {
                        //
                        // BUGFIX: The EvaluateFile method basically always
                        //         requires a non-null plugin context due to
                        //         how heavily the signed script evaluation
                        //         subsystem relies upon it for its various
                        //         built-in commands.
                        //
                        IPlugin managerPlugin = null;

                        if (CertificateAssemblyOps.MatchObjectOrName(plugin))
                            managerPlugin = plugin;

                        ReturnCode code = Check(
                            interpreter, plugin, true, ref managerPlugin,
                            ref result);

                        if (code == ReturnCode.Ok)
                        {
                            using (EvaluateClientData evaluateClientData =
                                    EvaluateClientData.CreateFrom(
                                        interpreter, managerPlugin,
                                        variantName, anyClientData,
                                        ref result))
                            {
                                if (evaluateClientData != null)
                                {
                                    /* IGNORED */
                                    evaluateClientData.MaybeSetConfigurationPhase(
                                        ConfigurationPhase.Demand |
                                        ConfigurationPhase.Manager);

                                    /* IGNORED */
                                    evaluateClientData.AttachTo(anyClientData);

#if TEST
                                    IClientData savedClientData = null;

                                    CertificateScriptOps.BeginClientData(
                                        interpreter, evaluateClientData,
                                        ref savedClientData);

                                    try
                                    {
#endif
                                        if (evaluateClientData.Stream != null)
                                        {
                                            code = CertificateScriptOps.EvaluateStream(
                                                evaluateClientData, ref result);
                                        }
                                        else
                                        {
                                            code = CertificateScriptOps.EvaluateFile(
                                                evaluateClientData, ref result);
                                        }
#if TEST
                                    }
                                    finally
                                    {
                                        CertificateScriptOps.EndClientData(
                                            interpreter, ref savedClientData);
                                    }
#endif
                                }
                                else
                                {
                                    code = ReturnCode.Error;
                                }
                            }
                        }

                        return code;
                    }
                    finally
                    {
                        _Utility.PopActiveInterpreter();
                    }
                }
                finally
                {
                    CertificateSdkMode.Disable();
                }
            }
            finally
            {
                if (!skipFailSafe)
                    CertificateFailSafeMode.Disable();
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Gets the license manager associated with the specified plugin
        /// data, optionally creating a new one when none is found.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, if any; this value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose associated license manager is sought.
        /// </param>
        /// <param name="create">
        /// Non-zero to create a new license manager when an existing one
        /// cannot be found.
        /// </param>
        /// <returns>
        /// The license manager, or null if none was found and one was not
        /// created.
        /// </returns>
        public static ILicenseManager GetLicenseManager(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in */
            bool create              /* in */
            )
        {
            ILicenseManager licenseManager = FindLicenseManager(
                interpreter, pluginData);

            if ((licenseManager == null) && create)
                licenseManager = new LicenseManager();

            return licenseManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the support URI for the certificate associated with the
        /// specified plugin data, optionally falling back to the assembly
        /// URI.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose support URI is sought.
        /// </param>
        /// <param name="fallback">
        /// Non-zero to fall back to the assembly URI when no support URI is
        /// available from the certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The support URI, or null if none could be determined.
        /// </returns>
        public static Uri GetSupport(
            IPluginData pluginData, /* in */
            bool fallback,          /* in */
            ref Result error        /* out */
            )
        {
            ILicenseCertificateData licenseCertificateData =
                CertificateSharedOps.GetLicenseCertificateData(
                    pluginData);

            if (licenseCertificateData != null)
            {
                Result result = null;

                ICertificate certificate = GetCertificate(
                    licenseCertificateData,
                    Constants.CertificateAnyResourcePublicKey,
                    Constants.CertificateValidate, ref result);

                if (certificate != null)
                {
                    if (CertificateSharedOps.MatchFlags(
                            certificate, FlagType.Feature,
                            _Utility.DefaultAttributeFlagsKey(),
                            Features.SupportOrAll, null,
                            false, false, true) == ReturnCode.Ok)
                    {
                        Uri uri = certificate.Support;

                        if (uri != null)
                        {
                            return uri;
                        }
                        else
                        {
                            error = String.Format(
                                "no support information found in certificate: {0}",
                                FormatCertificate(certificate));
                        }
                    }
                    else
                    {
                        error = String.Format(
                            "support is not enabled for certificate: {0}",
                            FormatCertificate(certificate));
                    }
                }
                else
                {
                    error = result;
                }
            }
            else
            {
                error = "license plugin data not available";
            }

            return GetSupport(CertificateAssemblyOps.GetObject(), fallback);
        }
        #endregion
    }
}
