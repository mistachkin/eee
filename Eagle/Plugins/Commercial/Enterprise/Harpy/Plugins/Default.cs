/*
 * Default.cs --
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
using System.Globalization;
using System.IO;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using _Plugins = Eagle._Plugins;
using Utility = Eagle._Components.Public.Utility;
using _Restrictions = Licensing.Components.Private.Restrictions;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;

#if !CONSOLE
using ConsoleColor = Eagle._Components.Public.ConsoleColor;
#endif

namespace Licensing.Plugins
{
    /// <summary>
    /// Provides the default Harpy licensing plugin implementation.  This
    /// plugin verifies the license certificate associated with its assembly,
    /// loads its configuration data, and exposes the configuration,
    /// licensing, and certificate information used by the rest of the
    /// licensing system.
    /// </summary>
    [ObjectId("1ec90b73-6b3a-44d8-b864-53d0159fcc23")]
    internal class Default : _Plugins.Default, IConfiguration
#if LICENSING
        , ILicensePluginData, ILicensePluginManagerData
#endif
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the per-instance
        /// configuration file name lists.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the default Harpy licensing plugin
        /// using the specified plugin initialization data.  This sets up the
        /// plugin flags, optional policy tracing, and the license, storage,
        /// and registry managers found within the initialization data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin initialization data used to construct this plugin and
        /// to locate the license, storage, and registry managers, if any.
        /// </param>
        public Default(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

            ///////////////////////////////////////////////////////////////////

            #region Plugin (Full) Policy Tracing Setup
#if CERTIFICATE_POLICY
            if (Configuration.DoesVariableExist(
                    Constants.FullPluginPolicyTracingEnvVarName))
            {
                CertificatePolicyOps.EnableFullPluginPolicyTracing(true);
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Plugin License Manager Setup
            //
            // NOTE: Check if the plugin initialization data contains a
            //       license manager, license data manager, or license
            //       registry manager to use.
            //
#if LICENSING
            this.licenseManager =
                CertificatePluginOps.FindLicenseManager(null, this, true);

            this.storageManager =
                CertificateSharedOps.FindStorageManager(null, this, true);

#if !NET_STANDARD_20
            this.registryManager =
                CertificateSharedOps.FindRegistryManager(null, this, true);
#endif
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

#if LICENSING
            this.certificateFileName = null;
            this.certificate = null;

            ///////////////////////////////////////////////////////////////////

            this.agreements = null;
            this.features = null;
            this.restrictions = null;
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
#if CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether this plugin is the "Security" core plugin (as
        /// opposed to one of the "Licensing" plugins).
        /// </summary>
        /// <returns>
        /// Non-zero if this plugin is the "Security" core plugin; otherwise,
        /// zero.
        /// </returns>
        protected virtual bool IsSecurityCore()
        {
            return CertificatePluginOps.IsSecurityCore(this);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the timeout, in milliseconds, to use for licensing operations
        /// performed on behalf of the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the timeout is being queried.
        /// </param>
        /// <returns>
        /// The timeout, in milliseconds, or null if no specific timeout is
        /// configured.
        /// </returns>
        protected internal virtual int? GetTimeout(
            Interpreter interpreter /* in */
            )
        {
            return CertificateSharedOps.GetTimeout(interpreter, null);
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Gets the assembly associated with this plugin to be used when
        /// verifying its license certificate.  The assembly is not provided
        /// when the specified license manager is a transparent proxy, since
        /// it cannot be passed across an application domain boundary.
        /// </summary>
        /// <param name="licenseManager">
        /// The license manager that will use the returned assembly.
        /// </param>
        /// <returns>
        /// The assembly associated with this plugin, or null if it cannot be
        /// provided to the specified license manager.
        /// </returns>
        protected virtual Assembly GetAssembly(
            ILicenseManager licenseManager /* in */
            )
        {
            Assembly assembly = null;

            if (!Utility.IsTransparentProxy(licenseManager))
                assembly = this.Assembly;

            return assembly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the assembly name associated with this plugin.  If an
        /// explicit assembly name is available it is returned; otherwise, the
        /// name is derived from the plugin assembly, if any.
        /// </summary>
        /// <returns>
        /// The assembly name associated with this plugin, or null if it
        /// cannot be determined.
        /// </returns>
        protected virtual AssemblyName GetAssemblyName()
        {
            AssemblyName assemblyName = this.AssemblyName;

            if (assemblyName != null)
                return assemblyName;

            Assembly assembly = this.Assembly;

            if (assembly == null)
                return null;

            return assembly.GetName();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the callback used to select the certificate file name to use.
        /// If a license manager instance is available, its configured
        /// callback is used; otherwise, the system default is used.
        /// </summary>
        /// <returns>
        /// The certificate file name selection callback, or null if none is
        /// configured.
        /// </returns>
        protected virtual ElementSelectionCallback GetFileNameCallback()
        {
            //
            // NOTE: Figure out the certificate file name callback to use.
            //       If there is a license manager instance available, use
            //       the one configured for it, if any.  Otherwise, fallback
            //       to using the system default.
            //
            return CertificatePluginOps.GetFileNameCallback(this, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the callback used to renew the license certificate.  If a
        /// license manager instance is available, its configured callback is
        /// used; otherwise, the system default is used.
        /// </summary>
        /// <returns>
        /// The certificate renewal callback, or null if none is configured or
        /// certificate renewal is not enabled.
        /// </returns>
        protected virtual RenewCallback GetRenewCallback()
        {
            //
            // NOTE: Figure out the certificate renewal callback to use.
            //       If there is a license manager instance available,
            //       use the one configured for it, if any.  Otherwise,
            //       fallback to using the system default.
            //
#if NETWORK && CERTIFICATE_RENEWAL
            return CertificateRenewalOps.GetRenewCallback(this, false);
#else
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the key used when verifying the license
        /// certificate for this plugin.
        /// </summary>
        /// <returns>
        /// The key name to use, or null if no specific key name applies.
        /// </returns>
        protected internal virtual string GetKeyName()
        {
#if CERTIFICATE_POLICY
            return CertificatePolicyOps.GetKeyName(PolicyType.License);
#else
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the key ring used when verifying the license
        /// certificate for this plugin.
        /// </summary>
        /// <returns>
        /// The key ring name to use, or null if no specific key ring applies.
        /// </returns>
        protected internal virtual string GetKeyRingName()
        {
            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the policy type associated with this plugin, used when
        /// loading its configuration data.
        /// </summary>
        /// <returns>
        /// The policy type to use, or null if no specific policy type
        /// applies.
        /// </returns>
        protected virtual PolicyType? GetPolicyType()
        {
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default execution policy used when verifying the license
        /// certificate for this plugin.  When certificate policy support is
        /// enabled, this combines the current license policy with the flag
        /// that prefers an embedded certificate.
        /// </summary>
        /// <returns>
        /// The default execution policy to use.
        /// </returns>
        protected virtual ExecutionPolicy GetDefaultExecutionPolicy()
        {
#if CERTIFICATE_POLICY
            //
            // TODO: Figure out when this flag should be used here.
            //
            // return ExecutionPolicy.NoLoadKeyRings;
            //
            // NOTE: This flag is used here to make sure that an embedded
            //       certificate in the Harpy assembly itself is always
            //       honored.
            //
            return Licensing.Policies.License.CurrentPolicy |
                ExecutionPolicy.PreferEmbedded;
#else
            return ExecutionPolicy.None;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the execution policy to use when loading configuration data,
        /// honoring any changes made to the default execution policy.
        /// </summary>
        /// <returns>
        /// The execution policy to use, or null to indicate that the default
        /// policy should be used because it has not been changed.
        /// </returns>
        protected virtual ExecutionPolicy? GetExecutionPolicy()
        {
            //
            // HACK: This asks the question "Has this been changed?"
            //       for the plugin execution policy.  If it has not
            //       been changed, just return null here, which does
            //       mean "default" in this context.  If it has been
            //       changed, honor the changes.
            //
            ExecutionPolicy policy = GetDefaultExecutionPolicy();

            if (policy == Constants.DefaultLicenseExecutionPolicy)
                return null;

            return policy;
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Resets the cached license certificate state, clearing both the
        /// certificate file name and the certificate instance.
        /// </summary>
        protected virtual void ResetCertificate()
        {
            /* NO RESULT */
            CertificateLicenseState.SetFileName(null);

            /* NO RESULT */
            CertificateLicenseState.SetCertificate(null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the license certificate required for this plugin to load
        /// and initialize properly.  By default, this looks for a
        /// "certificate.xml" file in the same directory as the plugin
        /// assembly, delegating to the configured license manager when one is
        /// available, or loading and processing the certificate directly
        /// otherwise.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the certificate is being verified.
        /// This value is optional and may be null.
        /// </param>
        /// <param name="anyClientData">
        /// The client data to pass through to the certificate verification
        /// operation.
        /// </param>
        /// <param name="fileName">
        /// On input, the candidate certificate file name, if any; on output,
        /// the certificate file name that was actually used.
        /// </param>
        /// <param name="certificate">
        /// Upon successful return, receives the verified license certificate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation or any error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        protected virtual ReturnCode VerifyCertificate(
            Interpreter interpreter,      /* in: OPTIONAL */
            IAnyClientData anyClientData, /* in */
            ref string fileName,          /* in, out */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            //
            // NOTE: This plugin requires a license certificate to load
            //       and initialize properly.  By default, it will look
            //       for a "certificate.xml" file in the same directory
            //       as the plugin assembly.
            //
            // BUGFIX: The assembly cannot be passed into an instance
            //         of the ILicenseManager if that instance is in
            //         another AppDomain.
            //
            ILicenseManager licenseManager = this.LicenseManager;

            return (licenseManager != null) ?
                licenseManager.VerifyCertificate(
                    interpreter, GetAssembly(licenseManager),
                    GetAssemblyName(), this, null, null,
                    DataOps.GetDefaultEncoding(), null,
                    this.Features, this.Restrictions,
                    GetDefaultExecutionPolicy(),
                    GetKeyName(), GetKeyRingName(),
                    GetTimeout(interpreter),
                    Constants.CertificateForce,
                    Constants.CertificateEmbedded,
                    Constants.CertificateValidate,
                    GetFileNameCallback(), GetRenewCallback(),
                    anyClientData, ref fileName,
                    ref certificate, ref result) :
                CertificateVerifyOps.LoadAndProcess(
                    interpreter, this.Assembly,
                    GetAssemblyName(), this, null, null,
                    DataOps.GetDefaultEncoding(), null,
                    this.Features, this.Restrictions,
                    GetDefaultExecutionPolicy(),
                    GetKeyName(), GetKeyRingName(),
                    GetTimeout(interpreter),
                    Constants.CertificateForce,
                    Constants.CertificateEmbedded,
                    Constants.CertificateValidate,
                    GetFileNameCallback(), GetRenewCallback(),
                    anyClientData, ref fileName,
                    ref certificate, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the certificate file name and certificate instance for this
        /// plugin and updates its "licensed" flag accordingly.
        /// </summary>
        /// <param name="fileName">
        /// The certificate file name to store.
        /// </param>
        /// <param name="certificate">
        /// The certificate instance to store.
        /// </param>
        /// <param name="licensed">
        /// Non-zero to mark the plugin as licensed; zero to clear the
        /// licensed flag.
        /// </param>
        protected virtual void SetFlagAndData(
            string fileName,          /* in */
            ICertificate certificate, /* in */
            bool licensed             /* in */
            )
        {
            this.certificateFileName = fileName;
            this.certificate = certificate;

            if (licensed)
                this.Flags |= PluginFlags.Licensed;
            else
                this.Flags &= ~PluginFlags.Licensed;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified agreement URIs to the set of agreements tracked
        /// by this plugin, associating each with the given value.
        /// </summary>
        /// <param name="collection">
        /// The collection of agreement URIs to add.  If null, no agreements
        /// are added.
        /// </param>
        /// <param name="value">
        /// The value to associate with each agreement URI.
        /// </param>
        protected virtual void AddAgreements(
            IEnumerable<Uri> collection, /* in */
            bool value                   /* in */
            )
        {
            if (agreements == null)
                agreements = new UriDictionary<bool>();

            if (collection == null)
                return;

            foreach (Uri item in collection)
                agreements[item] = value;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if PLUGIN_COMMANDS
        /// <summary>
        /// Ensures that the "secret" command provided by this plugin, if
        /// present in the specified interpreter, has an associated client
        /// data instance, creating one if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which to look up the "secret" command.
        /// </param>
        private void MaybeSetupClientDataForSecrets(
            Interpreter interpreter /* in */
            )
        {
            long token = 0; /* NOT USED */
            ICommand command = null;
            Result error = null; /* NOT USED */

            if (interpreter.GetCommandForPlugin(this,
                    typeof(Commands.Secret).Name.ToLowerInvariant(),
                    LookupFlags.Default, ref token, ref command,
                    ref error) == ReturnCode.Ok)
            {
                if ((command != null) &&
                    (command.ClientData == null))
                {
                    command.ClientData = new ClientData(null);
                }
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Internal Methods
        /// <summary>
        /// Cleans up the sandbox state for all interpreters associated with
        /// this plugin.
        /// </summary>
        /// <returns>
        /// The number of interpreters that were cleaned up.
        /// </returns>
        internal int Cleanup()
        {
            return CertificateSandboxState.CleanupInterpreters(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IConfiguration Members
        /// <summary>
        /// Gets the collection of sandbox tokens currently registered with
        /// the certificate sandbox state.
        /// </summary>
        public virtual IEnumerable<ulong> SandboxTokens
        {
            get { return CertificateSandboxState.CopyTokenKeys(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the primary sandbox token from the certificate sandbox state.
        /// </summary>
        /// <returns>
        /// The primary sandbox token.
        /// </returns>
        public virtual ulong GetPrimarySandboxToken()
        {
            return CertificateSandboxState.GetPrimaryToken();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified token is the primary sandbox
        /// token.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to check.
        /// </param>
        /// <returns>
        /// Non-zero if the specified token is the primary sandbox token;
        /// otherwise, zero.
        /// </returns>
        public virtual bool IsPrimarySandboxToken(
            ulong token /* in */
            )
        {
            return CertificateSandboxState.IsPrimaryToken(token);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified sandbox token to the certificate sandbox state,
        /// associating it with this plugin.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to add.
        /// </param>
        /// <returns>
        /// Non-zero if the token was added; otherwise, zero.
        /// </returns>
        public virtual bool AddSandboxToken(
            ulong token /* in */
            )
        {
            return CertificateSandboxState.AddToken(token, this);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the specified sandbox token from the certificate sandbox
        /// state.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to remove.
        /// </param>
        /// <returns>
        /// Non-zero if the token was removed; otherwise, zero.
        /// </returns>
        public virtual bool RemoveSandboxToken(
            ulong token /* in */
            )
        {
            return CertificateSandboxState.RemoveToken(token);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of configuration file names that were loaded
        /// successfully.
        /// </summary>
        private StringList configurationOkFileNames;
        /// <summary>
        /// Gets a snapshot of the configuration file names that were loaded
        /// successfully.
        /// </summary>
        public virtual IEnumerable<string> ConfigurationOkFileNames
        {
            get
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (configurationOkFileNames == null)
                        return null;

                    return new StringList(configurationOkFileNames);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of configuration file names that failed to load.
        /// </summary>
        private StringList configurationErrorFileNames;
        /// <summary>
        /// Gets a snapshot of the configuration file names that failed to
        /// load.
        /// </summary>
        public virtual IEnumerable<string> ConfigurationErrorFileNames
        {
            get
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (configurationErrorFileNames == null)
                        return null;

                    return new StringList(configurationErrorFileNames);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the tracked configuration file names, including those
        /// held by the certificate sandbox state and the per-instance
        /// success and error lists.
        /// </summary>
        /// <returns>
        /// The total number of file names that were cleared.
        /// </returns>
        public virtual int ClearConfigurationFileNames()
        {
            int count = CertificateSandboxState.ClearFileNames(this);

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (configurationOkFileNames != null)
                {
                    count += configurationOkFileNames.Count;
                    configurationOkFileNames.Clear();
                }

                if (configurationErrorFileNames != null)
                {
                    count += configurationErrorFileNames.Count;
                    configurationErrorFileNames.Clear();
                }
            }

            return count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified file names to the list of configuration file
        /// names that were loaded successfully, normalizing each path and
        /// recording it in the certificate sandbox state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to normalize the file name paths.
        /// </param>
        /// <param name="fileNames">
        /// The file names, keyed by path, to add along with their associated
        /// results.
        /// </param>
        /// <returns>
        /// Non-zero on success; zero if <paramref name="fileNames" /> is
        /// null.
        /// </returns>
        public virtual bool AddConfigurationOkFileNames(
            Interpreter interpreter,              /* in */
            IDictionary<string, Result> fileNames /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (configurationOkFileNames == null)
                    configurationOkFileNames = new StringList();

                if (fileNames == null)
                    return false;

                foreach (KeyValuePair<string, Result> pair in fileNames)
                {
                    string fileName = pair.Key;

                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    string newFileName = Utility.RobustNormalizePath(
                        interpreter, fileName);

                    /* IGNORED */
                    CertificateSandboxState.AddOkFileName(
                        newFileName, this, pair.Value);

                    configurationOkFileNames.Add(newFileName);
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified file names to the list of configuration file
        /// names that failed to load, normalizing each path and recording it
        /// in the certificate sandbox state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to normalize the file name paths.
        /// </param>
        /// <param name="fileNames">
        /// The file names, keyed by path, to add along with their associated
        /// results.
        /// </param>
        /// <returns>
        /// Non-zero on success; zero if <paramref name="fileNames" /> is
        /// null.
        /// </returns>
        public virtual bool AddConfigurationErrorFileNames(
            Interpreter interpreter,              /* in */
            IDictionary<string, Result> fileNames /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (configurationErrorFileNames == null)
                    configurationErrorFileNames = new StringList();

                if (fileNames == null)
                    return false;

                foreach (KeyValuePair<string, Result> pair in fileNames)
                {
                    string fileName = pair.Key;

                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    string newFileName = Utility.RobustNormalizePath(
                        interpreter, fileName);

                    /* IGNORED */
                    CertificateSandboxState.AddErrorFileName(
                        newFileName, this, pair.Value);

                    configurationErrorFileNames.Add(newFileName);
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the well-known configuration data for the current
        /// application domain.  In official builds this performs the actual
        /// setup; in other builds it merely emits a diagnostic trace.
        /// </summary>
        public virtual void SetupWellKnownConfigurationData()
        {
#if OFFICIAL || DEBUG || FORCE_TRACE
            AppDomain appDomain = this.AppDomain;
#endif

#if OFFICIAL
            WellKnownOps.SetupConfigurationData(appDomain);
#elif DEBUG || FORCE_TRACE
            TraceOps.NetworkDebugTrace(String.Format(
                "SetupWellKnownConfigurationData: Skipped setting up " +
                "well-known configuration data for {0}.",
                DataOps.FormatAppDomainId(appDomain , true, true)),
                typeof(Default).Name, TracePriority.High);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory from which this plugin's configuration data is
        /// loaded, based on the directory of the certificate assembly.
        /// </summary>
        /// <returns>
        /// The configuration directory path.
        /// </returns>
        public virtual string GetConfigurationDirectory()
        {
            return Configuration.GetDirectory(
                AssemblyOps.GetDirectory());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key pairs and key usage associated with this plugin's
        /// configuration, derived from the certificate assembly.
        /// </summary>
        /// <param name="keyPairs">
        /// Upon successful return, receives the configuration key pairs.
        /// </param>
        /// <param name="keyUsage">
        /// Upon successful return, receives the key usage value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public virtual ReturnCode GetConfigurationKeyPairs(
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref string keyUsage,                /* out */
            ref Result error                    /* out */
            )
        {
            return Configuration.GetKeyPairs( /* OK */
                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                ref keyPairs, ref keyUsage, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the configuration data for this plugin from the certificate
        /// assembly, applying the policy and execution policy associated with
        /// this plugin for the specified configuration phase.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the configurations are being
        /// loaded.  This value is optional and may be null.
        /// </param>
        /// <param name="anyClientData">
        /// The client data to pass through to the configuration loading
        /// operation.
        /// </param>
        /// <param name="configurationPhase">
        /// The configuration phase that identifies when the load is taking
        /// place.
        /// </param>
        /// <param name="keyName">
        /// The name of the key to use when loading the configuration.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to use when loading the configuration.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to use for the operation.  This
        /// value is optional and may be null.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the configuration to be loaded.
        /// </param>
        /// <param name="doNotTrack">
        /// Non-zero to prevent the loaded configuration from being tracked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation or any error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public virtual ReturnCode LoadConfigurations(
            Interpreter interpreter,               /* in: OPTIONAL */
            IAnyClientData anyClientData,          /* in */
            ConfigurationPhase configurationPhase, /* in */
            string keyName,                        /* in */
            string keyRingName,                    /* in */
            int? timeout,                          /* in: OPTIONAL */
            bool force,                            /* in */
            bool doNotTrack,                       /* in */
            ref Result result                      /* out */
            )
        {
            return Configuration.MaybeLoadFor(interpreter,
                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                this, this, null, anyClientData, configurationPhase,
                GetPolicyType(), GetExecutionPolicy(), keyName,
                keyRingName, timeout, force, doNotTrack, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseFlagsData Members
#if LICENSING
        /// <summary>
        /// The licensed features associated with this plugin.
        /// </summary>
        private string features;
        /// <summary>
        /// Gets the licensed features associated with this plugin.
        /// </summary>
        public virtual string Features
        {
            get { return features; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The license restrictions associated with this plugin.
        /// </summary>
        private string restrictions;
        /// <summary>
        /// Gets the license restrictions associated with this plugin.  In
        /// release builds the engineering-use restriction is always appended,
        /// since release builds may not use certificates designated for
        /// engineering use only.
        /// </summary>
        public virtual string Restrictions
        {
            get
            {
#if DEBUG
                return restrictions;
#else
                //
                // NOTE: *POLICY* As a matter of policy, release builds MAY
                //       NOT use any license certificates designated to be
                //       "For engineering use only".
                //
                return String.Format(
                    "{0}{1}", restrictions, _Restrictions.Engineering);
#endif
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseCertificateData Members
#if LICENSING
        /// <summary>
        /// The file name of the license certificate in use by this plugin.
        /// </summary>
        private string certificateFileName;
        /// <summary>
        /// Gets the file name of the license certificate in use by this
        /// plugin.
        /// </summary>
        public virtual string CertificateFileName
        {
            get { return certificateFileName; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The license certificate in use by this plugin.
        /// </summary>
        private ICertificate certificate;
        /// <summary>
        /// Gets the license certificate in use by this plugin.
        /// </summary>
        public virtual ICertificate Certificate
        {
            get { return certificate; }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicensePluginData Members
#if LICENSING
        /// <summary>
        /// The set of license agreements associated with this plugin, keyed
        /// by their URI.
        /// </summary>
        private UriDictionary<bool> agreements;
        /// <summary>
        /// Gets the set of license agreements associated with this plugin,
        /// keyed by their URI.
        /// </summary>
        public virtual UriDictionary<bool> Agreements
        {
            get { return agreements; }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicensePluginManagerData Members
#if LICENSING
        /// <summary>
        /// The license manager used by this plugin.
        /// </summary>
        private ILicenseManager licenseManager;
        /// <summary>
        /// Gets the license manager used by this plugin.
        /// </summary>
        public virtual ILicenseManager LicenseManager
        {
            get { return licenseManager; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The storage manager used by this plugin.
        /// </summary>
        private IStorageManager storageManager;
        /// <summary>
        /// Gets the storage manager used by this plugin.
        /// </summary>
        public virtual IStorageManager StorageManager
        {
            get { return storageManager; }
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// The registry manager used by this plugin.
        /// </summary>
        private IRegistryManager registryManager;
        /// <summary>
        /// Gets the registry manager used by this plugin.
        /// </summary>
        public virtual IRegistryManager RegistryManager
        {
            get { return registryManager; }
        }
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IState Members
        /// <summary>
        /// Initializes this plugin for use with the specified interpreter.
        /// This loads the plugin configurations, verifies the license
        /// certificate, validates its agreement, performs the base plugin
        /// initialization, and, on success, records the certificate and marks
        /// the plugin as licensed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the plugin is being initialized.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the initialization.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation or any error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Initialize(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            ReturnCode code;
            Result localResult; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            CertificateSharedOps.SetupForCoreLibraryState();

            ///////////////////////////////////////////////////////////////////

#if LICENSING || CERTIFICATE_POLICY
            /* NO RESULT */
            CertificateGlobalState.MaybeCleanupAll("Initialize");
#endif

            ///////////////////////////////////////////////////////////////////

            /* IGNORED */
            AssemblyOps.AddReference(interpreter, this);

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            KeyFile.InitializeKeyPairTypes(false);

            ///////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_POLICY && PLUGIN_COMMANDS
            CertificateShellState.ResetFlags();
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            /* NO RESULT */
            CertificatePluginState.InitializeMappings(false);
#endif

            /* NO RESULT */
            CertificateTimeState.InitializeDurations(false);

            /* NO RESULT */
            CertificateVersionState.InitializeRanges(false);

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            SetupWellKnownConfigurationData();

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY && LICENSING
            long threadId = Utility.GetCurrentThreadId();
#endif

            ///////////////////////////////////////////////////////////////////

            IAnyClientData anyClientData = new AnyClientData(
                clientData, false);

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY && LICENSING
            threadId = Utility.GetCurrentThreadId();

            /* IGNORED */
            CertificatePluginState.AddPending(
                interpreter, threadId, this);

            try
            {
#endif
                string keyName = null;
                string keyRingName = null;

#if LICENSING
                keyName = GetKeyName();
                keyRingName = GetKeyRingName();
#endif

                localResult = null;

                code = LoadConfigurations(interpreter,
                    anyClientData, ConfigurationPhase.Initialize,
                    keyName, keyRingName, GetTimeout(interpreter),
                    false, false, ref localResult);

                if (code != ReturnCode.Ok)
                {
                    result = localResult;

#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
                    /* NO RESULT */
                    CertificateIsolatedOps.MaybeFixupResult(
                        interpreter, this, result);
#endif

                    return code;
                }
#if CERTIFICATE_POLICY && LICENSING
            }
            finally
            {
                /* IGNORED */
                CertificatePluginState.RemovePending(
                    interpreter, threadId);
            }
#endif

            ///////////////////////////////////////////////////////////////////

            localResult = null;

            code = CertificatePluginOps.Check(
                interpreter, this, ref localResult);

            if (code != ReturnCode.Ok)
            {
                result = localResult;

#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
                /* NO RESULT */
                CertificateIsolatedOps.MaybeFixupResult(
                    interpreter, this, result);
#endif

                return code;
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK
#if DEBUG || EXTRA_DIAGNOSTICS
            if (!Configuration.DoesVariableExist(
                    Constants.NoNetworkTimeEnvVarName))
#endif
            {
                /* NO RESULT */
                CertificateNetworkOps.AsynchronousAccessChecks(
                    interpreter, Utility.GetUtcNow(), true);
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if LICENSING
            string fileName = null;
            ICertificate certificate = null;

#if CERTIFICATE_POLICY
            /* IGNORED */
            CertificatePluginState.AddPending(
                interpreter, threadId, this);

            try
            {
#endif
                localResult = null;

                code = VerifyCertificate(
                    interpreter, anyClientData, ref fileName,
                    ref certificate, ref localResult);

                if (code != ReturnCode.Ok)
                {
                    result = localResult;

#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
                    /* NO RESULT */
                    CertificateIsolatedOps.MaybeFixupResult(
                        interpreter, this, result);
#endif

                    return code;
                }
#if CERTIFICATE_POLICY
            }
            finally
            {
                /* IGNORED */
                CertificatePluginState.RemovePending(
                    interpreter, threadId);
            }
#endif

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Make sure this license certificate is intended to be
            //       used with this component.
            //
            // BUGFIX: We cannot validate the agreement if there no actual
            //         certificate instance.
            //
            if (certificate != null)
            {
                localResult = null;

                code = CertificateSharedOps.MatchAgreement(
                    certificate, this.Agreements, ref localResult);

                if (code != ReturnCode.Ok)
                {
                    ResetCertificate();

                    result = localResult;

#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
                    /* NO RESULT */
                    CertificateIsolatedOps.MaybeFixupResult(
                        interpreter, this, result);
#endif

                    return code;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Call the initialization for the default plugin now.
            //
            localResult = null;

            code = base.Initialize(
                interpreter, clientData, ref localResult);

            if (code == ReturnCode.Ok)
            {
#if PLUGIN_COMMANDS
                /* NO RESULT */
                MaybeSetupClientDataForSecrets(interpreter);
#endif

                result = localResult;
            }
            else
            {
#if LICENSING
                /* NO RESULT */
                ResetCertificate();
#endif

                result = localResult;

                /* NO RESULT */
                CertificateIsolatedOps.MaybeFixupResult(
                    interpreter, this, result);

                return code;
            }

            ///////////////////////////////////////////////////////////////////

#if LICENSING
            //
            // NOTE: If we have succeeded at initializing the plugin and the
            //       provided certificate is valid, save the certificate and
            //       its file name.  Also, mark the plugin as "licensed".
            //
            /* NO RESULT */
            SetFlagAndData(fileName, certificate, true);

            /* NO RESULT */
            CertificateSharedOps.SetViaPlugin(this, certificate);

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            /* IGNORED */
            CertificatePolicyOps.SetPluginDatas(this);

            /* IGNORED */
            CertificatePolicyOps.SetCertificates(certificate);
#endif
#endif

            ///////////////////////////////////////////////////////////////////

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Terminates this plugin for the specified interpreter.  This clears
        /// the configuration file names, cleans up the interpreter sandbox
        /// state, removes the assembly reference, clears the licensed flag
        /// and data, performs any required global cleanup, and then invokes
        /// the base termination.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the plugin is being terminated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the termination.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation or any error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Terminate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            /* IGNORED */
            ClearConfigurationFileNames();

            ///////////////////////////////////////////////////////////////////

            /* IGNORED */
            CertificateSandboxState.CleanupInterpreters(this);

            ///////////////////////////////////////////////////////////////////

#if LICENSING || CERTIFICATE_POLICY
            int referenceCount = AssemblyOps.RemoveReference(
                interpreter, this);

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            if (referenceCount <= 0)
            {
                //
                // HACK: This would also be done for this interpreter via the
                //       call to the CleanupOne method (below).
                //
                /* IGNORED */
                CertificateKeyRingState.RemoveAllTrusted(interpreter, true);
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if LICENSING
            /* NO RESULT */
            SetFlagAndData(null, null, false);
#endif

            ///////////////////////////////////////////////////////////////////

            if (referenceCount <= 0)
                CertificateGlobalState.CleanupOne(interpreter);

            ///////////////////////////////////////////////////////////////////

            CertificateGlobalState.MaybeCleanupAll("Terminate");
#endif

            ///////////////////////////////////////////////////////////////////

            return base.Terminate(interpreter, clientData, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecuteRequest Members
#if ISOLATED_PLUGINS
        /// <summary>
        /// Executes a request directed at this plugin, typically when it is
        /// running in an isolated application domain.  The request is
        /// dispatched to the certificate isolated operations using the
        /// current culture and the license manager, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request.  This parameter is
        /// not used directly but is forwarded to the request handler.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the request.
        /// </param>
        /// <param name="request">
        /// The request object to execute.
        /// </param>
        /// <param name="response">
        /// Upon successful return, receives the response object.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Execute(
            Interpreter interpreter, /* in: NOT USED */
            IClientData clientData,  /* in */
            object request,          /* in */
            ref object response,     /* out */
            ref Result error         /* out */
            )
        {
            CultureInfo cultureInfo;
            bool disposed;

            /* NO RESULT */
            DataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                error = "interpreter is disposed";
                return ReturnCode.Error;
            }

            CertificateSdkMode.Enable();

            try
            {
                ILicenseManager licenseManager;

#if LICENSING
                licenseManager = this.LicenseManager;
#else
                licenseManager = null;
#endif

                return CertificateIsolatedOps.ExecuteRequest(
                    interpreter, licenseManager, clientData,
                    request, cultureInfo, GetTimeout(interpreter),
                    ref response, ref error);
            }
            finally
            {
                CertificateSdkMode.Disable();
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
#if CERTIFICATE_POLICY && LIMITED_EDITION
        /// <summary>
        /// Performs post-initialization for the "Limited Edition" of this
        /// plugin.  This hard-wires all execution policies to secure mode
        /// and, unless suppressed, attempts to load the bootstrap (Class 0)
        /// key ring so that core script library and tool scripts can be
        /// verified.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which post-initialization is being performed.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the post-initialization.
        /// </param>
        public override void PostInitialize(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
        {
            //
            // NOTE: The "Limited Edition" of this plugin always hard-wires
            //       all the execution policies to "secure mode".
            //
            /* IGNORED */
            CertificatePolicyOps.SetPolicy(
                this, PolicyType.Script,
                Constants.LimitedScriptExecutionPolicy);

            /* IGNORED */
            CertificatePolicyOps.SetPolicy(
                this, PolicyType.File,
                Constants.LimitedFileExecutionPolicy);

            /* IGNORED */
            CertificatePolicyOps.SetPolicy(
                this, PolicyType.Other,
                Constants.LimitedOtherExecutionPolicy);

            /* IGNORED */
            CertificatePolicyOps.SetPolicy(
                this, PolicyType.Stream,
                Constants.LimitedStreamExecutionPolicy);

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Also, in the "Limited Edition" of this plugin, always
            //       attempt to load the bootstrap (Class 0) key ring so that
            //       the scripts within the the core script library and tools
            //       can be verified.  Obviously (?), this cannot be done for
            //       the interpreter that loads the boostrap key ring itself.
            //
            if (interpreter.DoesVariableExist(VariableFlags.None,
                    Constants.SkipAutoKeyRingBootstrap) != ReturnCode.Ok)
            {
                /* NO RESULT */
                CertificateKeyRingOps.LoadScriptKeyPairsPublicOnly(
                    interpreter, GetKeyRingName(), this, null, null,
                    CertificatePolicyOps.GetPolicy(this, PolicyType.Script),
                    TracePriority.Default, false, false, false, true);
            }

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            base.PostInitialize(interpreter, clientData);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a named string resource for this plugin.  When the requested
        /// name is the well-known certificate string name, the string form of
        /// the current certificate is returned; otherwise, the string is
        /// resolved through the plugin resource manager.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the string is being retrieved.
        /// </param>
        /// <param name="name">
        /// The name of the string resource to retrieve.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when resolving the string resource.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// The requested string, or null if it could not be retrieved.
        /// </returns>
        public override string GetString(
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
#if LICENSING
            if (DataOps.StringEquals(
                    name, Constants.CertificateStringName))
            {
                ICertificate certificate = this.Certificate;

                if (certificate != null)
                    return certificate.ToString();

                error = "certificate string not available";
                return null;
            }
#endif

            return Utility.GetAnyString(
                interpreter, this, ResourceManager, name,
                cultureInfo, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate file name for this plugin.  When a name is
        /// supplied, a plugin-relative file name is resolved for it;
        /// otherwise, the file name of the currently loaded certificate is
        /// returned.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the file name is being retrieved.
        /// </param>
        /// <param name="name">
        /// The name identifying the certificate type, or null or empty to
        /// use the currently loaded certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// The certificate file name, or null if it could not be determined.
        /// </returns>
        public override string GetCertificateFileName(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            string fileName;

            if (!String.IsNullOrEmpty(name))
            {
                fileName = Utility.GetPluginRelativeFileName(
                    this, null, name);

                if (fileName == null)
                    error = "unsupported certificate type";
            }
            else
            {
#if LICENSING
                fileName = certificateFileName;

                if (fileName == null)
                    error = "invalid file name";
#else
                error = "file name unavailable";
                fileName = null;
#endif
            }

            return fileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the license certificate associated with this plugin as an
        /// identifier.  A non-empty name is not supported, and the
        /// certificate is unavailable when the plugin is running
        /// cross-application-domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the certificate is being
        /// retrieved.
        /// </param>
        /// <param name="name">
        /// The name identifying the certificate type; only null or empty is
        /// supported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// The certificate as an <see cref="IIdentifier" />, or null if it
        /// could not be retrieved.
        /// </returns>
        public override IIdentifier GetCertificate(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (!String.IsNullOrEmpty(name))
            {
                error = "unsupported certificate type";
                return null;
            }

            if (CertificateSharedOps.IsCrossAppDomain(interpreter, this))
            {
                error = "unsupported when plugin is isolated";
                return null;
            }

            IIdentifier identifier;

#if LICENSING
            identifier = this.Certificate as IIdentifier;

            if (identifier == null)
                error = "invalid certificate";
#else
            error = "certificate unavailable";
            identifier = null;
#endif

            return identifier;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the public-only key pair associated with this plugin's
        /// assembly as an identifier.  A non-empty name is not supported, and
        /// the key pair is unavailable when the plugin is running
        /// cross-application-domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the key pair is being retrieved.
        /// </param>
        /// <param name="name">
        /// The name identifying the key pair type; only null or empty is
        /// supported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// The key pair as an <see cref="IIdentifier" />, or null if it
        /// could not be retrieved.
        /// </returns>
        public override IIdentifier GetKeyPair(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (!String.IsNullOrEmpty(name))
            {
                error = "unsupported key pair type";
                return null;
            }

            if (CertificateSharedOps.IsCrossAppDomain(interpreter, this))
            {
                error = "unsupported when plugin is isolated";
                return null;
            }

            IKeyPair keyPair = null;

            if (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                    AssemblyOps.GetObject(), AssemblyOps.GetName(),
                    ref keyPair, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (keyPair is IIdentifier)
                return (IIdentifier)keyPair;

            error = "key pair is not an identifier";
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
        /// <summary>
        /// Gets the trusted script key ring associated with this plugin as an
        /// identifier.  A non-empty name is not supported, and the key
        /// ring is unavailable when the plugin is running
        /// cross-application-domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the key ring is being retrieved.
        /// </param>
        /// <param name="name">
        /// The name identifying the key ring type; only null or empty is
        /// supported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// The key ring as an <see cref="IIdentifier" />, or null if it
        /// could not be retrieved.
        /// </returns>
        public override IIdentifier GetKeyRing(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (!String.IsNullOrEmpty(name))
            {
                error = "unsupported key ring type";
                return null;
            }

            if (CertificateSharedOps.IsCrossAppDomain(interpreter, this))
            {
                error = "unsupported when plugin is isolated";
                return null;
            }

            return CertificateKeyRingState.GetTrusted(
                interpreter, CertificateKeyRingOps.GetName(null,
                PolicyType.Script), ref error) as IIdentifier;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
        /// <summary>
        /// Emits a banner describing the relevant execution policies enforced
        /// by this plugin.  The banner is only emitted for the "Security"
        /// core plugin, and only reports the script and file execution
        /// policies that are currently set to "allow signed only".
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose host is used to emit the banner.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives the error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Banner(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            //
            // HACK: Only emit this banner for the "Security" plugin,
            //       not the "Licensing" ones.  This is because only
            //       the "Security" plugin is really intended to deal
            //       with execution policies.
            //
            if (!IsSecurityCore())
                return ReturnCode.Ok;

            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            IHost host = interpreter.Host;

            if (host == null)
            {
                result = "interpreter host not available";
                return ReturnCode.Error;
            }

            ConsoleColor foregroundColor = _ConsoleColor.None;
            ConsoleColor backgroundColor = _ConsoleColor.None;

            if (Utility.HasFlags(
                    host.GetHostFlags(), HostFlags.AllColors, false))
            {
                Result error = null;

                /* IGNORED */
                host.GetColors(null,
                    ColorName.Enabled, true, true, ref foregroundColor,
                    ref backgroundColor, ref error);
            }

            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
            {
                result = "policy types are not available";
                return ReturnCode.Error;
            }

            string prefix = AssemblyOps.MustGetSimpleName();
            bool newLine = false;

            foreach (PolicyType policyType in policyTypes)
            {
                if ((policyType != PolicyType.Script) &&
                    (policyType != PolicyType.File))
                {
                    continue;
                }

                bool local;

                ExecutionPolicy policy = CertificatePolicyOps.GetPolicy(
                    this, policyType, out local);

                ExecutionPolicy basePolicy = ExecutionPolicy.Undefined;

                if (CertificatePolicyOps.CheckPolicy(
                        policyType, interpreter, this, ref policy,
                        ref basePolicy) != ReturnCode.Ok)
                {
                    continue;
                }

                if (!Utility.HasFlags(
                        basePolicy, ExecutionPolicy.BasePolicyMask,
                        false))
                {
                    continue;
                }

                //
                // NOTE: For our purposes, only consider "AllowSignedOnly"
                //       to be "enabled".
                //
                if (basePolicy != ExecutionPolicy.AllowSignedOnly)
                    continue;

                //
                // NOTE: Emit a blank line to separate the status lines
                //       emitted by this plugin from those emitted by the
                //       core (or other plugins).  Obviously, this needs
                //       to be done only once (by this plugin).
                //
                if (!newLine)
                {
                    host.WriteLine();
                    newLine = true;
                }

                //
                // NOTE: Do we have colors configured for this output?  If
                //       so, use them; otherwise, use the method without
                //       any color output.
                //
                string value = String.Format(
                    "{0}: Execution policy {1} is {2} {3}.", prefix,
                    Utility.FormatWrapOrNull(policyType),
                    Utility.FormatWrapOrNull(basePolicy),
                    local ? "locally" : "globally");

                if ((foregroundColor != _ConsoleColor.None) ||
                    (backgroundColor != _ConsoleColor.None))
                {
                    host.WriteLine(value, foregroundColor, backgroundColor);
                }
                else
                {
                    host.WriteLine(value);
                }
            }

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets descriptive "about" information for this plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the information is being
        /// retrieved.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the about information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success.
        /// </returns>
        public override ReturnCode About(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            result = CertificatePluginOps.About(interpreter, this);
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the set of compile-time define constants that were in effect
        /// when this plugin was built.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the options are being retrieved.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the define constants or any error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Options(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            return CertificatePluginOps.GetDefineConstants(ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the status of this plugin.  For safe interpreters the status
        /// simply reports that the plugin is present; otherwise, it reports
        /// the file execution policy status.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on whose behalf the status is being retrieved.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the status or any error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Status(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
#if CERTIFICATE_POLICY
            if ((interpreter != null) && interpreter.IsSafe())
            {
                result = "Present";
                return ReturnCode.Ok;
            }

            result = CertificatePolicyOps.GetStatus(
                interpreter, this, PolicyType.File);

            return ReturnCode.Ok;
#else
            result = "not implemented";
            return ReturnCode.Error;
#endif
        }
        #endregion
    }
}
