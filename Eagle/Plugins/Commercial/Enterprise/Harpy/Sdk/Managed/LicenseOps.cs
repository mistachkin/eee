/*
 * LicenseOps.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Official Late-Bound License Validation & Verification API
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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

using SaltAndPasswordPair = Eagle._Components.Public.AnyPair<
    System.Guid, string>;

using CertificateDictionary = System.Collections.Generic.IDictionary<
    string, string>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Sdk.Private
{
    /// <summary>
    /// Provides the managed SDK's private helper routines for loading the
    /// license manager package, fetching and verifying certificates, and
    /// performing licensing operations on behalf of a host application.
    /// </summary>
    [ObjectId("18b3548d-98b2-4c1b-b36a-c0d0fb64bd2e")]
    internal static class LicenseOps
    {
        #region Private Manager / Library (SDK) Helper Constants
        #region Current Assembly Constants
        /// <summary>
        /// The assembly currently being executed -OR- null if it cannot be
        /// determined.
        /// </summary>
        private static readonly Assembly ThisAssembly =
            Assembly.GetExecutingAssembly();

        /// <summary>
        /// The name of the assembly currently being executed -OR- null if it
        /// cannot be determined.
        /// </summary>
        private static readonly AssemblyName ThisAssemblyName =
            (ThisAssembly != null) ? ThisAssembly.GetName() : null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Target Framework Constants
        /// <summary>
        /// This private class is used to contain the lists of compile-time
        /// options related to the target framework.
        /// </summary>
        [ObjectId("4aa7e242-effc-439b-904c-c9a13d76ea18")]
        private static class DefineConstants
        {
            /// <summary>
            /// This is the list of compile-time options that are related to
            /// the target framework our assembly was compiled for.
            /// </summary>
            public static readonly StringList OptionList =
                new StringList(new string[] {
#if NET_20
                "NET_20",
#endif

#if NET_20_ONLY
                "NET_20_ONLY",
#endif

#if NET_20_SP1
                "NET_20_SP1",
#endif

#if NET_20_SP2
                "NET_20_SP2",
#endif

#if NET_30
                "NET_30",
#endif

#if NET_35
                "NET_35",
#endif

#if NET_40
                "NET_40",
#endif

#if NET_45
                "NET_45",
#endif

#if NET_451
                "NET_451",
#endif

#if NET_452
                "NET_452",
#endif

#if NET_46
                "NET_46",
#endif

#if NET_461
                "NET_461",
#endif

#if NET_462
                "NET_462",
#endif

#if NET_47
                "NET_47",
#endif

#if NET_471
                "NET_471",
#endif

#if NET_472
                "NET_472",
#endif

#if NET_48
                "NET_48",
#endif

#if NET_481
                "NET_481",
#endif

#if NET_CORE_REFERENCES
                "NET_CORE_REFERENCES",
#endif

#if NET_CORE_20
                "NET_CORE_20",
#endif

#if NET_CORE_30
                "NET_CORE_30",
#endif

#if NET_CORE_50
                "NET_CORE_50",
#endif

#if NET_STANDARD_20
                "NET_STANDARD_20",
#endif

#if NET_STANDARD_21
                "NET_STANDARD_21",
#endif

                null
            });
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Eagle Script Constants
        /// <summary>
        /// These are base creation flags used to create interpreters that are
        /// only for use by this class. All these flags are applicable whether
        /// or not plugin isolation features are compiled into the Eagle core
        /// library.
        ///
        /// WARNING: These sets of interpreter creation flags are extremely
        ///          optimized to the exact usage patterns of interpreters
        ///          created for use by this class, and specifically to the
        ///          "EvaluateFile" and "VerifyCertificate" method overloads
        ///          that create their own interpreter context. Please do not
        ///          change any of these flag values unless you know exactly
        ///          what they all do.
        /// </summary>
        private const CreateFlags SdkBaseCreateFlags =
            (CreateFlags.FastSingleUse & ~CreateFlags.ThrowOnError) |
            CreateFlags.IfNecessary | CreateFlags.IfCannotLock |
            CreateFlags.MeasureTime | CreateFlags.SafeAndHideUnsafe |
            CreateFlags.NoDispose;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are base creation flags used to create interpreters that are
        /// only for use by the <c>VerifyCertificate</c> methods of this class.
        /// All these flags are applicable whether or not plugin isolation
        /// features are compiled into the Eagle core library.
        ///
        /// WARNING: These sets of interpreter creation flags are extremely
        ///          optimized to the exact usage patterns of interpreters
        ///          created for use by this class, and specifically to the
        ///          "EvaluateFile" and "VerifyCertificate" method overloads
        ///          that create their own interpreter context. Please do not
        ///          change any of these flag values unless you know exactly
        ///          what they all do.
        /// </summary>
        private const CreateFlags VerifyBaseCreateFlags =
            (SdkBaseCreateFlags & ~CreateFlags.UseNamespaces) |
            CreateFlags.NoLibrary | CreateFlags.NoLoader |
            CreateFlags.MinimumVariables | CreateFlags.NoHome |
            CreateFlags.NoChannels | CreateFlags.NoFunctions |
            CreateFlags.LicenseSdk | CreateFlags.NoRandom |
            CreateFlags.NoCorePolicies | CreateFlags.NoCoreTraces;

        ///////////////////////////////////////////////////////////////////////

#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        /// <summary>
        /// These are the extra, plugin-isolated related, creation flags used
        /// to create interpreters that are only for use by this class.
        /// </summary>
        private const CreateFlags SdkPluginCreateFlags =
            CreateFlags.ProbePlugins;
#else
        /// <summary>
        /// These are the extra, plugin-isolated related, creation flags used
        /// to create interpreters that are only for use by this class.
        /// </summary>
        private const CreateFlags SdkPluginCreateFlags = CreateFlags.None;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the creation flags used to create interpreters that are
        /// only for use by the <c>EvaluateFile</c> methods of this class.
        /// </summary>
        private const CreateFlags EvaluateCreateFlags =
            SdkBaseCreateFlags | SdkPluginCreateFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the creation flags used to create interpreters that are
        /// only for use by the VerifyCertificate methods of this class.
        /// </summary>
        private const CreateFlags VerifyCreateFlags =
            VerifyBaseCreateFlags | SdkPluginCreateFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are host creation flags used to create interpreter hosts
        /// that are only for use by this class.
        /// </summary>
        private const HostCreateFlags SdkHostCreateFlags =
            HostCreateFlags.FastSingleUse;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are script library initialization flags used to create
        /// interpreters that are only for use by this class.
        /// </summary>
        private const InitializeFlags SdkInitializeFlags =
            InitializeFlags.Direct | InitializeFlags.AutoPath |
            InitializeFlags.GlobalTracking;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are script library initialization flags used to create
        /// interpreters that are only for use by the EvaluateFile methods of
        /// this class.
        /// </summary>
        private const InitializeFlags EvaluateInitializeFlags =
            SdkInitializeFlags | InitializeFlags.SafeSdkUse;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are interpreter settings flags used to create interpreters
        /// that are only for use by this class.
        /// </summary>
        private const InterpreterFlags SdkInterpreterFlags =
            InterpreterFlags.Default | InterpreterFlags.NoThreadAbort;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are script flags used to create interpreters that are only
        /// for use by this class.
        /// </summary>
        private const ScriptFlags SdkScriptFlags =
            (ScriptFlags.Default & ~ScriptFlags.PreferFileSystem) |
            ScriptFlags.NoFileSystem;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are plugin flags used to create interpreters that are only
        /// for use by this class.
        /// </summary>
        private const PluginFlags SdkPluginFlags =
#if DEBUG
#if ISOLATED_PLUGINS
            PluginFlags.Isolated;
#else
            PluginFlags.None;
#endif
#else
#if ISOLATED_PLUGINS
            PluginFlags.VerifiedOnly | PluginFlags.TrustedOnly |
            PluginFlags.Isolated;
#else
            PluginFlags.VerifiedOnly | PluginFlags.TrustedOnly;
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the cache flags used to limit large object heap (LOH)
        /// memory usage.
        /// </summary>
        private const CacheFlags SdkCacheFlags =
            CacheFlags.Lock | CacheFlags.Reset | CacheFlags.Clear |
            CacheFlags.Argument;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the initial flags to use when disabling interpreter
        /// creation.
        /// </summary>
        private const DisableFlags SdkDisableFlags = DisableFlags.Sdk;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the trust flags used to evaluate the hard-coded scripts
        /// used by this class.
        /// </summary>
        private const TrustFlags ManagerTrustFlags =
            TrustFlags.MaybeMarkTrusted;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The maximum number of times to attempt interpreter creation from
        /// within the VerifyCertificate methods.
        /// </summary>
        private const int MaximumRetries = 40;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The minimum number of milliseconds to sleep between attempts to
        /// create an interpreter from within the VerifyCertificate and
        /// EvaluateFile methods.
        /// </summary>
        private const int SleepMilliseconds = 50;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This will supply random numbers used to sleep between retries of
        /// the interpreter creation process.
        /// </summary>
        private static readonly Random SleepRandom = new Random(12345679);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the Eagle command used to interact with its package
        /// manager.
        /// </summary>
        private static readonly string ManagerPackageCommandName =
            "::package";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the Eagle command used to interact with its plugin
        /// loader.
        /// </summary>
        private const string ManagerLoadCommandName = "::load";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The Eagle script variable name used to locate the script to be
        /// used before attempting to load the license manager components.
        /// </summary>
        private static readonly string ManagerPreLoadScriptVariableName =
            "::" + ((ThisAssemblyName != null) ? ThisAssemblyName.Name +
            Type.Delimiter : String.Empty) + "LicenseOps_ManagerPreLoadScript";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Individual arguments of the Eagle <c>[package ifneeded]</c> command
        /// to be used to force the package containing the license manager
        /// components to be provided to the interpreter. Normally, this would
        /// be done via a package index script (file); however, for
        /// performance, this should be hard-coded inline for interpreters
        /// created by this SDK.
        /// </summary>
        private const string ManagerPackageName = "Licensing.Core";
        /// <summary>
        /// The version of the license manager package required by this SDK.
        /// </summary>
        private const string ManagerPackageVersion = "1.0";
        /// <summary>
        /// The Eagle "package" sub-command used to register the license
        /// manager package for on-demand loading.
        /// </summary>
        private const string IfNeededSubCommandName = "ifneeded";
        /// <summary>
        /// The option name used to specify the required public key token when
        /// registering the license manager package.
        /// </summary>
        private const string PublicKeyTokenOptionName = "-publickeytoken";
        /// <summary>
        /// The option name used to allow the license manager package to be
        /// used from any thread.
        /// </summary>
        private const string AnyThreadOptionName = "-anythread";
        /// <summary>
        /// The package flags argument value used to lock the license manager
        /// package once it has been loaded.
        /// </summary>
        private const string PackageFlagsArgumentValue = "+Locked";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The Eagle script to be used to force the assembly containing the
        /// license manager components to load. In theory, this script could
        /// do practically anything; however, in practice it will simply end
        /// up loading the associated Eagle plugin (i.e. "Harpy").
        /// </summary>
        private const string ManagerRequireScript = "{0} require {1};";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The certificate property that contains its unique identifier.
        /// </summary>
        private const string CertificateIdProperty = "Id";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The certificate property that contains the name of the licensed
        /// entity.
        /// </summary>
        private const string CertificateEntityNameProperty = "EntityName";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The Eagle command and sub-command names to be used in order to
        /// fetch the entity name for the specified certificate.
        /// </summary>
        private const string CertificateCommandName = "::certificate";
        /// <summary>
        /// The certificate sub-command name used to fetch certificate
        /// metadata.
        /// </summary>
        private const string MetadataSubCommandName = "metadata";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Constants
        /// <summary>
        /// If this environment variable is set [to anything], the license
        /// manager will be forbidden from attempting to use _any_ network
        /// access. This may cause various internal failures, including
        /// certificate verification when time limitations are present.
        /// </summary>
        private const string ForceOfflineModeEnvVarName =
            "HarpyForceOfflineMode";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], the license
        /// manager (plugin) will be allowed to load on any thread; without
        /// this, it will only be allowed to load on the primary thread for
        /// its containing interpreter.
        /// </summary>
        private const string AllowAnyThreadEnvVarName = "HarpyAllowAnyThread";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The environment variable name used when figuring out the binary
        /// path for use in the hard-coded [package ifneeded] command to be
        /// executed by the license certification verification subsystem of
        /// the SDK. When set, this will be used instead of the value from the
        /// core library.
        /// </summary>
        private const string BinaryPathEnvVarName = "HarpyBinaryPath";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The environment variable name used when figuring out the maximum
        /// number of retries for various internal operations, e.g. creating
        /// interpreters for use by the SDK, etc.
        /// </summary>
        private const string MaximumRetriesEnvVarName = "HarpyMaximumRetries";

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20 && !NET_STANDARD_21 && NATIVE && WINDOWS
        /// <summary>
        /// The environment variable name format used to build the variable
        /// name that prevents the default application domain from having its
        /// authorization data set more than once (i.e. via copying it from
        /// the current application domain).
        /// </summary>
        private const string DefaultAppDomainEnvVarFormat =
            "Harpy_DefaultAppDomain_Authorization_{0}";
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the boolean datum within the clientData that will
        /// permit skipping of AppDomain authorization checks.
        /// </summary>
        public const string SkipAuthorizationDataName = "skipAuthorization";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) License Certificate Constants
        /// <summary>
        /// This is the format string used to build a certificate file name
        /// from a bare resource name (i.e. without a file extension) and a
        /// file extension.
        /// </summary>
        private const string ManagerResourceFileNameFormat = "{0}{1}";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], the license
        /// manager SDK will retry interpreter creation, with a short delay
        /// between attempts.
        /// </summary>
        private const string ManagerRetryCreation = "HarpyRetryCreation";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The environment variable name used when extracting a temporary
        /// license certificate file for the license manager assembly.
        /// </summary>
        private const string ManagerOverrideEnvVarName =
            "Override_Harpy_Certificate";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], the license
        /// manager will be forbidden from attempting to acquire a license
        /// certificate for itself, e.g. online.
        /// </summary>
        private const string ManagerNoAutoAcquireEnvVarName =
            "HarpyNoAutoAcquire";

        ///////////////////////////////////////////////////////////////////////

#if ISOLATED_PLUGINS
        /// <summary>
        /// If this environment variable is set [to anything], SDK plugin
        /// isolation will be disabled whenever the isolated parameter has not
        /// been explicitly specified.
        /// </summary>
        private const string ManagerNoIsolatedEnvVarName =
            "HarpyNoIsolated";
#endif

        ///////////////////////////////////////////////////////////////////////

#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        /// <summary>
        /// If this environment variable is set [to anything], plugins will
        /// not be probed for their package indexes within any interpreter
        /// created by this SDK.
        /// </summary>
        private const string ManagerNoProbePluginsEnvVarName =
            "HarpyNoProbePlugins";
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], well-known
        /// plugins will have their associated [package ifneeded] commands
        /// automatically evaluated within any interpreter created by this
        /// SDK.
        /// </summary>
        private const string ManagerWellKnownPluginsEnvVarName =
            "HarpyWellKnownPlugins";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// not attempt to disable (further) interpreter creation.
        /// </summary>
        private const string ManagerNoDisableCreationEnvVarName =
            "HarpyNoDisableCreation";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// not attempt to use the list of trusted hashes for newly created
        /// interpreters.
        /// </summary>
        private const string ManagerNoTrustedHashesEnvVarName =
            "HarpyNoTrustedHashes";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// open and use a console window when creating interpreters for use
        /// by scripts evaluated within the EvaluateFile methods of this
        /// class.
        /// </summary>
        private const string ManagerEvaluateWithConsole =
            "HarpyEvaluateWithConsole";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// open and use a console window when creating interpreters for use
        /// by scripts evaluated within the VerifyCertificate methods of this
        /// class.
        /// </summary>
        private const string ManagerVerifyWithConsole =
            "HarpyVerifyWithConsole";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used when getting and/or setting "well-known"
        /// configuration data for an AppDomain, e.g. encryption parameters
        /// used to decrypt license certificates and/or configuration files.
        /// </summary>
        private const string GetDataFormat = "{0}_{1}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Assembly Name Constants
        /// <summary>
        /// The simple name of the assembly containing the license manager
        /// components, roughly in order of priority.
        /// </summary>
        private static readonly string[] ManagerAssemblyNames = {
            "Harpy", "Harpy.Basic", "Harpy.Limited", "Harpy.Sdk"
        };

        /// <summary>
        /// The simple name of the assembly containing the script certificate
        /// manager components, roughly in order of priority.
        /// </summary>
        private static readonly string[] LibraryAssemblyNames = {
            "Badge", "Badge.Basic"
        };

        /// <summary>
        /// The (glob) pattern used to find the plugin associated with the
        /// license manager components.
        /// </summary>
        private static readonly string ManagerAssemblyPattern = "Harpy" +
            Characters.Asterisk;

        /// <summary>
        /// The (glob) pattern used to find the plugin associated with the
        /// script certificate manager components.
        /// </summary>
        private static readonly string LibraryAssemblyPattern = "Badge" +
            Characters.Asterisk;

        /// <summary>
        /// The version of the SDK supported by this class.
        /// </summary>
        private static readonly Version SdkVersion = new Version(1, 0);

        /// <summary>
        /// The public key token of the assemblies containing the license
        /// manager components and the script certificate manager components.
        ///
        /// TODO: *REKEY* Always change this value if the license manager
        ///       assembly is going to be signed with a different key.
        /// </summary>
        private static readonly byte[] SdkPublicKeyToken = {
            0x8b, 0xf4, 0x3b, 0x47, 0x49, 0xe4, 0x6a, 0x0b
        };

        /// <summary>
        /// Zero if the assembly matching is to be performed strictly based on
        /// the full assembly names (i.e. the public key tokens must match the
        /// one hard-coded into this module exactly).
        /// </summary>
        private const bool SdkAllowAssemblyNameOnly = true;

        /// <summary>
        /// The environment variable name (or suffix) to be checked when
        /// figuring out if a plugin should be loaded in isolated mode.
        /// </summary>
        private const string IsolatedEnvVarName = "Isolated";

        /// <summary>
        /// The environment variable name (or suffix) to be checked when
        /// figuring out if a plugin should NOT be loaded in isolated mode.
        /// </summary>
        private const string NoIsolatedEnvVarName = "NoIsolated";

        /// <summary>
        /// The environment variable name (or suffix) to be checked when
        /// figuring out if any license manager package from any license
        /// manager plugin should be used (i.e. the manager load script should
        /// be skipped).
        /// </summary>
        private const string UseAnyPackageEnvVarName = "UseAnyPackage";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Plugin Name Constants
        /// <summary>
        /// This is the public key token for the assembly containing license
        /// manager components. Assemblies that do not match this public key
        /// token will not be loaded via this SDK.
        ///
        /// TODO: *REKEY* Always change this value if the license manager
        ///       assembly is going to be signed with a different key.
        /// </summary>
        private const string ManagerPublicKeyToken = "8bf43b4749e46a0b";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This name (i.e. "glob" pattern) to be used to reset the plugin
        /// arguments for the license manager. This is only used when a
        /// license certificate file name is specified manually.
        /// </summary>
        private const string ManagerPluginPattern =
            "*, Harpy*, *, PublicKeyToken=" + ManagerPublicKeyToken;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Type Name Constants
        /// <summary>
        /// The full type name for the primary license manager component.
        /// </summary>
        private const string ManagerTypeName =
            "Licensing.Components.Public.LicenseManager";

        /// <summary>
        /// The full type name for the certificate renewal callback delegate
        /// used with the license manager verification subsystem.
        /// </summary>
        private const string RenewDelegateTypeName =
            "Licensing.Components.Public.Delegates.RenewCallback";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Method Name Constants
        /// <summary>
        /// The method name used to invoke the license manager certificate
        /// summary (i.e. "about") subsystem (via reflection).
        /// </summary>
        private const string AboutMethodName = "AboutCertificate";

        /// <summary>
        /// The method name used to invoke the license manager certificate
        /// cache subsystem (via reflection).
        /// </summary>
        private const string GetMethodName = "GetCertificate";

        /// <summary>
        /// The method name for the [default] certificate renewal callback
        /// contained in the primary license manager component.
        /// </summary>
        private const string RenewMethodName = "RenewCertificate";

        /// <summary>
        /// The method name used to invoke the license manager certificate
        /// verification subsystem (via reflection).
        /// </summary>
        private const string VerifyMethodName = "VerifyCertificate";

        /// <summary>
        /// The method name used to invoke the license manager certificate
        /// flag checking subsystem (via reflection).
        /// </summary>
        private const string MatchFlagsMethodName = "MatchCertificateFlags";

        /// <summary>
        /// The method name used to invoke the license manager "raw signed"
        /// script file evaluation subsystem (via reflection).
        /// </summary>
        private const string EvaluateFileMethodName = "EvaluateFile";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) Binding Flag Constants
        /// <summary>
        /// The common binding flags used when invoking or querying the
        /// license manager methods via reflection.
        /// </summary>
        private const BindingFlags methodBindingFlags =
            BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// The binding flags used when invoking the license manager
        /// certificate summary (i.e. "about") subsystem via reflection.
        /// </summary>
        private const BindingFlags aboutMethodBindingFlags =
            methodBindingFlags | BindingFlags.InvokeMethod;

        /// <summary>
        /// The binding flags used when invoking the license manager
        /// certificate cache subsystem via reflection.
        /// </summary>
        private const BindingFlags getMethodBindingFlags =
            methodBindingFlags | BindingFlags.InvokeMethod;

        /// <summary>
        /// The binding flags used when querying for the [default] certificate
        /// renewal callback via reflection.
        /// </summary>
        private const BindingFlags renewMethodBindingFlags =
            methodBindingFlags;

        /// <summary>
        /// The binding flags used when invoking the license manager
        /// certificate verification subsystem via reflection.
        /// </summary>
        private const BindingFlags verifyMethodBindingFlags =
            methodBindingFlags | BindingFlags.InvokeMethod;

        /// <summary>
        /// The binding flags used when invoking the license manager
        /// certificate flag checking subsystem via reflection.
        /// </summary>
        private const BindingFlags matchFlagsMethodBindingFlags =
            methodBindingFlags | BindingFlags.InvokeMethod;

        /// <summary>
        /// The binding flags used when invoking the license manager "raw
        /// signed" script file evaluation subsystem via reflection.
        /// </summary>
        private const BindingFlags evaluateFileMethodBindingFlags =
            methodBindingFlags | BindingFlags.InvokeMethod;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Opaque Object Handle Constants
        /// <summary>
        /// This is the object option type used when adding temporary objects
        /// for use with the license manager plugin.
        /// </summary>
        private const ObjectOptionType DefaultObjectOptionType =
            ObjectOptionType.Default;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the object flags used when adding temporary objects for
        /// use with the license manager plugin.
        /// </summary>
        private const ObjectFlags DefaultObjectFlags = ObjectFlags.Default |
            ObjectFlags.NoBinder | ObjectFlags.NoDispose;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Manager / Library (SDK) String Constants
        /// <summary>
        /// This name to be used when requesting the string representation of
        /// the certificate from the license manager plugin.
        /// </summary>
        private const string ManagerCertificateStringName =
            "pluginCertificate";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the certificate property that indicates the type of
        /// entity.
        /// </summary>
        private const string KindPropertyName = "Kind";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The expected value of the certificate property that indicates the
        /// type of entity.
        /// </summary>
        private const string KindPropertyValue = "Certificate";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cached Interpreter Constants
        /// <summary>
        /// These are the type flags for the interpreter created and used by
        /// this class.
        /// </summary>
        private const InterpreterType SdkInterpreterType =
            InterpreterType.Eagle | InterpreterType.Token;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Tracing Subsystem Integration Constants
        /// <summary>
        /// The environment variable name (or suffix) to be checked when
        /// figuring out if the tracing subsystem should be enabled in a
        /// forcible way.
        /// </summary>
        private const string ForceEnableTraceEnvVarName = "ForceEnableTrace";

        /// <summary>
        /// If this environment variable is set (to anything), any existing
        /// <see cref="Trace.Listeners" /> will be preserved during setup of
        /// the Harpy SDK trace listeners.
        /// </summary>
        private const string PreserveTraceEnvVarName = "HarpyPreserveTrace";

        /// <summary>
        /// The environment variable name (or suffix) to be checked when
        /// figuring out if the tracing subsystem should capture all its
        /// enabled output to a log file.
        /// </summary>
        private const string ForceEnableTraceLogFileEnvVarName =
            "ForceEnableTraceLogFile";

        /// <summary>
        /// This is the format string used to build the trace log file name.
        /// The inserted parameters are the process identifier, the
        /// application domain identifier, and the file extension.
        /// </summary>
        private const string TraceLogFileNameFormat =
            "HarpyLicensingSdk_{0}_{1}{2}";

        /// <summary>
        /// The name of the datum within the IAnyClientData that should
        /// contain the tracing log file name.
        /// </summary>
        private const string TraceLogFileDataName = "TraceLogFileName";

        /// <summary>
        /// The name of the datum within the IAnyClientData that should
        /// contain the trace listener name (i.e. not the log file name).
        /// </summary>
        private const string TraceLogDataName = "TraceLogName";
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// This is used to synchronize access to the static data managed by
        /// this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        /// <summary>
        /// The index of the selected simple name for the assembly containing
        /// the license manager components, if any.
        /// </summary>
        private static int ManagerAssemblyNameIndex = Index.Invalid;

        /// <summary>
        /// The index of the selected simple name for the assembly containing
        /// the script certificate manager components, if any.
        /// </summary>
        private static int LibraryAssemblyNameIndex = Index.Invalid;

        /// <summary>
        /// This is the lookup token for the interpreter that may be created
        /// by the "EvaluateFile" methods of this SDK.
        /// </summary>
        private static ulong? EvaluateInterpreterToken = null;

        /// <summary>
        /// This is the lookup token for the interpreter that may be created
        /// by the "VerifyCertificate" methods of this SDK.
        /// </summary>
        private static ulong? VerifyInterpreterToken = null;

        /// <summary>
        /// This will contain a random wide integer used to create obfuscated
        /// command names for use by scripts evaluated in this SDK.
        /// </summary>
        private static ulong? SdkCommandId = null;

        /// <summary>
        /// This is used to make sure that the tracing subsystem is setup one
        /// time (per application domain). This will only be incremented in
        /// "automatic" mode of the <see cref="SetupTraceSubsystem" /> method.
        /// </summary>
        private static int TraceSubsystemEnableCount = 0;

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
        /// <summary>
        /// This is used to make sure that the compile-time define constants
        /// are only checked once.
        /// </summary>
        private static int DefineConstantsCount = 0;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Cached Interpreter Helper Methods
        /// <summary>
        /// Used to obtain the cached interpreter token for use by the
        /// (configuration) script evaluation subsystem.
        /// </summary>
        /// <returns>
        /// The cached interpreter token for use by the (configuration) script
        /// evaluation subsystem -OR- null if it cannot be determined.
        /// </returns>
        private static ulong? GetInterpreterForEvaluate()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                ulong? interpreterToken = EvaluateInterpreterToken;

#if DEBUG || FORCE_TRACE
                if (interpreterToken != null)
                {
                    DebugTrace(
                        "GetInterpreterForEvaluate: Found an interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
                }
                else
                {
                    DebugTrace(
                        "GetInterpreterForEvaluate: Need a new interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
                }
#endif

                return interpreterToken;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cached interpreter token used by the (configuration)
        /// script evaluation subsystem.
        /// </summary>
        /// <param name="interpreterToken">
        /// The new value for the cached interpreter token.
        /// </param>
        /// <returns>
        /// Non-zero if the cached interpreter token was set; otherwise, zero.
        /// </returns>
        private static bool SetInterpreterForEvaluate(
            ulong? interpreterToken /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreterToken != null)
                {
                    EvaluateInterpreterToken = interpreterToken;
                    // SdkCommandId = null; /* REDUNDANT? */

#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "SetInterpreterForEvaluate: Saved interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif

                    return true;
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the cached interpreter token used by the certificate
        /// verification subsystem to null.
        /// </summary>
        private static void ResetInterpreterForEvaluate()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                EvaluateInterpreterToken = null;
                // SdkCommandId = null; /* REDUNDANT? */

#if DEBUG || FORCE_TRACE
                DebugTrace(
                    "ResetInterpreterForEvaluate: Reset interpreter.",
                    typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Used to obtain the cached interpreter token for use by the
        /// certificate verification subsystem.
        /// </summary>
        /// <returns>
        /// The cached interpreter token for use by the certificate subsystem
        /// verification -OR- null if it cannot be determined.
        /// </returns>
        private static ulong? GetInterpreterForVerify()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                ulong? interpreterToken = VerifyInterpreterToken;

#if DEBUG || FORCE_TRACE
                if (interpreterToken != null)
                {
                    DebugTrace(
                        "GetInterpreterForVerify: Found an interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
                }
                else
                {
                    DebugTrace(
                        "GetInterpreterForVerify: Need a new interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
                }
#endif

                return interpreterToken;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cached interpreter token used by the certificate
        /// verification subsystem.
        /// </summary>
        /// <param name="interpreterToken">
        /// The new value for the cached interpreter token.
        /// </param>
        /// <returns>
        /// Non-zero if the cached interpreter token was set; otherwise, zero.
        /// </returns>
        private static bool SetInterpreterForVerify(
            ulong? interpreterToken /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreterToken != null)
                {
                    VerifyInterpreterToken = interpreterToken;
                    SdkCommandId = null; /* REDUNDANT? */

#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "SetInterpreterForVerify: Saved interpreter.",
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif

                    return true;
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the cached interpreter token used by the certificate
        /// verification subsystem to null.
        /// </summary>
        private static void ResetInterpreterForVerify()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                VerifyInterpreterToken = null;
                SdkCommandId = null; /* CHILD */

#if DEBUG || FORCE_TRACE
                DebugTrace(
                    "ResetInterpreterForVerify: Reset interpreter.",
                    typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds an exit handler for the current application domain that will
        /// cleanup the interpreter created and used by this class.
        /// </summary>
        private static void MaybeAddExitedEventHandlers()
        {
            AppDomain appDomain = AppDomain.CurrentDomain;

            if (appDomain == null)
                return;

            if (appDomain.IsDefaultAppDomain())
            {
                appDomain.ProcessExit -= CleanupInterpreterForEvaluate;
                appDomain.ProcessExit += CleanupInterpreterForEvaluate;

                appDomain.ProcessExit -= CleanupInterpreterForVerify;
                appDomain.ProcessExit += CleanupInterpreterForVerify;
            }
            else
            {
                appDomain.DomainUnload -= CleanupInterpreterForEvaluate;
                appDomain.DomainUnload += CleanupInterpreterForEvaluate;

                appDomain.DomainUnload -= CleanupInterpreterForVerify;
                appDomain.DomainUnload += CleanupInterpreterForVerify;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes an exit handler for the current application domain that
        /// will cleanup the interpreter created and used by this class.
        /// </summary>
        /// <param name="force">
        /// Non-zero to ignore the cached interpreter tokens and forcibly
        /// remove the associated event handlers.
        /// </param>
        private static void MaybeRemoveExitedEventHandlers(
            bool force /* in */
            )
        {
            AppDomain appDomain = AppDomain.CurrentDomain;

            if (appDomain == null)
                return;

            if (force || (EvaluateInterpreterToken == null))
            {
                if (appDomain.IsDefaultAppDomain())
                    appDomain.ProcessExit -= CleanupInterpreterForEvaluate;
                else
                    appDomain.DomainUnload -= CleanupInterpreterForEvaluate;
            }

            if (force || (VerifyInterpreterToken == null))
            {
                if (appDomain.IsDefaultAppDomain())
                    appDomain.ProcessExit -= CleanupInterpreterForVerify;
                else
                    appDomain.DomainUnload -= CleanupInterpreterForVerify;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to cleanup a cached interpreter created and used by this
        /// class.
        /// </summary>
        /// <param name="sender">
        /// The source of the event. This parameter is not used.
        /// </param>
        /// <param name="e">
        /// An <see cref="EventArgs" /> that contains event data. This
        /// parameter is not used.
        /// </param>
        private static void CleanupInterpreterForEvaluate(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            try
            {
                Result error = null;

                if (PrivateCleanup(
                        EvaluateInterpreterToken, SdkInterpreterType,
                        ref error) == ReturnCode.Ok)
                {
                    /* NO RESULT */
                    ResetInterpreterForEvaluate();
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    DebugTrace(String.Format(
                        "CleanupInterpreterForEvaluate: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(LicenseOps).Name,
                        TracePriority.Highest);
#endif
                }
            }
            finally
            {
                /* NO RESULT */
                MaybeRemoveExitedEventHandlers(false);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to cleanup a cached interpreter created and used by this
        /// class.
        /// </summary>
        /// <param name="sender">
        /// The source of the event. This parameter is not used.
        /// </param>
        /// <param name="e">
        /// An <see cref="EventArgs" /> that contains event data. This
        /// parameter is not used.
        /// </param>
        private static void CleanupInterpreterForVerify(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            try
            {
                Result error = null;

                if (PrivateCleanup(
                        VerifyInterpreterToken, SdkInterpreterType,
                        ref error) == ReturnCode.Ok)
                {
                    /* NO RESULT */
                    ResetInterpreterForVerify();
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    DebugTrace(String.Format(
                        "CleanupInterpreterForVerify: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(LicenseOps).Name,
                        TracePriority.Highest);
#endif
                }
            }
            finally
            {
                /* NO RESULT */
                MaybeRemoveExitedEventHandlers(false);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Auxiliary Helper Methods
#if DEBUG || FORCE_TRACE
        /// <summary>
        /// Issues a message-based diagnostic trace to the configured
        /// listeners, if any.
        /// </summary>
        /// <param name="message">
        /// The message to issue.
        /// </param>
        /// <param name="category">
        /// The name of the category for the message.
        /// </param>
        /// <param name="priority">
        /// A set of zero or more priority flags for the message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DebugTrace(
            string message,        /* in */
            string category,       /* in */
            TracePriority priority /* in */
            )
        {
            TracePriority sdkPriority = TracePriority.FromSdk;

            Utility.DebugTrace(
                null, message, category, priority | sdkPriority, 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Issues an exception-based diagnostic trace to the configured
        /// listeners, if any.
        /// </summary>
        /// <param name="exception">
        /// The <see cref="Exception" /> being caught.
        /// </param>
        /// <param name="category">
        /// The name of the category for the exception.
        /// </param>
        /// <param name="priority">
        /// A set of zero or more priority flags for the exception.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DebugTrace(
            Exception exception,   /* in */
            string category,       /* in */
            TracePriority priority /* in */
            )
        {
            TracePriority sdkPriority = TracePriority.FromSdk;

            Utility.DebugTrace(
                null, exception, category, priority | sdkPriority, 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method performs a series of sanity checks to make sure the
        /// target framework for the project matches the current assembly.
        /// </summary>
        private static void CheckDefineConstants()
        {
            if (Interlocked.Increment(ref DefineConstantsCount) == 1)
            {
                StringList options = DefineConstants.OptionList;

                if (options == null)
                    return;

                options = new StringList(options);

                Result error = null;

                if (Utility.CheckDefineConstants(
                        options, ref error) != ReturnCode.Ok)
                {
                    DebugTrace(String.Format(
                        "CheckDefineConstants: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(LicenseOps).Name,
                        TracePriority.Highest);
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Figures out the maximum number of times that internal operations
        /// should be retried before giving up and returning an error to the
        /// caller.
        /// </summary>
        /// <returns>
        /// The maximum number of times that internal operations should be
        /// retried before giving up and returning an error to the caller.
        /// </returns>
        private static int GetMaximumRetries()
        {
            string value = Utility.GetEnvironmentVariable(
                MaximumRetriesEnvVarName);

            if (!String.IsNullOrEmpty(value))
            {
                int intValue = 0;

                if (Value.GetInteger2(
                        value, ValueFlags.AnyInteger, null,
                        ref intValue) == ReturnCode.Ok)
                {
                    //
                    // TODO: Maybe provide some extra sanity
                    //       checking for this value before
                    //       returning it?
                    //
                    return intValue;
                }
            }

            return MaximumRetries; /* COMPAT: Harpy Beta. */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Arranges for the tracing subsystem to be (re-)initialized while
        /// maximizing its useful output and making sure it is written to a
        /// log file if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to change the
        /// tracing subsystem configuration, if any.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable output generation from the tracing subsystem
        /// -OR- zero to disable its output. This parameter may be null. If
        /// this parameter is null, it will cause the tracing subsystem to be
        /// initialized once per application domain.
        /// </param>
        /// <returns>
        /// Non-zero if tracing subsystem was successfully setup; otherwise,
        /// zero.
        /// </returns>
        private static bool MaybeSetupTraceSubsystem(
            Interpreter interpreter, /* in */
            bool? enable             /* in */
            )
        {
            Result result = null;

            if (MaybeSetupTraceSubsystem(
                    interpreter, null, enable, false, ref result))
            {
                return true;
            }

#if DEBUG || FORCE_TRACE
            DebugTrace(String.Format(
                "MaybeSetupTraceSubsystem: result = {0}",
                Utility.FormatWrapOrNull(result)),
                typeof(LicenseOps).Name, TracePriority.Highest);
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Arranges for the tracing subsystem to be (re-)initialized while
        /// maximizing its useful output and making sure it is written to a
        /// log file if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to change the
        /// tracing subsystem configuration, if any.
        /// </param>
        /// <param name="priorities">
        /// Either null or the mask of trace priorities to enable / disable.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable output generation from the tracing subsystem
        /// -OR- zero to disable its output. This parameter may be null. If
        /// this parameter is null, it will cause the tracing subsystem to be
        /// initialized once per application domain.
        /// </param>
        /// <param name="logFile">
        /// Non-zero to configure a log file to receive messages from the
        /// tracing subsystem.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the fully qualified path to the
        /// log file, if applicable; otherwise, the contents are undefined.
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// Non-zero if tracing subsystem was successfully setup; otherwise,
        /// zero.
        /// </returns>
        public static bool MaybeSetupTraceSubsystem(
            Interpreter interpreter,   /* in */
            TracePriority? priorities, /* in */
            bool? enable,              /* in */
            bool logFile,              /* in */
            ref Result result          /* out */
            )
        {
            bool success = false;

            if (enable != null)
            {
                result = null;

                if (SetupTraceSubsystem(
                        interpreter, priorities, (bool)enable,
                        logFile, ref result) == ReturnCode.Ok)
                {
                    success = true;
                }
            }
            else
            {
                try
                {
                    if (Interlocked.Increment(
                            ref TraceSubsystemEnableCount) == 1)
                    {
                        result = null;

                        if (SetupTraceSubsystem(
                                interpreter, priorities, true, logFile,
                                ref result) == ReturnCode.Ok)
                        {
                            success = true;
                        }
                    }
                    else
                    {
                        result = "trace subsystem already setup";
                    }
                }
                finally
                {
                    if (!success)
                    {
                        Interlocked.Decrement(
                            ref TraceSubsystemEnableCount);
                    }
                }
            }

            return success;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Arranges for the tracing subsystem to be (re-)initialized while
        /// maximizing its useful output and making sure it is written to a
        /// log file if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to change the
        /// tracing subsystem configuration, if any.
        /// </param>
        /// <param name="priorities">
        /// Either null or the mask of trace priorities to enable / disable.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable output generation from the tracing subsystem
        /// -OR- zero to disable its output.
        /// </param>
        /// <param name="logFile">
        /// Non-zero to configure a log file to receive messages from the
        /// tracing subsystem.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the fully qualified path to the
        /// log file, if applicable; otherwise, the contents are undefined.
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode SetupTraceSubsystem(
            Interpreter interpreter,   /* in */
            TracePriority? priorities, /* in */
            bool enable,               /* in */
            bool logFile,              /* in */
            ref Result result          /* out */
            )
        {
            TraceClientData traceClientData = new TraceClientData();

            traceClientData.Interpreter = interpreter;
            traceClientData.Listeners = Trace.Listeners;

            traceClientData.StateType = enable ?
                TraceStateType.SdkEnableMask :
                TraceStateType.SdkDisableMask;

            traceClientData.Priorities = priorities;
            traceClientData.ForceEnabled = enable;
            traceClientData.ResetSystem = true;
            traceClientData.ResetListeners = true;
            traceClientData.UseNative = enable;

            string logFileName = null;

            if (enable && (logFile || Utility.DoesEnvironmentVariableExist(
                    ForceEnableTraceLogFileEnvVarName)))
            {
                string logFileNameOnly = String.Format(
                    TraceLogFileNameFormat, Utility.GetCurrentProcessId(),
                    Utility.GetCurrentAppDomainId(), FileExtension.Log);

                logFileName = Path.Combine(
                    Utility.GetTempPath(null), logFileNameOnly);

                string logName = logFileNameOnly;

                IAnyClientData anyClientData = new AnyClientData();

                if (!anyClientData.TrySetAny(
                        TraceLogFileDataName, logFileName))
                {
                    result = "could not set trace log file name";
                    return ReturnCode.Error;
                }

                if (!anyClientData.TrySetAny(
                        TraceLogDataName, logName))
                {
                    result = "could not set trace log name";
                    return ReturnCode.Error;
                }

                traceClientData.ClientData = anyClientData;
                traceClientData.RawLogFile = true;
            }

            IList<TraceListener> savedListeners = null;

            try
            {
                if (Utility.DoesEnvironmentVariableExist(
                        PreserveTraceEnvVarName))
                {
                    TraceListenerCollection listeners = Trace.Listeners;

                    if (listeners != null)
                    {
                        savedListeners = new List<TraceListener>();

                        foreach (TraceListener listener in listeners)
                        {
                            if (listener == null)
                                continue;

                            savedListeners.Add(listener);
                        }

                        if (savedListeners.Count == 0)
                            savedListeners = null;
                    }
                }

                Result localResult = null; /* REUSED */

                if (Utility.ProcessTraceClientData(
                        traceClientData, ref localResult) == ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "Utility.ProcessTraceClientData COMPLETED.",
                        typeof(LicenseOps).Name, TracePriority.Highest);
#endif
                }
                else
                {
                    result = localResult;
                    return ReturnCode.Error;
                }

                if ((interpreter != null) &&
                    Utility.IsTransparentProxy(interpreter))
                {
                    localResult = null;

                    if (interpreter.ProcessTraceClientData(
                            traceClientData, ref localResult) == ReturnCode.Ok)
                    {
#if DEBUG || FORCE_TRACE
                        DebugTrace(
                            "Interpreter.ProcessTraceClientData COMPLETED.",
                            typeof(LicenseOps).Name, TracePriority.Highest);
#endif
                    }
                    else
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }
                }
            }
            finally
            {
                if (savedListeners != null)
                {
                    foreach (TraceListener listener in savedListeners)
                    {
                        if (listener == null)
                            continue;

                        Trace.Listeners.Add(listener);
                    }
                }
            }

            if (logFileName != null)
                result = logFileName;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the "well-known" configuration data within the AppDomain,
        /// e.g. encryption parameters used to decrypt license certificates
        /// and/or configuration files.
        /// </summary>
        /// <param name="appDomain">
        /// Application domain context where the "well-known" configuration
        /// data should be setup.
        /// </param>
        public static void SetupWellKnownConfigurationData(
            AppDomain appDomain /* in */
            )
        {
            if (appDomain != null)
            {
                //
                // WARNING: DO NOT REMOVE any of the salt / password
                //          pairs listed here without prior approval
                //          from one of the project owners.
                //
                // NOTE: All of these strings should end up being
                //       obfuscated when this assembly is compiled
                //       for release (via whatever configured code
                //       obfuscation tool is in use).
                //
                SaltAndPasswordPair[] anyPairs = {
                    /* Mistachkin Solutions LLC */
                    /* System.Data.SQLite with SQLite Encryption Extension */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "21499d79-e39e-4c6e-8b8e-24794863f11b"),
                        "EB6C883120F3D5336F99C34DF2FB4863"),
                    /* Hipp, Wyrick, & Company, Inc */
                    /* System.Data.SQLite with SQLite Encryption Extension */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "6cbf2e98-2cdc-4c20-9f0a-d15327531a83"),
                        "8FFDCDCFF172FF3D1537F8E6F332455B"),
                    /* Eagle Development Team */
                    /* FOR ENGINEERING USE ONLY: "certificate.exml", */
                    /* is shared by all the official Harpy plugins, */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "c3e6f922-5b39-4a8b-b43c-18c74f00571b"),
                        "36EEAAFE585DCF682D06A3EC02C23589"),
                    /* NO NAME */
                    /* FOR CORE LIBRARY USE ONLY: "certificate.exml", */
                    /* is shared by all the official Harpy plugins, */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "a30a1ea5-33fc-499b-a615-93e273ff8abb"),
                        "1DEACD249D1C97CC9EB8B53E0012A827"),
                    /* "Harpy.v1.NuGet.eeagle" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "c7925879-0606-442d-9989-b2e12f44d217"),
                        "5E4F6BCE074E78C0AEB0838F109950BE"),
                    /* Shared with LicenseOps SDK */
                    /* "*.v1.*.eeagle" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "0d22343f-b7d4-4de4-b616-61d2c65fe50f"),
                        "81EF79920647EEE0134DA28BDAFEF107")
                };

                foreach (SaltAndPasswordPair anyPair in anyPairs)
                {
                    if (anyPair == null)
                        continue;

                    appDomain.SetData(String.Format(
                        GetDataFormat, anyPair.X.ToString(),
                        Utility.GetCurrentProcessId()), anyPair.Y);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks if any license manager package from any license manager
        /// plugin may be used for the specified type (i.e. skip forcibly
        /// evaluating the manager load script). This does not currently apply
        /// when using isolated mode.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name to be used as the basis for figuring out if any
        /// license manager package from any license manager plugin may be
        /// used. This parameter may be null.
        /// </param>
        /// <returns>
        /// Returns non-zero if any license manager package can be used.
        /// </returns>
        private static bool UseAnyPackage(
            AssemblyName assemblyName /* in */
            )
        {
            //
            // NOTE: First, check for the "global" environment variable.
            //
            if (Utility.DoesEnvironmentVariableExist(UseAnyPackageEnvVarName))
                return true;

            //
            // NOTE: Next, check for the license manager SDK environment
            //       variable.
            //
            if (Utility.DoesEnvironmentVariableExist(String.Format(
                    "{0}{1}{2}", ManagerTypeName, Characters.Underscore,
                    UseAnyPackageEnvVarName)))
            {
                return true;
            }

            //
            // NOTE: Finally, see if the specified assembly name, if any,
            //       matches one of the library assembly names.
            //
            return IsLibraryAssemblyName(assemblyName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the simple name of an assembly found in the application
        /// domain, if any.
        /// </summary>
        /// <param name="names">
        /// The list of candidate simple names for the assembly.
        /// </param>
        /// <param name="pattern">
        /// The pattern to return if the assembly cannot be found within the
        /// current application domain.
        /// </param>
        /// <param name="index">
        /// The location where the integer index of the selected simple name
        /// should be stored.
        /// </param>
        /// <returns>
        /// The simple name of the assembly -OR- null if it cannot be found.
        /// </returns>
        private static string GetAssemblyName(
            string[] names, /* in */
            string pattern, /* in */
            ref int index   /* in, out */
            )
        {
            if (names == null)
                return pattern;

            int length = names.Length;
            int localIndex; /* REUSED */

            lock (syncRoot) /* TRANSACTIONAL */
            {
                localIndex = index;

                if ((localIndex >= 0) && (localIndex < length))
                    return names[localIndex];
            }

            Assembly assembly = null; /* NOT USED */
            Result error = null; /* NOT USED */

            localIndex = Index.Invalid;

            if (FindAssembly(
                    names, SdkAllowAssemblyNameOnly, ref localIndex,
                    ref assembly, ref error) == ReturnCode.Ok)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if ((localIndex >= 0) && (localIndex < length))
                    {
                        index = localIndex;

                        return names[localIndex];
                    }
                }
            }

            return pattern;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the simple name of the assembly containing the license
        /// manager components, if any.
        /// </summary>
        /// <param name="fallback">
        /// Non-zero to return a (glob) pattern when the assembly cannot be
        /// found within the current application domain.
        /// </param>
        /// <returns>
        /// See above.
        /// </returns>
        private static string GetManagerAssemblyName(
            bool fallback /* in */
            )
        {
            return GetAssemblyName(
                ManagerAssemblyNames, fallback ? ManagerAssemblyPattern :
                null, ref ManagerAssemblyNameIndex);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the simple name of the assembly containing the script
        /// certificate manager components, if any.
        /// </summary>
        /// <param name="fallback">
        /// Non-zero to return a (glob) pattern when the assembly cannot be
        /// found within the current application domain.
        /// </param>
        /// <returns>
        /// See above.
        /// </returns>
        private static string GetLibraryAssemblyName(
            bool fallback /* in */
            )
        {
            return GetAssemblyName(
                LibraryAssemblyNames, fallback ? LibraryAssemblyPattern :
                null, ref LibraryAssemblyNameIndex);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns non-zero if the specified assembly name matches one of the
        /// library assembly names. If there are no library assembly names, a
        /// value of zero will be returned.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name to check. If this parameter is null, false will
        /// always be returned.
        /// </param>
        /// <returns>
        /// See above.
        /// </returns>
        private static bool IsLibraryAssemblyName(
            AssemblyName assemblyName /* in */
            )
        {
            if ((assemblyName == null) || (LibraryAssemblyNames == null))
                return false;

            StringComparison comparisonType = Utility.GetPathComparisonType();
            string name = assemblyName.Name;

            foreach (string libraryAssemblyName in LibraryAssemblyNames)
            {
                if (Utility.StringEquals(
                        libraryAssemblyName, name, comparisonType))
                {
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine which assembly, in the current application
        /// domain, contains the specified named resource stream, if any.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the embedded
        /// resource stream. This parameter may be null. This assembly is
        /// always checked first.
        /// </param>
        /// <param name="name">
        /// The exact name of the embedded resource stream being sought.
        /// </param>
        /// <returns>
        /// Either the assembly within the current application domain that
        /// contains the specified named resource -OR- the original value of
        /// the <paramref name="assembly" /> parameter. The returned value may
        /// be null. No return value is reserved to indicate an error.
        /// </returns>
        private static Assembly MaybeFindStream(
            Assembly assembly, /* in */
            string name        /* in */
            )
        {
            Assembly localAssembly;
            Result error = null; /* NOT USED */

            localAssembly = Utility.FindStream(
                assembly, name, false, ref error);

            return (localAssembly != null) ?
                localAssembly : assembly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine if the specified file name actually refers
        /// to an embedded resource within the specified assembly.
        /// </summary>
        /// <param name="fileName">
        /// The certificate file name as specified in the currently executing
        /// call to the "VerifyCertificate" method.
        /// </param>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the embedded
        /// resource stream.
        /// </param>
        /// <param name="resourceName">
        /// Upon success, receives the name of the embedded resource that the
        /// file name maps to.
        /// </param>
        /// <returns>
        /// Non-zero if the file name probably refers to an embedded resource
        /// within the specified assembly; otherwise, zero.
        /// </returns>
        private static bool IsResourceFileName(
            string fileName,        /* in */
            ref Assembly assembly,  /* in, out */
            ref string resourceName /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            fileName = Utility.ExpandEnvironmentVariables(fileName);

            if (String.IsNullOrEmpty(fileName))
                return false;

            if (Path.IsPathRooted(fileName))
                return false;

            string directory;

            try
            {
                directory = Path.GetDirectoryName(fileName); /* throw */
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                DebugTrace(
                    e, typeof(LicenseOps).Name,
                    TracePriority.Higher);
#endif

                return false;
            }

            if (!String.IsNullOrEmpty(directory))
                return false;

            assembly = MaybeFindStream(assembly, fileName);
            resourceName = fileName;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to locate a certificate on the file system, based on the
        /// location of the specified assembly and the candidate name of its
        /// embedded (certificate) resource.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the embedded
        /// certificate.
        /// </param>
        /// <param name="resourceName">
        /// This must be null -OR- the name of the embedded resource within
        /// <paramref name="assembly" /> to extract and forcibly use as the
        /// certificate for the license manager itself.
        /// </param>
        /// <returns>
        /// Non-zero if a suitable certificate file was found -OR- zero if
        /// not.
        /// </returns>
        private static bool SearchCertificate(
            Assembly assembly,  /* in */
            string resourceName /* in */
            )
        {
            if ((assembly != null) &&
                !String.IsNullOrEmpty(resourceName))
            {
                string assemblyDirectory = null;
                string bareResourceName = null;

                try
                {
                    assemblyDirectory = Path.GetDirectoryName(
                        assembly.Location); /* throw */

                    bareResourceName = Path.GetFileNameWithoutExtension(
                        resourceName); /* throw */
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
                {
                    DebugTrace(
                        e, typeof(LicenseOps).Name,
                        TracePriority.Higher);
                }
#else
                catch
                {
                    // do nothing.
                }
#endif

                if (!String.IsNullOrEmpty(assemblyDirectory) &&
                    Directory.Exists(assemblyDirectory))
                {
                    foreach (string localResourceName in new string[] {
                            resourceName,
                            (bareResourceName != null) ?
                                String.Format(
                                    ManagerResourceFileNameFormat,
                                    bareResourceName,
                                    FileExtension.EncryptedMarkup) :
                                null,
                            (bareResourceName != null) ?
                                String.Format(
                                    ManagerResourceFileNameFormat,
                                    bareResourceName,
                                    FileExtension.Markup) :
                                null,
                            bareResourceName
                        })
                    {
                        if (String.IsNullOrEmpty(localResourceName))
                            continue;

                        string assemblyFileName = Path.Combine(
                            assemblyDirectory, localResourceName);

                        if (!String.IsNullOrEmpty(assemblyFileName) &&
                            File.Exists(assemblyFileName))
                        {
                            Environment.SetEnvironmentVariable(
                                ManagerOverrideEnvVarName,
                                assemblyFileName);

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method attempts to extract a certificate from the specified
        /// assembly resource and write it to a temporary file.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the embedded
        /// certificate.
        /// </param>
        /// <param name="resourceName">
        /// This must be null -OR- the name of the embedded resource within
        /// <paramref name="assembly" /> to extract and forcibly use as the
        /// certificate for the license manager itself.
        /// </param>
        /// <param name="temporaryDirectory">
        /// The name of the temporary directory that should be cleaned up
        /// after the temporary certificate is no longer needed.
        /// </param>
        /// <param name="temporaryFileName">
        /// The name of the temporary file containing the extracted
        /// certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode ExtractCertificate(
            Assembly assembly,             /* in */
            string resourceName,           /* in */
            out string temporaryDirectory, /* out */
            out string temporaryFileName,  /* out */
            ref Result error               /* out */
            )
        {
            temporaryDirectory = null;
            temporaryFileName = null;

            if (resourceName == null)
                return ReturnCode.Ok;

            try
            {
                byte[] resourceBytes = Utility.GetResourceStreamData(
                    assembly, resourceName, true, ref error) as byte[];

                if (resourceBytes == null)
                    return ReturnCode.Error;

                temporaryDirectory = Utility.GetUniquePath(
                    null, Utility.GetTempPath(null), null, null,
                    ref error);

                if (temporaryDirectory == null)
                    return ReturnCode.Error;

                Directory.CreateDirectory(temporaryDirectory); /* throw */

                temporaryFileName = Path.Combine(
                    temporaryDirectory, resourceName);

                File.WriteAllBytes(
                    temporaryFileName, resourceBytes); /* throw */

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Auxiliary Helper Methods
        /// <summary>
        /// Returns the name of the directory that [directly] contains the
        /// assembly containing all the license manager components, if any.
        /// </summary>
        /// <param name="fallback">
        /// Non-zero to return a (glob) pattern when the assembly cannot be
        /// found within the current application domain.
        /// </param>
        /// <returns>
        /// See above.
        /// </returns>
        public static string GetManagerPackageDirectoryName(
            bool fallback /* in */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            string name = GetManagerAssemblyName(fallback);

            if (name == null)
                return null;

            name = name.Trim(Characters.Asterisk);

            return String.Format("{0}{1}", name, SdkVersion);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the name of the directory that [directly] contains the
        /// assembly containing all the script certificate manager components
        /// (i.e. the script certificates for the Eagle core standard
        /// library), if any.
        /// </summary>
        /// <param name="fallback">
        /// Non-zero to return a (glob) pattern when the assembly cannot be
        /// found within the current application domain.
        /// </param>
        /// <returns>
        /// See above.
        /// </returns>
        public static string GetLibraryPackageDirectoryName(
            bool fallback /* in */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            string name = GetLibraryAssemblyName(fallback);

            if (name == null)
                return null;

            name = name.Trim(Characters.Asterisk);

            return String.Format("{0}{1}", name, SdkVersion);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Manager Helper Methods
        /// <summary>
        /// Attempts to cleanup a cached interpreter created and used by this
        /// class.
        /// </summary>
        /// <param name="token">
        /// The optional lookup token for the interpreter to cleanup. If this
        /// parameter is null, nothing will be done.
        /// </param>
        /// <param name="interpreterType">
        /// The optional interpreter type for the interpreter to cleanup. If
        /// this parameter is null, nothing will be done.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode PrivateCleanup(
            ulong? token,                     /* in: OPTIONAL */
            InterpreterType? interpreterType, /* in: OPTIONAL */
            ref Result error                  /* out */
            )
        {
            if ((token == null) || (interpreterType == null))
                return ReturnCode.Ok;

            Interpreter interpreter = null;

            if (Value.GetInterpreter(
                    null, ((ulong)token).ToString(),
                    (InterpreterType)interpreterType,
                    ref interpreter, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                //
                // NOTE: If the cached interpreter is valid,
                //       first forcibly enable its disposal
                //       flag and then dispose it.
                //
                if (interpreter != null)
                {
                    interpreter.SetDisposalEnabled(
                        false, true); /* throw */

                    interpreter.Dispose(); /* throw */
                    interpreter = null;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if ISOLATED_PLUGINS
        /// <summary>
        /// Checks if the specified plugin has been loaded into an isolated
        /// application domain and returns the result.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to determine if
        /// the specified plugin resides in an application domain that is
        /// isolated from it.
        /// </param>
        /// <param name="plugin">
        /// This plugin is used to perform the isolated application domain
        /// check.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <returns>
        /// Returns non-zero if the plugin has been loaded into an isolated
        /// application domain -OR- the caller is forcing the isolated mode to
        /// be used. If the plugin is null the return value will always be
        /// false unless the caller is forcing isolated mode to be used.
        /// </returns>
        private static bool IsIsolated(
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            bool force               /* in */
            )
        {
            if (force)
                return true;

            if (plugin == null)
                return false;

            if (interpreter != null)
                return Utility.IsCrossAppDomain(interpreter, plugin);

            return Utility.IsCrossAppDomain(plugin);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes any keys from the specified dictionary that have a null
        /// -OR- empty string value.
        /// </summary>
        /// <param name="dictionary">
        /// The name/value pairs representing the certificate being operated
        /// upon.
        /// </param>
        private static void RemoveNullOrEmpty(
            CertificateDictionary dictionary /* in */
            )
        {
            //
            // HACK: Remove all keys that have values that are
            //       either null or an empty string.
            //
            if (dictionary == null)
                return;

            StringList keys = new StringList(dictionary.Keys);

            foreach (string key in keys)
            {
                if (key == null) /* IMPOSSIBLE? */
                    continue;

                string value;

                if (!dictionary.TryGetValue(key, out value))
                    continue;

                if (!String.IsNullOrEmpty(value))
                    continue;

                /* IGNORED */
                dictionary.Remove(key);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to convert an arbitrary object value to a string, using
        /// the <c>ToString</c> method of the object, if necessary.
        /// </summary>
        /// <param name="value">
        /// The object value to be converted.
        /// </param>
        /// <returns>
        /// The string corresponding to the specified object -OR- null if the
        /// object value is null.
        /// </returns>
        private static string ValueToString(
            object value /* in */
            )
        {
        retry:

            if (value == null)
                return null;

            if (value is string)
                return (string)value;

            if (value is Result)
            {
                value = ((Result)value).Value;
                goto retry;
            }

            return value.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to convert an arbitrary object value to a string or list
        /// of strings, using the ToString method of the object, if necessary.
        /// </summary>
        /// <param name="value">
        /// The object value to be converted.
        /// </param>
        /// <returns>
        /// A <see cref="Result" /> object containing the string or list that
        /// corresponds to the specified object -OR- null if the object value
        /// or the contained <see cref="Result" /> value is null.
        /// </returns>
        private static Result ValueToStringOrListResult(
            object value /* in */
            )
        {
        retry:

            if (value == null)
                return null;

            if (value is string)
                return (string)value;

            if (value is StringList)
                return (StringList)value;

            if (value is StringPairList)
                return (StringPairList)value;

            if (value is Result)
            {
                value = ((Result)value).Value;
                goto retry;
            }

            return value.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20 && !NET_STANDARD_21 && NATIVE && WINDOWS
        /// <summary>
        /// Attempts to query and return the integer identifier associated
        /// with the specified application domain.
        /// </summary>
        /// <param name="appDomain">
        /// The <see cref="_AppDomain" /> instance to query. If this instance
        /// cannot be cast to an <see cref="AppDomain" /> instance, the result
        /// of this method is undefined.
        /// </param>
        /// <returns>
        /// The integer identifier for the specified application domain -OR- a
        /// value less than zero if it cannot be determined.
        /// </returns>
        private static int GetAppDomainId(
            _AppDomain appDomain /* in */
            )
        {
            if (appDomain == null)
                return Identifier.Invalid;

            AppDomain localAppDomain = appDomain as AppDomain;

            if (localAppDomain == null)
                return Identifier.TypeMismatch;

            return localAppDomain.Id;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to locate the assembly containing the license manager
        /// components.
        /// </summary>
        /// <param name="names">
        /// The list of candidate simple names for the assembly to locate.
        /// </param>
        /// <param name="allowNameOnly">
        /// Zero if the assembly matching is to be performed strictly based on
        /// the full assembly name (i.e. the public key token must match the
        /// one hard-coded into this module exactly).
        /// </param>
        /// <param name="index">
        /// The index of the selected simple name for the assembly, if any.
        /// </param>
        /// <param name="assembly">
        /// Upon success, the assembly itself will be stored here; otherwise,
        /// the value of this parameter is undefined.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode FindAssembly(
            string[] names,        /* in */
            bool allowNameOnly,    /* in */
            ref int index,         /* out */
            ref Assembly assembly, /* out */
            ref Result error       /* out */
            )
        {
            if (names == null)
            {
                error = "invalid assembly names";
                return ReturnCode.Error;
            }

            int length = names.Length;

            for (int localIndex = 0; localIndex < length; localIndex++)
            {
                //
                // NOTE: Grab the license manager (simple) assembly name to
                //       match.
                //
                string name = names[localIndex];

                //
                // NOTE: The error message from FindAssemblyInAppDomain.  The
                //       resulting values are not currently used.
                //
                Result localError = null; /* NOT USED */

                //
                // NOTE: First, attempt to find the manager assembly loaded
                //       into the current application domain, based on its
                //       name -AND- public key token.
                //
                Assembly localAssembly = Utility.FindAssemblyInAppDomain(
                    null, name, null, SdkPublicKeyToken, ref localError);

                if (localAssembly != null)
                {
                    index = localIndex;
                    assembly = localAssembly;

                    return ReturnCode.Ok;
                }

                //
                // NOTE: Next, attempt to find the license manager assembly
                //       loaded into the current application domain, without
                //       matching the public key token.
                //
                if (allowNameOnly && (SdkPublicKeyToken != null))
                {
                    localError = null;

                    localAssembly = Utility.FindAssemblyInAppDomain(
                        null, name, null, null, ref localError);

                    if (localAssembly != null)
                    {
                        index = localIndex;
                        assembly = localAssembly;

                        return ReturnCode.Ok;
                    }
                }
            }

            error = "assembly not found in application domain";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the fully qualified path for the primary
        /// executable for the process.
        /// </summary>
        /// <param name="wasOverridden">
        /// This will be set to non-zero if the path being returned from this
        /// method was overridden via the process environment.
        /// </param>
        /// <returns>
        /// The fully qualified path for the primary executable for the
        /// process -OR- null if it cannot be determined.
        /// </returns>
        private static string GetBinaryPath(
            out bool wasOverridden /* out */
            )
        {
            string path = Utility.GetEnvironmentVariable(
                BinaryPathEnvVarName);

            if (!String.IsNullOrEmpty(path))
            {
                wasOverridden = true;
                return path;
            }

            wasOverridden = false;
            return Utility.GetBinaryPath();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the fully qualified path for the assembly
        /// containing the license manager components.
        /// </summary>
        /// <param name="directory">
        /// The base directory to search for the assembly containing the
        /// license manager components.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// The fully qualified path for the assembly containing the license
        /// manager components -OR- null if it cannot be determined.
        /// </returns>
        private static string GetManagerAssemblyPath(
            string directory, /* in */
            ref Result error  /* out */
            )
        {
            if (String.IsNullOrEmpty(directory))
            {
                error = String.Format(
                    "directory {0} is not valid",
                    Utility.FormatWrapOrNull(directory));

                return null;
            }

            try
            {
                if (!Directory.Exists(directory))
                {
                    error = String.Format(
                        "directory {0} does not exist",
                        Utility.FormatWrapOrNull(directory));

                    return null;
                }

                string pattern = String.Format("{0}{1}",
                    ManagerAssemblyPattern, FileExtension.Library);

                string[] fileNames = Directory.GetFiles(
                    directory, pattern, SearchOption.AllDirectories);

                if ((fileNames == null) || (fileNames.Length == 0))
                {
                    error = String.Format(
                        "file {0} not found under directory {1}",
                        Utility.FormatWrapOrNull(pattern),
                        Utility.FormatWrapOrNull(directory));

                    return null;
                }

                return fileNames[0];
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method attempts to manually provide the script package for
        /// the license manager components to the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to provide the
        /// package for the license manager.
        /// </param>
        /// <param name="assemblyPath">
        /// The fully qualified path to the directory for the assembly that
        /// contains the license manager components.
        /// </param>
        /// <param name="packageCommandName">
        /// The current, possibly obfuscated, name of the <c>[package]</c>
        /// command within the specified interpreter.
        /// </param>
        /// <param name="loadCommandName">
        /// The current, possibly obfuscated, name of the <c>[load]</c>
        /// command within the specified interpreter.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode ProvideManagerPackage(
            Interpreter interpreter,   /* in */
            string assemblyPath,       /* in */
            string packageCommandName, /* in */
            string loadCommandName,    /* in */
            ref Result error           /* out */
            )
        {
            //
            // HACK: This is somewhat "dangerous" in the
            //       sense that it will overwrite any
            //       existing [package ifneeded] data of
            //       the "Licensing.Core" package -AND-
            //       there is no way to override or even
            //       influence this behavior (as of beta
            //       48).  However, as of beta 49, the
            //       Harpy assembly path used here can
            //       be overridden via the environment,
            //       which helps mitigate problems that
            //       may arise from this limitation.
            //       This is especially important when
            //       the current AppDomain is based in
            //       a directory that has no relation
            //       to the core library, e.g. on IIS
            //       Express, etc.
            //
            // BUGFIX: Using String.Format here is a
            //         security issue because some of
            //         the inputs are external.
            //
            StringList provideCommand = new StringList();

            provideCommand.Add(packageCommandName);
            provideCommand.Add(IfNeededSubCommandName);
            provideCommand.Add(ManagerPackageName);
            provideCommand.Add(ManagerPackageVersion);

            StringList loadCommand = new StringList();

            loadCommand.Add(loadCommandName);

            //
            // HACK: Possibly allow plugin to load on
            //       any thread?  This should almost
            //       never be needed; however, it can
            //       be used as a last-resort.
            //
            if (Utility.DoesEnvironmentVariableExist(
                    AllowAnyThreadEnvVarName))
            {
#if DEBUG || FORCE_TRACE
                DebugTrace(String.Format(
                    "ProvideManagerPackage: Forcibly " +
                    "allowing manager plugin to load " +
                    "on any thread from {0}primary " +
                    "thread.",
                    interpreter.IsPrimaryThread() ?
                        String.Empty : "non-"),
                    typeof(LicenseOps).Name,
                    TracePriority.MediumHigh);
#endif

                loadCommand.Add(AnyThreadOptionName);
            }

            loadCommand.Add(PublicKeyTokenOptionName);

            loadCommand.Add(String.Format(
                "0x{0}", ManagerPublicKeyToken));

            loadCommand.Add(Option.EndOfOptions);
            loadCommand.Add(assemblyPath);
            loadCommand.Add(ManagerPackageName);

            provideCommand.Add(loadCommand.ToString());
            provideCommand.Add(PackageFlagsArgumentValue);

            Result result = null;

            if (interpreter.EvaluateTrustedScript(
                    provideCommand.ToString(), ManagerTrustFlags,
                    ref result) != ReturnCode.Ok)
            {
                error = result;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the current, possibly obfuscated, name of
        /// the specified command in the specified interpreter, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to determine
        /// the current, possibly obfuscated, name of the specified command,
        /// if any.
        /// </param>
        /// <param name="name">
        /// This parameter should be the original name of the command being
        /// obfuscated.
        /// </param>
        /// <returns>
        /// The current, possibly obfuscated, name of the specified command
        /// within the specified interpreter -OR- null if its name cannot be
        /// determined.
        /// </returns>
        private static string GetManagerCommandName(
            Interpreter interpreter, /* in */
            string name              /* in */
            )
        {
            bool created = false;

            try
            {
                return GetManagerCommandName(interpreter, name, out created);
            }
            finally
            {
                if (created)
                {
                    //
                    // NOTE: If we get to this point, there may be a fairly
                    //       serious internal state inconsistency.  Perhaps
                    //       this should cause a reset of the stored tokens
                    //       for the interpreter and commands?
                    //
#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "GetManagerCommandName: Not expecting to create.",
                        typeof(LicenseOps).Name, TracePriority.Highest);
#endif
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the current, possibly obfuscated, name of
        /// the specified command in the specified interpreter, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to determine
        /// the current, possibly obfuscated, name of the specified command,
        /// if any.
        /// </param>
        /// <param name="name">
        /// This parameter should be the original name of the command being
        /// obfuscated.
        /// </param>
        /// <param name="created">
        /// This parameter will be set to non-zero if the returned command
        /// name was just initialized to its initial (random) value.
        /// </param>
        /// <returns>
        /// The current, possibly obfuscated, name of the specified command
        /// within the specified interpreter -OR- null if its name cannot be
        /// determined.
        /// </returns>
        private static string GetManagerCommandName(
            Interpreter interpreter, /* in */
            string name,             /* in */
            out bool created         /* out */
            )
        {
            //
            // TODO: Why is the interpreter always considered here?  Maybe
            //       the caller should simply skip calling this method when
            //       the interpreter has an SDK bit set?
            //
            if ((interpreter == null) || !interpreter.IsLicenseSdk())
            {
                //
                // NOTE: This is a fairly rare case (i.e. in the context of
                //       this SDK; therefore, trace it.
                //
#if DEBUG || FORCE_TRACE
                DebugTrace(
                    "GetManagerCommandName: Not using obfuscated naming.",
                    typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif

                created = false;
                return name;
            }

            ulong id;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (SdkCommandId != null)
                {
                    id = (ulong)SdkCommandId;
                    created = false;
                }
                else
                {
                    SdkCommandId = id = Utility.GetRandomNumber();
                    created = true;
                }
            }

            return GetManagerCommandName(name, id);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the current, possibly obfuscated, name of
        /// the specified command in the specified interpreter, if any.
        /// </summary>
        /// <param name="name">
        /// This parameter should be the original name of the command being
        /// obfuscated.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <returns>
        /// The current, possibly obfuscated, name of the specified command
        /// within the specified interpreter -OR- null if its name cannot be
        /// determined.
        /// </returns>
        private static string GetManagerCommandName(
            string name, /* in */
            ulong id     /* in */
            )
        {
            if (id == 0)
                return name;

            return String.Format("{0}_{1:X16}", name, id);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sleeps for a while. It will sleep for a number of milliseconds
        /// that is between the minimum and double the minimum.
        /// </summary>
        private static void Sleep()
        {
            int milliseconds = 0;
            Random random = SleepRandom;

            if (random != null)
                milliseconds += random.Next(SleepMilliseconds);

            milliseconds += SleepMilliseconds;
            Thread.Sleep(milliseconds);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to load the license manager components by evaluating a
        /// script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="fileName">
        /// The file name, if any, that will be used as the location of the
        /// external certificate file to supply to the license manager plugin.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the evaluated
        /// script. Upon failure, this will contain an appropriate error
        /// message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode LoadManager(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ulong? id,               /* in */
            ref Result result        /* out */
            )
        {
            //
            // NOTE: Keep non-error final results from leaking out, use a local
            //       variable.
            //
            Result localResult; /* REUSED */

            //
            // NOTE: Next, we will need a valid interpreter context; therefore,
            //       check it and return an error if it is invalid.
            //
            if (interpreter == null)
            {
                localResult = "invalid interpreter";

#if DEBUG || FORCE_TRACE
                DebugTrace(String.Format(
                    "LoadManager: ERROR {0}",
                    Utility.FormatWrapOrNull(localResult)),
                    typeof(LicenseOps).Name,
                    TracePriority.Highest);
#endif

                result = localResult;
                return ReturnCode.Error;
            }

            //
            // NOTE: Next, fetch the optional Eagle script, via a variable,
            //       used to prepare to force the assembly containing the
            //       license manager components to load.
            //
            Result managerPreLoadScript = null;

            localResult = null; /* NOT USED */

            if (interpreter.GetVariableValue(
                    VariableFlags.None, ManagerPreLoadScriptVariableName,
                    ref managerPreLoadScript,
                    ref localResult) == ReturnCode.Ok)
            {
                //
                // NOTE: Next, evaluate the Eagle script used to prepare to
                //       force the assembly containing the license manager
                //       components to load.
                //
                // WARNING: *SECURITY* Do not use trusted script evaluation
                //          here because this script is not hard-coded.
                //
                localResult = null;

                if (interpreter.EvaluateScript(
                        (string)managerPreLoadScript,
                        ref localResult) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    DebugTrace(String.Format(
                        "LoadManager: ERROR {0}",
                        Utility.FormatWrapOrNull(localResult)),
                        typeof(LicenseOps).Name,
                        TracePriority.Highest);
#endif

                    result = localResult;
                    return ReturnCode.Error;
                }
            }

            //
            // NOTE: Next, if there is a certificate file name, add it
            //       to the Harpy plugin arguments.
            //
            if ((fileName != null) && (interpreter.AddPluginArguments(
                    ManagerPluginPattern, fileName) < 0))
            {
                localResult = "cannot add plugin arguments";

#if DEBUG || FORCE_TRACE
                DebugTrace(String.Format(
                    "LoadManager: ERROR {0}",
                    Utility.FormatWrapOrNull(localResult)),
                    typeof(LicenseOps).Name,
                    TracePriority.Highest);
#endif

                result = localResult;
                return ReturnCode.Error;
            }

            //
            // NOTE: Attempt to obtain the name of the hidden [package]
            //       command in the interpreter.  It should already have
            //       been renamed to its obfuscated name, if necessary.
            //       If that is not the case, fail now.  This handling
            //       will be skipped in cases where interpreters are not
            //       created by this SDK.
            //
            string packageCommandName;

            if (id != null)
            {
                packageCommandName = GetManagerCommandName(
                    ManagerPackageCommandName, (ulong)id);
            }
            else
            {
                bool created;

                packageCommandName = GetManagerCommandName(
                    interpreter, ManagerPackageCommandName, out created);

                if (created)
                {
                    localResult = "license manager load out-of-sequence";

#if DEBUG || FORCE_TRACE
                    DebugTrace(String.Format(
                        "LoadManager: ERROR {0}",
                        Utility.FormatWrapOrNull(localResult)),
                        typeof(LicenseOps).Name,
                        TracePriority.Highest);
#endif

                    result = localResult;
                    return ReturnCode.Error;
                }
            }

            //
            // NOTE: Next, evaluate the Eagle script used to "force" the
            //       assembly containing the license manager components
            //       to load.
            //
            // HACK: Using String.Format here is fine because all inputs
            //       are fully trusted.
            //
            localResult = null;

            if (interpreter.EvaluateTrustedScript(String.Format(
                    ManagerRequireScript, packageCommandName,
                    ManagerPackageName), ManagerTrustFlags,
                    ref localResult) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                DebugTrace(String.Format(
                    "LoadManager: ERROR {0}",
                    Utility.FormatWrapOrNull(localResult)),
                    typeof(LicenseOps).Name,
                    TracePriority.Highest);
#endif

                result = localResult;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to locate and/or load the assembly containing the license
        /// manager components.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="fileName">
        /// The file name, if any, that will be used as the location of the
        /// external certificate file to supply to the license manager plugin.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="useAnyPackage">
        /// Zero if the primary license manager package must be used (i.e.
        /// forcibly evaluate the manager load script). This only applies when
        /// not using isolated mode.
        /// </param>
        /// <param name="allowNameOnly">
        /// Zero if the assembly matching is to be performed strictly based on
        /// the full assembly name (i.e. the public key token must match the
        /// one hard-coded into this module exactly).
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// Returns the assembly containing the license manager components or
        /// null if the assembly cannot be found.
        /// </returns>
        private static Assembly GetAssembly(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ulong? id,               /* in */
            bool useAnyPackage,      /* in */
            bool allowNameOnly,      /* in */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: First, maybe attempt to find the assembly loaded into
            //       the current application domain.  This is only done if
            //       any license manager package should be used.  This has
            //       a good chance of succeeding for the Badge plugin thus
            //       making scripts like "enableSecurity" faster.
            //
            int index; /* REUSED */
            Assembly assembly; /* REUSED */
            Result localError; /* REUSED */

            if (useAnyPackage)
            {
                index = Index.Invalid; /* NOT USED */
                assembly = null;
                localError = null;

                if (FindAssembly(
                        ManagerAssemblyNames, allowNameOnly, ref index,
                        ref assembly, ref localError) == ReturnCode.Ok)
                {
                    return assembly;
                }
            }

            //
            // NOTE: Attempt to force the library manager assembly to be
            //       loaded by the interpreter.
            //
            Result result = null;

            if (LoadManager(interpreter,
                    fileName, id, ref result) != ReturnCode.Ok)
            {
                error = result;
                return null;
            }

            //
            // NOTE: Finally, re-attempt to find the assembly loaded into
            //       the current application domain.  With any luck, this
            //       time it should succeed.
            //
            index = Index.Invalid; /* NOT USED */
            assembly = null;
            localError = null;

            if (FindAssembly(
                    ManagerAssemblyNames, allowNameOnly, ref index,
                    ref assembly, ref localError) == ReturnCode.Ok)
            {
                return assembly;
            }

            error = localError;
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to locate and/or load the plugin containing the license
        /// manager components.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// plugin containing the license manager components (if required), if
        /// any.
        /// </param>
        /// <param name="fileName">
        /// The file name, if any, that will be used as the location of the
        /// external certificate file to supply to the license manager plugin.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="findOnly">
        /// When non-zero, the license manager plugin will not be loaded by
        /// this method, i.e. if it cannot be found, the method will simply
        /// fail.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// Returns the assembly containing the license manager components or
        /// null if the assembly cannot be found.
        /// </returns>
        private static IPlugin GetPlugin(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ulong? id,               /* in */
            bool findOnly,           /* in */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: We always need a valid interpreter context; therefore,
            //       check it and return an error if it is invalid.
            //
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return null;
            }

            //
            // NOTE: Attempt to find the license manager plugin already loaded
            //       into the interpreter.
            //
            IPlugin plugin;
            Result result = null;

            plugin = interpreter.FindPlugin(
                null, MatchMode.Glob, ManagerAssemblyPattern, null,
                SdkPublicKeyToken, LookupFlags.Default, false, ref result);

            if (plugin != null)
            {
                return plugin;
            }
            else if (findOnly)
            {
                error = result;
                return null;
            }

            //
            // NOTE: Attempt to force the library manager plugin to be loaded
            //       by the interpreter.
            //
            if (LoadManager(interpreter,
                    fileName, id, ref result) != ReturnCode.Ok)
            {
                error = result;
                return null;
            }

            //
            // NOTE: Finally, re-attempt to find the plugin loaded into the
            //       interpreter.  With any luck, this time it should succeed.
            //
            plugin = interpreter.FindPlugin(
                null, MatchMode.Glob, ManagerAssemblyPattern, null,
                SdkPublicKeyToken, LookupFlags.Default, false, ref result);

            if (plugin != null)
                return plugin;

            error = result;
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to create an instance of the primary license manager
        /// component.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to get the
        /// license manager plugin and create the primary license manager
        /// component.
        /// </param>
        /// <param name="plugin">
        /// This plugin is used in an attempt to determine if the license
        /// manager components can be accessed from the current application
        /// domain (i.e. this plugin has not been loaded into an isolated
        /// application domain); however, this is not a completely reliable
        /// method of determining if the license manager components are
        /// actually usable in the current application domain and this method
        /// may still fail. This parameter may be null.
        /// </param>
        /// <param name="fileName">
        /// The file name, if any, that will be used as the location of the
        /// external certificate file to supply to the license manager plugin.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="useAnyPackage">
        /// Zero if the primary license manager package must be used (i.e.
        /// forcibly evaluate the manager load script). This only applies when
        /// not using isolated mode.
        /// </param>
        /// <param name="allowNameOnly">
        /// Zero if the assembly matching is to be performed strictly based on
        /// the full assembly name (i.e. the public key token must match the
        /// one hard-coded into this module exactly).
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// Returns the created primary license manager component or null if
        /// it cannot be created for some reason.
        /// </returns>
        private static object CreateInstance(
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            string fileName,         /* in */
            ulong? id,               /* in */
            bool isolated,           /* in */
            bool useAnyPackage,      /* in */
            bool allowNameOnly,      /* in */
            ref Result error         /* out */
            )
        {
            try
            {
#if ISOLATED_PLUGINS
                if (IsIsolated(interpreter, plugin, isolated))
                {
                    error = "cannot create license manager " +
                        "from an isolated application domain";

                    return null;
                }
                else
#endif
                {
                    //
                    // NOTE: First, grab the assembly containing the license
                    //       manager components.  If we cannot find (or load)
                    //       it, we failed because it is required for all the
                    //       subsequent steps.
                    //
                    Assembly managerAssembly = GetAssembly(
                        interpreter, fileName, id, useAnyPackage,
                        allowNameOnly, ref error);

                    if (managerAssembly == null)
                        return null;

                    //
                    // NOTE: Next, attempt to actually create the license
                    //       manager via reflection.  The constructor being
                    //       used requires no arguments.  If we cannot create
                    //       it, we failed because it is required for some of
                    //       the subsequent steps.
                    //
                    object manager = managerAssembly.CreateInstance(
                        ManagerTypeName);

                    if (manager != null)
                        return manager;

                    error = "cannot create license manager";
                    return null;
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
        /// This method allows a third-party plugin or application to query
        /// the certificate properties without having to early-bind (i.e. add
        /// a reference) to the license manager assembly (i.e. by using
        /// reflection internally). However, a reference to the Eagle core
        /// library assembly itself is still required. The Eagle "license
        /// manager" (i.e. "Harpy") plugin must already be loaded into the
        /// provided interpreter and the <c>[certificate metadata]</c>
        /// sub-command must be available.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to fetch the
        /// certificate entity name, if any.
        /// </param>
        /// <param name="certificate">
        /// This must contain a reference to the certificate object currently
        /// in use by the plugin.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property value to extract from the certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode QueryCertificateProperty(
            Interpreter interpreter, /* in */
            object certificate,      /* in */
            string propertyName,     /* in */
            ref Result result        /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            if (propertyName == null)
            {
                result = "invalid property name";
                return ReturnCode.Error;
            }

            CertificateDictionary dictionary =
                certificate as CertificateDictionary;

            if (dictionary == null)
            {
                dictionary = StringDictionary.FromString(
                    certificate.ToString(), true);
            }

            if (dictionary != null)
            {
                string propertyValue;

                if (dictionary.TryGetValue(
                        propertyName, out propertyValue))
                {
                    result = propertyValue;
                    return ReturnCode.Ok;
                }
                else
                {
                    result = String.Format(
                        "certificate dictionary missing {0}",
                        CertificateEntityNameProperty);

                    return ReturnCode.Error;
                }
            }

            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            Type type = certificate.GetType();

            if (type == null)
            {
                result = "invalid certificate type";
                return ReturnCode.Error;
            }

            string objectName = String.Format(
                "{0}#Automatic#{1}", type.Name, interpreter.NextId());

            bool added = false;

            try
            {
                ReturnCode code;
                Result localResult = null;

                code = Utility.FixupReturnValue(
                    interpreter, null, DefaultObjectFlags, null,
                    null, DefaultObjectOptionType, objectName,
                    certificate, false, false, ref localResult);

                if (code == ReturnCode.Ok)
                {
                    added = true;
                    objectName = localResult;
                }
                else
                {
                    result = localResult;
                    return code;
                }

                //
                // BUGFIX: Using String.Format here is a security
                //         issue because some of the inputs are
                //         external.
                //
                StringList metadataCommand = new StringList();

                metadataCommand.Add(CertificateCommandName);
                metadataCommand.Add(MetadataSubCommandName);
                metadataCommand.Add(objectName);
                metadataCommand.Add(propertyName);

                return interpreter.EvaluateTrustedScript(
                    metadataCommand.ToString(), ManagerTrustFlags,
                    ref result);
            }
            finally
            {
                if (added)
                {
                    bool dispose = false;
                    ReturnCode removeCode;
                    Result removeResult = null;

                    removeCode = interpreter.RemoveObject(
                        objectName, ClientData.Empty,
                        ref dispose, ref removeResult);

                    if (removeCode != ReturnCode.Ok)
                    {
                        Utility.Complain(
                            interpreter, removeCode, removeResult);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to query authorization parameters for the application
        /// domain from the specified certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to fetch the
        /// authorization parameters.
        /// </param>
        /// <param name="certificate">
        /// This must contain a reference to the certificate object to be
        /// authorized by the caller.
        /// </param>
        /// <param name="id">
        /// Upon success, this will be modified to contain the unique
        /// identifier of the certificate to check.
        /// </param>
        /// <param name="entityName">
        /// Upon success, this will be modified to contain the entity name of
        /// the certificate to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode QueryAuthorizationParameters(
            Interpreter interpreter, /* in */
            object certificate,      /* in */
            out string id,           /* out */
            out string entityName,   /* out */
            ref Result error         /* out */
            )
        {
            id = null;
            entityName = null;

            Result localResult; /* REUSED */

            localResult = null;

            if (QueryCertificateProperty(interpreter,
                    certificate, CertificateIdProperty,
                    ref localResult) != ReturnCode.Ok)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            string localId = localResult;

            localResult = null;

            if (QueryCertificateProperty(interpreter,
                    certificate, CertificateEntityNameProperty,
                    ref localResult) != ReturnCode.Ok)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            string localEntityName = localResult;

            id = localId;
            entityName = localEntityName;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks to make sure the current application domain is authorized
        /// to use a certificate, via its unique identifier and entity name.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the certificate to check.
        /// </param>
        /// <param name="entityName">
        /// The entity name of the certificate to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode CheckAuthorizationParametersViaAppDomain(
            string id,         /* in */
            string entityName, /* in */
            ref Result error   /* out */
            )
        {
            if (String.IsNullOrEmpty(id))
            {
                error = "invalid certificate identifier";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(entityName))
            {
                error = "invalid certificate entity name";
                return ReturnCode.Error;
            }

            AppDomain currentAppDomain = AppDomain.CurrentDomain;

            if (currentAppDomain == null)
            {
                error = "invalid current application domain";
                return ReturnCode.Error;
            }

            long processId = Utility.GetCurrentProcessId();

            string dataName = String.Format(
                GetDataFormat, id, processId);

            object dataValue = currentAppDomain.GetData(
                dataName) as string;

            if (!Utility.SystemStringEquals(
                    entityName, dataValue as string))
            {
                error = String.Format(
                    "unauthorized application domain #{0}: {1}",
                    currentAppDomain.Id, (dataValue != null) ?
                    "<wrong>" : "<missing>");

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20 && !NET_STANDARD_21 && NATIVE && WINDOWS
            if (Utility.IsWindowsOperatingSystem() &&
                !Utility.IsMono() && !Utility.IsDotNetCore() &&
                !currentAppDomain.IsDefaultAppDomain())
            {
                if (Utility.MaybeChangeEnvironmentVariable(
                        String.Format(DefaultAppDomainEnvVarFormat,
                        processId), null, 1.ToString()) == null)
                {
                    try
                    {
                        _AppDomain defaultAppDomain = /* throw */
                            Utility.GetDefaultAppDomain() as _AppDomain;

                        if (defaultAppDomain != null)
                        {
                            defaultAppDomain.SetData(
                                dataName, dataValue); /* throw */

#if DEBUG || FORCE_TRACE
                            DebugTrace(String.Format(
                                "CheckAuthorizationParametersViaAppDomain: " +
                                "authorized default application domain " +
                                "#{0} {1} for process {2}",
                                GetAppDomainId(defaultAppDomain),
                                Utility.FormatWrapOrNull(
                                    defaultAppDomain.FriendlyName),
                                processId), typeof(LicenseOps).Name,
                                TracePriority.High);
#endif
                        }
#if DEBUG || FORCE_TRACE
                        else
                        {
                            DebugTrace(String.Format(
                                "CheckAuthorizationParametersViaAppDomain: " +
                                "invalid default application domain " +
                                "for process {0}",
                                processId), typeof(LicenseOps).Name,
                                TracePriority.Higher);
                        }
#endif
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
                    {
                        DebugTrace(
                            e, typeof(LicenseOps).Name,
                            TracePriority.Higher);
                    }
#else
                    catch
                    {
                        // do nothing.
                    }
#endif
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks to make sure the current process is authorized to use a
        /// certificate, via its unique identifier and entity name.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the certificate to check.
        /// </param>
        /// <param name="entityName">
        /// The entity name of the certificate to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        private static ReturnCode CheckAuthorizationParametersViaEnvironment(
            string id,         /* in */
            string entityName, /* in */
            ref Result error   /* out */
            )
        {
            if (String.IsNullOrEmpty(id))
            {
                error = "invalid certificate identifier";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(entityName))
            {
                error = "invalid certificate entity name";
                return ReturnCode.Error;
            }

            long[] processIds = {
                Utility.GetCurrentProcessId(),
                Utility.GetParentProcessId()
            };

            ResultList errors = null;

            foreach (long processId in processIds)
            {
                if (processId == 0)
                    continue;

                string dataName = String.Format(
                    GetDataFormat, id, processId);

                string dataValue = Utility.GetEnvironmentVariable(
                    dataName);

                if (Utility.SystemStringEquals(entityName, dataValue))
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "unauthorized process #{0}: {1}", processId,
                        (dataValue != null) ? "<wrong>" : "<missing>"));
                }
            }

            if (errors != null)
                error = errors;
            else
                error = "unauthorized process";

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Manager Helper Methods
        /// <summary>
        /// Attempts to cleanup a cached interpreter created and used by this
        /// class.
        /// </summary>
        /// <param name="token">
        /// The optional lookup token for the interpreter to cleanup. If this
        /// parameter is null, nothing will be done.
        /// </param>
        /// <param name="interpreterType">
        /// The optional interpreter type for the interpreter to cleanup. If
        /// this parameter is null, nothing will be done.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode Cleanup(
            ulong? token,                     /* in: OPTIONAL */
            InterpreterType? interpreterType, /* in: OPTIONAL */
            ref Result error                  /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            if ((token != null) && (interpreterType != null))
            {
                return PrivateCleanup(token, interpreterType, ref error);
            }
            else
            {
                if (EvaluateInterpreterToken != null)
                {
                    if (PrivateCleanup(
                            EvaluateInterpreterToken, SdkInterpreterType,
                            ref error) == ReturnCode.Ok)
                    {
                        /* NO RESULT */
                        ResetInterpreterForEvaluate();
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }

                if (VerifyInterpreterToken != null)
                {
                    if (PrivateCleanup(
                            VerifyInterpreterToken, SdkInterpreterType,
                            ref error) == ReturnCode.Ok)
                    {
                        /* NO RESULT */
                        ResetInterpreterForVerify();
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to fetch the certificate associated with the loaded
        /// license manager plugin, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to fetch the
        /// certificate, if any.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="error">
        /// Upon failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode GetCertificate(
            Interpreter interpreter, /* in */
            ref object certificate,  /* out */
            ref Result error         /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            IPlugin plugin = GetPlugin(
                interpreter, null, null, true, ref error);

            if (plugin == null)
                return ReturnCode.Error;

            string value; /* REUSED */

            value = plugin.GetString(
                interpreter, ManagerCertificateStringName, null, ref error);

            if (value == null)
                return ReturnCode.Error;

            CertificateDictionary dictionary = StringDictionary.FromString(
                value, true, ref error);

            if (dictionary == null)
                return ReturnCode.Error;

            if (!dictionary.TryGetValue(KindPropertyName, out value))
            {
                error = String.Format(
                    "malformed certificate string missing {0}",
                    Utility.FormatWrapOrNull(KindPropertyName));

                return ReturnCode.Error;
            }

            if (!Utility.SystemStringEquals(value, KindPropertyValue))
            {
                error = String.Format(
                    "malformed certificate string mismatch {0}: {1}",
                    Utility.FormatWrapOrNull(KindPropertyName),
                    Utility.FormatWrapOrNull(value));

                return ReturnCode.Error;
            }

            RemoveNullOrEmpty(dictionary);

            certificate = dictionary;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks if isolated mode should be used for loading a plugin for
        /// the specified type.
        /// </summary>
        /// <param name="type">
        /// The type associated with the plugin to be loaded -OR- null if the
        /// type information is not available.
        /// </param>
        /// <returns>
        /// Returns non-zero if isolated mode should be used.
        /// </returns>
        public static bool UseIsolated(
            Type type /* in */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            //
            // NOTE: First, check for the "global" environment variables.
            //
            if (Utility.DoesEnvironmentVariableExist(NoIsolatedEnvVarName))
                return false;

            if (Utility.DoesEnvironmentVariableExist(IsolatedEnvVarName))
                return true;

            //
            // NOTE: Next, check for the license manager SDK environment
            //       variables.
            //
            if (Utility.DoesEnvironmentVariableExist(String.Format(
                    "{0}{1}{2}", ManagerTypeName, Characters.Underscore,
                    NoIsolatedEnvVarName)))
            {
                return false;
            }

            if (Utility.DoesEnvironmentVariableExist(String.Format(
                    "{0}{1}{2}", ManagerTypeName, Characters.Underscore,
                    IsolatedEnvVarName)))
            {
                return true;
            }

            //
            // NOTE: Finally, check for per-type environment variables
            //       using the full type name and short type name.
            //
            if (type != null)
            {
                if (Utility.DoesEnvironmentVariableExist(
                        String.Format("{0}{1}{2}", type.FullName,
                        Characters.Underscore, NoIsolatedEnvVarName)))
                {
                    return false;
                }

                if (Utility.DoesEnvironmentVariableExist(
                        String.Format("{0}{1}{2}", type.FullName,
                        Characters.Underscore, IsolatedEnvVarName)))
                {
                    return true;
                }

                if (Utility.DoesEnvironmentVariableExist(
                        String.Format("{0}{1}{2}", type.Name,
                        Characters.Underscore, NoIsolatedEnvVarName)))
                {
                    return false;
                }

                if (Utility.DoesEnvironmentVariableExist(
                        String.Format("{0}{1}{2}", type.Name,
                        Characters.Underscore, IsolatedEnvVarName)))
                {
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate summary (i.e. "about") functionality provided by the
        /// Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="plugin">
        /// This plugin is used in an attempt to determine if the license
        /// manager components can be accessed from the current application
        /// domain (i.e. this plugin has not been loaded into an isolated
        /// application domain); however, this is not a completely reliable
        /// method of determining if the license manager components are
        /// actually usable in the current application domain and this method
        /// may still fail. This parameter may be null.
        /// </param>
        /// <param name="certificate">
        /// This must contain a reference to the certificate object currently
        /// in use by the plugin.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="result">
        /// This parameter is used for both input and output. Upon entry, this
        /// should contain the existing base information for the plugin. Upon
        /// success, this will contain the certificate summary (i.e. "about")
        /// information in addition to the base information for the plugin
        /// itself. Upon failure, this will contain an appropriate error
        /// message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode AboutCertificate( /* PRIMARY */
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            object certificate,      /* in */
            bool isolated,           /* in */
            ref Result result        /* in, out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            #region Isolated Plugin Support
#if ISOLATED_PLUGINS
            if (IsIsolated(interpreter, plugin, isolated))
            {
                try
                {
                    //
                    // NOTE: Attempt to find the license manager plugin loaded
                    //       into the interpreter.
                    //
                    IPlugin managerPlugin = GetPlugin(
                        interpreter, null, null, false, ref result);

                    if (managerPlugin == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Build the input data for the request.  For this
                    //       request type, it consists of an array containing
                    //       the necessary input parameters.
                    //
                    object[] request = {
                        interpreter, plugin, certificate, result
                    };

                    //
                    // NOTE: Setup the "well-known" configuration data using
                    //       the AppDomain for the manager plugin.
                    //
                    SetupWellKnownConfigurationData(managerPlugin.AppDomain);

                    //
                    // NOTE: Call into the manager plugin to request that
                    //       the certificate summary information be returned.
                    //
                    object response = null;

                    if (managerPlugin.Execute(interpreter,
                            new ClientData(AboutMethodName), request,
                            ref response, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Upon success, assume response can be converted
                    //       to a string.
                    //
                    result = ValueToStringOrListResult(response);

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown somewhere.  Record the
                    //       details in the result variable provided by the
                    //       caller.
                    //
                    result = e;
                }

                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Non-Isolated Plugin Support
            try
            {
                //
                // NOTE: Use any available license manager package?
                //
                bool useAnyPackage = UseAnyPackage(
                    (plugin != null) ? plugin.AssemblyName : null);

                //
                // NOTE: Attempt to create the primary license manager
                //       component now.  If this fails, we cannot continue.
                //
                object manager = CreateInstance(
                    interpreter, plugin, null, null, isolated,
                    useAnyPackage, SdkAllowAssemblyNameOnly,
                    ref result);

                if (manager == null)
                    return ReturnCode.Error;

                //
                // NOTE: Next, grab underlying type of the license manager.
                //       Based on how the CLR works, this should never fail;
                //       however, check the return value anyhow.  If this is
                //       invalid (null) for some reason, we failed because it
                //       is required for some of the subsequent steps.
                //
                Type managerType = manager.GetType();

                if (managerType == null) /* NEVER */
                {
                    result = "license manager type is invalid";
                    return ReturnCode.Error;
                }

                //
                // NOTE: Next, create the array of arguments to pass in the
                //       (late-bound) method call to the license manager
                //       certificate summary (i.e. "about") subsystem.
                //
                object[] args = {
                    interpreter, plugin, certificate, result
                };

                //
                // NOTE: Next, grab the length of the array of arguments that
                //       we just created.
                //
                int length = args.Length;

                //
                // NOTE: Setup the "well-known" configuration data within the
                //       current AppDomain, since this call should not cross an
                //       AppDomain boundary.
                //
                SetupWellKnownConfigurationData(AppDomain.CurrentDomain);

                //
                // NOTE: Next, invoke the license manager certificate summary
                //       (i.e. "about") subsystem via reflection.  The return
                //       value here must be an Eagle return code or the cast
                //       will cause an exception to be thrown.
                //
                ReturnCode code = (ReturnCode)managerType.InvokeMember(
                    AboutMethodName, aboutMethodBindingFlags, null, manager,
                    args);

                //
                // NOTE: Finally, always update the overall result (or error
                //       message) in the variable provided by the caller.
                //
                result = args[length - 1] as Result;

                return code;
            }
            catch (Exception e)
            {
                //
                // NOTE: An exception was thrown somewhere.  Record details
                //       in the result variable provided by the caller.
                //
                result = e;
            }

            return ReturnCode.Error;
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate cache functionality provided by the Eagle "license
        /// manager" (i.e. "Harpy") without having to early-bind (i.e. add a
        /// reference) to the license manager assembly (i.e. by using
        /// reflection internally). However, a reference to the Eagle core
        /// library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="plugin">
        /// This plugin is used in an attempt to determine if the license
        /// manager components can be accessed from the current application
        /// domain (i.e. this plugin has not been loaded into an isolated
        /// application domain); however, this is not a completely reliable
        /// method of determining if the license manager components are
        /// actually usable in the current application domain and this method
        /// may still fail. This parameter may be null.
        /// </param>
        /// <param name="id">
        /// The unique identifier for the certificate to be fetched.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to permit fetching the certificate from a plugin loaded
        /// in an isolated application domain; otherwise, zero.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode GetCertificate( /* PRIMARY */
            Interpreter interpreter,      /* in */
            IPlugin plugin,               /* in */
            Guid id,                      /* in */
            bool isolated,                /* in */
            ref object certificate,       /* out */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            #region Isolated Plugin Support
#if ISOLATED_PLUGINS
            if (IsIsolated(interpreter, plugin, isolated))
            {
                try
                {
                    //
                    // NOTE: Attempt to find the license manager plugin loaded
                    //       into the interpreter.
                    //
                    IPlugin managerPlugin = GetPlugin(
                        interpreter, null, null, false, ref result);

                    if (managerPlugin == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Build the input data for the request.  For this
                    //       request type, it consists of an array containing
                    //       the necessary input parameters.
                    //
                    object[] request = {
                        interpreter, plugin, id, certificate, result
                    };

                    //
                    // NOTE: Setup the "well-known" configuration data using
                    //       the AppDomain for the manager plugin.
                    //
                    SetupWellKnownConfigurationData(managerPlugin.AppDomain);

                    //
                    // NOTE: Call into the manager plugin to request that
                    //       the certificate summary information be returned.
                    //
                    object response = null;

                    if (managerPlugin.Execute(interpreter,
                            new ClientData(GetMethodName), request,
                            ref response, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Convert the response object to an object array
                    //       and verify that it has at least three elements.
                    //
                    object[] args = (object[])response;

                    //
                    // NOTE: Next, grab the length of the array of arguments
                    //       that we just grabbed.
                    //
                    int length = args.Length;

                    if (length < 2)
                    {
                        result = String.Format(
                            "malformed response: have {0} array elements, " +
                            "need at least 2", length);

                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Upon success, assume response can be converted
                    //       to string.
                    //
                    certificate = args[0];
                    result = ValueToString(args[1]);

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown somewhere.  Record the
                    //       details in the result variable provided by the
                    //       caller.
                    //
                    result = e;
                }

                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Non-Isolated Plugin Support
            try
            {
                //
                // NOTE: Use any available license manager package?
                //
                bool useAnyPackage = UseAnyPackage(
                    (plugin != null) ? plugin.AssemblyName : null);

                //
                // NOTE: Attempt to create the primary license manager
                //       component now.  If this fails, we cannot continue.
                //
                object manager = CreateInstance(
                    interpreter, plugin, null, null, isolated,
                    useAnyPackage, SdkAllowAssemblyNameOnly,
                    ref result);

                if (manager == null)
                    return ReturnCode.Error;

                //
                // NOTE: Next, grab underlying type of the license manager.
                //       Based on how the CLR works, this should never fail;
                //       however, check the return value anyhow.  If this is
                //       invalid (null) for some reason, we failed because it
                //       is required for some of the subsequent steps.
                //
                Type managerType = manager.GetType();

                if (managerType == null) /* NEVER */
                {
                    result = "license manager type is invalid";
                    return ReturnCode.Error;
                }

                //
                // NOTE: Next, create the array of arguments to pass in the
                //       (late-bound) method call to the license manager
                //       certificate summary (i.e. "about") subsystem.
                //
                object[] args = {
                    interpreter, plugin, id, certificate, result
                };

                //
                // NOTE: Next, grab the length of the array of arguments that
                //       we just created.
                //
                int length = args.Length;

                //
                // NOTE: Setup the "well-known" configuration data within the
                //       current AppDomain, since this call should not cross an
                //       AppDomain boundary.
                //
                SetupWellKnownConfigurationData(AppDomain.CurrentDomain);

                //
                // NOTE: Next, invoke the license manager certificate summary
                //       (i.e. "about") subsystem via reflection.  The return
                //       value here must be an Eagle return code or the cast
                //       will cause an exception to be thrown.
                //
                ReturnCode code = (ReturnCode)managerType.InvokeMember(
                    GetMethodName, getMethodBindingFlags, null, manager,
                    args);

                //
                // NOTE: Next, if the certificate cache succeeded, fetch the
                //       certificate object from the argument array and update
                //       the variables provided by the caller with their new
                //       values.
                //
                if (code == ReturnCode.Ok)
                    certificate = args[length - 2] as object;

                //
                // NOTE: Finally, always update the overall result (or error
                //       message) in the variable provided by the caller.
                //
                result = args[length - 1] as Result;

                return code;
            }
            catch (Exception e)
            {
                //
                // NOTE: An exception was thrown somewhere.  Record details
                //       in the result variable provided by the caller.
                //
                result = e;
            }

            return ReturnCode.Error;
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// WARNING: This method is not thread-safe and must be used by only
        ///          one thread at a time. Callers should wrap this method
        ///          call in a lock statement or similar construct.
        ///
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required. This method
        /// overload automatically creates and sets up the interpreter context
        /// required to perform these operations.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating an internal certificate resource
        /// -OR- an external certificate file itself, if necessary.
        /// </param>
        /// <param name="trustedHashes">
        /// The list of file hashes to use when checking if a plugin file is
        /// considered to be "fully trusted". This will only be used if the
        /// underlying platform does not support Authenticode.
        /// </param>
        /// <param name="resourceName">
        /// This must be null -OR- the name of the embedded resource within
        /// <paramref name="assembly" /> to extract and forcibly use as the
        /// certificate for the license manager itself.
        /// </param>
        /// <param name="options">
        /// This must be null -OR- a list of options to use when verifying the
        /// certificate.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode ExtractAndVerifyCertificate(
            Assembly assembly,           /* in */
            StringList trustedHashes,    /* in */
            string resourceName,         /* in */
            IEnumerable<string> options, /* in */
            ref string fileName,         /* in, out */
            ref object certificate,      /* out */
            ref Result result            /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
#if NETWORK
            bool setOfflineMode = false;
#endif

            try
            {
#if NETWORK
                if (Utility.DoesEnvironmentVariableExist(
                        ForceOfflineModeEnvVarName))
                {
                    Utility.SetOfflineMode(true);
                    setOfflineMode = true;
                }
#endif

                bool traceWasSetup = false;

                try
                {
                    if (Utility.DoesEnvironmentVariableExist(
                            ForceEnableTraceEnvVarName))
                    {
                        Utility.UnsetEnvironmentVariable(
                            ForceEnableTraceEnvVarName);

                        traceWasSetup = MaybeSetupTraceSubsystem(
                            null, null);
                    }

                    StringList localOptions = new StringList();

                    localOptions.Add(ManagerOverrideEnvVarName);
                    localOptions.Add(ManagerNoAutoAcquireEnvVarName);

                    if (options != null)
                        localOptions.Add(options);

                    IClientData clientData = null;

                    if (!Utility.SaveEnvironmentVariables(
                            localOptions, ref clientData))
                    {
                        result = "cannot save option environment variables";
                        return ReturnCode.Error;
                    }

                    try
                    {
                        if (!Utility.SetEnvironmentVariables(
                                localOptions, clientData))
                        {
                            result = "cannot set option environment variables";
                            return ReturnCode.Error;
                        }

                        string temporaryDirectory = null;

                        try
                        {
                            //
                            // NOTE: Before attempting to extract an embedded
                            //       license certificate from the specified
                            //       assembly, check for a file with the same
                            //       name as the resource in the directory
                            //       associated with said assembly.
                            //
                            if (SearchCertificate(assembly, resourceName))
                                goto verify;

                            string temporaryFileName;

                            if (ExtractCertificate(
                                    assembly, resourceName,
                                    out temporaryDirectory,
                                    out temporaryFileName,
                                    ref result) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            //
                            // HACK: The temporary file name cannot simply be
                            //       set to "1".  It must be set to the actual
                            //       extracted temporary certificate file name.
                            //
                            if (temporaryFileName != null)
                            {
                                Environment.SetEnvironmentVariable(
                                    ManagerOverrideEnvVarName,
                                    temporaryFileName);
                            }

                        verify:

                            return VerifyCertificate(
                                assembly, trustedHashes, ref fileName,
                                ref certificate, ref result);
                        }
                        finally
                        {
                            if (temporaryDirectory != null)
                            {
                                /* IGNORED */
                                Utility.CleanupDirectory(temporaryDirectory,
                                    new string[] { resourceName }, true);
                            }
                        }
                    }
                    finally
                    {
                        /* IGNORED */
                        Utility.RestoreEnvironmentVariables(
                            localOptions, clientData);
                    }
                }
                finally
                {
                    if (traceWasSetup)
                    {
                        /* IGNORED */
                        MaybeSetupTraceSubsystem(null, false);
                    }
                }
            }
            finally
            {
#if NETWORK
                if (setOfflineMode)
                {
                    Utility.SetOfflineMode(false);
                    setOfflineMode = false;
                }
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required. This method
        /// overload automatically creates and sets up the interpreter context
        /// required to perform these operations.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="trustedHashes">
        /// The list of file hashes to use when checking if a plugin file is
        /// considered to be "fully trusted". This will only be used if the
        /// underlying platform does not support Authenticode.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode VerifyCertificate(
            Assembly assembly,        /* in */
            StringList trustedHashes, /* in */
            ref string fileName,      /* in, out */
            ref Result result         /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            object certificate = null;

            return VerifyCertificate(
                assembly, trustedHashes, ref fileName, ref certificate,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required. This method
        /// overload automatically creates and sets up the interpreter context
        /// required to perform these operations.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="trustedHashes">
        /// The list of file hashes to use when checking if a plugin file is
        /// considered to be "fully trusted". This will only be used if the
        /// underlying platform does not support Authenticode.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode VerifyCertificate(
            Assembly assembly,        /* in */
            StringList trustedHashes, /* in */
            ref string fileName,      /* in, out */
            ref object certificate,   /* out */
            ref Result result         /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
#if ISOLATED_PLUGINS
            bool isolated = true;

            if (Utility.DoesEnvironmentVariableExist(
                    ManagerNoIsolatedEnvVarName))
            {
                isolated = false;
            }
#else
            bool isolated = false;
#endif

            return VerifyCertificate(
                assembly, trustedHashes, null, false, true, true,
                true, isolated, new AnyClientData(), ref fileName,
                ref certificate, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required. This method
        /// overload automatically creates and sets up the interpreter context
        /// required to perform these operations.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="trustedHashes">
        /// The list of file hashes to use when checking if a plugin file is
        /// considered to be "fully trusted". This will only be used if the
        /// underlying platform does not support Authenticode.
        /// </param>
        /// <param name="policy">
        /// The flags that control if certificate subject name matching is to
        /// be performed based on the simple name and/or prefix.
        /// </param>
        /// <param name="withAutoPath">
        /// Non-zero to force the auto-path to be temporarily modified so it
        /// includes the directory containing the binary associated with the
        /// current application domain.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the check to be performed even when it would
        /// otherwise be deemed unnecessary.
        /// </param>
        /// <param name="embedded">
        /// Non-zero to check for and attempt to use a certificate resource
        /// embedded in the plugin assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to use XML schema validation for certificates.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="anyClientData">
        /// The extra data to be supplied to the renewal callback, if any.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode VerifyCertificate(
            Assembly assembly,            /* in */
            StringList trustedHashes,     /* in */
            ExecutionPolicy? policy,      /* in */
            bool withAutoPath,            /* in */
            bool force,                   /* in */
            bool embedded,                /* in */
            bool validate,                /* in */
            bool isolated,                /* in */
            IAnyClientData anyClientData, /* in */
            ref string fileName,          /* in, out */
            ref object certificate,       /* out */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            bool wasOverridden;
            string binaryPath;
            string savedLibPath = null;

            binaryPath = GetBinaryPath(out wasOverridden);

            if (withAutoPath)
            {
                Utility.BeginWithAutoPath(
                    binaryPath, false, ref savedLibPath);
            }

            try
            {
                Result localResult; /* REUSED */
                CreateFlags createFlags = VerifyCreateFlags;
                HostCreateFlags hostCreateFlags = SdkHostCreateFlags;
                InitializeFlags initializeFlags = SdkInitializeFlags;
                PluginFlags pluginFlags = SdkPluginFlags;

#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                if (Utility.DoesEnvironmentVariableExist(
                        ManagerNoProbePluginsEnvVarName))
                {
                    createFlags &= ~CreateFlags.ProbePlugins;
                    initializeFlags &= ~InitializeFlags.AutoPath;
                }
#endif

                if (Utility.DoesEnvironmentVariableExist(
                        ManagerWellKnownPluginsEnvVarName))
                {
                    initializeFlags |= InitializeFlags.WellKnown;
                }

                HostCreateFlags embeddedConsoleUse =
                    HostCreateFlags.EmbeddedConsoleUse;

                if (Utility.DoesEnvironmentVariableExist(
                        ManagerVerifyWithConsole))
                {
                    hostCreateFlags &= ~HostCreateFlags.NoConsole;
                    hostCreateFlags |= embeddedConsoleUse;
                }
                else
                {
                    hostCreateFlags &= ~embeddedConsoleUse;
                    hostCreateFlags |= HostCreateFlags.NoConsole;
                }

                //
                // HACK: *SECURITY* The necessary functions are
                //       not available on non-Windows operating
                //       systems when running on .NET Core.  So,
                //       skip checking plugin for Authenticode
                //       signatures in those cases; however, if
                //       the list of trusted hashes is available,
                //       use those instead of totally disabling
                //       trust checking.
                //
                // HACK: *SECURITY* The necessary support is (also)
                //       not available for core library builds that
                //       do not include the native code integration
                //       via P/Invoke feature.
                //
                bool useTrustedHashes = false;

                if (!Utility.HaveEagleNative(null) ||
                    (!Utility.IsWindowsOperatingSystem() &&
                     !Utility.IsMono()))
                {
                    if (!Utility.DoesEnvironmentVariableExist(
                            ManagerNoTrustedHashesEnvVarName) &&
                        (trustedHashes != null) &&
                        (trustedHashes.Count > 0))
                    {
                        useTrustedHashes = true;
                    }
                    else
                    {
                        pluginFlags &= ~PluginFlags.TrustedOnly;
                    }
                }

#if ISOLATED_PLUGINS
                //
                // HACK: Permit plugin isolation to be disabled
                //       because it is very expensive.  Creating
                //       a new AppDomain is most of that cost.
                //       By default, plugin isolation is enabled
                //       for extra security.
                //
                if (Utility.DoesEnvironmentVariableExist(
                        ManagerNoIsolatedEnvVarName))
                {
                    pluginFlags &= ~PluginFlags.Isolated;
                }
#endif

                ulong? interpreterToken = GetInterpreterForVerify();
                bool newInterpreterToken = false;

                if (interpreterToken == null)
                {
                    interpreterToken = Utility.GetRandomNumber();
                    newInterpreterToken = true;
                }

                MaybeAddExitedEventHandlers();

                int retries = 0;

            retry:

#if DEBUG || FORCE_TRACE
                DateTime createStart = Utility.GetUtcNow();
#endif

                localResult = null;

                using (Interpreter interpreter = Interpreter.Create(
                        interpreterToken, null, createFlags,
                        hostCreateFlags, initializeFlags,
                        SdkScriptFlags, SdkInterpreterFlags,
                        pluginFlags, null, ref localResult))
                {
#if DEBUG || FORCE_TRACE
                    DateTime createStop = Utility.GetUtcNow();

                    DebugTrace(String.Format(
                        "VerifyCertificate: Create {0} in {1} milliseconds.",
                        (interpreter != null) ? "SUCCESS" : "FAILURE",
                        createStop.Subtract(createStart).TotalMilliseconds),
                        typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif

                    if (interpreter == null)
                    {
                        if (Utility.DoesEnvironmentVariableExist(
                                ManagerRetryCreation) &&
                            (retries++ < GetMaximumRetries()))
                        {
                            Sleep();
                            goto retry;
                        }

                        result = localResult;
                        return ReturnCode.Error;
                    }

                    //
                    // HACK: Attempt to prevent too much large object heap
                    //       (LOH) memory usage.
                    //
                    /* IGNORED */
                    interpreter.EnableCaches(SdkCacheFlags, false);

                    //
                    // HACK: When using the IfNecessary and/or IfCannotLock
                    //       flags, it is now necessary to double-check that
                    //       the token of the returned (i.e. "created")
                    //       interpreter matches what we think it should,
                    //       i.e. just in case a temporary interpreter had
                    //       to be created due to internal locking issues.
                    //
                    if (!newInterpreterToken &&
                        (interpreter.CreateCount == 1))
                    {
                        newInterpreterToken = true;

#if DEBUG || FORCE_TRACE
                        DebugTrace(
                            "VerifyCertificate: Unexpectedly created " +
                            "interpreter.", typeof(LicenseOps).Name,
                            TracePriority.High);
#endif
                    }

                    bool mismatchedToken = false;

                    if (!interpreter.MatchToken(interpreterToken))
                    {
                        mismatchedToken = true;

#if DEBUG || FORCE_TRACE
                        DebugTrace(
                            "VerifyCertificate: Unexpectedly mismatched " +
                            "interpreter.", typeof(LicenseOps).Name,
                            TracePriority.High);
#endif
                    }

                    //
                    // NOTE: Use the caller supplied list of trusted hashes,
                    //       verbatim.
                    //
                    if (newInterpreterToken &&
                        useTrustedHashes && (trustedHashes != null))
                    {
                        /* IGNORED */
                        interpreter.MergeTrustedHashes(trustedHashes);

#if DEBUG || FORCE_TRACE
                        DebugTrace(String.Format(
                            "VerifyCertificate: Added trusted hashes: {0}.",
                            Utility.FormatWrapOrNull(trustedHashes)),
                            typeof(LicenseOps).Name,
                            TracePriority.MediumHigh);
#endif
                    }

                    //
                    // HACK: *SECURITY* At this point, disable creating any
                    //       further interpreters in this AppDomain.  Since
                    //       this entry-point is intended exclusively for
                    //       use with the Harpy licensing SDK, this should
                    //       not be an issue.  As an extra precaution, this
                    //       is only done when a non-null interpreter token
                    //       has actually been saved.  Also, since this may
                    //       be expensive, it should only be done when the
                    //       interpreter was actually just created.  This
                    //       cannot be done if the interpreter token was
                    //       mismatched because that may mean we do not yet
                    //       have a valid non-transient interpreter to use
                    //       (next time).
                    //
                    if (newInterpreterToken && !mismatchedToken &&
                        SetInterpreterForVerify(interpreterToken))
                    {
                        //
                        // HACK: Do not disable interpreter creation if the
                        //       appropriate environment variable is set.
                        //       Technically, this makes the license SDK a
                        //       bit less secure; however, this is seen as
                        //       a reasonable trade-off now that the plugin
                        //       can disable interpreter creation on-demand
                        //       via one of its configuration files.
                        //
                        if (Utility.DoesEnvironmentVariableExist(
                                ManagerNoDisableCreationEnvVarName))
                        {
#if DEBUG || FORCE_TRACE
                            DebugTrace(
                                "VerifyCertificate: Interpreter " +
                                "creation will not be disabled.",
                                typeof(LicenseOps).Name,
                                TracePriority.MediumHigh);
#endif
                        }
                        else
                        {
                            //
                            // HACK: The persistent flag should be used with
                            //       extreme caution.  It causes an assembly
                            //       to be dynamically loaded and invoked,
                            //       which could be slow.  Also, after it is
                            //       loaded, it cannot be unloaded -AND- no
                            //       further interpreters can be created in
                            //       the process.
                            //
                            DisableFlags disableFlags = SdkDisableFlags;

                            bool persistent = Utility.HasFlags(
                                policy, ExecutionPolicy.DisableCreation,
                                true);

                            if (persistent)
                            {
                                disableFlags |= DisableFlags.Persistent;

                                /* NO RESULT */
                                Utility.EnableStubAssembly(
                                    disableFlags); /* throw */
                            }

                            /* IGNORED */
                            Utility.DisableInterpreterCreation(
                                disableFlags); /* throw */
                        }
                    }

                    //
                    // HACK: Make sure we created (or not?) a new command
                    //       token if we created a new interpreter token;
                    //       this does not apply if the interpreter token
                    //       does not match (see above).
                    //
                    ulong? commandId = null;
                    string packageCommandName;
                    string loadCommandName;
                    bool newCommandToken;

                    if (mismatchedToken)
                    {
                        commandId = Utility.GetRandomNumber();

                        packageCommandName = GetManagerCommandName(
                            ManagerPackageCommandName, (ulong)commandId);

                        loadCommandName = GetManagerCommandName(
                            ManagerLoadCommandName, (ulong)commandId);

                        newCommandToken = true;
                    }
                    else
                    {
                        packageCommandName = GetManagerCommandName(
                            interpreter, ManagerPackageCommandName,
                            out newCommandToken);

                        if (newCommandToken != newInterpreterToken)
                        {
                            result = String.Format(
                                "mismatch, needed {0} command token",
                                newInterpreterToken ? "new" : "old");

                            return ReturnCode.Error;
                        }

                        loadCommandName = GetManagerCommandName(
                            interpreter, ManagerLoadCommandName);
                    }

                    if (newCommandToken)
                    {
                        localResult = null;

                        if (interpreter.MaybeRenameHiddenCommand(
                                Utility.MakeCommandName(
                                    ManagerPackageCommandName),
                                Utility.MakeCommandName(
                                    packageCommandName), false,
                                ref localResult) != ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }

                        localResult = null;

                        if (interpreter.MaybeRenameHiddenCommand(
                                Utility.MakeCommandName(
                                    ManagerLoadCommandName),
                                Utility.MakeCommandName(
                                    loadCommandName), false,
                                ref localResult) != ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }

                    Result assemblyError = null;

                    if (newInterpreterToken)
                    {
                        string assemblyPath;

                        assemblyPath = withAutoPath ?
                            null : GetManagerAssemblyPath(
                                binaryPath, ref assemblyError);

                        if (assemblyPath != null)
                        {
#if DEBUG || FORCE_TRACE
                            DebugTrace(String.Format(
                                "VerifyCertificate: Found the manager " +
                                "assembly path {0} using binary path {1}, " +
                                "which was {2}manually overridden: {3}",
                                Utility.FormatWrapOrNull(assemblyPath),
                                Utility.FormatWrapOrNull(binaryPath),
                                wasOverridden ? String.Empty : "not ",
                                Utility.FormatDefineConstants(
                                    DefineConstants.OptionList)),
                                typeof(LicenseOps).Name,
                                TracePriority.MediumHigh);

                            CheckDefineConstants();
#endif

                            localResult = null;

                            if (ProvideManagerPackage(
                                    interpreter, assemblyPath,
                                    packageCommandName, loadCommandName,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;
                                return ReturnCode.Error;
                            }
                        }
#if DEBUG || FORCE_TRACE
                        else
                        {
                            DebugTrace(String.Format(
                                "VerifyCertificate: Missing the manager " +
                                "assembly path using binary path {0}, " +
                                "which was {1}manually overridden: {2}",
                                Utility.FormatWrapOrNull(binaryPath),
                                wasOverridden ? String.Empty : "not ",
                                Utility.FormatWrapOrNull(assemblyError)),
                                typeof(LicenseOps).Name,
                                TracePriority.MediumHigh);
                        }
#endif
                    }

                    ReturnCode code = ReturnCode.Break;

#if DEBUG || FORCE_TRACE
                    DateTime verifyStart = Utility.GetUtcNow();

                    try
                    {
#endif
                        localResult = null;

                        code = VerifyCertificate(
                            interpreter, assembly, null, null,
                            null, null, null, null, null, null,
                            policy, null, null, null, commandId,
                            false, force, embedded, validate,
                            isolated, null, null, anyClientData,
                            ref fileName, ref certificate,
                            ref localResult);

                        if (code != ReturnCode.Ok)
                        {
                            result = new ResultList(
                                localResult, assemblyError);

                            return code;
                        }

                        //
                        // NOTE: Permit authorization to be
                        //       skipped if the correct flag
                        //       is set in the clientData by
                        //       the license certificate
                        //       verification subsystem.
                        //
                        bool skipAuthorization;

                        if (anyClientData != null)
                        {
                            Result localError = null;

                            /* IGNORED */
                            anyClientData.TryGetBoolean(
                                SkipAuthorizationDataName,
                                false, out skipAuthorization,
                                ref localError);
                        }
                        else
                        {
                            skipAuthorization = false;
                        }

                        //
                        // NOTE: The certificate has now been
                        //       verified; next, check if the
                        //       AppDomain has been authorized
                        //       to make use of it.
                        //
                        Result verifyResult = localResult;

                        if (!skipAuthorization)
                        {
                            string id;
                            string entityName;

                            localResult = null;

                            code = QueryAuthorizationParameters(
                                interpreter, certificate, out id,
                                out entityName, ref localResult);

                            if (code != ReturnCode.Ok)
                            {
                                result = localResult;

#if DEBUG || FORCE_TRACE
                                DebugTrace(String.Format(
                                    "VerifyCertificate: " +
                                    "Authorization query failed: {0}",
                                    Utility.FormatWrapOrNull(result)),
                                    typeof(LicenseOps).Name,
                                    TracePriority.Highest);
#endif

                                return code;
                            }

                            Result appDomainResult = null;
                            Result environmentResult = null;

                            code = CheckAuthorizationParametersViaAppDomain(
                                id, entityName, ref appDomainResult);

                            if (code != ReturnCode.Ok)
                            {
                                code = CheckAuthorizationParametersViaEnvironment(
                                    id, entityName, ref environmentResult);

                                if (code != ReturnCode.Ok)
                                {
                                    result = new ResultList(
                                        appDomainResult, environmentResult);

#if DEBUG || FORCE_TRACE
                                    DebugTrace(String.Format(
                                        "VerifyCertificate: " +
                                        "Authorization check failed: {0}",
                                        Utility.FormatWrapOrNull(result)),
                                        typeof(LicenseOps).Name,
                                        TracePriority.Highest);
#endif

                                    return code;
                                }
                            }
                        }

#if DEBUG || FORCE_TRACE
                        if (code == ReturnCode.Ok)
                        {
                            DebugTrace(String.Format(
                                "VerifyCertificate: Verified certificate {0}",
                                Utility.FormatTypeAndWrapOrNull(certificate)),
                                typeof(LicenseOps).Name, TracePriority.MediumHigh);
                        }
#endif

                        result = verifyResult;
                        return code;
#if DEBUG || FORCE_TRACE
                    }
                    finally
                    {
                        DateTime verifyStop = Utility.GetUtcNow();

                        DebugTrace(String.Format(
                            "VerifyCertificate: Verify {0} in {1} milliseconds.",
                            Utility.FormatWrapOrNull(code),
                            verifyStop.Subtract(verifyStart).TotalMilliseconds),
                            typeof(LicenseOps).Name, TracePriority.MediumHigh);
                    }
#endif
                }
            }
            finally
            {
                if (withAutoPath)
                    Utility.EndWithAutoPath(false, ref savedLibPath);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required. This method
        /// overload omits several parameters that are rarely used.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="policy">
        /// The flags that control if certificate subject name matching is to
        /// be performed based on the simple name and/or prefix.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the check to be performed even when it would
        /// otherwise be deemed unnecessary.
        /// </param>
        /// <param name="embedded">
        /// Non-zero to check for and attempt to use a certificate resource
        /// embedded in the plugin assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to use XML schema validation for certificates.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="anyClientData">
        /// The extra data to be supplied to the renewal callback, if any.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode VerifyCertificate(
            Interpreter interpreter,      /* in */
            Assembly assembly,            /* in */
            ExecutionPolicy? policy,      /* in */
            bool force,                   /* in */
            bool embedded,                /* in */
            bool validate,                /* in */
            bool isolated,                /* in */
            IAnyClientData anyClientData, /* in */
            ref string fileName,          /* in, out */
            ref object certificate,       /* out */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            return VerifyCertificate(
                interpreter, assembly, null, null, null, null, null,
                null, null, null, policy, null, null, null, null,
                false, force, embedded, validate, isolated, null,
                null, anyClientData, ref fileName, ref certificate,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate validation and verification functionality provided by
        /// the Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name to be used as the basis for locating the strong
        /// name key pair to validate the certificate against. This parameter
        /// may be null. If this parameter is null, the assembly itself will
        /// be queried for its name.
        /// </param>
        /// <param name="plugin">
        /// The plugin to be used as the basis for locating the embedded
        /// certificate resource, should that be necessary. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary. This parameter may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use when cryptographically
        /// checking the validity of the certificate against the strong name
        /// key pair. This parameter may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key to use in conjunction with the hash algorithm used when
        /// cryptographically checking the validity of the certificate against
        /// the strong name key pair. This parameter may be null.
        /// </param>
        /// <param name="encoding">
        /// The text encoding of the certificate data. This parameter may be
        /// null.
        /// </param>
        /// <param name="keyPairs">
        /// Collection of additional key pairs to use when cryptographically
        /// checking the validity of the certificate. This parameter may be
        /// null.
        /// </param>
        /// <param name="features">
        /// The feature flags required to be present in the certificate data.
        /// This parameter may be null.
        /// </param>
        /// <param name="restrictions">
        /// The restriction flags required to not be present in the
        /// certificate data. This parameter may be null.
        /// </param>
        /// <param name="policy">
        /// The flags that control if certificate subject name matching is to
        /// be performed based on the simple name and/or prefix.
        /// </param>
        /// <param name="keyName">
        /// The name of the public key pair to use when verifying the
        /// certificate data. This parameter may be null.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to use when verifying the certificate
        /// data. This parameter may be null.
        /// </param>
        /// <param name="timeout">
        /// Optional timeout in milliseconds to use for various network
        /// operations. If this parameter has a value of null, there will be
        /// no timeouts.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="renew">
        /// Non-zero to enable certificate renewal semantics. This will
        /// require the underlying support to be present in the license
        /// manager or the entire operation will fail.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the check to be performed even when it would
        /// otherwise be deemed unnecessary.
        /// </param>
        /// <param name="embedded">
        /// Non-zero to check for and attempt to use a certificate resource
        /// embedded in the plugin assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to use XML schema validation for certificates.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="fileNameCallback">
        /// The user-defined certificate file name selection callback, if any.
        /// If this is null the default semantics for certificate file name
        /// selection will be used.
        /// </param>
        /// <param name="renewDelegate">
        /// The user-defined certificate renewal callback, if any. If this is
        /// null and the certificate renewal semantics are enabled, the
        /// default callback will be used.
        /// </param>
        /// <param name="anyClientData">
        /// The extra data to be supplied to the renewal callback, if any.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this will contain a reference to the certificate
        /// object currently in use by the plugin. Upon failure, the value of
        /// this parameter is undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode VerifyCertificate( /* PRIMARY */
            Interpreter interpreter,                   /* in */
            Assembly assembly,                         /* in */
            AssemblyName assemblyName,                 /* in */
            IPlugin plugin,                            /* in */
            string hashAlgorithmName,                  /* in */
            byte[] hashKey,                            /* in */
            Encoding encoding,                         /* in */
            object keyPairs,                           /* in */
            string features,                           /* in */
            string restrictions,                       /* in */
            ExecutionPolicy? policy,                   /* in */
            string keyName,                            /* in */
            string keyRingName,                        /* in */
            int? timeout,                              /* in */
            ulong? id,                                 /* in */
            bool renew,                                /* in */
            bool force,                                /* in */
            bool embedded,                             /* in */
            bool validate,                             /* in */
            bool isolated,                             /* in */
            ElementSelectionCallback fileNameCallback, /* in */
            Delegate renewDelegate,                    /* in */
            IAnyClientData anyClientData,              /* in */
            ref string fileName,                       /* in, out */
            ref object certificate,                    /* out */
            ref Result result                          /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            #region Embedded Certificate Support (Recursive)
            Assembly resourceAssembly = (assembly != null) ?
                assembly : Assembly.GetExecutingAssembly();

            string resourceName = null;

            if (IsResourceFileName(
                    fileName, ref resourceAssembly, ref resourceName))
            {
                string temporaryDirectory = null;

                try
                {
                    string temporaryFileName;

                    if (ExtractCertificate(
                            resourceAssembly, resourceName,
                            out temporaryDirectory,
                            out temporaryFileName,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    /* RECURSIVE */
                    return VerifyCertificate(
                        interpreter, assembly, assemblyName,
                        plugin, hashAlgorithmName, hashKey,
                        encoding, keyPairs, features,
                        restrictions, policy, keyName,
                        keyRingName, timeout, id, renew,
                        force, embedded, validate, isolated,
                        fileNameCallback, renewDelegate,
                        anyClientData, ref temporaryFileName,
                        ref certificate, ref result);
                }
                finally
                {
                    if (temporaryDirectory != null)
                    {
                        /* IGNORED */
                        Utility.CleanupDirectory(temporaryDirectory,
                            new string[] { resourceName }, true);
                    }
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Isolated Plugin Support
#if ISOLATED_PLUGINS
            if (IsIsolated(interpreter, plugin, isolated))
            {
                try
                {
                    //
                    // NOTE: Attempt to find the license manager plugin loaded
                    //       into the interpreter.
                    //
                    // BUGFIX: Cannot use the candidate file name that was
                    //         (possibly) supplied by the caller here as it
                    //         may not actually correspond to the license
                    //         manager plugin itself.
                    //
                    IPlugin managerPlugin = GetPlugin(
                        interpreter, null, id, false, ref result);

                    if (managerPlugin == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Figure out where the strong name key pair to be
                    //       used for verification should come from.  This is
                    //       very important for third-party plugins as only
                    //       the AssemblyName type can be serialized to the
                    //       other application domain.  Without this, the
                    //       license manager cannot match the public key token
                    //       of the assembly with the one embedded in the
                    //       certificate, which will produce the "public key
                    //       token mismatch" error.
                    //
                    AssemblyName localAssemblyName = null;

                    if (assemblyName != null)
                        localAssemblyName = assemblyName;
                    else if (assembly != null)
                        localAssemblyName = assembly.GetName();

                    //
                    // NOTE: Build the input data for the request.  For this
                    //       request type, it consists of an array containing
                    //       the necessary input parameters.
                    //
                    object[] request = {
                        interpreter, null, localAssemblyName, plugin,
                        hashAlgorithmName, hashKey, encoding, keyPairs,
                        features, restrictions, policy, keyName, keyRingName,
                        timeout, force, embedded, validate, fileNameCallback,
                        renewDelegate, anyClientData, fileName
                    };

                    //
                    // NOTE: Setup the "well-known" configuration data using
                    //       the AppDomain for the manager plugin.
                    //
                    SetupWellKnownConfigurationData(managerPlugin.AppDomain);

                    //
                    // NOTE: Call into the manager plugin to request that the
                    //       certificate be verified.
                    //
                    object response = null;

                    if (managerPlugin.Execute(
                            interpreter, new ClientData(VerifyMethodName),
                            request, ref response, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Verify the response object is an object array.
                    //
                    if (!(response is object[]))
                    {
                        result = String.Format(
                            "malformed response: have type {0}, " +
                            "need type {1}: {2}", (response != null) ?
                                response.GetType() : typeof(object),
                            typeof(object[]), Utility.FormatWrapOrNull(
                            response));

                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Convert the response object to an object array
                    //       and verify that it has at least three elements.
                    //
                    object[] args = (object[])response;

                    //
                    // NOTE: Next, grab the length of the array of arguments
                    //       that we just grabbed.
                    //
                    int length = args.Length;

                    if (length < 3)
                    {
                        result = String.Format(
                            "malformed response: have {0} array elements, " +
                            "need at least 3", length);

                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Upon success, assume response can be converted
                    //       to string.
                    //
                    fileName = ValueToString(args[0]);
                    certificate = args[1];
                    result = ValueToString(args[2]);

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown somewhere.  Record the
                    //       details in the result variable provided by the
                    //       caller.
                    //
                    result = e;
                }

                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Non-Isolated Plugin Support
            try
            {
                //
                // NOTE: Figure out where the strong name key pair to be
                //       used for verification should come from.  This is
                //       very important for third-party plugins as only
                //       the AssemblyName type can be serialized to the
                //       other application domain.  Without this, the
                //       license manager cannot match the public key token
                //       of the assembly with the one embedded in the
                //       certificate, which will produce the "public key
                //       token mismatch" error.
                //
                AssemblyName localAssemblyName = null;

                if (assemblyName != null)
                    localAssemblyName = assemblyName;
                else if (assembly != null)
                    localAssemblyName = assembly.GetName();

                //
                // NOTE: Use any available license manager package?
                //
                bool useAnyPackage = UseAnyPackage(localAssemblyName);

                //
                // NOTE: Attempt to create the primary license manager
                //       component now.  If this fails, we cannot continue.
                //
                // BUGFIX: Cannot use the candidate file name that was
                //         (possibly) supplied by the caller here as it
                //         may not actually correspond to the license
                //         manager plugin itself.
                //
                object manager = CreateInstance(
                    interpreter, plugin, null, id, isolated,
                    useAnyPackage, SdkAllowAssemblyNameOnly,
                    ref result);

                if (manager == null)
                    return ReturnCode.Error;

                //
                // NOTE: Next, grab underlying type of the license manager.
                //       Based on how the CLR works, this should never fail;
                //       however, check the return value anyhow.  If this is
                //       invalid (null) for some reason, we failed because it
                //       is required for some of the subsequent steps.
                //
                Type managerType = manager.GetType();

                if (managerType == null) /* NEVER */
                {
                    result = "license manager type is invalid";
                    return ReturnCode.Error;
                }

                //
                // NOTE: This variable will be used to hold the delegate used
                //       by the license manager to renew the certificate should
                //       the need arise, if any.  If this value is null (for
                //       whatever reason) and certificate renewal is actually
                //       required, the entire operation will fail.
                //
                Delegate localRenewDelegate = null;

                //
                // NOTE: Enable the optional certificate renewal semantics with
                //       a user-defined callback?
                //
                if (renew && (renewDelegate != null))
                {
                    localRenewDelegate = renewDelegate;
                }
                //
                // NOTE: Enable the optional certificate renewal semantics with
                //       the default callback?  This will not work from inside
                //       of an isolated application domain.
                //
                else if (renew)
                {
                    //
                    // NOTE: First, grab the assembly containing the license
                    //       manager components.  If we cannot find (or load)
                    //       it, we failed because it is required for all the
                    //       subsequent steps.
                    //
                    // BUGFIX: Cannot use the candidate file name that was
                    //         (possibly) supplied by the caller here as it
                    //         may not actually correspond to the license
                    //         manager plugin itself.
                    //
                    Assembly managerAssembly = GetAssembly(
                        interpreter, null, id, useAnyPackage,
                        SdkAllowAssemblyNameOnly, ref result);

                    if (managerAssembly == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Next, grab the [delegate] type for the certificate
                    //       renewal callback via reflection.  If we cannot
                    //       find it, we failed because it is required for some
                    //       of the subsequent steps.  This value, combined
                    //       with the late-bound method information (below),
                    //       will be used to build the actual delegate to be
                    //       passed to the license manager certificate
                    //       verification subsystem.
                    //
                    Type renewDelegateType = managerAssembly.GetType(
                         RenewDelegateTypeName);

                    if (renewDelegateType == null)
                    {
                        result = "cannot get renewal callback delegate type";
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Next, grab the license manager method information
                    //       to be used as the basis for the certificate
                    //       renewal callback via reflection.  If we cannot
                    //       find it, we failed because it is required some of
                    //       the subsequent steps.  This value, combined with
                    //       the late-bound type information (above), will be
                    //       used to build the actual delegate to be passed to
                    //       the license manager certificate verification
                    //       subsystem.
                    //
                    MethodInfo renewMethodInfo = managerType.GetMethod(
                        RenewMethodName, renewMethodBindingFlags);

                    if (renewMethodInfo == null)
                    {
                        result = "cannot get renewal callback method";
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Next, attempt to create the actual delegate that
                    //       will be used for for the certificate renewal
                    //       callback.  If we cannot create it, we failed
                    //       because it is required some of the subsequent
                    //       steps.
                    //
                    localRenewDelegate = Delegate.CreateDelegate(
                        renewDelegateType, manager, renewMethodInfo);

                    if (localRenewDelegate == null) /* NEVER */
                    {
                        result = "cannot create renewal delegate";
                        return ReturnCode.Error;
                    }
                }

                //
                // NOTE: Next, create the array of arguments to pass in the
                //       (late-bound) method call to the license manager
                //       certificate verification subsystem.
                //
                object[] args = {
                    interpreter, assembly, localAssemblyName, plugin,
                    hashAlgorithmName, hashKey, encoding, keyPairs,
                    features, restrictions, policy, keyName, keyRingName,
                    timeout, force, embedded, validate, fileNameCallback,
                    localRenewDelegate, anyClientData, fileName, certificate,
                    result
                };

                //
                // NOTE: Next, grab the length of the array of arguments that we
                //       just created.
                //
                int length = args.Length;

                //
                // NOTE: Setup the "well-known" configuration data within the
                //       current AppDomain, since this call should not cross an
                //       AppDomain boundary.
                //
                SetupWellKnownConfigurationData(AppDomain.CurrentDomain);

                //
                // NOTE: Next, invoke license manager verification subsystem
                //       via reflection.  The return value here must be an
                //       Eagle return code or the cast will cause an exception
                //       to be thrown.
                //
                ReturnCode code = (ReturnCode)managerType.InvokeMember(
                    VerifyMethodName, verifyMethodBindingFlags, null, manager,
                    args);

                //
                // NOTE: Next, if the certificate validation and verification
                //       succeeded, fetch the [potentially modified] file name
                //       and the certificate object from the argument array and
                //       update the variables provided by the caller with their
                //       new values.
                //
                if (code == ReturnCode.Ok)
                {
                    fileName = args[length - 3] as string;
                    certificate = args[length - 2] as object;
                }

                //
                // NOTE: Finally, always update the overall result (or error
                //       message) in the variable provided by the caller.
                //
                result = args[length - 1] as Result;

                return code;
            }
            catch (Exception e)
            {
                //
                // NOTE: An exception was thrown somewhere.  Record details
                //       in the result variable provided by the caller.
                //
                result = e;
            }

            return ReturnCode.Error;
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to query
        /// the certificate entity name without having to early-bind (i.e. add
        /// a reference) to the license manager assembly (i.e. by using
        /// reflection internally). However, a reference to the Eagle core
        /// library assembly itself is still required. The Eagle "license
        /// manager" (i.e. "Harpy") plugin must already be loaded into the
        /// provided interpreter and the <c>[certificate metadata]</c> sub-command
        /// must be available.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to fetch the
        /// certificate entity name, if any.
        /// </param>
        /// <param name="certificate">
        /// This must contain a reference to the certificate object currently
        /// in use by the plugin.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode QueryCertificateEntityName(
            Interpreter interpreter, /* in */
            object certificate,      /* in */
            ref Result result        /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            return QueryCertificateProperty(
                interpreter, certificate, CertificateEntityNameProperty,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// certificate flag checking functionality provided by the Eagle
        /// "license manager" (i.e. "Harpy") without having to early-bind
        /// (i.e. add a reference) to the license manager assembly (i.e. by
        /// using reflection internally). However, a reference to the Eagle
        /// core library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="plugin">
        /// The plugin to be used as the basis for locating the embedded
        /// certificate resource, should that be necessary. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary. This parameter may be null.
        /// </param>
        /// <param name="certificate">
        /// This must contain a reference to the certificate object currently
        /// in use by the plugin.
        /// </param>
        /// <param name="type">
        /// This must contain the integer value 1 to check feature flags or 2
        /// to check restriction flags. The actual type of this parameter is
        /// FlagType; however, using that type would require early binding to
        /// the license manager assembly.
        /// </param>
        /// <param name="key">
        /// The integer used to identity the selected group of flags. A value
        /// of zero should be used to select the default group of flags.
        /// </param>
        /// <param name="hasFlags">
        /// This must contain the flags that are required to be present, if
        /// any.
        /// </param>
        /// <param name="notHasFlags">
        /// This must contain the flags that are required to be absent, if
        /// any.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require every flag in the "hasFlags" parameter to be
        /// present.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require every flag in the "notHasFlags" parameter to
        /// be absent.
        /// </param>
        /// <param name="strict">
        /// Non-zero if the certificate flag matching is to be performed based
        /// only on the allowed list of flag characters.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode MatchCertificateFlags( /* PRIMARY */
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            object certificate,      /* in */
            /* FlagType */ int type, /* in */
            long key,                /* in */
            string hasFlags,         /* in */
            string notHasFlags,      /* in */
            bool hasAll,             /* in */
            bool notHasAll,          /* in */
            bool strict,             /* in */
            bool isolated,           /* in */
            ref Result result        /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            #region Isolated Plugin Support
#if ISOLATED_PLUGINS
            if (IsIsolated(interpreter, plugin, isolated))
            {
                try
                {
                    //
                    // NOTE: Attempt to find the license manager plugin loaded
                    //       into the interpreter.
                    //
                    IPlugin managerPlugin = GetPlugin(
                        interpreter, null, null, false, ref result);

                    if (managerPlugin == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Build the input data for the request.  For this
                    //       request type, it consists of an array containing
                    //       the necessary input parameters.
                    //
                    object[] request = {
                        plugin, certificate, type, key, hasFlags, notHasFlags,
                        hasAll, notHasAll, strict
                    };

                    //
                    // NOTE: Setup the "well-known" configuration data using
                    //       the AppDomain for the manager plugin.
                    //
                    SetupWellKnownConfigurationData(managerPlugin.AppDomain);

                    //
                    // NOTE: Call into the manager plugin to request that the
                    //       certificate flags be matched against the specified
                    //       criteria.
                    //
                    object response = null;

                    if (managerPlugin.Execute(interpreter,
                            new ClientData(MatchFlagsMethodName), request,
                            ref response, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Upon success, assume response can be converted
                    //       to a string.
                    //
                    result = ValueToString(response);

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown somewhere.  Record the
                    //       details in the result variable provided by the
                    //       caller.
                    //
                    result = e;
                }

                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Non-Isolated Plugin Support
            try
            {
                //
                // NOTE: Use any available license manager package?
                //
                bool useAnyPackage = UseAnyPackage(
                    (plugin != null) ? plugin.AssemblyName : null);

                //
                // NOTE: Attempt to create the primary license manager
                //       component now.  If this fails, we cannot continue.
                //
                object manager = CreateInstance(
                    interpreter, plugin, null, null, isolated,
                    useAnyPackage, SdkAllowAssemblyNameOnly,
                    ref result);

                if (manager == null)
                    return ReturnCode.Error;

                //
                // NOTE: Next, grab underlying type of the license manager.
                //       Based on how the CLR works, this should never fail;
                //       however, check the return value anyhow.  If this is
                //       invalid (null) for some reason, we failed because it
                //       is required for some of the subsequent steps.
                //
                Type managerType = manager.GetType();

                if (managerType == null) /* NEVER */
                {
                    result = "license manager type is invalid";
                    return ReturnCode.Error;
                }

                //
                // NOTE: Next, create the array of arguments to pass in the
                //       (late-bound) method call to the license manager
                //       certificate flag checking subsystem.
                //
                object[] args = {
                    plugin, certificate, type, key, hasFlags, notHasFlags,
                    hasAll, notHasAll, strict, result
                };

                //
                // NOTE: Next, grab the length of the array of arguments that
                //       we just created.
                //
                int length = args.Length;

                //
                // NOTE: Setup the "well-known" configuration data within the
                //       current AppDomain, since this call should not cross an
                //       AppDomain boundary.
                //
                SetupWellKnownConfigurationData(AppDomain.CurrentDomain);

                //
                // NOTE: Next, invoke license manager verification subsystem
                //       via reflection.  The return value here must be an
                //       Eagle return code or the cast will cause an exception
                //       to be thrown.
                //
                ReturnCode code = (ReturnCode)managerType.InvokeMember(
                    MatchFlagsMethodName, matchFlagsMethodBindingFlags, null,
                    manager, args);

                //
                // NOTE: Finally, always update the overall result (or error
                //       message) in the variable provided by the caller.
                //
                result = args[length - 1] as Result;

                return code;
            }
            catch (Exception e)
            {
                //
                // NOTE: An exception was thrown somewhere.  Record details
                //       in the result variable provided by the caller.
                //
                result = e;
            }

            return ReturnCode.Error;
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// "raw signed" script file evaluation functionality provided by the
        /// Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required.
        /// </summary>
        /// <param name="plugin">
        /// The plugin to be used as the basis for locating the embedded
        /// certificate resource, should that be necessary. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary. This parameter may be null.
        /// </param>
        /// <param name="variantName">
        /// The name for the build configuration variant being used.
        /// </param>
        /// <param name="anyClientData">
        /// The <see cref="IAnyClientData" /> instance containing parameters
        /// used to control the script evaluation. Please refer to the main
        /// method overload (below) for more details.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode EvaluateFile(
            IPlugin plugin,               /* in */
            string variantName,           /* in */
            IAnyClientData anyClientData, /* in */
            bool isolated,                /* in */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            bool wasOverridden;
            string binaryPath;

            binaryPath = GetBinaryPath(out wasOverridden);

            CreateFlags createFlags = EvaluateCreateFlags;
            HostCreateFlags hostCreateFlags = SdkHostCreateFlags;
            InitializeFlags initializeFlags = EvaluateInitializeFlags;
            PluginFlags pluginFlags = SdkPluginFlags;

#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            if (Utility.DoesEnvironmentVariableExist(
                    ManagerNoProbePluginsEnvVarName))
            {
                createFlags &= ~CreateFlags.ProbePlugins;
                initializeFlags &= ~InitializeFlags.AutoPath;
            }
#endif

            if (Utility.DoesEnvironmentVariableExist(
                    ManagerWellKnownPluginsEnvVarName))
            {
                initializeFlags |= InitializeFlags.WellKnown;
            }

            HostCreateFlags embeddedConsoleUse =
                HostCreateFlags.EmbeddedConsoleUse;

            if (Utility.DoesEnvironmentVariableExist(
                    ManagerEvaluateWithConsole))
            {
                hostCreateFlags &= ~HostCreateFlags.NoConsole;
                hostCreateFlags |= embeddedConsoleUse;
            }
            else
            {
                hostCreateFlags &= ~embeddedConsoleUse;
                hostCreateFlags |= HostCreateFlags.NoConsole;
            }

#if ISOLATED_PLUGINS
            //
            // HACK: Permit plugin isolation to be disabled
            //       because it is very expensive.  Creating
            //       a new AppDomain is most of that cost.
            //       By default, plugin isolation is enabled
            //       for extra security.
            //
            if (Utility.DoesEnvironmentVariableExist(
                    ManagerNoIsolatedEnvVarName))
            {
                pluginFlags &= ~PluginFlags.Isolated;
            }
#endif

            ulong? interpreterToken = GetInterpreterForEvaluate();
            bool newInterpreterToken = false;

            if (interpreterToken == null)
            {
                interpreterToken = Utility.GetRandomNumber();
                newInterpreterToken = true;
            }

            MaybeAddExitedEventHandlers();

            int retries = 0;

        retry:

#if DEBUG || FORCE_TRACE
            DateTime createStart = Utility.GetUtcNow();
#endif

            Result localResult = null;

            using (Interpreter interpreter = Interpreter.Create(
                    interpreterToken, null, createFlags,
                    hostCreateFlags, initializeFlags,
                    SdkScriptFlags, SdkInterpreterFlags,
                    pluginFlags, null, ref localResult))
            {
#if DEBUG || FORCE_TRACE
                DateTime createStop = Utility.GetUtcNow();

                DebugTrace(String.Format(
                    "EvaluateFile: Create {0} in {1} milliseconds.",
                    (interpreter != null) ? "SUCCESS" : "FAILURE",
                    createStop.Subtract(createStart).TotalMilliseconds),
                    typeof(LicenseOps).Name, TracePriority.MediumHigh);
#endif

                if (interpreter == null)
                {
                    if (Utility.DoesEnvironmentVariableExist(
                            ManagerRetryCreation) &&
                        (retries++ < GetMaximumRetries()))
                    {
                        Sleep();
                        goto retry;
                    }

                    result = localResult;
                    return ReturnCode.Error;
                }

                //
                // HACK: Attempt to prevent too much large object heap
                //       (LOH) memory usage.
                //
                /* IGNORED */
                interpreter.EnableCaches(SdkCacheFlags, false);

                //
                // HACK: When using IfNecessary and/or IfCannotLock
                //       flags, it is necessary to double-check that
                //       the token of the returned (i.e. "created")
                //       interpreter matches what we think it should,
                //       i.e. just in case a temporary interpreter had
                //       to be created due to internal locking issues.
                //
                if (!newInterpreterToken &&
                    (interpreter.CreateCount == 1))
                {
                    newInterpreterToken = true;

#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "EvaluateFile: Unexpectedly created " +
                        "interpreter.", typeof(LicenseOps).Name,
                        TracePriority.High);
#endif
                }

                bool mismatchedToken = false;

                if (!interpreter.MatchToken(interpreterToken))
                {
                    mismatchedToken = true;

#if DEBUG || FORCE_TRACE
                    DebugTrace(
                        "EvaluateFile: Unexpectedly mismatched " +
                        "interpreter.", typeof(LicenseOps).Name,
                        TracePriority.High);
#endif
                }

                if (newInterpreterToken && !mismatchedToken)
                    SetInterpreterForEvaluate(interpreterToken);

                string packageCommandName = ManagerPackageCommandName;
                string loadCommandName = ManagerLoadCommandName;
                Result assemblyError = null;

                if (newInterpreterToken)
                {
                    string assemblyPath;

                    assemblyPath = GetManagerAssemblyPath(
                        binaryPath, ref assemblyError);

                    if (assemblyPath != null)
                    {
#if DEBUG || FORCE_TRACE
                        DebugTrace(String.Format(
                            "EvaluateFile: Found the manager " +
                            "assembly path {0} using binary path {1}, " +
                            "which was {2}manually overridden: {3}",
                            Utility.FormatWrapOrNull(assemblyPath),
                            Utility.FormatWrapOrNull(binaryPath),
                            wasOverridden ? String.Empty : "not ",
                            Utility.FormatDefineConstants(
                                DefineConstants.OptionList)),
                            typeof(LicenseOps).Name,
                            TracePriority.MediumHigh);

                        CheckDefineConstants();
#endif

                        localResult = null;

                        if (ProvideManagerPackage(
                                interpreter, assemblyPath,
                                packageCommandName, loadCommandName,
                                ref localResult) != ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }
#if DEBUG || FORCE_TRACE
                    else
                    {
                        DebugTrace(String.Format(
                            "EvaluateFile: Missing the manager " +
                            "assembly path using binary path {0}, " +
                            "which was {1}manually overridden: {2}",
                            Utility.FormatWrapOrNull(binaryPath),
                            wasOverridden ? String.Empty : "not ",
                            Utility.FormatWrapOrNull(assemblyError)),
                            typeof(LicenseOps).Name,
                            TracePriority.MediumHigh);
                    }
#endif
                }

                return EvaluateFile(
                    interpreter, plugin, variantName, 0, anyClientData,
                    isolated, ref result);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// "raw signed" script file evaluation functionality provided by the
        /// Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="plugin">
        /// The plugin to be used as the basis for locating the embedded
        /// certificate resource, should that be necessary. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary. This parameter may be null.
        /// </param>
        /// <param name="variantName">
        /// The name for the build configuration variant being used.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="anyClientData">
        /// The <see cref="IAnyClientData" /> instance containing parameters
        /// used to control the script evaluation. See the primary overload of
        /// this method (below) for full details.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode EvaluateFile(
            Interpreter interpreter,      /* in */
            IPlugin plugin,               /* in */
            string variantName,           /* in */
            ulong? id,                    /* in */
            IAnyClientData anyClientData, /* in */
            bool isolated,                /* in */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            string fileName = null; /* NOT USED */

            return EvaluateFile(
                interpreter, null, plugin, variantName, id, anyClientData,
                isolated, ref fileName, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method allows a third-party plugin or application to use the
        /// "raw signed" script file evaluation functionality provided by the
        /// Eagle "license manager" (i.e. "Harpy") without having to
        /// early-bind (i.e. add a reference) to the license manager assembly
        /// (i.e. by using reflection internally). However, a reference to the
        /// Eagle core library assembly itself is still required.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to be used when attempting to load the
        /// assembly containing the license manager components (if required),
        /// if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly to be used as the basis for locating the strong name
        /// key pair to validate the certificate against. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary.
        /// </param>
        /// <param name="plugin">
        /// The plugin to be used as the basis for locating the embedded
        /// certificate resource, should that be necessary. Also used as the
        /// basis for automatically locating the external certificate file
        /// itself, should that be necessary. This parameter may be null.
        /// </param>
        /// <param name="variantName">
        /// The name for the build configuration variant being used.
        /// </param>
        /// <param name="id">
        /// Unique identifier to use when building the (obfuscated) command
        /// names. If this parameter has a value of zero, the command name
        /// will be returned verbatim.
        /// </param>
        /// <param name="anyClientData">
        /// The <see cref="IAnyClientData" /> instance containing parameters
        /// used to control the script evaluation. The following parameters
        /// are currently supported:
        ///
        ///   "data"                (OPTIONAL: System.Object)
        ///   "name"                (OPTIONAL: System.String)
        ///   "id"                  (OPTIONAL: System.Guid)
        ///   "group"               (OPTIONAL: System.String)
        ///   "description"         (OPTIONAL: System.String)
        ///   "contextName"         (OPTIONAL: System.String)
        ///   "refreshEvent"        (OPTIONAL: S.Threading.EventWaitHandle)
        ///   "sandboxToken"        (OPTIONAL: System.UInt64?)
        ///   "commandTokens"       (OPTIONAL: EConP.LongList)
        ///   "useSettingsCallback" (OPTIONAL: System.Boolean)
        ///   "ruleSet"             (OPTIONAL: EIP.IRuleSet)
        ///   "interpreter"         (OPTIONAL: EComP.Interpreter)
        ///   "plugin"              (OPTIONAL: EIP.IPlugin)
        ///   "minimumVersion"      (OPTIONAL: System.Version)
        ///   "maximumVersion"      (OPTIONAL: System.Version)
        ///   "variantName"         (OPTIONAL: System.String)
        ///   "hashAlgorithmName"   (OPTIONAL: System.String)
        ///   "hashKey"             (OPTIONAL: System.Byte[])
        ///   "encoding"            (OPTIONAL: System.Text.Encoding)
        ///   "type"                (OPTIONAL: System.String)
        ///   "subType"             (OPTIONAL: System.String)
        ///   "directory"           (OPTIONAL: System.String)
        ///   "fileName"            (REQUIRED: System.String)
        ///   "stream"              (OPTIONAL: System.IO.Stream)
        ///   "keyPairs"            (OPTIONAL: SCG.IEnumerable`1[LIP.IKeyPair])
        ///   "keyPair"             (OPTIONAL: LIP.IKeyPair)
        ///   "keyName"             (OPTIONAL: System.String)
        ///   "keyRingName"         (OPTIONAL: System.String)
        ///   "hashValue"           (OPTIONAL: System.Byte[])
        ///   "signature"           (OPTIONAL: System.Byte[])
        ///   "keyUsage"            (OPTIONAL: System.String)
        ///   "configurationPhase"  (OPTIONAL: LCP.ConfigurationPhase)
        ///   "trustFlags"          (OPTIONAL: EComP.TrustFlags)
        ///   "policyType"          (OPTIONAL: EComP.PolicyType?)
        ///   "policy"              (OPTIONAL: EComP.ExecutionPolicy?)
        ///   "timeout"             (OPTIONAL: System.Int32?)
        ///   "referenceCount"      (OPTIONAL: System.Int32)
        ///   "untrusted"           (OPTIONAL: System.Boolean)
        ///   "allowRemoteUri"      (OPTIONAL: System.Boolean)
        ///   "useContext"          (OPTIONAL: System.Boolean)
        ///   "withCommands"        (OPTIONAL: System.Boolean)
        ///   "removeCommands"      (OPTIONAL: System.Boolean)
        ///   "swapCommands"        (OPTIONAL: System.Boolean)
        ///   "noGlobalOnly"        (OPTIONAL: System.Boolean)
        ///   "allowLocalPolicy"    (OPTIONAL: System.Boolean)
        ///   "extractAndApply"     (OPTIONAL: System.Boolean)
        ///   "failOnError"         (OPTIONAL: System.Boolean)
        ///   "fatalError"          (OPTIONAL: System.Boolean)
        ///
        /// Additional parameters may be added in the future. Unrecognized
        /// parameters will be ignored. Most of these parameters are optional
        /// (i.e. when absent, a suitable default value will be used). If any
        /// of the required parameters are missing, the script evaluation will
        /// not succeed -AND- an appropriate error will be returned.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to force the isolated mode to be used.
        /// </param>
        /// <param name="fileName">
        /// This parameter is used for both input and output. Upon entry, this
        /// file name, if any, will be used as the location of the external
        /// certificate file. Upon success, this will contain the fully
        /// qualified path and file name of the certificate currently in use
        /// by the plugin. Upon failure, the value of this parameter is
        /// undefined.
        /// </param>
        /// <param name="result">
        /// Upon success, this will contain the result of the method. Upon
        /// failure, this will contain an appropriate error message.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success, ReturnCode.Error on failure.
        /// </returns>
        public static ReturnCode EvaluateFile( /* PRIMARY */
            Interpreter interpreter,      /* in */
            Assembly assembly,            /* in */
            IPlugin plugin,               /* in */
            string variantName,           /* in */
            ulong? id,                    /* in */
            IAnyClientData anyClientData, /* in */
            bool isolated,                /* in */
            ref string fileName,          /* in, out */
            ref Result result             /* out */
            ) /* ENTRY-POINT, THREAD-SAFE, REENTRANT */
        {
            #region Embedded Certificate Support (Recursive)
            Assembly resourceAssembly = (assembly != null) ?
                assembly : Assembly.GetExecutingAssembly();

            string resourceName = null;

            if (IsResourceFileName(
                    fileName, ref resourceAssembly, ref resourceName))
            {
                string temporaryDirectory = null;

                try
                {
                    string temporaryFileName;

                    if (ExtractCertificate(
                            resourceAssembly, resourceName,
                            out temporaryDirectory,
                            out temporaryFileName,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    /* RECURSIVE */
                    return EvaluateFile(
                        interpreter, assembly, plugin,
                        variantName, id, anyClientData,
                        isolated, ref temporaryFileName,
                        ref result);
                }
                finally
                {
                    if (temporaryDirectory != null)
                    {
                        /* IGNORED */
                        Utility.CleanupDirectory(temporaryDirectory,
                            new string[] { resourceName }, true);
                    }
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Isolated Plugin Support
#if ISOLATED_PLUGINS
            if (IsIsolated(interpreter, plugin, isolated))
            {
                try
                {
                    //
                    // NOTE: Attempt to find the license manager plugin loaded
                    //       into the interpreter.
                    //
                    IPlugin managerPlugin = GetPlugin(
                        interpreter, null, id, false, ref result);

                    if (managerPlugin == null)
                        return ReturnCode.Error;

                    //
                    // NOTE: Build the input data for the request.  For this
                    //       request type, it consists of an array containing
                    //       the necessary input parameters.
                    //
                    object[] request = {
                        interpreter, plugin, variantName, anyClientData
                    };

                    //
                    // NOTE: Setup the "well-known" configuration data using
                    //       the AppDomain for the manager plugin.
                    //
                    SetupWellKnownConfigurationData(managerPlugin.AppDomain);

                    //
                    // NOTE: Call into the manager plugin to request that the
                    //       certificate flags be matched against the specified
                    //       criteria.
                    //
                    object response = null;

                    if (managerPlugin.Execute(interpreter,
                            new ClientData(EvaluateFileMethodName), request,
                            ref response, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Upon success, assume response can be converted
                    //       to a string.
                    //
                    result = ValueToString(response);

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown somewhere.  Record the
                    //       details in the result variable provided by the
                    //       caller.
                    //
                    result = e;
                }

                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Non-Isolated Plugin Support
            try
            {
                //
                // NOTE: Use any available license manager package?
                //
                bool useAnyPackage = UseAnyPackage(
                    (plugin != null) ? plugin.AssemblyName : null);

                //
                // NOTE: Attempt to create the primary license manager
                //       component now.  If this fails, we cannot continue.
                //
                object manager = CreateInstance(
                    interpreter, plugin, null, id, isolated,
                    useAnyPackage, SdkAllowAssemblyNameOnly,
                    ref result);

                if (manager == null)
                    return ReturnCode.Error;

                //
                // NOTE: Next, grab underlying type of the license manager.
                //       Based on how the CLR works, this should never fail;
                //       however, check the return value anyhow.  If this is
                //       invalid (null) for some reason, we failed because it
                //       is required for some of the subsequent steps.
                //
                Type managerType = manager.GetType();

                if (managerType == null) /* NEVER */
                {
                    result = "license manager type is invalid";
                    return ReturnCode.Error;
                }

                //
                // NOTE: Next, create the array of arguments to pass in the
                //       (late-bound) method call to the license manager
                //       certificate flag checking subsystem.
                //
                object[] args = {
                    interpreter, plugin, variantName, anyClientData, result
                };

                //
                // NOTE: Next, grab the length of the array of arguments that
                //       we just created.
                //
                int length = args.Length;

                //
                // NOTE: Setup the "well-known" configuration data within the
                //       current AppDomain, since this call should not cross an
                //       AppDomain boundary.
                //
                SetupWellKnownConfigurationData(AppDomain.CurrentDomain);

                //
                // NOTE: Next, invoke license manager verification subsystem
                //       via reflection.  The return value here must be an
                //       Eagle return code or the cast will cause an exception
                //       to be thrown.
                //
                ReturnCode code = (ReturnCode)managerType.InvokeMember(
                    EvaluateFileMethodName, evaluateFileMethodBindingFlags,
                    null, manager, args);

                //
                // NOTE: Finally, always update the overall result (or error
                //       message) in the variable provided by the caller.
                //
                result = args[length - 1] as Result;

                return code;
            }
            catch (Exception e)
            {
                //
                // NOTE: An exception was thrown somewhere.  Record details
                //       in the result variable provided by the caller.
                //
                result = e;
            }

            return ReturnCode.Error;
            #endregion
        }
        #endregion
    }
}
