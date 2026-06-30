/*
 * Features.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Defines the single-character feature flag values, and various combined
    /// feature checking flags, used by this plugin when interpreting the
    /// "Features" property of a license certificate.
    /// </summary>
    [ObjectId("79055448-26e5-49e6-bb46-11190b82548c")]
    internal static class Features
    {
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     When a new flag is used here, update these strings.           //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //     Available upper flags: "".                                    //
        //     Available lower flags: "op".                                  //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        #region Generic Feature Flags (Global / Reserved)
        //
        // NOTE: This flag is not used by this plugin.  However, it is being
        //       reserved for future use as the "vendor" indicator flag.  It
        //       means that the entity referenced by the certificate is also
        //       a vendor.
        //
        /// <summary>
        /// Reserved feature flag indicating that the entity referenced by
        /// the certificate is also a vendor or partner.  This flag is not
        /// currently used by this plugin.
        /// </summary>
        public const string Vendor = "Q"; // For vendor -OR- partner.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that an active
        //       Enterprise Support Contract exists for Eagle and this plugin.
        //       Ideally, it should enable associated application developers
        //       to contact the support team via mail and/or phone.
        //
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /// <summary>
        /// Feature flag indicating that an active Enterprise Support Contract
        /// exists for Eagle and this plugin.
        /// </summary>
        public const string Support = "S"; // Has associated support contract.
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that an active
        //       Enterprise Subscription exists for Eagle and this plugin.  If
        //       present, attempts will be made to renew expired certificates;
        //       otherwise, that handling will be skipped.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that an active Enterprise Subscription
        /// exists, permitting attempts to renew expired certificates.
        /// </summary>
        public const string Renewal = "R"; // May be renewed (or not).

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that all optional
        //       features (i.e. that can be controlled via the certificate) are
        //       enabled.  It is recommended that all plugins use this flag to
        //       indicate the same thing.  Any concerns that Harpy certificates
        //       will be used to bypass the feature limitations of third-party
        //       certificates is mitigated by the fact that extra checking can
        //       be performed on the certificate properties after it has been
        //       verified by Harpy.
        //
        // HACK: Generally, this flag should not be used for new certificates.
        //       It is considered a legacy feature and may be removed in the
        //       future.  Using the individual feature flags is considered to
        //       be a "best practice".
        //
        /* CORE */
        /// <summary>
        /// Legacy feature flag indicating that all optional certificate
        /// features are enabled.  Using the individual feature flags is
        /// considered the recommended best practice.
        /// </summary>
        public const string All = "X"; // All features are enabled.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to permit the certificate
        //       to be verified even if the X509 certificate subject does not
        //       match the one for the plugin assembly.
        //
        /* CORE */
        /// <summary>
        /// Feature flag permitting the certificate to be verified even when
        /// the X509 certificate subject does not match the one for the
        /// plugin assembly.
        /// </summary>
        public const string NoSubject = "Y"; // Bypass cert subject check.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to permit the certificate
        //       to be verified even if the strong name signature for the
        //       plugin assembly is missing.
        //
        /* CORE */
        /// <summary>
        /// Feature flag permitting the certificate to be verified even when
        /// the strong name signature for the plugin assembly is missing.
        /// </summary>
        public const string NoStrongName = "H"; // Bypass strong name.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to permit the certificate
        //       to be verified even if the strong name signature for the
        //       plugin assembly cannot be verified.
        //
        /* CORE */
        /// <summary>
        /// Feature flag permitting the certificate to be verified even when
        /// the strong name signature for the plugin assembly cannot be
        /// verified.
        /// </summary>
        public const string NoVerified = "I"; // Bypass strong name verified.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to permit the certificate
        //       to be verified even if the X509 certificate is not present.
        //
        /* CORE */
        /// <summary>
        /// Feature flag permitting the certificate to be verified even when
        /// the X509 certificate is not present.
        /// </summary>
        public const string NoCertificate = "L"; // Bypass X509 cert checks.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that checking
        //       of the X509 certificate may be skipped -IF- the Eagle core
        //       library is considered "trusted" by the interpreter.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that checking of the X509 certificate may
        /// be skipped if the Eagle core library is considered "trusted" by
        /// the interpreter.
        /// </summary>
        public const string SkipCertificate = "G"; // Skip X509 cert checks.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to permit the certificate
        //       to be verified even if the X509 certificate for the plugin
        //       assembly is not trusted.
        //
        /* CORE */
        /// <summary>
        /// Feature flag permitting the certificate to be verified even when
        /// the X509 certificate for the plugin assembly is not trusted.
        /// </summary>
        public const string NoTrusted = "Z"; // Bypass cert trusted check.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that no remote
        //       network time (NTP/HTTPS) server checks should be performed
        //       by this library when validating certificate expiration dates.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that no remote network time (NTP/HTTPS)
        /// server checks should be performed when validating certificate
        /// expiration dates.
        /// </summary>
        public const string NoNetworkTime = "T"; // Bypass cert time check.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used to allow a certificate to be used prior to
        //       its recorded creation date.  This can be useful for allowing
        //       license certificates generated in advance to be used _prior_
        //       to the official start of a particular promotional event.
        //
        /* CORE */
        /// <summary>
        /// Feature flag allowing a certificate to be used prior to its
        /// recorded creation date.
        /// </summary>
        public const string CreatedAnyTime = "C"; // Created-before-now ok.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *POLICY* As a matter of policy, license certificates for
        //       the Harpy plugin itself containing the "Promotional"
        //       RESTRICTION FLAG (i.e. not this FEATURE FLAG) will cause
        //       the package certificate verification subsystem to refuse
        //       to verify license certificates associated with non-Harpy
        //       public key tokens UNLESS this FEATURE FLAG is present as
        //       well.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that the certificate is for promotional
        /// use only.  When the "Promotional" restriction is present on a
        /// Harpy license certificate, this feature flag must also be present
        /// to verify license certificates for non-Harpy public key tokens.
        /// </summary>
        public const string Promotional = "P"; // For promotional use only.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Treat network time server errors in a way favorable to the
        //       certificate being verified.  Without this flag, failure to
        //       query a network time server will cause verification of any
        //       time-limited certificate to fail.  It should be noted that
        //       certificates with the NoNetworkTime feature will never need
        //       this flag.  If the StrictNetworkTime restriction is present
        //       in the certificate, this flag will be ignored.
        //
        /* CORE */
        /// <summary>
        /// Feature flag causing network time server errors to be treated in
        /// a way favorable to the certificate being verified.  This flag is
        /// ignored when the "StrictNetworkTime" restriction is present.
        /// </summary>
        public const string RelaxedNetworkTime = "O"; // Ignore time error.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Treat a failure to contact the remote certificate revocation
        //       server more generously.  Instead of failing the verification
        //       process, assume the certificate is not revoked.
        //
        /* CORE */
        /// <summary>
        /// Feature flag causing a failure to contact the remote certificate
        /// revocation server to be treated as though the certificate is not
        /// revoked.
        /// </summary>
        public const string RelaxedRevocation = "V"; // Ignore server error.

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: If a certificate is signed with a "well-known" public key,
        //       never treat the certificate as expired.
        //
        /* CORE? */
        /// <summary>
        /// Feature flag indicating that a certificate signed with a
        /// "well-known" public key should never be treated as expired.
        /// </summary>
        public const string WellKnownNeverExpired = "A"; // Ignore expired.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Upon successfully loading the Harpy plugin, make sure that
        //       policy tracing is always enabled.
        //
        /* CORE? */
        /// <summary>
        /// Feature flag indicating that policy tracing should always be
        /// enabled upon successfully loading the Harpy plugin.
        /// </summary>
        public const string EnablePolicyTracing = "W"; // Enable policy tracing.
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that callers are
        //       allowed to create further interpreters within this process;
        //       otherwise, if the "stub" assembly is present in the AppDomain
        //       interpreter creation will be disallowed.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that callers are allowed to create
        /// further interpreters within this process.
        /// </summary>
        public const string EnableCreation = "E"; // Create interpreters?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that "test-mode"
        //       is enabled.  If "test-mode" is enabled, key pairs marked with
        //       the "TestOnly" key usage flag will be treated just like any
        //       other keys.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that "test-mode" is enabled, causing key
        /// pairs marked with the "TestOnly" key usage flag to be treated
        /// just like any other keys.
        /// </summary>
        public const string EnableTestMode = "J"; // Trust test (mode) keys?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that AppDomain
        //       based (et al?) authorization checking should be skipped by
        //       the license SDK.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that AppDomain based authorization
        /// checking should be skipped by the license SDK.
        /// </summary>
        public const string SkipAuthorization = "K"; // Skip license auth?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that a license
        //       certificate should be automatically provisioned if needed,
        //       based on the existing certificate data, if any.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that a license certificate should be
        /// automatically provisioned if needed, based on the existing
        /// certificate data, if any.
        /// </summary>
        public const string AutoProvision = "U"; // Auto-provision license?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate the a license
        //       certificate should allow configuration files to bypass one
        //       or more or their restricted sections, e.g. configuration
        //       file for the NuGet package should skip requiring network
        //       revocation checking.
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that a license certificate should allow
        /// configuration files to bypass one or more of their restricted
        /// sections.
        /// </summary>
        public const string ForceConfiguration = "F"; // Skip config checks?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate an expired
        //       license certificate is allowed as long as the assembly
        //       version is less-than-or-equal-to the product version in
        //       the license certificate.  In general, this flag SHOULD
        //       NOT be used for any "trial" certificates, license -OR-
        //       otherwise (i.e. script, etc).
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that an expired license certificate is
        /// allowed as long as the assembly version is less-than-or-equal-to
        /// the product version in the license certificate.
        /// </summary>
        public const string UseVersionForExpiration = "B"; // No expire version?

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This flag is used by this plugin to indicate that secrets
        //       may be signed using non-root key pairs when they have the
        //       associated direct key usage ("KeyUsage.Secret").
        //
        /* CORE */
        /// <summary>
        /// Feature flag indicating that secrets may be signed using non-root
        /// key pairs when they have the associated direct key usage
        /// ("KeyUsage.Secret").
        /// </summary>
        public const string RelaxedSecretsKeyUsage = "n"; // Non-root secrets?
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to verify that environment
        //       variable "AsynchronousLicensing" is allowed to exist (i.e.
        //       for use by example #3 within the managed SDK).
        //
        /// <summary>
        /// Feature flag used to verify that the "AsynchronousLicensing"
        /// environment variable is allowed to exist.
        /// </summary>
        public const string AsynchronousLicensing = "v"; // Async licensing?

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        //
        // NOTE: This flag is used to prevent the license manager components
        //       from forcibly enabling "fail-safe" mode.
        //
        /// <summary>
        /// Feature flag used to prevent the license manager components from
        /// forcibly enabling "fail-safe" mode.
        /// </summary>
        public const string SkipFailSafeMode = "q"; // Disable SDK fail-safe?
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Combined Generic Feature Checking Flags
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="Support" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string SupportOrAll = Support + All;
#endif

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="Renewal" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string RenewalOrAll = Renewal + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoSubject" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoSubjectOrAll = NoSubject + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoStrongName" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoStrongNameOrAll = NoStrongName + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoVerified" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoVerifiedOrAll = NoVerified + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoCertificate" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoCertificateOrAll = NoCertificate + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="SkipCertificate" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string SkipCertificateOrAll = SkipCertificate + All;

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoTrusted" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoTrustedOrAll = NoTrusted + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NoNetworkTime" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string NoNetworkTimeOrAll = NoNetworkTime + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="CreatedAnyTime" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string CreatedAnyTimeOrAll = CreatedAnyTime + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="Promotional" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string PromotionalOrAll = Promotional + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="RelaxedNetworkTime" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string RelaxedNetworkTimeOrAll = RelaxedNetworkTime + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="RelaxedRevocation" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string RelaxedRevocationOrAll = RelaxedRevocation + All;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /* CORE? */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="WellKnownNeverExpired" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string WellKnownNeverExpiredOrAll = WellKnownNeverExpired + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE? */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="EnablePolicyTracing" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string EnablePolicyTracingOrAll = EnablePolicyTracing + All;
#endif

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="EnableCreation" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string EnableCreationOrAll = EnableCreation + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="EnableTestMode" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string EnableTestModeOrAll = EnableTestMode + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="SkipAuthorization" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string SkipAuthorizationOrAll = SkipAuthorization + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="AutoProvision" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string AutoProvisionOrAll = AutoProvision + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="ForceConfiguration" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string ForceConfigurationOrAll = ForceConfiguration + All;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="UseVersionForExpiration" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string UseVersionForExpirationOrAll = UseVersionForExpiration + All;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="RelaxedSecretsKeyUsage" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string RelaxedSecretsKeyUsageOrAll = RelaxedSecretsKeyUsage + All;
#endif

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="AsynchronousLicensing" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string AsynchronousLicensingOrAll = AsynchronousLicensing + All;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /* CORE */
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="SkipFailSafeMode" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string SkipFailSafeModeOrAll = SkipFailSafeMode + All;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Debugger Feature Flags (Non-Certificate Only)
#if CERTIFICATE_PLUGIN
        //
        // NOTE: These flags are used [only] by this plugin to permit a
        //       managed and/or native debugger to be attached to this
        //       process.  These flags have no effect whatsoever if not
        //       present in the "ExtraFeatures" registry setting (i.e.
        //       they have no effect in the "Features" property of a
        //       particular certificate).  The "debug.eagle" tool script
        //       can be used to enable these features.  When these flags
        //       are not present having a managed and/or native debugger
        //       attached to this process before the plugin loading is
        //       completed will most likely cause failures, due to the
        //       various checks contained in the static CheckPlugin
        //       method.
        //
        /// <summary>
        /// Non-certificate feature flag permitting any (managed or native)
        /// debugger to be attached to this process.
        /// </summary>
        public const string AnyDebuggerOk = "D"; // Ignore any debugger.
        /// <summary>
        /// Non-certificate feature flag permitting a native debugger to be
        /// attached to this process.
        /// </summary>
        public const string NativeDebuggerOk = "N"; // Ignore native debugger.
        /// <summary>
        /// Non-certificate feature flag permitting a managed debugger to be
        /// attached to this process.
        /// </summary>
        public const string ManagedDebuggerOk = "M"; // Ignore managed debugger.
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Combined Debugger Feature Checking Flags
#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="AnyDebuggerOk" /> flag or the <see cref="All" /> flag.
        /// </summary>
        public const string AnyDebuggerOkOrAll = AnyDebuggerOk + All;
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="NativeDebuggerOk" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string NativeDebuggerOkOrAll = NativeDebuggerOk + All;
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="ManagedDebuggerOk" /> flag or the
        /// <see cref="All" /> flag.
        /// </summary>
        public const string ManagedDebuggerOkOrAll = ManagedDebuggerOk + All;

        ///////////////////////////////////////////////////////////////////////

#if !LIMITED_EDITION
        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="AnyDebuggerOkOrAll" /> flag or the
        /// <see cref="NativeDebuggerOk" /> flag.
        /// </summary>
        public const string AnyOrNativeDebuggerOkOrAll =
            AnyDebuggerOkOrAll + NativeDebuggerOk;

        /// <summary>
        /// Combined feature checking flag matching either the
        /// <see cref="AnyDebuggerOkOrAll" /> flag or the
        /// <see cref="ManagedDebuggerOk" /> flag.
        /// </summary>
        public const string AnyOrManagedDebuggerOkOrAll =
            AnyDebuggerOkOrAll + ManagedDebuggerOk;
#endif
#endif
        #endregion
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Feature Flags (Harpy Specific)
#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Defines the single-character command feature flag values, and the
        /// combined command feature checking flags, specific to Harpy.
        /// </summary>
        [ObjectId("0e9838cc-2e3b-4324-9490-7e1601dbb436")]
        internal static class Commands
        {
            /// <summary>
            /// Feature flag enabling all Harpy-specific commands.
            /// </summary>
            private const string All = "b";

            ///////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
            /// <summary>
            /// Feature flag enabling the Harpy certificate commands.
            /// </summary>
            private const string Certificate = "t";
            /// <summary>
            /// Feature flag enabling the Harpy cryptography commands.
            /// </summary>
            private const string Cryptography = "w";
            /// <summary>
            /// Feature flag enabling the Harpy flags commands.
            /// </summary>
            private const string Flags = "f";
            /// <summary>
            /// Feature flag enabling the Harpy key pair commands.
            /// </summary>
            private const string KeyPair = "k";
            /// <summary>
            /// Feature flag enabling the Harpy storage commands.
            /// </summary>
            private const string Storage = "g";
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            /// <summary>
            /// Feature flag enabling the core Harpy commands.
            /// </summary>
            private const string Harpy = "h";
            /// <summary>
            /// Feature flag enabling the Harpy key ring commands.
            /// </summary>
            private const string KeyRing = "r";
            /// <summary>
            /// Feature flag enabling the Harpy "ksource" commands.
            /// </summary>
            private const string Ksource = "j";
            /// <summary>
            /// Feature flag enabling the Harpy security commands.
            /// </summary>
            private const string Security = "i";
#endif

            ///////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_POLICY && PLUGIN_COMMANDS
            /// <summary>
            /// Feature flag enabling the Harpy "keval" commands.
            /// </summary>
            private const string Keval = "m";
#endif

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Feature flag enabling the Harpy secret commands.
            /// </summary>
            private const string Secret = "l";
            /// <summary>
            /// Feature flag enabling the Harpy support commands.
            /// </summary>
            private const string Support = "u";

            ///////////////////////////////////////////////////////////////////

            #region Combined Command Feature Checking Flags (Harpy Specific)
#if PLUGIN_COMMANDS
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Certificate" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string CertificateOrAll =
                Certificate + All + Features.All;

            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Cryptography" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string CryptographyOrAll =
                Cryptography + All + Features.All;

            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Flags" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string FlagsOrAll = Flags + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="KeyPair" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string KeyPairOrAll = KeyPair + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Storage" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string StorageOrAll = Storage + All + Features.All;
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Harpy" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string HarpyOrAll = Harpy + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="KeyRing" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string KeyRingOrAll = KeyRing + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Ksource" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string KsourceOrAll = Ksource + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Security" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string SecurityOrAll = Security + All + Features.All;
#endif

            ///////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_POLICY && PLUGIN_COMMANDS
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Keval" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string KevalOrAll = Keval + All + Features.All;
#endif

            ///////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Secret" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string SecretOrAll = Secret + All + Features.All;
            /// <summary>
            /// Combined command feature checking flag matching the
            /// <see cref="Support" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string SupportOrAll = Support + All + Features.All;
#endif
            #endregion
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Feature Flags (Harpy Specific)
#if CERTIFICATE_POLICY
        /// <summary>
        /// Defines the single-character policy feature flag values, and the
        /// combined policy feature checking flags, specific to Harpy.
        /// </summary>
        [ObjectId("88bc56bb-505f-4719-8c58-02260456c909")]
        internal static class Policies
        {
            /// <summary>
            /// Feature flag enabling all Harpy-specific policies.
            /// </summary>
            private const string All = "d";
            /// <summary>
            /// Feature flag enabling the Harpy script policy.
            /// </summary>
            private const string Script = "x";
            /// <summary>
            /// Feature flag enabling the Harpy file policy.
            /// </summary>
            private const string File = "y";
            /// <summary>
            /// Feature flag enabling the Harpy stream policy.
            /// </summary>
            private const string Stream = "z";

            ///////////////////////////////////////////////////////////////////

            #region Combined Policy Feature Checking Flags (Harpy Specific)
            /// <summary>
            /// Combined policy feature checking flag matching the
            /// <see cref="Script" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string ScriptOrAll = Script + All + Features.All;
            /// <summary>
            /// Combined policy feature checking flag matching the
            /// <see cref="File" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string FileOrAll = File + All + Features.All;
            /// <summary>
            /// Combined policy feature checking flag matching the
            /// <see cref="Stream" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string StreamOrAll = Stream + All + Features.All;
            #endregion
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Feature Flags (Harpy Specific)
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /// <summary>
        /// Defines the single-character plugin feature flag values, and the
        /// combined plugin feature checking flags, specific to Harpy.
        /// </summary>
        [ObjectId("1888de1f-8ce8-4a6d-82cd-cdbaf7d03c41")]
        internal static class Plugins
        {
            /// <summary>
            /// Feature flag enabling all Harpy-specific plugins.
            /// </summary>
            private const string All = "a";
            /// <summary>
            /// Feature flag enabling the Harpy core plugin.
            /// </summary>
            private const string Core = "c";
            /// <summary>
            /// Feature flag enabling the Harpy standard plugin.
            /// </summary>
            private const string Standard = "s";
            /// <summary>
            /// Feature flag enabling the Harpy enterprise plugin.
            /// </summary>
            private const string Enterprise = "e";

            ///////////////////////////////////////////////////////////////////

            #region Combined Plugin Feature Checking Flags (Harpy Specific)
            /// <summary>
            /// Combined plugin feature checking flag matching the
            /// <see cref="Core" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string CoreOrAll = Core + All + Features.All;

            /// <summary>
            /// Combined plugin feature checking flag matching the
            /// <see cref="Standard" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string StandardOrAll = Standard + All + Features.All;

            /// <summary>
            /// Combined plugin feature checking flag matching the
            /// <see cref="Enterprise" />, <see cref="All" />, or
            /// <see cref="Features.All" /> flag.
            /// </summary>
            public const string EnterpriseOrAll =
                Enterprise + All + Features.All;
            #endregion
        }
#endif
        #endregion
    }
}
