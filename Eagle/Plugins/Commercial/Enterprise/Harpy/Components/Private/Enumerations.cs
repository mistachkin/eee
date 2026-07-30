/*
 * Enumerations.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Flags that control which optional support diagnostics are gathered,
    /// and how extra diagnostic behavior is enabled or disabled.
    /// </summary>
    [Flags()]
    [ObjectId("ba6f5111-0652-4bbb-82b9-de66bc876484")]
    internal enum SupportDiagnostic : ulong
    {
        /// <summary>
        /// No support diagnostics; do not use.
        /// </summary>
        None = 0,
        /// <summary>
        /// Invalid support diagnostic flag; do not use.
        /// </summary>
        Invalid = 1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether extra diagnostics are enabled.
        /// </summary>
        GetExtraDiagnostics = 0x100,
        /// <summary>
        /// Enable extra diagnostics (unsupported at compile-time).
        /// </summary>
        EnableExtraDiagnostics = 0x200,    // unsupported (compile-time)
        /// <summary>
        /// Disable extra diagnostics (unsupported at compile-time).
        /// </summary>
        DisableExtraDiagnostics = 0x400,   // unsupported (compile-time)

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether tracing is being forced.
        /// </summary>
        GetForceTrace = 0x1000,
        /// <summary>
        /// Enable forced tracing (unsupported at compile-time).
        /// </summary>
        EnableForceTrace = 0x2000,         // unsupported (compile-time)
        /// <summary>
        /// Disable forced tracing (unsupported at compile-time).
        /// </summary>
        DisableForceTrace = 0x4000,        // unsupported (compile-time)

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether URI diagnostics are enabled.
        /// </summary>
        GetUri = 0x10000,
        /// <summary>
        /// Enable URI diagnostics (unsupported, not implemented).
        /// </summary>
        EnableUri = 0x20000,               // unsupported (not implemented)
        /// <summary>
        /// Disable URI diagnostics (unsupported, not implemented).
        /// </summary>
        DisableUri = 0x40000,              // unsupported (not implemented)

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether error normalization is enabled.
        /// </summary>
        GetNormalizeErrors = 0x100000,
        /// <summary>
        /// Enable error normalization.
        /// </summary>
        EnableNormalizeErrors = 0x200000,
        /// <summary>
        /// Disable error normalization.
        /// </summary>
        DisableNormalizeErrors = 0x400000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether the public key token is included.
        /// </summary>
        GetIncludePublicKeyToken = 0x1000000,
        /// <summary>
        /// Enable inclusion of the public key token.
        /// </summary>
        EnableIncludePublicKeyToken = 0x2000000,
        /// <summary>
        /// Disable inclusion of the public key token.
        /// </summary>
        DisableIncludePublicKeyToken = 0x4000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether tracing is enabled.
        /// </summary>
        GetTracing = 0x10000000,
        /// <summary>
        /// Enable tracing.
        /// </summary>
        EnableTracing = 0x20000000,
        /// <summary>
        /// Disable tracing.
        /// </summary>
        DisableTracing = 0x40000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Query whether log file names are reported.
        /// </summary>
        GetLogFileNames = 0x100000000,
        /// <summary>
        /// Enable reporting of log file names (unsupported, not implemented).
        /// </summary>
        EnableLogFileNames = 0x200000000,  // unsupported (not implemented)
        /// <summary>
        /// Disable reporting of log file names (unsupported, not
        /// implemented).
        /// </summary>
        DisableLogFileNames = 0x400000000, // unsupported (not implemented)

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x800000000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of support diagnostic flags.
        /// </summary>
        Default = ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that indicate how a command should be handled when it is not
    /// directly available, e.g. via the debugger, warnings, grace periods,
    /// web, or remote handling.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("d996d4e2-c266-4880-98d6-5195761ee264")]
    internal enum NotCommandFlags : ulong
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Only applies when running under the debugger.
        /// </summary>
        DebuggerOnly = 0x1000,
        /// <summary>
        /// Only emit a warning instead of failing.
        /// </summary>
        WarnOnly = 0x2000,
        /// <summary>
        /// First grace level.
        /// </summary>
        Grace1 = 0x4000,
        /// <summary>
        /// Second grace level.
        /// </summary>
        Grace2 = 0x8000,
        /// <summary>
        /// Third grace level.
        /// </summary>
        Grace3 = 0x10000,
        /// <summary>
        /// Handle the command via the web.
        /// </summary>
        Web = 0x20000,
        /// <summary>
        /// Handle the command via a remote endpoint.
        /// </summary>
        Remote = 0x40000,
        /// <summary>
        /// Fail immediately (fail-fast) on error.
        /// </summary>
        FailFast = 0x80000,
        /// <summary>
        /// Fail safely on error.
        /// </summary>
        FailSafe = 0x100000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Wait for the web handling to complete.
        /// </summary>
        WaitForWeb = 0x1000000000,
        /// <summary>
        /// Wait for the remote handling to complete.
        /// </summary>
        WaitForRemote = 0x2000000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of not-command flags.
        /// </summary>
        Default = DebuggerOnly | Grace1 | Web | Grace2 |
                  Remote | WaitForRemote | Grace3
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control the level of restriction applied, e.g. safe mode,
    /// hiding unsafe commands, SDK, and security restrictions.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("1a8d710c-f819-4d21-abfb-2ebf21bb20b6")]
    internal enum RestrictionFlags
    {
        /// <summary>
        /// No restrictions; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Apply safe-mode restrictions.
        /// </summary>
        Safe = 0x1000,
        /// <summary>
        /// Hide unsafe commands.
        /// </summary>
        HideUnsafe = 0x2000,
        /// <summary>
        /// Apply SDK restrictions.
        /// </summary>
        Sdk = 0x4000,
        /// <summary>
        /// Apply security restrictions.
        /// </summary>
        Security = 0x8000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x10000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The set of restrictions that constitute a restricted environment.
        /// </summary>
        Restricted = Safe | HideUnsafe | Sdk,
        /// <summary>
        /// All available restrictions.
        /// </summary>
        Any = Safe | HideUnsafe | Sdk | Security,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of restriction flags.
        /// </summary>
        Default = Restricted | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

#if NETWORK
    /// <summary>
    /// Identifies the source of a time string, e.g. which kind of time server
    /// it was obtained from.
    /// </summary>
    [ObjectId("0c4c689d-bcb9-4ca6-8f8e-dc817621169a")]
    internal enum TimeStringType
    {
        /// <summary>
        /// No time string type; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// An unknown error occurred while obtaining the time.
        /// </summary>
        UnknownError = 0x1000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The time was obtained from the default server.
        /// </summary>
        DefaultServer = 0x10000,
        /// <summary>
        /// The time was obtained from the primary server.
        /// </summary>
        PrimaryServer = 0x20000,
        /// <summary>
        /// The time was obtained from the per-interpreter server.
        /// </summary>
        InterpreterServer = 0x40000,
        /// <summary>
        /// The time was obtained from a manually specified server.
        /// </summary>
        ManualServer = 0x80000
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify a kind of URI used by the licensing subsystem,
    /// together with options controlling how the final URI is resolved.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("dae80675-0d41-4bad-a4a7-48abf8f3e8bf")]
    internal enum UriType : long
    {
        /// <summary>
        /// No URI type; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Base URI for ping requests.
        /// </summary>
        PingBase = 0x1000,
        /// <summary>
        /// Relative URI for ping requests.
        /// </summary>
        PingRelative = 0x2000,

        /// <summary>
        /// Base URI for NTP requests.
        /// </summary>
        NtpBase = 0x4000,
        /// <summary>
        /// Relative URI for NTP requests.
        /// </summary>
        NtpRelative = 0x8000,

        /// <summary>
        /// Base URI for HTTP time requests.
        /// </summary>
        HttpTimeBase = 0x10000,
        /// <summary>
        /// Relative URI for HTTP time requests.
        /// </summary>
        HttpTimeRelative = 0x20000,

        /// <summary>
        /// Base URI for secret requests.
        /// </summary>
        SecretBase = 0x40000,
        /// <summary>
        /// Relative URI for secret requests.
        /// </summary>
        SecretRelative = 0x80000,

        /// <summary>
        /// Base URI for authority requests.
        /// </summary>
        AuthorityBase = 0x100000,
        /// <summary>
        /// Relative URI for authority requests.
        /// </summary>
        AuthorityRelative = 0x200000,

        /// <summary>
        /// Base URI for renewal requests.
        /// </summary>
        RenewalBase = 0x400000,
        /// <summary>
        /// Relative URI for renewal requests.
        /// </summary>
        RenewalRelative = 0x800000,

        /// <summary>
        /// Base URI for revocation requests.
        /// </summary>
        RevocationBase = 0x1000000,
        /// <summary>
        /// Relative URI for revocation requests.
        /// </summary>
        RevocationRelative = 0x2000000,

        /// <summary>
        /// Base URI for support requests.
        /// </summary>
        SupportBase = 0x4000000,
        /// <summary>
        /// Relative URI for support requests.
        /// </summary>
        SupportRelative = 0x8000000,

        /// <summary>
        /// Base URI for script requests.
        /// </summary>
        ScriptBase = 0x10000000,
        /// <summary>
        /// Relative URI for script requests.
        /// </summary>
        ScriptRelative = 0x20000000,

        /// <summary>
        /// Base URI for storage requests.
        /// </summary>
        StorageBase = 0x40000000,
        /// <summary>
        /// Relative URI for storage requests.
        /// </summary>
        StorageRelative = 0x80000000,

        /// <summary>
        /// Base URI for general requests.
        /// </summary>
        RequestBase = 0x100000000,
        /// <summary>
        /// Relative URI for general requests.
        /// </summary>
        RequestRelative = 0x200000000,

        /// <summary>
        /// Base URI for provisioning requests.
        /// </summary>
        ProvisionBase = 0x400000000,
        /// <summary>
        /// Relative URI for provisioning requests.
        /// </summary>
        ProvisionRelative = 0x800000000,

        /// <summary>
        /// Base URI for test requests.
        /// </summary>
        TestBase = 0x1000000000,
        /// <summary>
        /// Relative URI for test requests.
        /// </summary>
        TestRelative = 0x2000000000,

        /// <summary>
        /// Base URI for license requests.
        /// </summary>
        LicenseBase = 0x4000000000,
        /// <summary>
        /// Relative URI for license requests.
        /// </summary>
        LicenseRelative = 0x8000000000,

        /// <summary>
        /// Base URI for script library requests.
        /// </summary>
        LibraryBase = 0x10000000000,
        /// <summary>
        /// Relative URI for script library requests.
        /// </summary>
        LibraryRelative = 0x20000000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempt to use the primary license certificate.
        /// </summary>
        UseCertificate = 0x1000000000000000, /* attempt to use primary license
                                              * certificate */
        /// <summary>
        /// Allow script library procedures to be evaluated when figuring out
        /// the final URI.
        /// </summary>
        UseLibrary = 0x2000000000000000,     /* allow script library procedures
                                              * to be evaluated when figuring
                                              * out the final URI. */
        /// <summary>
        /// Allow script variables to be used to fetch the URI.
        /// </summary>
        UseVariable = 0x4000000000000000,    /* allow script variables to be
                                              * used to fetch the URI. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of all base URI types.
        /// </summary>
        BaseTypeMask = PingBase | NtpBase | HttpTimeBase |
                       SecretBase | AuthorityBase | RenewalBase |
                       RevocationBase | SupportBase | ScriptBase |
                       StorageBase | RequestBase | ProvisionBase |
                       TestBase | LicenseBase | LibraryBase,

        /// <summary>
        /// Mask of all relative URI types.
        /// </summary>
        RelativeTypeMask = PingRelative | NtpRelative |
                           HttpTimeRelative | SecretRelative |
                           AuthorityRelative | RenewalRelative |
                           RevocationRelative | SupportRelative |
                           ScriptRelative | StorageRelative |
                           RequestRelative | ProvisionRelative |
                           TestRelative | LicenseRelative |
                           LibraryRelative,

        /// <summary>
        /// Mask of all base and relative URI types.
        /// </summary>
        TypeMask = BaseTypeMask | RelativeTypeMask,

        /// <summary>
        /// Mask of all URI resolution option flags.
        /// </summary>
        FlagMask = UseCertificate | UseLibrary | UseVariable,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default URI type.
        /// </summary>
        Default = None
    }

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
    /// <summary>
    /// Identifies the desired security-policy enforcement state, e.g. query,
    /// disable, enable, or wait.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("513b681b-79a9-40bf-8974-1648f8de9fd9")]
    internal enum SecurityEnabledType
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,     /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,  /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: Use this enumeration from the [security] command
        //       in order to allow for more precise control of the
        //       security state.
        //
        /// <summary>
        /// Do not change the security state.
        /// </summary>
        Query = 0x1000, /* Do not change the security state. */
        /// <summary>
        /// Disable security policy enforcement.
        /// </summary>
        False = 0x2000, /* Disable security policy enforcement. */
        /// <summary>
        /// Enable security policy enforcement.
        /// </summary>
        True = 0x4000,  /* Enable security policy enforcement. */
        /// <summary>
        /// Wait for the security policy to be enabled or disabled.
        /// </summary>
        Wait = 0x8000,  /* Wait for security policy to be enabled
                         * or disabled. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default security enabled type.
        /// </summary>
        Default = None
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify which embedded or assembly public key is being
    /// referenced.
    /// </summary>
    [Flags()]
    [ObjectId("de555b5f-9e28-41a0-8c9e-112f3dcb60ef")]
    internal enum AssemblyKeyType /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,             /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,          /* Invalid, do not use. */

        /// <summary>
        /// Assembly (RSA) public key, which was used to sign the assembly.
        /// </summary>
        Signature = 0x1000,     /* Assembly (RSA) public key, which
                                 * was used to sign the assembly. */
        /// <summary>
        /// Embedded assembly public key.
        /// </summary>
        Assembly = 0x2000,      /* Embedded assembly public key. */
        /// <summary>
        /// Embedded licensing public key.
        /// </summary>
        License = 0x4000,       /* Embedded licensing public key. */
        /// <summary>
        /// Embedded time server public key.
        /// </summary>
        Time = 0x8000,          /* Embedded time server public key. */
        /// <summary>
        /// Embedded auxiliary public key.
        /// </summary>
        Auxiliary = 0x10000,    /* Embedded auxiliary public key. */

        /// <summary>
        /// Mask of all base assembly key types.
        /// </summary>
        BaseMask = Signature | Assembly | License | Time | Auxiliary,

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x1000000,

        /// <summary>
        /// The default assembly key type.
        /// </summary>
        Default = Signature | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the scope within which a ticket is considered valid.
    /// </summary>
    [ObjectId("1a17391a-df23-44eb-8181-cc607f291af1")]
    internal enum TicketScope /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,             /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,          /* Invalid, do not use. */

        /// <summary>
        /// Ticket valid anywhere.
        /// </summary>
        Unlimited = 0x1000,     /* Ticket valid anywhere. */
        /// <summary>
        /// Ticket valid on this machine only.
        /// </summary>
        Machine = 0x2000,       /* Ticket valid on machine only. */
        /// <summary>
        /// Ticket valid in this process only.
        /// </summary>
        Process = 0x4000,       /* Ticket valid in process only. */
        /// <summary>
        /// Ticket valid in this application domain only.
        /// </summary>
        AppDomain = 0x8000,     /* Ticket valid in AppDomain only. */
        /// <summary>
        /// Ticket valid in this thread only.
        /// </summary>
        Thread = 0x10000,       /* Ticket valid in thread only. */
        /// <summary>
        /// Ticket valid in this interpreter only.
        /// </summary>
        Interpreter = 0x20000,  /* Ticket valid in interpreter only. */
        /// <summary>
        /// Ticket valid for the specific interpreter context only.
        /// </summary>
        Context = 0x40000,      /* Ticket valid for the specific
                                 * interpreter context only. */

        /// <summary>
        /// Indicates that the default was being used.
        /// </summary>
        ForDefault = 0x1000000, /* The default was being used. */

        /// <summary>
        /// The default ticket scope.
        /// </summary>
        Default = Process | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that select which pieces of licensing status information are
    /// included in a status query result.
    /// </summary>
    [Flags()]
    [ObjectId("326e93ff-b2c0-41e0-b404-988e407d8167")]
    internal enum QueryStatusFlags : ulong /* CORE */
    {
        /// <summary>
        /// No status fields; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Include the configuration phase.
        /// </summary>
        ConfigurationPhase = 0x2,
        /// <summary>
        /// Include the public key token.
        /// </summary>
        PublicKeyToken = 0x4,
        /// <summary>
        /// Include the change count.
        /// </summary>
        ChangeCount = 0x8,
        /// <summary>
        /// Include the script type.
        /// </summary>
        ScriptType = 0x10,
        /// <summary>
        /// Include the script sub-type.
        /// </summary>
        ScriptSubType = 0x20,
        /// <summary>
        /// Include the script directory.
        /// </summary>
        ScriptDirectory = 0x40,
        /// <summary>
        /// Include the script file identifier.
        /// </summary>
        ScriptFileId = 0x80,
        /// <summary>
        /// Include the script file name.
        /// </summary>
        ScriptFileName = 0x100,
        /// <summary>
        /// Include the plugin type.
        /// </summary>
        PluginType = 0x200,
        /// <summary>
        /// Include the context name.
        /// </summary>
        ContextName = 0x400,
        /// <summary>
        /// Include the variant name.
        /// </summary>
        VariantName = 0x800,
        /// <summary>
        /// Include whether the plugin is isolated.
        /// </summary>
        Isolated = 0x1000,
        /// <summary>
        /// Include whether security is required.
        /// </summary>
        MustHaveSecurity = 0x2000,
        /// <summary>
        /// Include the plugin count.
        /// </summary>
        PluginCount = 0x4000,
        /// <summary>
        /// Include the machine name.
        /// </summary>
        Machine = 0x8000,
        /// <summary>
        /// Include whether a key ring is pending.
        /// </summary>
        PendingKeyRing = 0x10000,
        /// <summary>
        /// Include whether a license is pending.
        /// </summary>
        PendingLicense = 0x20000,
        /// <summary>
        /// Include the key pair.
        /// </summary>
        KeyPair = 0x40000,
        /// <summary>
        /// Include the key pairs.
        /// </summary>
        KeyPairs = 0x80000,
        /// <summary>
        /// Include whether a demo is pending.
        /// </summary>
        PendingDemo = 0x100000,
        /// <summary>
        /// Include whether a renewal is pending.
        /// </summary>
        PendingRenewal = 0x200000,
        /// <summary>
        /// Include whether a network license is forced.
        /// </summary>
        ForceNetworkLicense = 0x400000,
        /// <summary>
        /// Include whether licensing is skipped.
        /// </summary>
        SkipLicense = 0x800000,
        /// <summary>
        /// Include the snippet.
        /// </summary>
        Snippet = 0x1000000,
        /// <summary>
        /// Include the storage type.
        /// </summary>
        StorageType = 0x2000000,
        /// <summary>
        /// Include whether SDK mode is active.
        /// </summary>
        SdkMode = 0x4000000,
        /// <summary>
        /// Include whether demo mode is active.
        /// </summary>
        DemoMode = 0x8000000,
        /// <summary>
        /// Include whether test mode is active.
        /// </summary>
        TestMode = 0x10000000,
        /// <summary>
        /// Include whether fail-safe mode is active.
        /// </summary>
        FailSafeMode = 0x20000000,
        /// <summary>
        /// Include whether offline mode is active.
        /// </summary>
        OfflineMode = 0x40000000,
        /// <summary>
        /// Include the extra features.
        /// </summary>
        ExtraFeatures = 0x80000000,
        /// <summary>
        /// Include the license path flags.
        /// </summary>
        LicensePathFlags = 0x100000000,
        /// <summary>
        /// Include the script path flags.
        /// </summary>
        ScriptPathFlags = 0x200000000,
        /// <summary>
        /// Include the license network flags.
        /// </summary>
        LicenseNetworkFlags = 0x400000000,
        /// <summary>
        /// Include the script network flags.
        /// </summary>
        ScriptNetworkFlags = 0x800000000,
        /// <summary>
        /// Include the shell flags.
        /// </summary>
        ShellFlags = 0x1000000000,
        /// <summary>
        /// Include whether a network policy is forced.
        /// </summary>
        ForceNetworkPolicy = 0x2000000000,
        /// <summary>
        /// Include whether a network key pair is forced.
        /// </summary>
        ForceNetworkKeyPair = 0x4000000000,
        /// <summary>
        /// Include whether creation is disabled.
        /// </summary>
        CreationDisabled = 0x8000000000,
        /// <summary>
        /// Include the levels.
        /// </summary>
        Levels = 0x10000000000,
        /// <summary>
        /// Include the trusted levels.
        /// </summary>
        TrustedLevels = 0x20000000000,
        /// <summary>
        /// Include the shell trusted levels.
        /// </summary>
        ShellTrustedLevels = 0x40000000000,
        /// <summary>
        /// Include the shell fallback levels.
        /// </summary>
        ShellFallbackLevels = 0x80000000000,
        /// <summary>
        /// Include whether the current user is an administrator.
        /// </summary>
        IsAdministrator = 0x100000000000,
        /// <summary>
        /// Include whether the session is interactive.
        /// </summary>
        IsInteractive = 0x200000000000,
        /// <summary>
        /// Include the runtime.
        /// </summary>
        Runtime = 0x400000000000,
        /// <summary>
        /// Include the operating system.
        /// </summary>
        OperatingSystem = 0x800000000000,
        /// <summary>
        /// Include the version.
        /// </summary>
        Version = 0x1000000000000,
        /// <summary>
        /// Include the temporary directory.
        /// </summary>
        TemporaryDirectory = 0x2000000000000,
        /// <summary>
        /// Include the context.
        /// </summary>
        Context = 0x4000000000000,
        /// <summary>
        /// Include the time stamp.
        /// </summary>
        TimeStamp = 0x8000000000000,
        /// <summary>
        /// Include the duration.
        /// </summary>
        Duration = 0x10000000000000,
        /// <summary>
        /// Include the version range.
        /// </summary>
        VersionRange = 0x20000000000000,
        /// <summary>
        /// Include the timeout.
        /// </summary>
        Timeout = 0x40000000000000,
        /// <summary>
        /// Include the license failure count.
        /// </summary>
        LicenseFailureCount = 0x80000000000000,
        /// <summary>
        /// Include whether errors are normalized.
        /// </summary>
        NormalizeErrors = 0x100000000000000,
        /// <summary>
        /// Include whether the public key token is included.
        /// </summary>
        IncludePublicKeyToken = 0x200000000000000,
        /// <summary>
        /// Include the RSA provider count.
        /// </summary>
        RsaProviderCount = 0x400000000000000,
        /// <summary>
        /// Include the DSA provider count.
        /// </summary>
        DsaProviderCount = 0x800000000000000,
        /// <summary>
        /// Include whether big-integer cryptography is used.
        /// </summary>
        UseBigCrypto = 0x1000000000000000
    }

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
    /// <summary>
    /// Flags that control how the security state of an interpreter (or
    /// plugin) is changed, and how bootstrap key rings are handled.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("43c239c0-6d40-412e-9ae5-e67f97290d89")]
    internal enum EnableSecurityFlags : ulong /* CORE? */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,          /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,       /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Toggle the security state for the specified interpreter.
        /// </summary>
        Toggle = 0x2,        /* Toggle the security state for the specified
                              * interpreter. */
        /// <summary>
        /// Set the security state to disabled for the specified interpreter.
        /// </summary>
        Disable = 0x4,       /* Set the security state to disabled for the
                              * specified interpreter. */
        /// <summary>
        /// Set the security state to enabled for the specified interpreter.
        /// </summary>
        Enable = 0x8,        /* Set the security state to enabled for the
                              * specified interpreter. */
        /// <summary>
        /// Set the security state to disabled for the specified interpreter
        /// unless it is already disabled.
        /// </summary>
        MaybeDisable = 0x10, /* Set the security state to disabled for the
                              * specified interpreter unless it is already
                              * disabled. */
        /// <summary>
        /// Set the security state to enabled for the specified interpreter
        /// unless it is already enabled.
        /// </summary>
        MaybeEnable = 0x20,  /* Set the security state to enabled for the
                              * specified interpreter unless it is already
                              * enabled. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Change the security state for the entire application domain.
        /// </summary>
        GlobalOnly = 0x1000,        /* Change the security state for the
                                     * entire AppDomain. */
        /// <summary>
        /// Change the security state for the specified plugin.
        /// </summary>
        LocalOnly = 0x2000,         /* Change the security state for the
                                     * specified plugin. */
        /// <summary>
        /// Skip doing anything if the plugin is being initialized.
        /// </summary>
        SkipIfInitialize = 0x4000,  /* Skip doing anything if the plugin
                                     * is being initialized. */
        /// <summary>
        /// Skip doing anything if the plugin already has security policies
        /// enabled.
        /// </summary>
        SkipIfEnabled = 0x8000,     /* Skip doing anything if the plugin
                                     * already has security policies
                                     * enabled. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Also load the bootstrap key rings.
        /// </summary>
        AlwaysKeyRings = 0x10000,   /* Also load the bootstrap key rings. */
        /// <summary>
        /// Also load the bootstrap key rings unless other key rings are
        /// already pending load.
        /// </summary>
        MaybeKeyRings = 0x20000,    /* Also load the bootstrap key rings
                                     * unless other key rings are already
                                     * pending load. */
        /// <summary>
        /// Skip enabling or disabling script policies unless the key rings
        /// can be loaded.
        /// </summary>
        WithKeyRingsOnly = 0x40000, /* Skip enabling or disabling script
                                     * policies unless the key rings can
                                     * be loaded. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allow any plugin to enable and/or disable the security state.
        /// </summary>
        AllowAnyPlugin = 0x80000,   /* Allow any plugin to enable and/or
                                     * disable the security state; without
                                     * this flag, only the Security.Core
                                     * plugin is allowed. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ignore errors when setting or unsetting the security state.
        /// </summary>
        IgnoreErrors = 0x100000,    /* Ignore errors when setting -OR-
                                     * unsetting the security state. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x10000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Toggle the security state for the entire application domain.
        /// </summary>
        ToggleGlobally = Toggle | GlobalOnly,
        /// <summary>
        /// Disable the security state for the entire application domain.
        /// </summary>
        DisableGlobally = Disable | GlobalOnly,
        /// <summary>
        /// Enable the security state for the entire application domain.
        /// </summary>
        EnableGlobally = Enable | GlobalOnly,
        /// <summary>
        /// Disable the security state globally unless it is already disabled.
        /// </summary>
        MaybeDisableGlobally = MaybeDisable | GlobalOnly,
        /// <summary>
        /// Enable the security state globally unless it is already enabled.
        /// </summary>
        MaybeEnableGlobally = MaybeEnable | GlobalOnly,

        /// <summary>
        /// Toggle the security state for the specified plugin.
        /// </summary>
        ToggleLocally = Toggle | LocalOnly,
        /// <summary>
        /// Disable the security state for the specified plugin.
        /// </summary>
        DisableLocally = Disable | LocalOnly,
        /// <summary>
        /// Enable the security state for the specified plugin.
        /// </summary>
        EnableLocally = Enable | LocalOnly,
        /// <summary>
        /// Disable the security state locally unless it is already disabled.
        /// </summary>
        MaybeDisableLocally = MaybeDisable | LocalOnly,
        /// <summary>
        /// Enable the security state locally unless it is already enabled.
        /// </summary>
        MaybeEnableLocally = MaybeEnable | LocalOnly,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the flags that disable the security state.
        /// </summary>
        DisableMask = Disable | MaybeDisable,
        /// <summary>
        /// Mask of the flags that enable the security state.
        /// </summary>
        EnableMask = Enable | MaybeEnable,
        /// <summary>
        /// Mask of the flags that change the security state.
        /// </summary>
        ChangeMask = Toggle | DisableMask | EnableMask,

        /// <summary>
        /// Mask of the bootstrap key ring flags.
        /// </summary>
        KeyRingsMask = AlwaysKeyRings | MaybeKeyRings | WithKeyRingsOnly,

        /// <summary>
        /// Mask of the modifier flags.
        /// </summary>
        FlagsMask = GlobalOnly | LocalOnly | SkipIfInitialize |
                    SkipIfEnabled | KeyRingsMask,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of enable-security flags.
        /// </summary>
        Default = MaybeEnableLocally | MaybeKeyRings | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control how a command or script is evaluated, e.g. trust,
    /// verification, and error handling behavior.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("835b9b62-43f7-4e29-80ca-ee3873a6c395")]
    internal enum EvaluateCommandFlags /* CORE? */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Skip the requirement that only signed scripts be evaluated.
        /// </summary>
        SkipSignedOnly = 0x1000,
        /// <summary>
        /// Do not raise errors (complain) on failure.
        /// </summary>
        NoComplain = 0x2000,
        /// <summary>
        /// Evaluate with trust.
        /// </summary>
        WithTrust = 0x4000,
        /// <summary>
        /// Evaluate via the shell.
        /// </summary>
        ViaShell = 0x8000,
        /// <summary>
        /// Evaluate with verification.
        /// </summary>
        WithVerify = 0x10000,
        /// <summary>
        /// Use strict shell semantics.
        /// </summary>
        StrictShell = 0x20000,
        /// <summary>
        /// Evaluate via a bundle.
        /// </summary>
        ViaBundle = 0x40000,
        /// <summary>
        /// Raise an error when the input is empty.
        /// </summary>
        ErrorOnEmpty = 0x80000,
        /// <summary>
        /// Stop evaluation on the first error.
        /// </summary>
        StopOnError = 0x100000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x1000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluate with verification, allowing only signed scripts.
        /// </summary>
        WithVerifyOnly = WithVerify | SkipSignedOnly,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the modifier flags.
        /// </summary>
        FlagsMask = SkipSignedOnly | NoComplain | WithTrust |
                    ViaShell | WithVerify | StrictShell |
                    ViaBundle | StopOnError,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of evaluate-command flags.
        /// </summary>
        Default = ForDefault
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies how recorded arguments should be interpreted, e.g. as a
    /// single command, a script block, or automatically detected.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("6d5b1b75-87e3-4391-8969-3a5ed36ebe9f")]
    internal enum RecordResultType : ulong /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,              /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,           /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Execute a single command with its arguments.
        /// </summary>
        ForCommand = 0x1000,     /* Execute single command with its
                                  * arguments. */

        /// <summary>
        /// Evaluate a single script block.
        /// </summary>
        ForScript = 0x2000,      /* Evaluate single script block. */

        /// <summary>
        /// Automatically detect, based on the available context, whether the
        /// supplied arguments represent a single command to be executed or a
        /// script block to be evaluated.
        /// </summary>
        ForAutomatic = 0x4000,   /* Attempt to automatically detect,
                                  * based on the available context,
                                  * whether the supplied arguments
                                  * represent a single command to be
                                  * executed or a script block to be
                                  * evaluated. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x10000000, /* Indicates that the default flags
                                  * are in use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default record result type.
        /// </summary>
        Default = ForAutomatic | ForDefault,
    }

    ///////////////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
    /// <summary>
    /// Flags that control the certificate shell, e.g. installing callbacks
    /// and the conditions under which untrusted fallback evaluation is
    /// allowed.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("8a696332-009c-42f6-aca3-56572da593a2")]
    internal enum ShellFlags : ulong
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,    /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1, /* Invalid, do not use. */
        /// <summary>
        /// Force the operation (or state change).
        /// </summary>
        Force = 0x2,   /* Force the operation (or state change). */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Permit the use of all flags in this enumeration with the
        /// [certificate shell] sub-command.
        /// </summary>
        AllowDangerousFlags = 0x10, /* Permit the use of all flags
                                     * in this enumeration for use
                                     * with the [certificate shell]
                                     * sub-command. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Install shell callbacks that permit trusted evaluation of signed
        /// scripts.
        /// </summary>
        InstallCallbacks = 0x100,   /* Install shell callbacks that
                                     * permit trusted evaluation of
                                     * signed scripts. */
        /// <summary>
        /// Uninstall shell callbacks that permit trusted evaluation of signed
        /// scripts.
        /// </summary>
        UninstallCallbacks = 0x200, /* Uninstall shell callbacks that
                                     * permit trusted evaluation of
                                     * signed scripts. */
        /// <summary>
        /// Uninstall and then install shell callbacks that permit trusted
        /// evaluation of signed scripts.
        /// </summary>
        ResetCallbacks = 0x400,     /* Uninstall and then install
                                     * shell callbacks that permit
                                     * trusted evaluation of signed
                                     * scripts. */
        /// <summary>
        /// Reset the shell flags to the value specified, while masking off
        /// those related to changing the shell callbacks.
        /// </summary>
        SetFlags = 0x800,           /* Reset the shell flags to the
                                     * value specified, while masking
                                     * off those related to changing
                                     * the shell callbacks. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Skip all policy checks when a script is being evaluated via the
        /// fallback path.
        /// </summary>
        NoPoliciesOnFallback = 0x1000, /* Skip all policy checks when a
                                        * script is being evaluated via
                                        * the "fallback" path.  DO NOT
                                        * USE THIS FLAG UNLESS YOU KNOW
                                        * EXACTLY HOW IT WORKS. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allow untrusted evaluation of scripts when the interpreter is not
        /// marked as safe.
        /// </summary>
        FallbackOnUnsafe = 0x10000,   /* Allow untrusted evaluation of
                                       * scripts when the interpreter
                                       * is not marked as "safe". */
        /// <summary>
        /// Allow untrusted evaluation of scripts when the interpreter has one
        /// or more missing file policy settings.
        /// </summary>
        FallbackOnNoPolicy = 0x20000, /* Allow untrusted evaluation of
                                       * scripts when the interpreter
                                       * has one or more missing file
                                       * policy settings. */
        /// <summary>
        /// Allow untrusted evaluation of a script (or file) when the policies
        /// neither approve nor deny it.
        /// </summary>
        FallbackOnNeutral = 0x40000,  /* Allow untrusted evaluation of
                                       * a script (or file) when the
                                       * policies neither approve nor
                                       * deny it. */
        /// <summary>
        /// Allow untrusted evaluation of scripts when the policy denies it.
        /// </summary>
        FallbackOnDenied = 0x80000,   /* Allow untrusted evaluation of
                                       * scripts when the policy denies
                                       * it. */
        /// <summary>
        /// Allow untrusted evaluation of scripts when the policy raises an
        /// error.
        /// </summary>
        FallbackOnFailure = 0x100000, /* Allow untrusted evaluation of
                                       * scripts when the policy raises
                                       * an error. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the legacy flags are in use.
        /// </summary>
        ForLegacy = 0x10000000,
        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x20000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the fallback flags.
        /// </summary>
        FallbackMask = FallbackOnUnsafe | FallbackOnNoPolicy |
                       FallbackOnNeutral | FallbackOnDenied |
                       FallbackOnFailure,

        /// <summary>
        /// Mask of the flags applied when changing shell callbacks.
        /// </summary>
        ApplyMask = AllowDangerousFlags | InstallCallbacks |
                    UninstallCallbacks | ResetCallbacks |
                    SetFlags,

        /// <summary>
        /// Mask of the flags that affect evaluation fallback.
        /// </summary>
        EvaluateMask = NoPoliciesOnFallback | FallbackMask,

        /// <summary>
        /// The legacy default set of fallback flags.
        /// </summary>
        OldDefaultMask = FallbackOnUnsafe | FallbackOnNoPolicy |
                         FallbackOnNeutral,

        /// <summary>
        /// The current default set of fallback flags.
        /// </summary>
        NewDefaultMask = FallbackOnNoPolicy | FallbackOnNeutral,

        /// <summary>
        /// Mask of the flags forbidden in SDK mode.
        /// </summary>
        SdkForbidMask = EvaluateMask,

        /// <summary>
        /// Mask of the dangerous flags that are forbidden.
        /// </summary>
        DangerForbidMask = NoPoliciesOnFallback | FallbackOnUnsafe |
                           FallbackOnDenied | FallbackOnFailure,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The legacy set of shell flags.
        /// </summary>
        Legacy = OldDefaultMask | ForLegacy,
        /// <summary>
        /// The default set of shell flags.
        /// </summary>
        Default = NewDefaultMask | ForDefault
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

#if XML && NETWORK && WEB
    /// <summary>
    /// Flags that control how files are installed, e.g. whether changes are
    /// simulated, overwriting, backups, and rollback behavior.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("179aac7e-c4ab-4c54-9785-95109c4a34ac")]
    internal enum InstallFlags : ulong
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,                      /* No special handling. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,                   /* Invalid, do not use. */
        /// <summary>
        /// Do not actually make any changes to the system.
        /// </summary>
        WhatIf = 0x2,                    /* Do not actually make any
                                          * changes to the system. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Force the installation to have a unique identifier for use in
        /// naming of files and directories.
        /// </summary>
        ForceUniqueId = 0x100,           /* Force the installation to have
                                          * a unique identifier for use in
                                          * naming of files and directories. */
        /// <summary>
        /// Skip evaluating any manifest script files that are found.
        /// </summary>
        NoEvaluateManifests = 0x200,     /* Skip evaluating any manifest
                                          * script files that are found. */
        /// <summary>
        /// Allow files to be installed if the interpreter considers them to
        /// be trusted.
        /// </summary>
        AllowTrustedFiles = 0x400,       /* Allow files to be installed if
                                          * the interpreter considers them
                                          * to be "trusted". */
        /// <summary>
        /// Allow existing files to be overwritten.
        /// </summary>
        AllowOverwrite = 0x800,          /* Allow existing files to be
                                          * overwritten. */
        /// <summary>
        /// Do not backup any pre-existing files.
        /// </summary>
        SkipBackup = 0x1000,             /* Do not backup any pre-existing
                                          * files. */
        /// <summary>
        /// Halt the rollback phase if any errors are encountered.
        /// </summary>
        StopRollbackOnError = 0x2000,    /* Halt the rollback phase if any
                                          * errors are encountered. */
        /// <summary>
        /// Do not delete backup files even if the overall installation is
        /// considered successful.
        /// </summary>
        KeepBackupFiles = 0x4000,        /* Do not delete backup files even
                                          * if the overall installation is
                                          * considered successful. */
        /// <summary>
        /// Include some extra diagnostic information in the installation
        /// results.
        /// </summary>
        VerboseResult = 0x8000,          /* Include some extra diagnostic
                                          * information in the installation
                                          * results. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that all the defaults are being used.
        /// </summary>
        ForDefault = 0x8000000000000000, /* All the defaults are being
                                          * used. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of install flags.
        /// </summary>
        Default = ForceUniqueId | AllowTrustedFiles |
                  AllowOverwrite | KeepBackupFiles |
                  ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the current pass of a multi-pass installation operation.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("21cb4f4f-92d0-4d44-9f0b-6b03ab2fb9d4")]
    internal enum InstallPass
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,     /* No special handling. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,  /* Invalid, do not use. */
        /// <summary>
        /// Reserved, do not use.
        /// </summary>
        Reserved = 0x2, /* Reserved, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Pre-evaluate manifest files, if any, gather source file names,
        /// etc.
        /// </summary>
        Pass0 = 0x100,  /* Pre-evaluate manifest files, if
                         * any, gather source file names,
                         * etc. */
        /// <summary>
        /// Verify overwrite and signatures, and gather backup file names.
        /// </summary>
        Pass1 = 0x200,  /* Verify overwrite, signatures,
                         * gather backup file names. */
        /// <summary>
        /// Perform backup of existing files, if any.
        /// </summary>
        Pass2 = 0x400,  /* Perform backup of existing
                         * files, if any. */
        /// <summary>
        /// Commit all source files to the target directory.
        /// </summary>
        Pass3 = 0x800,  /* Commit all source files to
                         * target directory. */
        /// <summary>
        /// Rollback all source files within the target directory.
        /// </summary>
        Pass4 = 0x1000, /* Rollback all source files within
                         * target directory. */
        /// <summary>
        /// Maybe cleanup (delete) the backup files.
        /// </summary>
        Pass5 = 0x2000, /* Maybe cleanup (delete) the "backup"
                         * files. */
        /// <summary>
        /// Post-evaluate manifest files, if any.
        /// </summary>
        Pass6 = 0x4000  /* Post-evaluate manifest files, if
                         * any. */
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
    /// <summary>
    /// Flags that select which pieces of licensing and policy state are
    /// reset.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("070b0d38-0cfd-4785-a822-02cd848c409a")]
    internal enum ResetFlags : ulong
    {
        /// <summary>
        /// No state; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reset the settings interpreter.
        /// </summary>
        SettingsInterpreter = 0x100,
        /// <summary>
        /// Reset the global key ring state.
        /// </summary>
        GlobalKeyRingState = 0x200,
        /// <summary>
        /// Reset the local key ring state.
        /// </summary>
        LocalKeyRingState = 0x400,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reset the global file cache.
        /// </summary>
        GlobalFileCache = 0x1000,
        /// <summary>
        /// Reset the global license state.
        /// </summary>
        GlobalLicenseState = 0x2000,
        /// <summary>
        /// Reset the global license cache.
        /// </summary>
        GlobalLicenseCache = 0x4000,
        /// <summary>
        /// Reset the policy license state.
        /// </summary>
        PolicyLicenseState = 0x8000,
        /// <summary>
        /// Reset the plugin license state.
        /// </summary>
        PluginLicenseState = 0x10000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reset the global policy data.
        /// </summary>
        GlobalPolicyData = 0x20000,
        /// <summary>
        /// Reset the local policy data.
        /// </summary>
        LocalPolicyData = 0x40000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reset the global configuration.
        /// </summary>
        GlobalConfiguration = 0x100000,

        /// <summary>
        /// Allow local policy.
        /// </summary>
        AllowLocalPolicy = 0x1000000,
        /// <summary>
        /// Disallow local policy.
        /// </summary>
        DisallowLocalPolicy = 0x2000000,
        /// <summary>
        /// Use the default policy.
        /// </summary>
        UseDefaultPolicy = 0x4000000,
        /// <summary>
        /// Do not use the default policy.
        /// </summary>
        NoUseDefaultPolicy = 0x8000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reset the global duration data.
        /// </summary>
        GlobalDurationData = 0x100000000,
        /// <summary>
        /// Reset the global version range data.
        /// </summary>
        GlobalVersionRangeData = 0x200000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stop resetting on the first error.
        /// </summary>
        StopOnError = 0x400000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the state related to key pairs.
        /// </summary>
        KeyPairsMask = SettingsInterpreter | GlobalKeyRingState |
                       LocalKeyRingState | GlobalDurationData |
                       GlobalVersionRangeData,

        /// <summary>
        /// Mask of the state related to licensing.
        /// </summary>
        LicenseMask = GlobalFileCache | GlobalLicenseState |
                      GlobalLicenseCache | PolicyLicenseState |
                      PluginLicenseState | GlobalDurationData |
                      GlobalVersionRangeData,

        /// <summary>
        /// Mask of the state related to policies.
        /// </summary>
        PolicyMask = GlobalPolicyData | LocalPolicyData |
                     GlobalDurationData | GlobalVersionRangeData,

        /// <summary>
        /// Mask of the local-policy configuration flags.
        /// </summary>
        ConfigurationFlagMask = AllowLocalPolicy | DisallowLocalPolicy |
                                UseDefaultPolicy | NoUseDefaultPolicy,

        /// <summary>
        /// Mask of the configuration state.
        /// </summary>
        ConfigurationMask = GlobalConfiguration | ConfigurationFlagMask,

        /// <summary>
        /// Mask of all resettable state.
        /// </summary>
        AllMask = KeyPairsMask | LicenseMask |
                  PolicyMask | GlobalConfiguration,

        /// <summary>
        /// Mask of the state not reset by default.
        /// </summary>
        NonDefaultMask = KeyPairsMask | GlobalFileCache |
                         GlobalLicenseCache | GlobalConfiguration,

        /// <summary>
        /// The default mask of resettable state.
        /// </summary>
        DefaultMask = AllMask & ~NonDefaultMask,
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control how configuration files are processed.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("74110a65-0eec-428f-b2e2-424b66ee6d94")]
    internal enum ConfigurationFileFlags /* CORE */
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Reserved, do not use.
        /// </summary>
        Reserved1 = 0x2,
        /// <summary>
        /// Reserved, do not use.
        /// </summary>
        Reserved2 = 0x4,

        /// <summary>
        /// Apply to the global configuration.
        /// </summary>
        Global = 0x100,
        /// <summary>
        /// Apply to the plugin only.
        /// </summary>
        PluginOnly = 0x200,
        /// <summary>
        /// Include successful results.
        /// </summary>
        WithOkResults = 0x400,
        /// <summary>
        /// Include error results.
        /// </summary>
        WithErrorResults = 0x800,
        /// <summary>
        /// Reset the configuration.
        /// </summary>
        Reset = 0x1000,

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x100000,

        /// <summary>
        /// The default set of configuration file flags.
        /// </summary>
        Default = Reserved1 | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that select which categories of status information are reported,
    /// e.g. paths, network, time servers, policies, and shell flags.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("0c89d0e2-4a29-49ec-b6e5-a385398ddf8a")]
    internal enum StatusFlags : ulong /* CORE */
    {
        /// <summary>
        /// No status categories; do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the path flags.
        /// </summary>
        PathFlags = 0x10,
        /// <summary>
        /// Include the network flags.
        /// </summary>
        NetworkFlags = 0x20,
        /// <summary>
        /// Include the time servers.
        /// </summary>
        TimeServers = 0x40,

        ///////////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// Include the shell flags.
        /// </summary>
        ShellFlags = 0x80,
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        /// <summary>
        /// Include the extra features.
        /// </summary>
        ExtraFeatures = 0x100,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the context.
        /// </summary>
        Context = 0x200,

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The script category.  Must be the same value as PolicyType.Script.
        /// </summary>
        Script = 0x1000,   /* WARNING: Must be same as PolicyType.Script. */
        /// <summary>
        /// The file category.  Must be the same value as PolicyType.File.
        /// </summary>
        File = 0x2000,     /* WARNING: Must be same as PolicyType.File. */
        /// <summary>
        /// The stream category.  Must be the same value as PolicyType.Stream.
        /// </summary>
        Stream = 0x4000,   /* WARNING: Must be same as PolicyType.Stream. */
        /// <summary>
        /// The license category.  Must be the same value as
        /// PolicyType.License.
        /// </summary>
        License = 0x8000,  /* WARNING: Must be same as PolicyType.License. */
        /// <summary>
        /// The key pair category.  Must be the same value as
        /// PolicyType.KeyPair.
        /// </summary>
        KeyPair = 0x10000, /* WARNING: Must be same as PolicyType.KeyPair. */
        /// <summary>
        /// The trace category.  Must be the same value as PolicyType.Trace.
        /// </summary>
        Trace = 0x20000,   /* WARNING: Must be same as PolicyType.Trace. */
        /// <summary>
        /// The other category.  Must be the same value as PolicyType.Other.
        /// </summary>
        Other = 0x40000,   /* WARNING: Must be same as PolicyType.Other. */

        /// <summary>
        /// Mask of all policy type categories.
        /// </summary>
        PolicyTypeMask = Script | File | Stream |
                         License | KeyPair | Trace |
                         Other,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the policy.
        /// </summary>
        Policy = 0x10000000,
        /// <summary>
        /// Include the dumped state.
        /// </summary>
        DumpState = 0x20000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the key name.
        /// </summary>
        KeyName = 0x100000000,
        /// <summary>
        /// Include the key ring name.
        /// </summary>
        KeyRingName = 0x200000000,
        /// <summary>
        /// Include the execution flags.
        /// </summary>
        ExecutionFlags = 0x400000000,
        /// <summary>
        /// Include the script flags.
        /// </summary>
        ScriptFlags = 0x800000000,
        /// <summary>
        /// Include the key ring.
        /// </summary>
        KeyRing = 0x1000000000,

        /// <summary>
        /// Mask of the policy property categories.
        /// </summary>
        PropertyMask = KeyName | KeyRingName | ExecutionFlags |
                       ScriptFlags | KeyRing,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the script policy and its properties.
        /// </summary>
        ScriptPolicy = Script | Policy | PropertyMask,
        /// <summary>
        /// Include the file policy and its properties.
        /// </summary>
        FilePolicy = File | Policy | PropertyMask,
        /// <summary>
        /// Include the stream policy and its properties.
        /// </summary>
        StreamPolicy = Stream | Policy | PropertyMask,
        /// <summary>
        /// Include the license policy and its properties.
        /// </summary>
        LicensePolicy = License | Policy | PropertyMask,
        /// <summary>
        /// Include the key pair policy and its properties.
        /// </summary>
        KeyPairPolicy = KeyPair | Policy | PropertyMask,
        /// <summary>
        /// Include the trace policy and its properties.
        /// </summary>
        TracePolicy = Trace | Policy | PropertyMask,
        /// <summary>
        /// Include the other policy and its properties.
        /// </summary>
        OtherPolicy = Other | Policy | PropertyMask,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the shell flags when they are available.
        /// </summary>
#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        MaybeShellFlags = ShellFlags,
#else
        MaybeShellFlags = None,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the extra features when they are available.
        /// </summary>
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        MaybeExtraFeatures = ExtraFeatures,
#else
        MaybeExtraFeatures = None,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the dumped state when it is available.
        /// </summary>
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        MaybeDumpState = DumpState,
#else
        MaybeDumpState = None,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the policy categories when they are available.
        /// </summary>
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        MaybePolicyMask = ScriptPolicy | FilePolicy |
                          StreamPolicy | LicensePolicy |
                          KeyPairPolicy | TracePolicy |
                          OtherPolicy,
#else
        MaybePolicyMask = None,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include every available status category.
        /// </summary>
        MaybeEverything = PathFlags | NetworkFlags | TimeServers |
                          MaybeShellFlags | MaybeExtraFeatures |
                          MaybeDumpState | MaybePolicyMask,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of status flags.
        /// </summary>
        Default = PathFlags | NetworkFlags | TimeServers |
                  MaybeShellFlags | MaybeExtraFeatures |
                  MaybePolicyMask
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify the format of a key file, e.g. SNK, CryptoAPI, or
    /// PVK, along with the algorithm and storage it is for.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("be44d0fa-0ae8-4c73-8f14-d3c8be40d82d")]
    internal enum KeyFileFormat /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,      /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,   /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Enable the special SNK file format prefix that emits the
        //       three fields of the PublicKeyBlob structure prior to the
        //       raw CryptoAPI compatible key blob.
        //
        /// <summary>
        /// SNK file format, with a possible prefix for public-only keys.
        /// </summary>
        StrongName = 0x10, /* SNK file format, with possible prefix for
                            * public only? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raw CryptoAPI blob, without any prefix.
        /// </summary>
        CryptoAPI = 0x20,  /* Raw CryptoAPI blob, without any prefix. */
        /// <summary>
        /// PVK file format, possibly encrypted.
        /// </summary>
        PrivateKey = 0x40, /* PVK file format, maybe encrypted? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For an RSA key.
        /// </summary>
        ForRsa = 0x100,
        /// <summary>
        /// For a DSA key.
        /// </summary>
        ForDsa = 0x200,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For an assembly key.
        /// </summary>
        ForAssembly = 0x1000,
        /// <summary>
        /// For an embedded key.
        /// </summary>
        ForEmbedded = 0x2000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// RSA key in SNK file format.
        /// </summary>
        RsaStrongName = StrongName | ForRsa,
        /// <summary>
        /// DSA key in SNK file format.
        /// </summary>
        DsaStrongName = StrongName | ForDsa,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// RSA key in PVK file format.
        /// </summary>
        RsaPrivateKey = PrivateKey | ForRsa,
        /// <summary>
        /// DSA key in PVK file format.
        /// </summary>
        DsaPrivateKey = PrivateKey | ForDsa,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// RSA assembly key in SNK file format.
        /// </summary>
        Assembly = RsaStrongName | ForAssembly,
        /// <summary>
        /// RSA embedded key in SNK file format.
        /// </summary>
        Embedded = RsaStrongName | ForEmbedded,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the base key file formats.
        /// </summary>
        BaseMask = StrongName | CryptoAPI | PrivateKey
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify the kind of license being referenced, e.g.
    /// assembly, key ring, feature, context, or command.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("5f2a1d0e-7dc8-42e0-990c-cc06564260e7")]
    internal enum LicenseType /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Reserved, do not use.
        /// </summary>
        Reserved1 = 0x2,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// An assembly license.
        /// </summary>
        Assembly = 0x10,
        /// <summary>
        /// A key ring license.
        /// </summary>
        KeyRing = 0x20,
        /// <summary>
        /// A license key ring.
        /// </summary>
        LicenseKeyRing = 0x40,
        /// <summary>
        /// A feature license.
        /// </summary>
        Feature = 0x80,
        /// <summary>
        /// A context license.
        /// </summary>
        Context = 0x100,
        /// <summary>
        /// A command license.
        /// </summary>
        Command = 0x200,
        /// <summary>
        /// Any license type.
        /// </summary>
        Any = 0x400,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For use by a command.
        /// </summary>
        ForCommand = 0x1000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the key ring license types.
        /// </summary>
        KeyRingMask = Assembly | Feature,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The license key ring license type, for use by a command.
        /// </summary>
        LicenseKeyRingOnly = KeyRingMask | KeyRing | LicenseKeyRing |
                             ForCommand
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control how a file name is resolved and validated, and the
    /// associated tracing behavior.
    /// </summary>
    [Flags()]
    [ObjectId("5677b4f4-b3b7-48e7-b4f4-cb8eaa2fd11d")]
    internal enum FileNameFlags /* CORE */
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allow a remote URI.
        /// </summary>
        AllowRemoteUri = 0x2,
        /// <summary>
        /// The content looks like XML.
        /// </summary>
        LooksLikeXml = 0x4,
        /// <summary>
        /// Pre-validate the content as XML.
        /// </summary>
        PreValidateXml = 0x8,
        /// <summary>
        /// Use an embedded resource.
        /// </summary>
        UseResource = 0x10,
        /// <summary>
        /// Emit trace output on error.
        /// </summary>
        TraceOnError = 0x20,
        /// <summary>
        /// Emit trace output when found.
        /// </summary>
        TraceOnFound = 0x40,
        /// <summary>
        /// Emit trace output when not found.
        /// </summary>
        TraceOnNotFound = 0x80,
        /// <summary>
        /// Allow any resource public key.
        /// </summary>
        AnyResourcePublicKey = 0x100,
        /// <summary>
        /// The file name is for this assembly.
        /// </summary>
        IsForThisAssembly = 0x200,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of file name flags.
        /// </summary>
        Default = None
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the kind of storage action being performed, e.g. read,
    /// write, delete, or list.
    /// </summary>
    [ObjectId("8e1ab2ce-53c0-46a2-83ff-e92be0e202d0")]
    internal enum StorageAction /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        /// <summary>
        /// Read from storage.
        /// </summary>
        Read = 0x1000,
        /// <summary>
        /// Write to storage.
        /// </summary>
        Write = 0x2000,
        /// <summary>
        /// Delete from storage.
        /// </summary>
        Delete = 0x4000,
        /// <summary>
        /// List the contents of storage.
        /// </summary>
        List = 0x8000
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify the kind of key pair, e.g. its algorithm and
    /// intended use.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("a8e6c639-dbcf-4679-9ff7-564eecbb4560")]
    internal enum KeyPairType /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,     /* None, do not use. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,  /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// An RSA key pair.
        /// </summary>
        RSA = 0x100,
        /// <summary>
        /// A DSA key pair.
        /// </summary>
        DSA = 0x200,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For an assembly key pair.
        /// </summary>
        ForAssembly = 0x1000,
        /// <summary>
        /// For an embedded key pair.
        /// </summary>
        ForEmbedded = 0x2000,
        /// <summary>
        /// For a legacy key pair.
        /// </summary>
        ForLegacy = 0x4000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// RSA assembly key pair.
        /// </summary>
        Assembly = RSA | ForAssembly, /* COMPAT: CLRv2, CLRv4, etc. */
        /// <summary>
        /// RSA embedded key pair.
        /// </summary>
        Embedded = RSA | ForEmbedded, /* COMPAT: Harpy beta. */
        /// <summary>
        /// RSA legacy key pair.
        /// </summary>
        Legacy = RSA | ForLegacy,     /* COMPAT: [keypair generate],
                                       * [keypair metadata], etc. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask of the base key pair algorithms.
        /// </summary>
        BaseMask = RSA | DSA
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control core library policy tracing, e.g. enabling tracing,
    /// log file handling, and trace priority adjustments.
    /// </summary>
    [Flags()]
    [ObjectId("be7c826d-aadb-4d33-84c7-a07523a6fb83")]
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    internal enum PolicyTraceFlags /* CORE */
    {
        /// <summary>
        /// Do nothing.
        /// </summary>
        None = 0x0,          /* Do nothing. */
        /// <summary>
        /// Do not use.
        /// </summary>
        Invalid = 0x1,       /* Do not use. */
        /// <summary>
        /// Enable (or disable) the core library per-interpreter and/or global
        /// policy tracing setting.
        /// </summary>
        Enable = 0x2,        /* Enable (or disable) the core library
                              * per-interpreter and/or global policy
                              * tracing setting? */
        /// <summary>
        /// Automatically generate a trace log file name if one is not
        /// explicitly set via the environment.
        /// </summary>
        AutoFile = 0x4,      /* Automatically generate a trace log
                              * file name if one is not explicitly
                              * set via the environment. */
        /// <summary>
        /// Initially open the trace log file in append mode; without this,
        /// previous contents could be lost.
        /// </summary>
        Append = 0x8,        /* Initially open the trace log file in
                              * append mode; without this, previous
                              * contents could be lost. */
        /// <summary>
        /// Initially open the trace log file in shared mode.
        /// </summary>
        Shared = 0x10,       /* Initially open the trace log file in
                              * shared mode.  WARNING: This is kinda
                              * dangerous since no mechanism exists
                              * to synchronize writes to the trace
                              * log file. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// When tracing a policy context object, force the full contents to
        /// be emitted verbatim.
        /// </summary>
        Full = 0x20,         /* When tracing a policy context object,
                              * force the full contents to be emitted
                              * verbatim. */
        /// <summary>
        /// When tracing a policy context object, attempt to forcibly reset
        /// the core library tracing subsystem.
        /// </summary>
        Reset = 0x40,        /* When tracing a policy context object,
                              * attempt to forcibly reset the core
                              * library tracing subsystem. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjust the allowed trace priorities to include all messages while
        /// enabled.
        /// </summary>
        Priorities = 0x80,   /* Adjust allowed trace priorities to
                              * include all messages while enabled. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enable trace priority modifications in order to capture far more
        /// trace output.
        /// </summary>
        Tracing = 0x100,     /* Enable trace priority modifications
                              * in order to capture far more trace
                              * output. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Instead of copying a reference to the open trace log file, open it
        /// again.
        /// </summary>
        Clone = 0x200,       /* Instead of copying a reference to
                              * the open trace log file (stream),
                              * open it again.  WARNING: Normally,
                              * this also requires the "Shared"
                              * flag. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Modify the core library per-interpreter policy tracing setting.
        /// </summary>
        Interpreter = 0x400, /* Modify core library per-interpreter
                              * policy tracing setting? */
        /// <summary>
        /// Modify the core library global policy tracing setting.
        /// </summary>
        Global = 0x800,      /* Modify core library global policy
                              * tracing setting? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Modify the base trace priority used to override the trace
        /// priorities of incoming messages.
        /// </summary>
        Priority = 0x1000,   /* Modify the base trace priority that
                              * is used to override all the trace
                              * priorities of incoming messages? */
        /// <summary>
        /// Modify the trace limits.
        /// </summary>
        Limits = 0x2000,     /* Modify the trace limits? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enable stack traces, et al, when tracing.
        /// </summary>
        Enhance = 0x4000,    /* Enable stack traces, et al, when
                              * tracing. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the defaults are being used.
        /// </summary>
        ForDefault = 0x8000, /* This indicates that the defaults
                              * are being used. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// All policy trace flags.
        /// </summary>
        All = Enable | AutoFile | Append | Shared |
              Full | Reset | Priorities | Tracing |
              Clone | Interpreter | Global | Priority |
              Limits | Enhance,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The verbose set of policy trace flags.
        /// </summary>
        Verbose = All & ~(Shared | Reset | Clone | Interpreter | Global),
        /// <summary>
        /// The standard set of policy trace flags.
        /// </summary>
        Standard = Verbose & ~(Priorities | Priority | Limits | Enhance),

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of policy trace flags.
        /// </summary>
        Default = Priorities | Limits | ForDefault
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control how a plugin name (and its associated file or
    /// directory) is formatted and resolved.
    /// </summary>
    [Flags()]
    [ObjectId("4b66557f-dd31-4a7b-9336-82b4e807fa0c")]
    internal enum PluginNameFlags /* CORE */
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Use the plugin data.
        /// </summary>
        UsePluginData = 0x2,
        /// <summary>
        /// Do not use the default.
        /// </summary>
        NoDefault = 0x4,

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The plugin name is encrypted.
        /// </summary>
        Encrypted = 0x8,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Use the first name format.
        /// </summary>
        Format1 = 0x10,
        /// <summary>
        /// Use the second name format.
        /// </summary>
        Format2 = 0x20,
        /// <summary>
        /// Use the third name format.
        /// </summary>
        Format3 = 0x40,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For use with the environment.
        /// </summary>
        ForEnvironment = 0x80,
        /// <summary>
        /// For use with a directory.
        /// </summary>
        ForDirectory = 0x100,
        /// <summary>
        /// For use with a file name.
        /// </summary>
        ForFileName = 0x200,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The flags used for the first pass.
        /// </summary>
        Pass1 = Format1 | UsePluginData,
        /// <summary>
        /// The flags used for the third pass.
        /// </summary>
        Pass3 = Format1,
        /// <summary>
        /// The flags used for the fifth pass.
        /// </summary>
        Pass5 = Pass1 | NoDefault,

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The flags used for the second pass.
        /// </summary>
        Pass2 = Format1 | UsePluginData | Encrypted,
        /// <summary>
        /// The flags used for the fourth pass.
        /// </summary>
        Pass4 = Format1 | Encrypted,
        /// <summary>
        /// The flags used for the sixth pass.
        /// </summary>
        Pass6 = Pass1 | NoDefault | Encrypted,
        /// <summary>
        /// The flags used for the seventh pass.
        /// </summary>
        Pass7 = Pass2 | NoDefault,
        /// <summary>
        /// The flags used for the eighth pass.
        /// </summary>
        Pass8 = Pass2 | NoDefault | Encrypted,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The flags used for Harpy.
        /// </summary>
        Harpy = 0x400,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is for use by the GetCertificateDirectory method only.
        //
        /// <summary>
        /// The flags used by the GetCertificateDirectory method only.
        /// </summary>
        Directory = UsePluginData | ForDirectory
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that control how an assembly name is formatted and resolved.
    /// </summary>
    [Flags()]
    [ObjectId("7a4d98e5-80bb-4154-b2e5-daf8904dacde")]
    internal enum AssemblyNameFlags /* CORE */
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Do not use the default.
        /// </summary>
        NoDefault = 0x2,

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The assembly name is encrypted.
        /// </summary>
        Encrypted = 0x4,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Use the first name format.
        /// </summary>
        Format1 = 0x8,
        /// <summary>
        /// Use the second name format.
        /// </summary>
        Format2 = 0x10,
        /// <summary>
        /// Use the third name format.
        /// </summary>
        Format3 = 0x20,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The flags used for the first pass.
        /// </summary>
        Pass1 = Format2,
        /// <summary>
        /// The flags used for the third pass.
        /// </summary>
        Pass3 = Format1,
        /// <summary>
        /// The flags used for the fifth pass.
        /// </summary>
        Pass5 = Pass1 | NoDefault,

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The flags used for the second pass.
        /// </summary>
        Pass2 = Format2 | Encrypted,
        /// <summary>
        /// The flags used for the fourth pass.
        /// </summary>
        Pass4 = Format1 | Encrypted,
        /// <summary>
        /// The flags used for the sixth pass.
        /// </summary>
        Pass6 = Pass1 | NoDefault | Encrypted,
        /// <summary>
        /// The flags used for the seventh pass.
        /// </summary>
        Pass7 = Pass2 | NoDefault,
        /// <summary>
        /// The flags used for the eighth pass.
        /// </summary>
        Pass8 = Pass2 | NoDefault | Encrypted,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Placeholder member; not used.
        /// </summary>
        Stub_Not_Used
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify how a hash algorithm name is selected and the
    /// context in which it is used.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("ccb4b774-7c3b-43d3-a878-5de187924ca1")]
    internal enum HashAlgorithmType /* CORE */
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,        /* No special handling. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,     /* Invalid, do not use. */
        /// <summary>
        /// Always use the legacy hash algorithm, e.g. SHA1.
        /// </summary>
        Legacy = 0x2,      /* Always use legacy hash algorithm, e.g. SHA1. */
        /// <summary>
        /// For use with local signatures, e.g. script certificates.
        /// </summary>
        LocalUse = 0x4,    /* For use with local signatures, e.g. script
                            * certificates. */
        /// <summary>
        /// For use with remote signatures, e.g. revocation lists.
        /// </summary>
        RemoteUse = 0x8,   /* For use with remote signatures, e.g. revocation
                            * lists. */
        /// <summary>
        /// The hash algorithm name was explicitly specified by a script
        /// command and must be used verbatim.
        /// </summary>
        CommandUse = 0x10, /* The hash algorithm name was explicitly specified
                            * by a script command, it must be used verbatim.
                            */
        /// <summary>
        /// For use with signed script certificates.
        /// </summary>
        ScriptUse = 0x20,  /* For use with signed script certificates. */
        /// <summary>
        /// The hash algorithm name is purely optional and fallback hash
        /// algorithm names should not be used.
        /// </summary>
        OptionalUse = 0x40 /* The hash algorithm name is purely optional and
                            * fallback hash algorithm names should not be
                            * used. */
    }

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
    /// <summary>
    /// Identifies the CryptoAPI key number (key specification) to use.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a6d0c404-bfad-4a4b-a499-3b7e4c21df43")]
    internal enum KeyNumber
    {
        /// <summary>
        /// No key number; do not use.
        /// </summary>
        AT_NONE = 0,
        /// <summary>
        /// The key exchange key (AT_KEYEXCHANGE).
        /// </summary>
        AT_KEYEXCHANGE = 1,
        /// <summary>
        /// The signature key (AT_SIGNATURE).
        /// </summary>
        AT_SIGNATURE = 2,
        /// <summary>
        /// The default key number.
        /// </summary>
        AT_DEFAULT = AT_SIGNATURE
    }
#endif

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify the kind of bootstrap data being processed, e.g.
    /// license, script, general, or bundle.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("f8822dc2-ee53-4fd8-ae4e-a200d1787759")]
    internal enum BootstrapType : ulong /* CORE */
    {
        /// <summary>
        /// None, do not use.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        /// <summary>
        /// License bootstrap data.
        /// </summary>
        License = 0x2,
        /// <summary>
        /// Script bootstrap data.
        /// </summary>
        Script = 0x20,
        /// <summary>
        /// General bootstrap data.
        /// </summary>
        General = 0x200,
        /// <summary>
        /// Bundle bootstrap data.
        /// </summary>
        Bundle = 0x400,

        /// <summary>
        /// Do not reorder the bootstrap data.
        /// </summary>
        DoNotReorder = 0x20000000,
        /// <summary>
        /// Use the primary bootstrap data only.
        /// </summary>
        PrimaryOnly = 0x80000000,

        /// <summary>
        /// Any script bootstrap data.
        /// </summary>
        AnyScript = Script | General,
        /// <summary>
        /// Any license bootstrap data.
        /// </summary>
        AnyLicense = License | General,

        /// <summary>
        /// Any bootstrap data.
        /// </summary>
        Any = Script | License | General
    }

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
    /// <summary>
    /// Flags that control how flag rules are matched, e.g. allow/deny
    /// semantics and the order in which rules are evaluated.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("e63f44f2-afbf-4a91-98ab-9ee24cc19610")]
    internal enum FlagRuleType
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,    /* No special handling. */
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1, /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Rule must match.
        /// </summary>
        Allow = 0x2, /* Rule must match. */
        /// <summary>
        /// Rule must not match.
        /// </summary>
        Deny = 0x4,  /* Rule must not match. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Process allow rules first.
        /// </summary>
        AllowDeny = 0x2000, /* Process "allow" rules first. */
        /// <summary>
        /// Process deny rules first.
        /// </summary>
        DenyAllow = 0x4000, /* Process "deny" rules first. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For an overall match, all keys within the specified rule must
        /// match.
        /// </summary>
        MatchAllKey = 0x100000,   /* For an overall match, all keys
                                   * within the specified rule must
                                   * match. */
        /// <summary>
        /// For an overall match, at least one key within the specified rule
        /// must match.
        /// </summary>
        MatchAnyKey = 0x200000,   /* For an overall match, at least
                                   * one key within the specified
                                   * rule must match. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For an overall match, all the specified rules must match.
        /// </summary>
        MatchAllRule = 0x1000000, /* For an overall match, all the
                                   * specified rules must match. */
        /// <summary>
        /// For an overall match, at least one rule within the specified rules
        /// must match.
        /// </summary>
        MatchAnyRule = 0x2000000, /* For an overall match, at least
                                   * one rule within the specified
                                   * rules must match. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default flags are in use.
        /// </summary>
        ForDefault = 0x10000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of flag rule type flags.
        /// </summary>
        Default = AllowDeny | MatchAnyKey | MatchAnyRule | ForDefault
    }
#endif
}
