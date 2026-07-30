/*
 * Constants.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Encodings;
using Licensing.Components.Public;
using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the shared constant values, environment variable names,
    /// default settings, regular expressions, and format strings used
    /// throughout the private licensing (Harpy) subsystem.
    /// </summary>
    [ObjectId("51783a25-4477-4b5f-8a05-d4363a9a2042")]
    internal static class Constants
    {
        #region Fail-Safe Constants
        /// <summary>
        /// The format string used for the trip fail safe error message.
        /// </summary>
        /* CORE */
        public const string TripFailSafeErrorFormat =
            "fail-safe check failure: {0}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the fallback fail safe error message.
        /// </summary>
        /* CORE */
        public const string FallbackFailSafeErrorFormat =
            "FATAL FALLBACK abort due to fail-safe: {0}"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Encoding Constants
        //
        // WARNING: Do not change this as it must be a UTF8 encoding
        //          (preferably without the byte-order-mark enabled).
        //
        /// <summary>
        /// The default encoding used by the licensing subsystem.
        /// </summary>
        /* CORE */
        public static readonly Encoding DefaultEncoding = new UTF8Encoding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The log encoding used by the licensing subsystem.
        /// </summary>
        /* CORE */
        public static readonly Encoding LogEncoding = new UTF8Encoding(true);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The raw encoding used by the licensing subsystem.
        /// </summary>
        /* CORE */
        public static readonly Encoding RawEncoding = OneByteEncoding.OneByte;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Encryption Constants
        //
        // HACK: This is used to (optionally) obfuscate the keys used to
        //       encrypt stuff, e.g. process license tickets with RC4.
        //
        /// <summary>
        /// The bit mask describing the obfuscate bit flags.
        /// </summary>
        public const int ObfuscateBitMask = 85; /* 0b01010101 */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region [RD]SACryptoServiceProvider Constants
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS && !NET_STANDARD_20
        //
        // HACK: Per MSDN documentation, these are the values to use for
        //       the CspParameters.ProviderType field that are compatible
        //       with the [RD]SACryptoServiceProvider classes.
        //
        /// <summary>
        /// The PROV RSA FULL constant value.
        /// </summary>
        public const int PROV_RSA_FULL = 1;
        /// <summary>
        /// The PROV DSS DH constant value.
        /// </summary>
        public const int PROV_DSS_DH = 13;
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // HACK: Mono does not handle key sizes of zero when attempting
        //       to generate a key pair; therefore, codify the default
        //       key sizes (e.g. 1024) used for Windows CryptoAPI there
        //       as well.
        //
        /// <summary>
        /// The default RSA key size.
        /// </summary>
        public const int DefaultRsaKeySize = 1024;
        /// <summary>
        /// The default DSA key size.
        /// </summary>
        public const int DefaultDsaKeySize = 1024;
#endif

        ///////////////////////////////////////////////////////////////////////

#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
        //
        // HACK: If this environment variable is set [to anything], the
        //       RSA provider used will be BigRSACryptoServiceProvider.
        //
        /// <summary>
        /// The name of the use big crypto environment variable.
        /// </summary>
        /* CORE */
        public const string UseBigCryptoEnvVarName =
            "UseBigCrypto"; /* MAY NOT BE NULL */

        //
        // HACK: If this environment variable is set [to anything], the
        //       BigRSACryptoServiceProvider private-key modular exponentiation
        //       will use the in-house BigBigInteger engine instead of the
        //       framework System.Numerics.BigInteger.ModPow. Off by default.
        //
        /// <summary>
        /// The name of the use big big integer environment variable.
        /// </summary>
        /* CORE */
        public const string UseBigBigIntegerEnvVarName =
            "UseBigBigInteger"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hash Algorithm Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     Changing these values WILL break ALL existing certificates    //
        //     and MAY break ALL existing license renewal requests.          //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default legacy hash algorithm name.
        /// </summary>
        /* CORE */
        private const string DefaultLegacyHashAlgorithmName =
            "SHA1"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default local hash algorithm name.
        /// </summary>
        /* CORE */
        private const string DefaultLocalHashAlgorithmName =
            "SHA512"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default remote hash algorithm name.
        /// </summary>
        /* CORE */
        private const string DefaultRemoteHashAlgorithmName =
            "SHA512"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default script hash algorithm name.
        /// </summary>
        /* CORE */
        private const string DefaultScriptHashAlgorithmName =
            "SHA512"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default assembly hash algorithm name.
        /// </summary>
        /* CORE */
        private const string DefaultAssemblyHashAlgorithmName =
            "SHA512"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
        //
        // NOTE: The hash algorithm to use instead of falling back to the
        //       system default.
        //
        /// <summary>
        /// The name of the hash algorithm environment variable.
        /// </summary>
        /* CORE */
        public const string HashAlgorithmEnvVarName =
            "HashAlgorithm"; /* MAY NOT BE NULL */
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending License Constants
        /// <summary>
        /// The name of the pending license certificate count environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string PendingLicenseCertificateCountEnvVarName =
            "PendingLicenseCertificateCount"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Key Ring Constants
        #region Environment Variable Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The name of the pending key ring count environment variable.
        /// </summary>
        /* CORE? */
        public const string PendingKeyRingCountEnvVarName =
            "PendingKeyRingCount"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the pending license key ring count environment
        /// variable.
        /// </summary>
        /* CORE? */
        public const string PendingLicenseKeyRingCountEnvVarName =
            "PendingLicenseKeyRingCount"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The name of the pending key ring file name environment variable.
        /// </summary>
        /* CORE? */
        public const string PendingKeyRingFileNameEnvVarName =
            "PendingKeyRingFileName"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// The name of the pending renewal key ring count environment
        /// variable.
        /// </summary>
        /* CORE? */
        public const string PendingRenewalKeyRingCountEnvVarName =
            "PendingRenewalKeyRingCount"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the pending policy count environment variable.
        /// </summary>
        /* CORE? */
        public const string PendingPolicyCountEnvVarName =
            "PendingPolicyCount"; /* MAY NOT BE NULL */
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Network Time Constants
        /// <summary>
        /// The name of the pending time via NTP count environment variable.
        /// </summary>
        /* CORE? */
        public const string PendingTimeViaNtpCountEnvVarName =
            "PendingTimeViaNtpCount"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the pending time via HTTPS count environment variable.
        /// </summary>
        /* CORE? */
        public const string PendingTimeViaHttpsCountEnvVarName =
            "PendingTimeViaHttpsCount"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Constants
        /// <summary>
        /// The placeholder text displayed for null values.
        /// </summary>
        /* CORE */
        public static object DisplayNull = _Utility.FormatMaybeNull(
            null); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The placeholder text displayed for empty values.
        /// </summary>
        /* CORE */
        public static object DisplayEmpty = _Utility.FormatMaybeNullOrEmpty(
            String.Empty); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The placeholder text displayed for error values.
        /// </summary>
        /* CORE */
        public static string DisplayError = "<error>"; /* MAY BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Trace Constants
        #region Environment Variable Constants
        //
        // NOTE: Flags enumeration value to use when modifying the default
        //       license execution policy (i.e. in order to enable tracing
        //       for the duration of license certificate verification).
        //
        /// <summary>
        /// The name of the license execution policy environment variable.
        /// </summary>
        /* CORE */
        public const string LicenseExecutionPolicyEnvVarName =
            "LicenseExecutionPolicy"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: Flags enumeration value to use when modifying the default
        //       script execution policy (i.e. in order to enable tracing
        //       for the duration of script certificate verification).
        //
        /// <summary>
        /// The name of the script execution policy environment variable.
        /// </summary>
        /* CORE */
        public const string ScriptExecutionPolicyEnvVarName =
            "ScriptExecutionPolicy"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this environment variable is set (to anything), the
        //       license key rings will not be loaded via the file system
        //       while license certificates are being verified.  Instead,
        //       only the assembly public key will be used.
        //
        /// <summary>
        /// The name of the no load license key rings environment variable.
        /// </summary>
        /* CORE? */
        public const string NoLoadLicenseKeyRingsEnvVarName =
            "NoLoadLicenseKeyRings"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this environment variable is set (to anything), all
        //       policy tracing features will be enabled during the plugin
        //       loading process -AND- prior to doing any significant work.
        //       This can be very useful for troubleshooting issues with
        //       script execution policy enforcement.
        //
        /// <summary>
        /// The name of the full plugin policy tracing environment variable.
        /// </summary>
        /* CORE? */
        public const string FullPluginPolicyTracingEnvVarName =
            "FullPluginPolicyTracing"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this environment variable is set (to anything), the
        //       "EnablePolicyTracing" feature flag will be honored for a
        //       license certificate being processed.
        //
        /// <summary>
        /// The name of the enable policy tracing environment variable.
        /// </summary>
        /* CORE? */
        public const string EnablePolicyTracingEnvVarName =
            "EnablePolicyTracing"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this environment variable is set (to anything), the
        //       "DumpKeyPairs" method will increase the priority of its
        //       trace output.
        //
        /// <summary>
        /// The name of the dump key pairs environment variable.
        /// </summary>
        /* CORE? */
        public const string DumpKeyPairsEnvVarName =
            "DumpKeyPairs"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        //
        // NOTE: When this environment variable is set (to anything), the
        //       "GetMachineId" method will use its value instead of the
        //       actual machine GUID (from the Windows registry).
        //
        /// <summary>
        /// The name of the machine GUID environment variable.
        /// </summary>
        /* CORE? */
        public const string MachineGuidEnvVarName =
            "MachineGuid"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this environment variable is set (to anything), the
        //       "GetMachineId" method will use its value instead of the
        //       actual volume serial number.
        //
        /// <summary>
        /// The name of the machine volume serial number environment variable.
        /// </summary>
        /* CORE? */
        public const string MachineVolumeSerialNumberEnvVarName =
            "MachineVolumeSerialNumber"; /* MAY NOT BE NULL */
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The tracing log file name to use when enhanced tracing is
        //       enabled.  In general, this will be used in troubleshooting
        //       issues related to certificate verification.
        //
        /// <summary>
        /// The name of the certificate trace file environment variable.
        /// </summary>
        /* CORE */
        public const string CertificateTraceFileEnvVarName =
            "CertificateTraceFile"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The trace priority flags to use when the verbose option has
        //       been set.  Without this, it will fallback to defaults, which
        //       has the value "TroubleshootingMask".
        //
        /// <summary>
        /// The name of the verbose trace priority environment variable.
        /// </summary>
        /* CORE */
        public const string VerboseTracePriorityEnvVarName =
            "VerboseTracePriority"; /* MAY NOT BE NULL */
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Ticket Constants
        /// <summary>
        /// The name of the ticket ID environment variable.
        /// </summary>
        /* CORE */
        public const string TicketIdEnvVarName =
            "HarpyTicketId"; /* MAY NOT BE NULL */

        /// <summary>
        /// The composite format string used to construct the ticket
        /// environment variable name.
        /// </summary>
        /* CORE */
        public const string TicketEnvVarFormat =
            "Harpy{0}Ticket"; /* MAY NOT BE NULL */

        //
        // TODO: Update the Harpy SDK version here when it is changed
        //       in the "HarpyRes.h" file.
        //
        /// <summary>
        /// The format string used for the ticket.
        /// </summary>
        /* CORE */
        public const string TicketFormat =
            "Harpy SDK v1.15 {0} Ticket for {1} {2}"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region RFC 2898 Constants
        //
        // NOTE: Various sources on the Internet point out that the minimum
        //       "safe" iteration count is currently (as of November 2016)
        //       around 100K.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The RFC 2898 iteration count.
        /// </summary>
        public static int Rfc2898IterationCount = 100000;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The legacy HMACSHA1 will be used when this is null.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The RFC 2898 hash algorithm name.
        /// </summary>
        public static string Rfc2898HashAlgorithmName = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Generally, this should not be used; however, it should be
        //       the signature associated with the "default" RFC 2898 data.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The RFC 2898 signature.
        /// </summary>
        public static string Rfc2898Signature = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Reason Constants
        /// <summary>
        /// The explanatory reason text for the enterprise key case.
        /// </summary>
        public const string EnterpriseKeyReason =
            "it is the enterprise assembly key pair";

        ///////////////////////////////////////////////////////////////////////

#if DEBUG
        /// <summary>
        /// The explanatory reason text for the build machine key case.
        /// </summary>
        public const string BuildKeyReason =
            "it is the build machine key pair";
#endif

        ///////////////////////////////////////////////////////////////////////

#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// The explanatory reason text for the renewal skip case.
        /// </summary>
        public const string RenewalSkipReason = "renewal is pending";
#endif

        ///////////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The explanatory reason text for the demo skip case.
        /// </summary>
        public const string DemoSkipReason = "\"demo\" license is pending";
        /// <summary>
        /// The explanatory reason text for the demo key case.
        /// </summary>
        public const string DemoKeyReason = "it is the \"demo\" key pair";
#endif

        ///////////////////////////////////////////////////////////////////////

#if !LIMITED_EDITION
        /// <summary>
        /// The explanatory reason text for the promotional skip case.
        /// </summary>
        public const string PromotionalSkipReason = "promotional feature is enabled";
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The explanatory reason text for the feature skip case.
        /// </summary>
        public const string FeatureSkipReason = "feature license skipping is enabled";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The explanatory reason text for the unknown skip case.
        /// </summary>
        public const string UnknownSkipReason = "unknown reason";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Asymmetric Algorithm Constants
#if CERTIFICATE_PLUGIN
        //
        // HACK: This is only used as a fallback value when the key pair
        //       cannot be located or used.
        //
        /// <summary>
        /// The public key algorithm name.
        /// </summary>
        public const string PublicKeyAlgorithmName = "Unknown";
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Symmetric Algorithm Constants
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The name of the force allow only FIPS algorithms environment
        /// variable.
        /// </summary>
        public const string ForceAllowOnlyFipsAlgorithmsEnvVarName =
            "ForceAllowOnlyFipsAlgorithms"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if NET_40
        //
        // TODO: Will this always work correctly for .NET Core?
        //
        /// <summary>
        /// The default FIPS symmetric algorithm version.
        /// </summary>
        public static readonly Version DefaultFipsSymmetricAlgorithmVersion =
            new Version(4, 0, 0, 0); /* MAY NOT BE NULL */
#else
        /// <summary>
        /// The default FIPS symmetric algorithm version.
        /// </summary>
        public static readonly Version DefaultFipsSymmetricAlgorithmVersion =
            new Version(3, 5, 0, 0); /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: For reasons which are not 100% clear, this "algorithm name"
        //       must be a full, assembly qualified type name in order for
        //       creation to work via the SymmetricAlgorithm.Create method.
        //
        // NOTE: This is the default symmetric algorithm when FIPS mode is
        //       being used by Windows.  Apparently, this is a wrapper that
        //       simply uses the FIPS facilities provided by the underlying
        //       (Windows) operating system.
        //
        // TODO: On other operating systems, does this make any difference?
        //
        /// <summary>
        /// The format string used for the default FIPS symmetric algorithm.
        /// </summary>
        public const string DefaultFipsSymmetricAlgorithmFormat =
            "System.Security.Cryptography.AesCryptoServiceProvider, " +
            "System.Core, Version={0}, Culture=neutral, " +
            "PublicKeyToken={1}"; /* MAY NOT BE NULL */

        //
        // NOTE: This is the default symmetric algorithm when FIPS mode is
        //       not being used by Windows.  As the name suggests, this is
        //       a purely managed implementation of the Rijndael encryption
        //       algorithm.  It is not FIPS compliant.
        //
        /// <summary>
        /// The default symmetric algorithm name.
        /// </summary>
        public const string DefaultSymmetricAlgorithmName =
            "RijndaelManaged"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default cipher mode.
        /// </summary>
        public const CipherMode DefaultCipherMode = CipherMode.CBC;
        /// <summary>
        /// The default padding mode.
        /// </summary>
        public const PaddingMode DefaultPaddingMode = PaddingMode.PKCS7;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default key bits.
        /// </summary>
        public const int DefaultKeyBits = 256;       /* 32 bytes */
        /// <summary>
        /// The default block bits.
        /// </summary>
        public const int DefaultBlockBits = 128;     /* 16 bytes */
        /// <summary>
        /// The default feedback bits.
        /// </summary>
        public const int DefaultFeedbackBits = 128;  /* 16 bytes */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The minimum password represented as an array of bytes.
        /// </summary>
        public const int MinimumPasswordBytes = 12;  /* SHA1 - salt(minimum) */
        /// <summary>
        /// The maximum password represented as an array of bytes.
        /// </summary>
        public const int MaximumPasswordBytes = 256; /* SQLite (SEE) */

        /// <summary>
        /// The minimum salt represented as an array of bytes.
        /// </summary>
        public const int MinimumSaltBytes = 8;       /* PBKDF2 */
        /// <summary>
        /// The maximum salt represented as an array of bytes.
        /// </summary>
        public const int MaximumSaltBytes = -1;      /* PBKDF2: no limit */

        /// <summary>
        /// The default iterations.
        /// </summary>
        public const int DefaultIterations = 1000;   /* PBKDF2 */
        /// <summary>
        /// The minimum iterations.
        /// </summary>
        public const int MinimumIterations = 1000;   /* PBKDF2 */
        /// <summary>
        /// The maximum iterations.
        /// </summary>
        public const int MaximumIterations = -1;     /* PBKDF2: no limit */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default derive represented as an array of bytes.
        /// </summary>
        public const int DefaultDeriveBytes = 20;    /* 160 bits: HMAC-SHA-1 */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The generate password represented as an array of bytes.
        /// </summary>
        public const int GeneratePasswordBytes = 128;
        /// <summary>
        /// The generate salt represented as an array of bytes.
        /// </summary>
        public const int GenerateSaltBytes = 64;

        /// <summary>
        /// The generate entropy represented as an array of bytes.
        /// </summary>
        public const int GenerateEntropyBytes =
            GeneratePasswordBytes + GenerateSaltBytes;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Generic Name/Value Parameter Handling
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The prefix string used for the parameter.
        /// </summary>
        public const char ParameterPrefix = Characters.ExclamationMark;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The regular expression used to match the parameter.
        /// </summary>
        public static readonly Regex ParameterRegEx = new Regex(
            "^!\\s+(\\w+):\\s+(.*)$"); /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the get data.
        /// </summary>
        public static readonly string GetDataFormat = "{0}_{1}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Parsing Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //    Changing these values MAY break ALL existing certificates.     //
        //    Do not change any of these values unless you know exactly      //
        //    what they do.                                                  //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix string used for the default hex.
        /// </summary>
        /* CORE */
        public const string DefaultHexPrefix = "0x"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the default int.
        /// </summary>
        /* CORE */
        public const string DefaultIntFormat = "x8"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the default long.
        /// </summary>
        /* CORE */
        public const string DefaultLongFormat = "x16"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default number styles.
        /// </summary>
        /* CORE */
        public const NumberStyles DefaultNumberStyles = NumberStyles.HexNumber;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Checking Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     Changing these values WILL break ALL existing certificates    //
        //     and/or license renewal requests.                              //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The legacy hash algorithm name.
        /// </summary>
        /* CORE */
        public const string LegacyHashAlgorithmName =
            DefaultLegacyHashAlgorithmName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The local hash algorithm name.
        /// </summary>
        /* CORE */
        public const string LocalHashAlgorithmName =
            DefaultLocalHashAlgorithmName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The remote hash algorithm name.
        /// </summary>
        /* CORE */
        public const string RemoteHashAlgorithmName =
            DefaultRemoteHashAlgorithmName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script hash algorithm name.
        /// </summary>
        /* CORE */
        public const string ScriptHashAlgorithmName =
            DefaultScriptHashAlgorithmName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The assembly hash algorithm name.
        /// </summary>
        /* CORE */
        public const string AssemblyHashAlgorithmName =
            DefaultAssemblyHashAlgorithmName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The optional hash algorithm name.
        /// </summary>
        /* CORE */
        public const string OptionalHashAlgorithmName = null; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// The certificate force.
        /// </summary>
        public const bool CertificateForce = false;
#endif

        ///////////////////////////////////////////////////////////////////////

        /* NOTE: This is actually a NON-BREAKING setting. */
#if EMBED_CERTIFICATES
        /// <summary>
        /// The certificate embedded.
        /// </summary>
        public const bool CertificateEmbedded = true;
#else
        /// <summary>
        /// The certificate embedded.
        /// </summary>
        public const bool CertificateEmbedded = false;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The certificate validate.
        /// </summary>
        public const bool CertificateValidate = true;
        /// <summary>
        /// The certificate any resource public key.
        /// </summary>
        public const bool CertificateAnyResourcePublicKey = true;

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// The error message text used for the default trace on.
        /// </summary>
        public const bool DefaultTraceOnError = true;
        /// <summary>
        /// The default trace on found.
        /// </summary>
        public const bool DefaultTraceOnFound = true;
        /// <summary>
        /// The default trace on not found.
        /// </summary>
        public const bool DefaultTraceOnNotFound = true;
#else
        /// <summary>
        /// The error message text used for the default trace on.
        /// </summary>
        public const bool DefaultTraceOnError = false;
        /// <summary>
        /// The default trace on found.
        /// </summary>
        public const bool DefaultTraceOnFound = false;
        /// <summary>
        /// The default trace on not found.
        /// </summary>
        public const bool DefaultTraceOnNotFound = false;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The placeholder text displayed for none values.
        /// </summary>
        /* CORE */
        public const string DisplayNone = "<none>"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants (Key Rings)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The name of the specific key ring only environment variable.
        /// </summary>
        /* CORE? */
        public const string SpecificKeyRingOnlyEnvVarName =
            "SpecificKeyRingOnly"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the primary key ring only environment variable.
        /// </summary>
        /* CORE? */
        public const string PrimaryKeyRingOnlyEnvVarName =
            "PrimaryKeyRingOnly"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants (License Key Rings)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The name of the no load key rings environment variable.
        /// </summary>
        /* CORE? */
        public const string NoLoadKeyRingsEnvVarName =
            "NoLoadKeyRings"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants (Debugging Only)
#if DEBUG || EXTRA_DIAGNOSTICS
        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will *NOT* query the network certificate server in
        //       order to determine if a key pair and/or certificate has
        //       been revoked.
        //
        /// <summary>
        /// The name of the no network revocation environment variable.
        /// </summary>
        /* CORE */
        public const string NoNetworkRevocationEnvVarName =
            "NoNetworkRevocation"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will *NOT* query the network time server in order
        //       to determine if the system clock has been tampered with.
        //
        /// <summary>
        /// The name of the no network time environment variable.
        /// </summary>
        /* CORE */
        public const string NoNetworkTimeEnvVarName =
            "NoNetworkTime"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), only
        //       the primary network time server will be used to determine
        //       if the system clock has been tampered with.
        //
        /// <summary>
        /// The name of the primary network time environment variable.
        /// </summary>
        /* CORE */
        public const string PrimaryNetworkTimeEnvVarName =
            "PrimaryNetworkTime"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), it will
        //       be used as the network time URI.  Depending on the network
        //       time subsystem used, this must either be just a host name,
        //       address, or a full URI.
        //
        /// <summary>
        /// The name of the network time URI environment variable.
        /// </summary>
        /* CORE */
        public const string NetworkTimeUriEnvVarName =
            "NetworkTimeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), it will
        //       be used as the network time URI for the specified protocol.
        //       Depending on the network time subsystem used, this must
        //       either be just a host name, address, or a full URI.
        //
        /// <summary>
        /// The composite format string used to construct the network time URI
        /// environment variable name.
        /// </summary>
        /* CORE */
        public const string NetworkTimeUriEnvVarFormat =
            "NetworkTime{0}Uri"; /* MAY NOT BE NULL */
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants (Common)
        //
        // NOTE: Always treat all certificates as revoked?  This is for use
        //       during testing only.
        //
        /// <summary>
        /// The name of the always revoked environment variable.
        /// </summary>
        /* CORE */
        public const string AlwaysRevokedEnvVarName =
            "AlwaysRevoked"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Always expire all certificates?  This does not apply when
        //       called by the license certificate renewal subsystem.
        //
        /// <summary>
        /// The name of the always expires environment variable.
        /// </summary>
        /* CORE */
        public const string AlwaysExpiresEnvVarName =
            "AlwaysExpires"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Always treat all certificates as "limited quantity"?
        //
        /// <summary>
        /// The name of the always limited quantity environment variable.
        /// </summary>
        /* CORE */
        public const string AlwaysLimitedQuantityEnvVarName =
            "AlwaysLimitedQuantity"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: For use by various (sub-command) implmentations when an
        //       explicit API key has not been specified.
        //
        /// <summary>
        /// The name of the harpy API key environment variable.
        /// </summary>
        /* CORE */
        public const string HarpyApiKeyEnvVarName =
            "HarpyApiKey"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: For use by various (sub-command) implmentations when an
        //       explicit API identifier has not been specified.
        //
        /// <summary>
        /// The name of the harpy API ID environment variable.
        /// </summary>
        /* CORE */
        public const string HarpyApiIdEnvVarName =
            "HarpyApiId"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), license
        //       (verification) subsystem will verify the extra features -OR-
        //       license certificate itself have the AsynchronousLicensing
        //       feature enabled.
        //
        //       This is primarily for use by the managed SDK, generally via
        //       its examples.  When this is set, it will generally cause the
        //       license certificate verification to be performed on a thread
        //       pool thread.
        //
        /// <summary>
        /// The name of the asynchronous licensing environment variable.
        /// </summary>
        /* CORE */
        public const string AsynchronousLicensingEnvVarName =
            "AsynchronousLicensing"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The license (verification) subsystem should aggressively
        //       attempt to use (all available?) cached certificate files.
        //
        /// <summary>
        /// The name of the harpy aggressive cache environment variable.
        /// </summary>
        /* CORE */
        public const string HarpyAggressiveCacheEnvVarName =
            "HarpyAggressiveCache"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The tracing subsystem should NOT disable its special trace
        //       handling, including its log file.
        //
        /// <summary>
        /// The name of the harpy persistent tracing environment variable.
        /// </summary>
        /* CORE */
        public const string HarpyPersistentTracingEnvVarName =
            "HarpyPersistentTracing"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Never create an interpreter, e.g. for the configuration
        //       subsystem, et al?
        //
        /// <summary>
        /// The name of the no create interpreter environment variable.
        /// </summary>
        /* CORE */
        public const string NoCreateInterpreterEnvVarName =
            "HarpyNoCreateInterpreter"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force use of full stack traces within all trace messages
        //       that originate from the Harpy tracing subsystem.
        //
        /// <summary>
        /// The name of the force stack trace environment variable.
        /// </summary>
        /* CORE */
        public const string ForceStackTraceEnvVarName =
            "HarpyForceStackTrace"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Emit special trace messages for all network access that
        //       is needed to verify certificates, e.g. network time and
        //       certificate / key revocation checking.
        //
        /// <summary>
        /// The name of the network trace environment variable.
        /// </summary>
        /* CORE */
        public const string NetworkTraceEnvVarName =
            "HarpyNetworkTrace"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the no internal environment variable.
        /// </summary>
        /* CORE */
        public const string NoInternalEnvVarName =
            "HarpyNoInternal"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This may be the timeout value, in milliseconds, used by a
        //       few different subsystems.  Primarily, it is intended for
        //       use by the default plugin.
        //
        /// <summary>
        /// The name of the timeout environment variable.
        /// </summary>
        public const string TimeoutEnvVarName = "HarpyTimeout";

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: For the purposes of the configuration subsystem, pretend
        //       that only the "encrypted" configuration files actually
        //       exist (i.e. all other configuration files are ignored).
        //
        /// <summary>
        /// The name of the harpy encrypted configurations only environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string HarpyEncryptedConfigurationsOnlyEnvVarName =
            "HarpyEncryptedConfigurationsOnly";
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: For the purposes of the configuration subsystem, pretend
        //       that a debugger is attached.
        //
        /// <summary>
        /// The name of the force debugger configuration environment variable.
        /// </summary>
        /* CORE */
        public const string ForceDebuggerConfigurationEnvVarName =
            "HarpyForceDebuggerConfiguration"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Do not merge all available loaded key pairs for querying
        //       the remote HTTP time server when checking for certificate
        //       expiration.
        //
        /// <summary>
        /// The name of the no merge key pairs for expiration environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string NoMergeKeyPairsForExpirationEnvVarName =
            "NoMergeKeyPairsForExpiration"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force the logging subsystem to use one file name, within
        //       a particular directory, for the current process.
        //
        /// <summary>
        /// The name of the force log per process environment variable.
        /// </summary>
        /* CORE */
        public const string ForceLogPerProcessEnvVarName =
            "ForceLogPerProcess";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force the logging subsystem to use one file name, within
        //       a particular directory, for the current AppDomain.
        //
        /// <summary>
        /// The name of the force log per app domain environment variable.
        /// </summary>
        /* CORE */
        public const string ForceLogPerAppDomainEnvVarName =
            "ForceLogPerAppDomain";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force logging all trace messages that originate from
        //       within the network subsystem entry points (i.e. the
        //       "NetworkDebugTrace" method).
        //
        /// <summary>
        /// The name of the force log network environment variable.
        /// </summary>
        /* CORE */
        public const string ForceLogNetworkEnvVarName =
            "ForceLogNetwork"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force logging all trace messages that originate from
        //       within the license verification subsystem entry point
        //       (i.e. the "LoadAndProcess" method).
        //
        /// <summary>
        /// The name of the force log license environment variable.
        /// </summary>
        /* CORE */
        public const string ForceLogLicenseEnvVarName =
            "ForceLogLicense"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: Force logging all trace messages that originate from
        //       within the policy implementations.
        //
        /// <summary>
        /// The name of the force log script environment variable.
        /// </summary>
        /* CORE */
        public const string ForceLogScriptEnvVarName =
            "ForceLogScript"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force the plugin to go into SDK mode prior to verifying a
        //       license certificate.
        //
        /// <summary>
        /// The name of the force SDK mode environment variable.
        /// </summary>
        /* CORE */
        public const string ForceSdkModeEnvVarName =
            "HarpyForceSdkMode"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Force tracing of the full package name.
        //
        /// <summary>
        /// The name of the use full package name environment variable.
        /// </summary>
        /* CORE */
        public const string UseFullPackageNameEnvVarName =
            "UseFullPackageName"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Never attempt to acquire a license for Harpy online?
        //
        /// <summary>
        /// The name of the no auto acquire environment variable.
        /// </summary>
        /* CORE */
        public const string NoAutoAcquireEnvVarName =
            "HarpyNoAutoAcquire"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the treat as isolated environment variable.
        /// </summary>
        /* CORE */
        public const string TreatAsIsolatedEnvVarName =
            "HarpyTreatAsIsolated"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        /// <summary>
        /// The name of the harpy web maximum retries environment variable.
        /// </summary>
        /* CORE */
        public const string HarpyWebMaximumRetriesEnvVarName =
            "HarpyWebMaximumRetries"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Emit trace messages in the event that something prevents a
        //       "candidate" license certificate file from being imported?
        //
        /// <summary>
        /// The name of the trace on error environment variable.
        /// </summary>
        /* CORE */
        public const string TraceOnErrorEnvVarName =
            "HarpyTraceOnError"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Emit trace messages in the event that something prevents a
        //       license certificate file from being imported?
        //
        /// <summary>
        /// The name of the no trace on error environment variable.
        /// </summary>
        /* CORE */
        public const string NoTraceOnErrorEnvVarName =
            "HarpyNoTraceOnError"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS && NETWORK && WEB
        //
        // NOTE: Attempt to get passwords from a remote server in the event
        //       that one cannot be found locally.
        //
        /// <summary>
        /// The name of the use remote passwords environment variable.
        /// </summary>
        /* CORE */
        public const string UseRemotePasswordsEnvVarName =
            "HarpyUseRemotePasswords"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Error Message Constants
        /// <summary>
        /// The error message text used for the public key token mismatch.
        /// </summary>
        /* CORE */
        public const string PublicKeyTokenMismatchError =
            "public key token mismatch"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// The pattern string used to match the public key token mismatch.
        /// </summary>
        /* CORE (?!) */
        public const string PublicKeyTokenMismatchPattern =
            "public key * token mismatch"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the public key token mismatch.
        /// </summary>
        /* CORE (?!) */
        public const string PublicKeyTokenMismatchFormat =
            "public key {0} versus {1} token mismatch"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
        /// <summary>
        /// The error message text used for the public key untrusted.
        /// </summary>
        /* CORE */
        public const string PublicKeyUntrustedError =
            "public key not trusted for usage"; /* MAY BE NULL */
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Revocation & Renewal Constants
        #region URI Constants
#if NETWORK
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     Changing these values MAY break ALL certificate renewal       //
        //     requests.                                                     //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //     These values, when changed, must also be changed in the       //
        //     Kapok project (i.e. the license renewal server).              //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// The format string used for the default local host URI.
        /// </summary>
        /* CORE */
        public const string DefaultLocalHostUriFormat =
            "http://localhost{0}/"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default authority URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultAuthorityUriComponents =
            UriComponents.AbsoluteUri;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default origin URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultOriginUriComponents =
            UriComponents.SchemeAndServer | UriComponents.UserInfo;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default assembly authority URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultAssemblyAuthorityUriComponents =
            UriComponents.AbsoluteUri;

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// The default local host URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultLocalHostUriComponents =
            UriComponents.SchemeAndServer;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default plugin URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultPluginUriComponents =
            UriComponents.SchemeAndServer | UriComponents.UserInfo;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default assembly URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultAssemblyUriComponents =
            UriComponents.SchemeAndServer | UriComponents.UserInfo;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default relative URI components.
        /// </summary>
        /* CORE */
        public const UriComponents DefaultRelativeUriComponents =
            UriComponents.Path | UriComponents.Query | UriComponents.Fragment;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
#if NETWORK
#if DEBUG || EXTRA_DIAGNOSTICS
        //
        // NOTE: Always use the license certificate renewal server that is
        //       present on the local host?  This is for use with the debug
        //       build configuration only.
        //
        /// <summary>
        /// The name of the use local host environment variable.
        /// </summary>
        public const string UseLocalHostEnvVarName =
            "UseLocalHost"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Override any certificate authority URI that may be present
        //       in certificates and/or the assembly itself.
        //
        /// <summary>
        /// The name of the authority base URI environment variable.
        /// </summary>
        public const string AuthorityBaseUriEnvVarName =
            "AuthorityBaseUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The optional environment variable containing the license
        //       certificate renewal server port number to use (i.e. on the
        //       local host only).  This is for use with the debug build
        //       configuration only.
        //
        /// <summary>
        /// The name of the server port environment variable.
        /// </summary>
        public const string ServerPortEnvVarName =
            "ServerPort"; /* MAY NOT BE NULL */
#endif
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Revocation Constants
        #region URI Constants
#if NETWORK
        /// <summary>
        /// The default relative URI used for the default revocation request.
        /// </summary>
        /* CORE */
        public const string DefaultRevocationRelativeUri =
            "certificate/revoked.cgi?"; /* MAY BE NULL */

        /* CORE */
        // public const string DefaultRevocationRelativeUri =
        //     "cgi-bin/revoked.cgi?"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
#if NETWORK
        //
        // NOTE: The optional environment variable containing the license
        //       certificate revocation URI to use.  This URI "fragment" will
        //       be combined with the configured license certificate revocation
        //       server and port and then the formatted query string will be
        //       appended to it, verbatim.
        //
        /// <summary>
        /// The name of the revocation relative URI environment variable.
        /// </summary>
        public const string RevocationRelativeUriEnvVarName =
            "RevocationRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), the
        //       license revocation URI does not require any relative URI
        //       added to it.  If the "RevocationRelativeUri" environment
        //       variable is set, this one is not consulted.
        //
        /// <summary>
        /// The name of the no revocation relative URI environment variable.
        /// </summary>
        public const string NoRevocationRelativeUriEnvVarName =
            "NoRevocationRelativeUri"; /* MAY NOT BE NULL */
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Renewal Constants
#if NETWORK && CERTIFICATE_RENEWAL
        #region Hash Algorithm Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     Changing these values MAY break ALL certificate renewal       //
        //     requests.                                                     //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The request hash algorithm name.
        /// </summary>
        public const string RequestHashAlgorithmName =
            "HMACSHA512"; /* MAY BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region URI Constants
        /// <summary>
        /// The default relative URI used for the default renewal request.
        /// </summary>
        public const string DefaultRenewalRelativeUri =
            "certificate/renew.cgi?"; /* MAY BE NULL */

        // public const string DefaultRenewalRelativeUri =
        //     "cgi-bin/renew.cgi?"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the renewal request.
        /// </summary>
        public const string RenewalRequestFormat =
            "{0}+{1}+{2}+{3}+{4}+{5}"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Response Data Constants
        //
        // NOTE: This is the name for the certificate data name/value pair
        //       within the dictionary parsed from the Kapok response text.
        //
        /// <summary>
        /// The name of the renewal certificate data item.
        /// </summary>
        public const string RenewalCertificateDataName =
            "CertificateData"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name for the key ring data name/value pair
        //       within the dictionary parsed from the Kapok response text.
        //
        /// <summary>
        /// The name of the renewal key ring data item.
        /// </summary>
        public const string RenewalKeyRingDataName =
            "KeyRingData"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name for the key ring signature name/value pair
        //       within the dictionary parsed from the Kapok response text.
        //
        /// <summary>
        /// The renewal key ring signature name.
        /// </summary>
        public const string RenewalKeyRingSignatureName =
            "KeyRingSignature"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the server that sent the response.
        //
        /// <summary>
        /// The renewal server info name.
        /// </summary>
        public const string RenewalServerInfoName =
            "ServerInfo"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
        //
        // NOTE: If this environment variable is set (to anything), the
        //       license renewal server will not be automatically considered
        //       'trusted' in calls to the HTTPS server.
        //
        /// <summary>
        /// The name of the no trusted renewal environment variable.
        /// </summary>
        public const string NoTrustedRenewalEnvVarName =
            "NoTrustedRenewal"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The optional environment variable containing the license
        //       certificate renewal URI to use.  This URI "fragment" will
        //       be combined with the configured license certificate renewal
        //       server and port and then the formatted query string will be
        //       appended to it, verbatim.
        //
        /// <summary>
        /// The name of the renewal relative URI environment variable.
        /// </summary>
        public const string RenewalRelativeUriEnvVarName =
            "RenewalRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), the
        //       license renewal URI does not require any relative URI
        //       added to it.  If the "RenewalRelativeUri" environment
        //       variable is set, this one is not consulted.
        //
        /// <summary>
        /// The name of the no renewal relative URI environment variable.
        /// </summary>
        public const string NoRenewalRelativeUriEnvVarName =
            "NoRenewalRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Skip backing up the current certificate file prior to writing
        //       the new certificate when certificate renewal is successful?
        //
        /// <summary>
        /// The name of the no backup certificate file environment variable.
        /// </summary>
        public const string NoBackupCertificateFileEnvVarName =
            "NoBackupCertificateFile"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        //
        // NOTE: Always use the default license certificate renewal callback?
        //       This is for use with the debug build configuration only.
        //
        /// <summary>
        /// The name of the use default renew callback environment variable.
        /// </summary>
        public const string UseDefaultRenewCallbackEnvVarName =
            "UseDefaultRenewCallback"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Backup File Name Constants
        //
        // NOTE: These are used when backing up the existing certificate file
        //       after a successful renewal.
        //
        /// <summary>
        /// The format string used for the backup date time.
        /// </summary>
        public const string BackupDateTimeFormat =
            "yyyy-MM-dd-HH-mm-ss"; /* MAY NOT BE NULL */

        /// <summary>
        /// The backup file extension.
        /// </summary>
        public const string BackupFileExtension = ".bak"; /* MAY BE NULL */
        #endregion
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region File Name Constants
        /// <summary>
        /// The prefix string used for the temporary license.
        /// </summary>
        public const string TemporaryLicensePrefix = /* MAY BE NULL */
            "htlc_"; /* Harpy Temporary License Certificate */

        ///////////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION && NETWORK && CERTIFICATE_RENEWAL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The prefix string used for the temporary key ring.
        /// </summary>
        public const string TemporaryKeyRingPrefix = /* MAY BE NULL */
            "htkr_"; /* Harpy Temporary Key Ring */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Request Constants
        #region URI Constants
#if NETWORK && WEB
        /// <summary>
        /// The default relative URI used for the default request request.
        /// </summary>
        /* CORE */
        public const string DefaultRequestRelativeUri =
            "certificate/request.cgi?"; /* MAY BE NULL */

        /* CORE */
        // public const string DefaultRequestRelativeUri =
        //     "cgi-bin/request.cgi?"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default relative URI used for the default provision request.
        /// </summary>
        /* CORE */
        public const string DefaultProvisionRelativeUri =
            "service/provision.cgi?"; /* MAY BE NULL */

        /* CORE */
        // public const string DefaultProvisionRelativeUri =
        //     "cgi-bin/provision.cgi?"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default relative URI used for the default test request.
        /// </summary>
        /* CORE */
        public const string DefaultTestRelativeUri =
            "test/page.cgi?"; /* MAY BE NULL */

        /* CORE */
        // public const string DefaultTestRelativeUri =
        //     "cgi-bin/page.cgi?"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
#if NETWORK && WEB
        //
        // NOTE: The optional environment variable containing the license
        //       certificate request URI to use.  This URI "fragment" will
        //       be combined with the configured license certificate request
        //       server and port and then the formatted query string will be
        //       appended to it, verbatim.
        //
        /// <summary>
        /// The name of the request relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string RequestRelativeUriEnvVarName =
            "RequestRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), the
        //       license request URI does not require any relative URI
        //       added to it.  If the "RequestRelativeUri" environment
        //       variable is set, this one is not consulted.
        //
        /// <summary>
        /// The name of the no request relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string NoRequestRelativeUriEnvVarName =
            "NoRequestRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Override any certificate request URI that may be present
        //       in the assembly itself.
        //
        /// <summary>
        /// The name of the request base URI environment variable.
        /// </summary>
        /* CORE */
        public const string RequestBaseUriEnvVarName =
            "RequestBaseUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The optional environment variable containing the license
        //       certificate provisioning URI to use.  This URI "fragment"
        //       will be combined with the configured license certificate
        //       provisioning server and port and then the formatted query
        //       string will be appended to it, verbatim.
        //
        /// <summary>
        /// The name of the provision relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string ProvisionRelativeUriEnvVarName =
            "ProvisionRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), the
        //       license provisioning URI does not require any relative URI
        //       added to it.  If the "ProvisionRelativeUri" environment
        //       variable is set, this one is not consulted.
        //
        /// <summary>
        /// The name of the no provision relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string NoProvisionRelativeUriEnvVarName =
            "NoProvisionRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Override any certificate provisioning URI that may be
        //       present in the assembly itself.
        //
        /// <summary>
        /// The name of the provision base URI environment variable.
        /// </summary>
        /* CORE */
        public const string ProvisionBaseUriEnvVarName =
            "ProvisionBaseUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The optional environment variable containing the test URI
        //       to use.  This URI "fragment" will be combined with the
        //       configured test server and port and then the formatted
        //       query string will be appended to it, verbatim.
        //
        /// <summary>
        /// The name of the test relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string TestRelativeUriEnvVarName =
            "TestRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), the
        //       test URI does not require any relative URI added to it.
        //       If the "TestRelativeUri" environment variable is set,
        //       this one is not consulted.
        //
        /// <summary>
        /// The name of the no test relative URI environment variable.
        /// </summary>
        /* CORE */
        public const string NoTestRelativeUriEnvVarName =
            "NoTestRelativeUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Override any test URI that may be present in the assembly
        //       itself.
        //
        /// <summary>
        /// The name of the test base URI environment variable.
        /// </summary>
        /* CORE */
        public const string TestBaseUriEnvVarName =
            "TestBaseUri"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region URI Type Constants
        /// <summary>
        /// The library URI script.
        /// </summary>
        public const string LibraryUriScript =
            "package require Harpy.Test; " +
            "getRequestLicenseCertificateUri"; /* MAY NOT BE NULL */
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Plugin Constants
#if CERTIFICATE_PLUGIN
#if LICENSING
        //
        // NOTE: This is the string used to request the license certificate of
        //       the default plugin (i.e. as a string returned via a GetString
        //       method call).
        //
        /// <summary>
        /// The certificate string name.
        /// </summary>
        public const string CertificateStringName = "pluginCertificate";
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the substring to search for when attempting to detect
        //       the existing base length for a line of dashes (i.e. as a line
        //       separator).
        //
        /// <summary>
        /// The base length separator.
        /// </summary>
        public static readonly string BaseLengthSeparator =
            Characters.MinusSign.ToString() +
            Characters.MinusSign.ToString(); /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the minimum line length used for the separator lines
        //       in the certificate summary.
        //
        /// <summary>
        /// The minimum summary length.
        /// </summary>
        public const int MinimumSummaryLength = 40;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Used when converting the feature / restriction flags to a
        //       list of string pair descriptions.
        //
        /// <summary>
        /// The with global feature.
        /// </summary>
        public const string WithGlobalFeature = "With Global Feature"; /* MAY BE NULL */
        /// <summary>
        /// The with global restriction.
        /// </summary>
        public const string WithGlobalRestriction = "With Global Restriction"; /* MAY BE NULL */

        /// <summary>
        /// The with feature.
        /// </summary>
        public const string WithFeature = "With Feature"; /* MAY BE NULL */
        /// <summary>
        /// The with restriction.
        /// </summary>
        public const string WithRestriction = "With Restriction"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Badge Plugin Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The pattern string used to match the badge plugin.
        /// </summary>
        /* CORE? */
        public const string BadgePluginPattern = "Badge*"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Key Token Constants
        //
        // NOTE: This is the public key token normally used to sign this
        //       assembly, as a prefixed hexadecimal string value.
        //
        // TODO: *REKEY* Always change this value if the license manager
        //       assembly is going to be signed with a different key.
        //
        /// <summary>
        /// The enterprise public key token string.
        /// </summary>
        /* CORE */
        private const string EnterprisePublicKeyTokenString =
            "0x8bf43b4749e46a0b"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if DEBUG
        //
        // NOTE: This is the public key token normally used to sign this
        //       assembly on build machines, as a prefixed hexadecimal
        //       string value.
        //
        /// <summary>
        /// The build machine public key token string.
        /// </summary>
        /* CORE */
        private const string BuildPublicKeyTokenString =
            "0x645d697a1b3acac5"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the public key token normally used to sign this
        //       assembly, as an array of bytes.
        //
        /// <summary>
        /// The enterprise public key token represented as an array of bytes.
        /// </summary>
        /* CORE */
        public static readonly byte[] EnterprisePublicKeyTokenBytes =
            CertificateDataOps.ParsePublicKeyToken(
                EnterprisePublicKeyTokenString); /* throw */

        ///////////////////////////////////////////////////////////////////////

#if DEBUG
        //
        // NOTE: This is the public key token normally used to sign this
        //       assembly on build machines, as an array of bytes.
        //
        /// <summary>
        /// The build machine public key token represented as an array of
        /// bytes.
        /// </summary>
        /* CORE */
        public static readonly byte[] BuildPublicKeyTokenBytes =
            CertificateDataOps.ParsePublicKeyToken(
                BuildPublicKeyTokenString); /* throw */
#endif

        ///////////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
        //
        // NOTE: This is the public key token normally used to sign demo
        //       license certificates, as a prefixed hexadecimal string
        //       value.
        //
        /// <summary>
        /// The demo public key token string.
        /// </summary>
        /* CORE */
        private const string DemoPublicKeyTokenString =
            "0x5f8230f3e7b9b317"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the public key token normally used to sign demo
        //       license certificates, as an array of bytes.
        //
        /// <summary>
        /// The demo public key token represented as an array of bytes.
        /// </summary>
        /* CORE */
        public static readonly byte[] DemoPublicKeyTokenBytes =
            CertificateDataOps.ParsePublicKeyToken(
                DemoPublicKeyTokenString); /* throw */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Product Name Constants
        //
        // TODO: Add more products here.
        //
        /// <summary>
        /// The products.
        /// </summary>
        /* CORE */
        public static readonly StringDictionary Products =
            new StringDictionary(new string[] {
            "Eagle Community Edition",
            "Eagle Standard Edition",
            "Eagle Enterprise Edition",
            "Licensing SDK for Eagle",
            "Security Plugin for Eagle"
        }, true, false);

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// The suffix string used for the support product.
        /// </summary>
        public const string SupportProductSuffix =
            " with Support"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
#if CERTIFICATE_PLUGIN
        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will *NOT* append any certificate summaries to the
        //       results produced by the plugin "About" methods.
        //
        /// <summary>
        /// The name of the no certificate summary environment variable.
        /// </summary>
        public const string NoCertificateSummaryEnvVarName =
            "NoCertificateSummary"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will return a list of name/value string pairs in
        //       response to the plugin "About" methods; otherwise, the
        //       default (legacy) certificate summary style (i.e. block
        //       of pre-formatted text) will be returned.
        //
        /// <summary>
        /// The name of the certificate summary pairs environment variable.
        /// </summary>
        public const string CertificateSummaryPairsEnvVarName =
            "CertificateSummaryPairs"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will exclude all important restriction flags from
        //       the summary information.
        //
        /// <summary>
        /// The name of the no certificate summary restrictions environment
        /// variable.
        /// </summary>
        public const string NoCertificateSummaryRestrictionsEnvVarName =
            "NoCertificateSummaryRestrictions"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set (to anything), this
        //       class will include all important feature flags in the
        //       summary information.
        //
        /// <summary>
        /// The name of the certificate summary features environment variable.
        /// </summary>
        public const string CertificateSummaryFeaturesEnvVarName =
            "CertificateSummaryFeatures"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
        /// <summary>
        /// The name of the default environment variable.
        /// </summary>
        /* CORE? */
        public static readonly string DefaultEnvVarName =
            typeof(Certificate).Name; /* MAY NOT BE NULL */
#endif
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Policy Constants
#if (XML || (NETWORK && CERTIFICATE_RENEWAL)) && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The format string used to construct the certificate file name.
        /// </summary>
        public const string CertificateFileNameFormat =
            "{0}{1}"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The format string used to construct the hash certificate file
        /// name.
        /// </summary>
        public const string HashCertificateFileNameFormat =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the hash certificate file name only.
        /// </summary>
        public const string HashCertificateFileNameOnlyFormat =
            "{0}{1}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        /// <summary>
        /// The name of the authority URI.
        /// </summary>
        public const string AuthorityUriName = "authority"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The import engine flags.
        /// </summary>
        public const EngineFlags ImportEngineFlags =
            EngineFlags.NoPolicy |
#if TEST
            EngineFlags.SetSecurityProtocol |
#endif
            EngineFlags.None;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the cache flags used to disable caching for
        //       the configuration subsystem.
        //
        /// <summary>
        /// The default cache flags.
        /// </summary>
        /* CORE */
        public const CacheFlags DefaultCacheFlags =
            CacheFlags.Lock | CacheFlags.Reset | CacheFlags.Clear |
            CacheFlags.TypicalMask;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the script flags to use for calls into the
        //       GetData method from within the policy implementation
        //       methods.
        //
        /// <summary>
        /// The default script flags.
        /// </summary>
        public const ScriptFlags DefaultScriptFlags =
#if XML
            ScriptFlags.NoXml |
#endif
            ScriptFlags.PackageRequiredFile;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default script script flags.
        /// </summary>
        public const ScriptFlags DefaultScriptScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default file script flags.
        /// </summary>
        public const ScriptFlags DefaultFileScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default stream script flags.
        /// </summary>
        public const ScriptFlags DefaultStreamScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default license script flags.
        /// </summary>
        public const ScriptFlags DefaultLicenseScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default key pair script flags.
        /// </summary>
        public const ScriptFlags DefaultKeyPairScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default trace script flags.
        /// </summary>
        public const ScriptFlags DefaultTraceScriptFlags =
            DefaultScriptFlags;

        /// <summary>
        /// The default other script flags.
        /// </summary>
        public const ScriptFlags DefaultOtherScriptFlags =
            DefaultScriptFlags;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Resource Constants
        //
        // NOTE: Due to some versions (pre-4.0) of MSBuild being totally
        //       broken when it comes to processing the LogicalName of an
        //       embedded resource that deals with multiple files, create
        //       this fallback resource format that represents the fully
        //       qualified resource names.
        //
        /// <summary>
        /// The format string used for the fallback embedded resource.
        /// </summary>
        /* CORE */
        public const string FallbackEmbeddedResourceFormat =
            "{0}.Resources.Certificates.{1}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When attempting to use embedded resources for a license
        //       certificate for the Harpy assembly itself,
        //
        /// <summary>
        /// The format string used for the this assembly embedded resource.
        /// </summary>
        /* CORE */
        public const string ThisAssemblyEmbeddedResourceFormat =
            "Harpy.certificate{0}"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Warning Constants
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This is the name of the embedded resource that contains the
        //       "warning" header text for license certificates.
        //
        /// <summary>
        /// The warning XML file name.
        /// </summary>
        public static readonly string WarningXmlFileName =
            "warning" + FileExtension.Markup; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The warning txt file name.
        /// </summary>
        public static readonly string WarningTxtFileName =
            "warning" + FileExtension.Text; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Command Constants
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This is the mask used to help determine which metadata
        //       operation is being invoked, to figure out which set of
        //       binding flags to use.
        //
        /// <summary>
        /// The bit mask describing the binding flags property flags.
        /// </summary>
        public const BindingFlags BindingFlagsPropertyMask =
            BindingFlags.GetProperty | BindingFlags.SetProperty;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the binding flags used by both of the "metadata"
        //       reading sub-commands.
        //
        /// <summary>
        /// The get metadata binding flags.
        /// </summary>
        public const BindingFlags GetMetadataBindingFlags =
            BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly |
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.GetProperty;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the binding flags used by both of the "metadata"
        //       writing sub-commands.
        //
        /// <summary>
        /// The set metadata binding flags.
        /// </summary>
        public const BindingFlags SetMetadataBindingFlags =
            BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly |
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.SetProperty;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Path Constants
        //
        // HACK: This is purposely overly strict.  There are many perfectly
        //       valid directory names that will not match this pattern.
        //
        /// <summary>
        /// The regular expression used to match the directory name.
        /// </summary>
        /* CORE */
        public static readonly Regex DirectoryNameRegEx = new Regex(
            "^[0-9A-Z_]+$", RegexOptions.IgnoreCase |
            RegexOptions.Compiled); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This is purposely overly strict.  There are many perfectly
        //       valid file names that will not match this pattern.
        //
        /// <summary>
        /// The regular expression used to match the file name.
        /// </summary>
        /* CORE */
        public static readonly Regex FileNameRegEx = new Regex(
            "^[0-9A-Z_]+(?:\\.[0-9A-Z_]+)*$", RegexOptions.IgnoreCase |
            RegexOptions.Compiled); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The trim separators.
        /// </summary>
        /* CORE */
        public static readonly char[] TrimSeparators = {
            Characters.MinusSign, Characters.Period, Characters.Underscore
        }; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default path flags.
        /// </summary>
        /* CORE */
        public static readonly PathFlags DefaultPathFlags =
            PathFlags.LibExists | PathFlags.Absolute;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default directory name.
        /// </summary>
        /* CORE */
        public static readonly string DefaultDirectoryName =
            typeof(Certificate).Name + "s"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The key ring directory name.
        /// </summary>
        /* CORE? */
        public const string KeyRingDirectoryName =
            "KeyRings"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default file name.
        /// </summary>
        /* CORE */
        public static readonly string DefaultFileName =
            typeof(Certificate).Name + FileExtension.Markup; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The trial file name.
        /// </summary>
        /* CORE */
        public static readonly string TrialFileName = "trial-" +
            DefaultFileName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The internal file name.
        /// </summary>
        /* CORE */
        public static readonly string InternalFileName = "internal-" +
            DefaultFileName; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the assembly file name
        /// (variant 1).
        /// </summary>
        /* CORE */
        public const string AssemblyFileNameFormat1 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the assembly file name
        /// (variant 2).
        /// </summary>
        /* CORE */
        public const string AssemblyFileNameFormat2 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the assembly file name
        /// (variant 3).
        /// </summary>
        /* CORE */
        public const string AssemblyFileNameFormat3 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the package file name (variant
        /// 1).
        /// </summary>
        /* CORE */
        public const string PackageFileNameFormat1 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the package file name (variant
        /// 2).
        /// </summary>
        /* CORE */
        public const string PackageFileNameFormat2 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the package file name (variant
        /// 3).
        /// </summary>
        /* CORE */
        public const string PackageFileNameFormat3 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the default file name (variant
        /// 1).
        /// </summary>
        /* CORE */
        public const string DefaultFileNameFormat1 =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
        /// <summary>
        /// The composite format string used to construct the assembly package
        /// environment variable name.
        /// </summary>
        /* CORE */
        public const string AssemblyPackageEnvVarFormat =
            "{0}{1}{2}{3}{4}{5}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The composite format string used to construct the assembly
        /// environment variable name.
        /// </summary>
        /* CORE */
        public const string AssemblyEnvVarFormat =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The composite format string used to construct the plugin
        /// environment variable name.
        /// </summary>
        /* CORE */
        public const string PluginEnvVarFormat =
            "{0}{1}{2}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The composite format string used to construct the  environment
        /// variable name.
        /// </summary>
        /* CORE */
        public const string EnvVarFormat =
            "{0}{1}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix used when constructing override environment variable
        /// names.
        /// </summary>
        /* CORE */
        public static readonly string OverrideEnvVarPrefix = "Override" +
            Characters.Underscore; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix used when constructing  environment variable names.
        /// </summary>
        /* CORE */
        public static readonly string EnvVarPrefix =
            typeof(Certificate).Name; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The suffix used when constructing  environment variable names.
        /// </summary>
        /* CORE */
        public static readonly string EnvVarSuffix =
            typeof(Certificate).Name; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the name prefix environment variable.
        /// </summary>
        /* CORE */
        public static readonly string NamePrefixEnvVarName = String.Format(
            "{0}NamePrefix", EnvVarPrefix); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the path environment variable.
        /// </summary>
        /* CORE */
        public static readonly string PathEnvVarName = String.Format(
            "{0}Path", EnvVarPrefix); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the no script path environment variable.
        /// </summary>
        /* CORE */
        public const string NoScriptPathEnvVarName =
            "NoScriptPath"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The harpy assembly simple name.
        /// </summary>
        /* CORE */
        public const string HarpyAssemblySimpleName =
            "Harpy"; /* MAY NOT BE NULL */
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Request Constants
        //
        // NOTE: This is the script to evaluate when attempting to acquire a
        //       license certificate automatically.  This is only used when
        //       loading the Harpy plugin itself.
        //
        /// <summary>
        /// The request script.
        /// </summary>
        /* CORE */
        public const string RequestScript =
            "::requestLicenseCertificate"; /* MAY BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Xml Constants
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The encrypted data version name.
        /// </summary>
        public const string EncryptedDataVersionName =
            "EncryptedData"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The encrypted data version value.
        /// </summary>
        public const string EncryptedDataVersionValue =
            "v1.0"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the encrypted data header.
        /// </summary>
        public const string EncryptedDataHeaderFormat =
            "! {0}: {1}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The encrypted data header.
        /// </summary>
        public static readonly string EncryptedDataHeader =
            String.Format(EncryptedDataHeaderFormat, EncryptedDataVersionName,
            EncryptedDataVersionValue); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The encrypted data header represented as an array of bytes.
        /// </summary>
        public static readonly byte[] EncryptedDataHeaderBytes =
            (DefaultEncoding != null) ?
                DefaultEncoding.GetBytes(EncryptedDataHeader) :
                null; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The schema resource name.
        /// </summary>
        /* CORE */
        public const string SchemaResourceName =
            "Harpy.xsd"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The schema namespace name.
        /// </summary>
        /* CORE */
        public const string SchemaNamespaceName =
            "harpy"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The schema namespace URI.
        /// </summary>
        /* CORE */
        public static readonly Uri SchemaNamespaceUri =
            CertificateAssemblyOps.GetXmlSchemaUri(); /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The suffix string used for the sanity check.
        /// </summary>
        /* CORE */
        public const string SanityCheckSuffix =
            " for deserialize sanity check"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        #region Embedded Formatting Constants
#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        //
        // NOTE: This spacing must match what the "sign.eagle" tool uses.
        //
        /// <summary>
        /// The magic spacing.
        /// </summary>
        public static readonly string MagicSpacing =
            Characters.DosNewLine; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These magic strings must match what the "sign.eagle" tool
        //       uses.
        //
        /// <summary>
        /// The begin magic.
        /// </summary>
        public const string BeginMagic =
            "# <<CERTIFICATE-1.0>>"; /* MAY BE NULL */

        /// <summary>
        /// The end magic.
        /// </summary>
        public const string EndMagic =
            "# <</CERTIFICATE-1.0>>"; /* MAY BE NULL */
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Configuration Constants
        //
        // NOTE: These are the normal trust flags used with EvaluateFile
        //       from the CertificateScriptOps class.
        //
        // HACK: These flags may now be modified to include the "Shared"
        //       flag because we must be able to change variables in the
        //       target interpreter on behalf of the [evaluateInSandbox]
        //       and [waitForSandbox] configuration commands.
        //
        /// <summary>
        /// The script trust flags.
        /// </summary>
        /* CORE */
        public const TrustFlags ScriptTrustFlags = TrustFlags.MaybeMarkTrusted;

        /// <summary>
        /// The file trust flags.
        /// </summary>
        /* CORE */
        public const TrustFlags FileTrustFlags = TrustFlags.MaybeTrustedFile;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default trust flags.
        /// </summary>
        /* CORE */
        public const TrustFlags DefaultTrustFlags = FileTrustFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The configuration trust flags.
        /// </summary>
        /* CORE */
        public const TrustFlags ConfigurationTrustFlags = DefaultTrustFlags;

        /// <summary>
        /// The command trust flags.
        /// </summary>
        /* CORE */
        public const TrustFlags CommandTrustFlags = DefaultTrustFlags;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used to help build the fully qualified path of
        //       the directory containing configuration script files.
        //
        /// <summary>
        /// The configurations directory name.
        /// </summary>
        /* CORE */
        public const string ConfigurationsDirectoryName =
            "Configurations"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: These two environment variables allow a (relatively small)
        //       "configuration" script to be specified directly within the
        //       process environment.  If set, the script block read from
        //       the process environment will be evaluated before any other
        //       configuration script.
        //
        /// <summary>
        /// The name of the configuration file name environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameEnvVarName =
            "ConfigurationFileName"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the configuration script text environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationScriptTextEnvVarName =
            "ConfigurationScriptText"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the configuration signature text environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationSignatureTextEnvVarName =
            "ConfigurationSignatureText"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the configuration index (variant 1).
        /// </summary>
        /* CORE */
        public const string ConfigurationIndexFormat1 =
            "{0}{1}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the configuration index (variant 2).
        /// </summary>
        /* CORE */
        public const string ConfigurationIndexFormat2 =
            "{0}{1}_{2}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the configuration index (variant 3).
        /// </summary>
        /* CORE */
        public const string ConfigurationIndexFormat3 =
            "{0}{1}_{2}_{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The configuration minimum index.
        /// </summary>
        /* CORE */
        public const int ConfigurationMinimumIndex = 0;

        /// <summary>
        /// The configuration maximum index.
        /// </summary>
        /* CORE */
        public const int ConfigurationMaximumIndex = 9;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This can be used to override the directory that will
        //       contain the plugin configuration files, if any.
        //
        /// <summary>
        /// The name of the configuration directory environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationDirectoryEnvVarName =
            "ConfigurationDirectory"; /* MAY NOT BE NULL */

        //
        // NOTE: This is a list of manually specified configuration file
        //       names to load.  These configuration files should always
        //       be loaded prior to any other configuration files.
        //
        /// <summary>
        /// The name of the configuration file names environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNamesEnvVarName =
            "ConfigurationFileNames"; /* MAY NOT BE NULL */

        //
        // NOTE: This is a list of manually specified configuration file
        //       (name) patterns.  These configuration files should always
        //       be loaded after those specified by the "ConfigurationFileNames"
        //       environment variable and prior to any remaining configuration
        //       files.
        //
        /// <summary>
        /// The name of the configuration file patterns environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationFilePatternsEnvVarName =
            "ConfigurationFilePatterns"; /* MAY NOT BE NULL */

        //
        // NOTE: This is used to prevent configuration files from being
        //       loaded unless they were explicitly specified via the
        //       "ConfigurationFileNames" environment variable.
        //
        /// <summary>
        /// The name of the configuration override only environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationOverrideOnlyEnvVarName =
            "ConfigurationOverrideOnly"; /* MAY NOT BE NULL */

        //
        // NOTE: This is used to load an additional configuration file
        //       after each one that was (initially) scheduled to be
        //       loaded.
        //
        /// <summary>
        /// The name of the configuration epilogue file name environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationEpilogueFileNameEnvVarName =
            "ConfigurationEpilogueFileName"; /* MAY NOT BE NULL */

        //
        // NOTE: This is used to prevent configuration files from being
        //       skipped even if they have the same name as another one
        //       being loaded.
        //
        /// <summary>
        /// The name of the configuration no unique file names environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationNoUniqueFileNamesEnvVarName =
            "ConfigurationNoUniqueFileNames"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The load all context name.
        /// </summary>
        /* CORE */
        public const string LoadAllContextName =
            "Configuration"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the debugger suffix.
        /// </summary>
        /* CORE */
        public const string DebuggerSuffixFormat = "{0}.Debugger";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the configuration interpreter
        /// variable name (variant 1).
        /// </summary>
        /* CORE */
        public const string ConfigurationVariableFormat1 = "{0}{1}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These constants are used to help build the list
        //       of configuration script file names used prior to
        //       loading the plugin.
        //
        // HACK: The "v1" portion of these constants represents the
        //       version of the "Harpy Configuration Script Protocol"
        //       and may be incremented in the future.
        //
        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 1).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat1 =
            "{0}.v1{1}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 2).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat2 =
            "{0}.v1.{1}{2}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 3).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat3 =
            "{0}.v1.{1}.{2}{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 4).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat4 =
            "{0}.v1.{1}.{2}.{3}{4}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 5).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat5 =
            "{0}.{1}.v1{2}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 6).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat6 =
            "{0}.{1}.v1.{2}{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 7).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat7 =
            "{0}.{1}.v1.{2}.{3}{4}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 8).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat8 =
            "{0}.{1}.v1.{2}.{3}.{4}{5}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 9).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat9 =
            "{0}.v1.{1}{2}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 10).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat10 =
            "{0}.v1.{1}.{2}{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 11).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat11 =
            "{0}.v1.{1}.{2}.{3}{4}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 12).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat12 =
            "{0}.v1.{1}.{2}.{3}.{4}{5}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 13).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat13 =
            "{0}.{1}.v1.{2}{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 14).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat14 =
            "{0}.{1}.v1.{2}.{3}.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 15).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat15 =
            "{0}.{1}.v1.{2}.{3}.{4}{5}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the configuration file name
        /// (variant 16).
        /// </summary>
        /* CORE */
        public const string ConfigurationFileNameFormat16 =
            "{0}.{1}.v1.{2}.{3}.{4}.{5}{6}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix string used for the trace file name.
        /// </summary>
        /* CORE */
        public const string TraceFileNamePrefix =
            "SpecialTrace"; /* MAY BE NULL */

        /// <summary>
        /// The format string used for the trace file with tag name.
        /// </summary>
        /* CORE */
        public const string TraceFileWithTagNameFormat =
            "{0}-{1}-0x{2:X}{3}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the trace file without tag name.
        /// </summary>
        /* CORE */
        public const string TraceFileWithoutTagNameFormat =
            "{0}-0x{2:X}{3}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS && NETWORK && WEB
        /// <summary>
        /// The name of the harpy secret URI environment variable.
        /// </summary>
        public const string HarpySecretUriEnvVarName =
            "HarpySecretUri"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the password URI.
        /// </summary>
        public const string PasswordUriName =
            "password"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the secret URI.
        /// </summary>
        public const string SecretUriName =
            "secret"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the secret entropy.
        /// </summary>
        public const string SecretEntropyFormat = "SecretEntropy{0}";

        /// <summary>
        /// The minimum entropy index.
        /// </summary>
        public const int MinimumEntropyIndex = 1;
        /// <summary>
        /// The maximum entropy index.
        /// </summary>
        public const int MaximumEntropyIndex = 9;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The OK result text.
        /// </summary>
        public const string OkResult = "OK"; /* MAY BE NULL */
        /// <summary>
        /// The error result text.
        /// </summary>
        public const string ErrorResult = "ERROR"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the snippet configuration command.
        /// </summary>
        /* CORE */
        public const string SnippetCommandName =
            "snippet"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the snippet wrong num args.
        /// </summary>
        /* CORE */
        public const string SnippetWrongNumArgsFormat =
            "wrong # args: should be \"{0} add text ?snippetFlags? " +
            "?lookupFlags? ?name? -OR- {0} clear ?snippetFlags? " +
            "?lookupFlags? -OR- {0} dump name ?snippetFlags? " +
            "?lookupFlags? -OR- {0} evaluate name ?snippetFlags? " +
            "?lookupFlags?\""; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require verified configuration command.
        /// </summary>
        /* CORE */
        public const string RequireVerifiedCommandName =
            "requireVerified"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require trusted configuration command.
        /// </summary>
        /* CORE */
        public const string RequireTrustedCommandName =
            "requireTrusted"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the write status configuration command.
        /// </summary>
        /* CORE */
        public const string WriteStatusCommandName =
            "writeStatus"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the write without fail configuration command.
        /// </summary>
        /* CORE */
        public const string WriteWithoutFailCommandName =
            "writeWithoutFail"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if XML
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The name of the set password configuration command.
        /// </summary>
        /* CORE */
        public const string SetPasswordCommandName =
            "setPassword"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if NETWORK && WEB
        /// <summary>
        /// The format string used for the backup prefix.
        /// </summary>
        /* CORE */
        public const string BackupPrefixFormat =
            "backup-{0}-"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the install pass interpreter variable.
        /// </summary>
        /* CORE */
        public const string InstallPassVariableName =
            "installPass"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the source directory interpreter variable.
        /// </summary>
        /* CORE */
        public const string SourceDirectoryVariableName =
            "sourceDirectory"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the target directory interpreter variable.
        /// </summary>
        /* CORE */
        public const string TargetDirectoryVariableName =
            "targetDirectory"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the relative file names interpreter variable.
        /// </summary>
        /* CORE */
        public const string RelativeFileNamesVariableName =
            "relativeFileNames"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the rollback interpreter variable.
        /// </summary>
        /* CORE */
        public const string RollbackVariableName =
            "rollback"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the return code interpreter variable.
        /// </summary>
        /* CORE */
        public const string ReturnCodeVariableName =
            "returnCode"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the error interpreter variable.
        /// </summary>
        /* CORE */
        public const string ErrorVariableName =
            "error"; /* MAY NOT BE NULL */

        /// <summary>
        /// The manifest file name.
        /// </summary>
        /* CORE */
        public const string ManifestFileName =
            "manifest.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The download directory name.
        /// </summary>
        /* CORE */
        public const string DownloadDirectoryName =
            "download"; /* MAY NOT BE NULL */

        /// <summary>
        /// The extract directory name.
        /// </summary>
        /* CORE */
        public const string ExtractDirectoryName =
            "extract"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the download and install configuration command.
        /// </summary>
        /* CORE */
        public const string DownloadAndInstallCommandName =
            "downloadAndInstall"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the extract zip file configuration command.
        /// </summary>
        /* CORE */
        public const string ExtractZipFileCommandName =
            "extractZipFile"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the make URI request configuration command.
        /// </summary>
        /* CORE */
        public const string MakeUriRequestCommandName =
            "makeUriRequest"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the remove license configuration command.
        /// </summary>
        /* CORE */
        public const string RemoveLicenseCommandName =
            "removeLicense"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the request license configuration command.
        /// </summary>
        /* CORE */
        public const string RequestLicenseCommandName =
            "requestLicense"; /* MAY NOT BE NULL */
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The name of the change license property configuration command.
        /// </summary>
        public const string ChangeLicensePropertyCommandName =
            "changeLicenseProperty"; /* MAY NOT BE NULL */

        //
        // HACK: *FORMATTING* Having this all on one line is necessary for
        //       the "syntax.eagle" tool script to work correctly.
        //
        /// <summary>
        /// The format string used for the change license property wrong num
        /// args.
        /// </summary>
        public const string ChangeLicensePropertyWrongNumArgsFormat =
            "wrong # args: should be \"{0} certificateId ?propertyName? " +
            "?propertyValue?\""; /* MAY NOT BE NULL */

        /// <summary>
        /// The change license property names.
        /// </summary>
        public static readonly StringDictionary ChangeLicensePropertyNames =
            new StringDictionary(new string[] { "Features", "Quantity",
            "Restrictions", "Duration", "TimeStamp" },
            true, false); /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// The name of the no sandbox shutdown environment variable.
        /// </summary>
        /* CORE */
        public const string NoSandboxShutdownEnvVarName =
            "NoSandboxShutdown"; /* MAY NOT BE NULL */

#if WINFORMS
        /// <summary>
        /// The name of the no sandbox status environment variable.
        /// </summary>
        /* CORE */
        public const string NoSandboxStatusEnvVarName =
            "NoSandboxStatus"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the sandbox name.
        /// </summary>
        /* CORE */
        public const string SandboxNameFormat =
            "{0} ==> [{1}, {2}, {3}, {4}, {5}]"; /* MAY NOT BE NULL */

        /// <summary>
        /// The status message text used for the sandbox activate
        /// notification.
        /// </summary>
        /* CORE */
        public const string SandboxActivateStatusText =
            "Activated {1}configuration sandbox for file: {0}{2}"; /* MAY BE NULL */

        /// <summary>
        /// The status message text used for the sandbox refresh notification.
        /// </summary>
        /* CORE */
        public const string SandboxRefreshStatusText =
            "Refreshed {1}configuration sandbox for file: {0}{2}"; /* MAY BE NULL */

        /// <summary>
        /// The status message text used for the sandbox shutdown
        /// notification.
        /// </summary>
        /* CORE */
        public const string SandboxShutdownStatusText =
            "Idle shutdown for {1}configuration sandbox will check for " +
            "activity every {0} seconds.{2}"; /* MAY BE NULL */

        /// <summary>
        /// The timeout, in milliseconds, used for the sandbox report status.
        /// </summary>
        /* CORE */
        public const int SandboxReportStatusTimeout = 200;

        /// <summary>
        /// The sleep interval, in milliseconds, used for the sandbox report
        /// status.
        /// </summary>
        /* CORE */
        public const int SandboxReportStatusSleep = 1000;
#endif

        /// <summary>
        /// The timeout, in milliseconds, used for the evaluate in sandbox.
        /// </summary>
        /* CORE */
        public const int EvaluateInSandboxTimeout = 10000;

        /// <summary>
        /// The timeout, in milliseconds, used for the shutdown sandbox.
        /// </summary>
        /* CORE */
        public const int ShutdownSandboxTimeout = 60000;

        /// <summary>
        /// The timeout, in milliseconds, used for the sandbox variable.
        /// </summary>
        /* CORE */
        public const int SandboxVariableTimeout = 2000;

        /// <summary>
        /// The name of the cleanup for sandbox configuration command.
        /// </summary>
        /* CORE */
        public const string CleanupForSandboxCommandName =
            "cleanupForSandbox"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate in sandbox configuration command.
        /// </summary>
        /* CORE */
        public const string EvaluateInSandboxCommandName =
            "evaluateInSandbox"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the wait for sandbox configuration command.
        /// </summary>
        /* CORE */
        public const string WaitForSandboxCommandName =
            "waitForSandbox"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the sandbox wait var name.
        /// </summary>
        /* CORE */
        public const string SandboxWaitVarNameFormat =
            "{0}({1},wait)"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the sandbox return code var name.
        /// </summary>
        /* CORE */
        public const string SandboxReturnCodeVarNameFormat =
            "{0}({1},code)"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the sandbox result var name.
        /// </summary>
        /* CORE */
        public const string SandboxResultVarNameFormat =
            "{0}({1},result)"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if WEB
        /// <summary>
        /// The request URI flags.
        /// </summary>
        /* CORE */
        public const UriFlags RequestUriFlags =
            UriFlags.SecureOnlyMask | UriFlags.NoHost;

        /// <summary>
        /// The request URI components.
        /// </summary>
        /* CORE */
        public const UriComponents RequestUriComponents =
            UriComponents.AbsoluteUri;
#endif

        ///////////////////////////////////////////////////////////////////////

#if NETWORK && WEB
        /// <summary>
        /// The get method.
        /// </summary>
        /* CORE */
        public const string GetMethod = "GET";

        /// <summary>
        /// The post method.
        /// </summary>
        /* CORE */
        public const string PostMethod = "POST";

        /// <summary>
        /// The regular expression used to match the var name.
        /// </summary>
        /* CORE */
        public static readonly Regex VarNameRegEx = new Regex(
            "^[A-Z_][0-9A-Z_]*$", RegexOptions.IgnoreCase |
            RegexOptions.Compiled); /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// The transient callbacks type name.
        /// </summary>
        /* CORE */
        public static readonly string TransientCallbacksTypeName =
            typeof(Components.Private.Commands.Callbacks).FullName;

#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        /// <summary>
        /// The pattern string used to match the transient command name.
        /// </summary>
        /* CORE */
        public static readonly string TransientCommandNamePattern = String.Format(
            "{0}{1}{2}", typeof(Transient.Commands.WriteWithoutFail).Namespace,
            Type.Delimiter, Characters.Asterisk);
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The exist variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags ExistVariableFlags =
            VariableFlags.GlobalOnly;

        /// <summary>
        /// The context exist variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags ContextExistVariableFlags =
            ExistVariableFlags;

        /// <summary>
        /// The get variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags GetVariableFlags =
            VariableFlags.GlobalOnly;

        /// <summary>
        /// The context get variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags ContextGetVariableFlags =
            GetVariableFlags | VariableFlags.NoComplain;

        /// <summary>
        /// The command get variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags CommandGetVariableFlags =
            GetVariableFlags & ~VariableFlags.GlobalOnly;

        /// <summary>
        /// The set variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags SetVariableFlags =
            VariableFlags.GlobalOnly;

        /// <summary>
        /// The context set variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags ContextSetVariableFlags =
            SetVariableFlags;

        /// <summary>
        /// The command set variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags CommandSetVariableFlags =
            SetVariableFlags & ~VariableFlags.GlobalOnly;

        /// <summary>
        /// The unset variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags UnsetVariableFlags =
            VariableFlags.GlobalOnly | VariableFlags.NoComplain;

        /// <summary>
        /// The context unset variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags ContextUnsetVariableFlags =
            UnsetVariableFlags;

        /// <summary>
        /// The command unset variable flags.
        /// </summary>
        /* CORE */
        public const VariableFlags CommandUnsetVariableFlags =
            UnsetVariableFlags & ~VariableFlags.GlobalOnly;

        ///////////////////////////////////////////////////////////////////////

#if NETWORK
        /// <summary>
        /// The name of the plugin offline mode interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginOfflineModeVariableName =
            "pluginOfflineMode"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the offline mode configuration command.
        /// </summary>
        /* CORE */
        public const string OfflineModeCommandName =
            "offlineMode"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if WEB
        /// <summary>
        /// The name of the join URI parts configuration command.
        /// </summary>
        /* CORE */
        public const string JoinUriPartsCommandName =
            "joinUriParts"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the apply context variables configuration command.
        /// </summary>
        /* CORE */
        public const string ApplyContextVariablesCommandName =
            "applyContextVariables"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the enable extract and apply configuration command.
        /// </summary>
        /* CORE */
        public const string EnableExtractAndApplyCommandName =
            "enableExtractAndApply"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate with cleanup configuration command.
        /// </summary>
        /* CORE */
        public const string EvaluateWithCleanupCommandName =
            "evaluateWithCleanup"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate without error configuration command.
        /// </summary>
        /* CORE */
        public const string EvaluateWithoutErrorCommandName =
            "evaluateWithoutError"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate with scope configuration command.
        /// </summary>
        /* CORE */
        public const string EvaluateWithScopeCommandName =
            "evaluateWithScope"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the fail on error configuration command.
        /// </summary>
        /* CORE */
        public const string FailOnErrorCommandName =
            "failOnError"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the fatal error configuration command.
        /// </summary>
        /* CORE */
        public const string FatalErrorCommandName =
            "fatalError"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the for each file configuration command.
        /// </summary>
        /* CORE */
        public const string ForEachFileCommandName =
            "forEachFile"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have component configuration command.
        /// </summary>
        /* CORE */
        public const string HaveComponentCommandName =
            "haveComponent"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have identifier configuration command.
        /// </summary>
        /* CORE */
        public const string HaveIdentifierCommandName =
            "haveIdentifier"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have environment configuration command.
        /// </summary>
        /* CORE */
        public const string HaveEnvironmentCommandName =
            "haveEnvironment"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have configuration configuration command.
        /// </summary>
        /* CORE */
        public const string HaveConfigurationCommandName =
            "haveConfiguration"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the is debugger present or attached configuration
        /// command.
        /// </summary>
        /* CORE */
        public const string IsDebuggerPresentOrAttachedCommandName =
            "isDebuggerPresentOrAttached"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the is restricted interpreter configuration command.
        /// </summary>
        /* CORE */
        public const string IsRestrictedInterpreterCommandName =
            "isRestrictedInterpreter"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the issue ticket configuration command.
        /// </summary>
        /* CORE */
        public const string IssueTicketCommandName =
            "issueTicket"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the redeem ticket configuration command.
        /// </summary>
        /* CORE */
        public const string RedeemTicketCommandName =
            "redeemTicket"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the match platform configuration command.
        /// </summary>
        /* CORE */
        public const string MatchPlatformCommandName =
            "matchPlatform"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the join sub paths configuration command.
        /// </summary>
        /* CORE */
        public const string JoinSubPathsCommandName =
            "joinSubPaths"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key usage configuration command.
        /// </summary>
        /* CORE */
        public const string KeyUsageCommandName =
            "keyUsage"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the key usage wrong num args.
        /// </summary>
        /* CORE */
        public const string KeyUsageWrongNumArgsFormat =
            "wrong # args: should be \"{0} change entityType " +
            "?flags? ?all? ?root? -OR- {0} clear ?entityType? " +
            "-OR- {0} default entityType -OR- {0} forbid " +
            "entityType -OR- {0} get entityType -OR- {0} list " +
            "-OR- {0} modify entityType ?flags? ?all? ?root? " +
            "-OR- {0} resolve entityType\""; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the list components configuration command.
        /// </summary>
        /* CORE */
        public const string ListComponentsCommandName =
            "listComponents"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the list context variables configuration command.
        /// </summary>
        /* CORE */
        public const string ListContextVariablesCommandName =
            "listContextVariables"; /* MAY NOT BE NULL */

        /// <summary>
        /// The error message text used for the nothing done.
        /// </summary>
        /* CORE */
        public const string NothingDoneError =
            "unexpected error: nothing was done"; /* MAY BE NULL */

        /// <summary>
        /// The name of the maybe record result configuration command.
        /// </summary>
        /* CORE */
        public const string MaybeRecordResultCommandName =
            "maybeRecordResult"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the maybe evaluate configuration command.
        /// </summary>
        /* CORE */
        public const string MaybeEvaluateCommandName =
            "maybeEvaluate"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the maybe iterate using expression configuration
        /// command.
        /// </summary>
        /* CORE */
        public const string MaybeIterateUsingExpressionCommandName =
            "maybeIterateUsingExpression"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the iteration count interpreter variable.
        /// </summary>
        /* CORE */
        public const string IterationCountVariableName =
            "iterationCount"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the return back now configuration command.
        /// </summary>
        /* CORE */
        public const string ReturnBackNowCommandName =
            "returnBackNow"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the break out now configuration command.
        /// </summary>
        /* CORE */
        public const string BreakOutNowCommandName =
            "breakOutNow"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the change duration configuration command.
        /// </summary>
        /* CORE */
        public const string ChangeDurationCommandName =
            "changeDuration"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the change version range configuration command.
        /// </summary>
        /* CORE */
        public const string ChangeVersionRangeCommandName =
            "changeVersionRange"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin duration
        /// interpreter variable name.
        /// </summary>
        /* CORE */
        public const string PluginDurationVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin duration interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginDurationVariableName =
            "pluginDuration"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin version range
        /// interpreter variable name.
        /// </summary>
        /* CORE */
        public const string PluginVersionRangeVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin version range interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginVersionRangeVariableName =
            "pluginVersionRange"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the continue with now configuration command.
        /// </summary>
        /* CORE */
        public const string ContinueWithNowCommandName =
            "continueWithNow"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the not after configuration command.
        /// </summary>
        /* CORE */
        public const string NotAfterCommandName =
            "notAfter"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the not before configuration command.
        /// </summary>
        /* CORE */
        public const string NotBeforeCommandName =
            "notBefore"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the peek on flags configuration command.
        /// </summary>
        /* CORE */
        public const string PeekOnFlagsCommandName =
            "peekOnFlags"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the queue script configuration command.
        /// </summary>
        /* CORE */
        public const string QueueScriptCommandName =
            "queueScript"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the read script configuration command.
        /// </summary>
        /* CORE */
        public const string ReadScriptCommandName =
            "readScript"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the refresh context variables configuration command.
        /// </summary>
        /* CORE */
        public const string RefreshContextVariablesCommandName =
            "refreshContextVariables"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the request license URI.
        /// </summary>
        /* CORE */
        public const string RequestLicenseUriName =
            "license"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the provision URI.
        /// </summary>
        /* CORE */
        public const string ProvisionUriName = "provision";

        /// <summary>
        /// The name of the test URI.
        /// </summary>
        /* CORE */
        public const string TestUriName = "test";

        /// <summary>
        /// The request license encrypted.
        /// </summary>
        /* CORE */
        public const bool RequestLicenseEncrypted = true;

        /// <summary>
        /// The provision license encrypted.
        /// </summary>
        /* CORE */
        public const bool ProvisionLicenseEncrypted = false;

        /// <summary>
        /// The name of the require identifier configuration command.
        /// </summary>
        /* CORE */
        public const string RequireIdentifierCommandName =
            "requireIdentifier"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require component configuration command.
        /// </summary>
        /* CORE */
        public const string RequireComponentCommandName =
            "requireComponent"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require network configuration command.
        /// </summary>
        /* CORE */
        public const string RequireNetworkCommandName =
            "requireNetwork"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require license configuration command.
        /// </summary>
        /* CORE */
        public const string RequireLicenseCommandName =
            "requireLicense"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require tracing configuration command.
        /// </summary>
        /* CORE */
        public const string RequireTracingCommandName =
            "requireTracing"; /* MAY NOT BE NULL */

        /// <summary>
        /// The context object flags.
        /// </summary>
        /* CORE */
        public const ObjectFlags ContextObjectFlags =
            ObjectFlags.None;

        /// <summary>
        /// The name of the have variable configuration command.
        /// </summary>
        /* CORE */
        public const string HaveVariableCommandName =
            "haveVariable"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require variable configuration command.
        /// </summary>
        /* CORE */
        public const string RequireVariableCommandName =
            "requireVariable"; /* MAY NOT BE NULL */

        /// <summary>
        /// The empty version.
        /// </summary>
        /* CORE */
        public static readonly Version EmptyVersion =
            new Version(0, 0, 0, 0); /* MAY BE NULL */

        /// <summary>
        /// The name of the require version configuration command.
        /// </summary>
        /* CORE */
        public const string RequireVersionCommandName =
            "requireVersion"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the restore context variables configuration command.
        /// </summary>
        /* CORE */
        public const string RestoreContextVariablesCommandName =
            "restoreContextVariables"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the save context variables configuration command.
        /// </summary>
        /* CORE */
        public const string SaveContextVariablesCommandName =
            "saveContextVariables"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the set variable configuration command.
        /// </summary>
        /* CORE */
        public const string SetVariableCommandName =
            "setVariable"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the configure packages configuration command.
        /// </summary>
        /* CORE */
        public const string ConfigurePackagesCommandName =
            "configurePackages"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the configure rule set configuration command.
        /// </summary>
        /* CORE */
        public const string ConfigureRuleSetCommandName =
            "configureRuleSet"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the query status configuration command.
        /// </summary>
        /* CORE */
        public const string QueryStatusCommandName =
            "queryStatus"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require data configuration command.
        /// </summary>
        /* CORE */
        public const string RequireDataCommandName =
            "requireData"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the set data configuration command.
        /// </summary>
        /* CORE */
        public const string SetDataCommandName =
            "setData"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require administrator configuration command.
        /// </summary>
        /* CORE */
        public const string RequireAdministratorCommandName =
            "requireAdministrator"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require environment configuration command.
        /// </summary>
        /* CORE */
        public const string RequireEnvironmentCommandName =
            "requireEnvironment"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require process configuration command.
        /// </summary>
        /* CORE */
        public const string RequireProcessCommandName =
            "requireProcess"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the set environment configuration command.
        /// </summary>
        /* CORE */
        public const string SetEnvironmentCommandName =
            "setEnvironment"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the set configuration configuration command.
        /// </summary>
        /* CORE */
        public const string SetConfigurationCommandName =
            "setConfiguration"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the unset variable configuration command.
        /// </summary>
        /* CORE */
        public const string UnsetVariableCommandName =
            "unsetVariable"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the skip license configuration command.
        /// </summary>
        /* CORE */
        public const string SkipLicenseCommandName =
            "skipLicense"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the storage type configuration command.
        /// </summary>
        /* CORE */
        public const string StorageTypeCommandName =
            "storageType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the change time servers configuration command.
        /// </summary>
        /* CORE */
        public const string ChangeTimeServersCommandName =
            "changeTimeServers"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the swap commands configuration command.
        /// </summary>
        /* CORE */
        public const string SwapCommandsCommandName =
            "swapCommands"; /* MAY NOT BE NULL */

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The name of the demo mode configuration command.
        /// </summary>
        /* CORE */
        public const string DemoModeCommandName =
            "demoMode"; /* MAY NOT BE NULL */
#endif

        /// <summary>
        /// The name of the fail safe mode configuration command.
        /// </summary>
        /* CORE */
        public const string FailSafeModeCommandName =
            "failSafeMode"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the test mode configuration command.
        /// </summary>
        /* CORE */
        public const string TestModeCommandName =
            "testMode"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin skip license
        /// interpreter variable name.
        /// </summary>
        /* CORE */
        public const string PluginSkipLicenseVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin time servers interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginTimeServersVariableName =
            "pluginTimeServers"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin skip license interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginSkipLicenseVariableName =
            "pluginSkipLicense"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the enabled configuration element.
        /// </summary>
        /* CORE */
        public const string EnabledElementName =
            "enabled"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the types configuration element.
        /// </summary>
        /* CORE */
        public const string TypesElementName =
            "types"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin force network
        /// interpreter variable name.
        /// </summary>
        /* CORE */
        public const string PluginForceNetworkVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin force network interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginForceNetworkVariableName =
            "pluginForceNetwork"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin storage type interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginStorageTypeVariableName =
            "pluginStorageType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin SDK mode interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginSdkModeVariableName =
            "pluginSdkMode"; /* MAY NOT BE NULL */

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The name of the plugin demo mode interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginDemoModeVariableName =
            "pluginDemoMode"; /* MAY NOT BE NULL */
#endif

        /// <summary>
        /// The name of the plugin test mode interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginTestModeVariableName =
            "pluginTestMode"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin fail safe mode interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginFailSafeModeVariableName =
            "pluginFailSafeMode"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin must have security interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginMustHaveSecurityVariableName =
            "pluginMustHaveSecurity"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin public key token interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginPublicKeyTokenVariableName =
            "pluginPublicKeyToken"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the changed interpreter
        /// variable name.
        /// </summary>
        /* CORE */
        public const string ChangedVariableFormat =
            "changed({0})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the save state interpreter
        /// variable name.
        /// </summary>
        /* CORE */
        public const string SaveStateVariableFormat =
            "saveState({0})"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum number of milliseconds to wait for
        //       the static (interpreter) lock, when loading key rings,
        //       etc.
        //
        /// <summary>
        /// The timeout, in milliseconds, used for the interpreter create
        /// lock.
        /// </summary>
        /* CORE */
        public const int InterpreterCreateLockTimeout = 60000;

        ///////////////////////////////////////////////////////////////////////

#if SHELL
        /// <summary>
        /// The name of the no announce interactive loop environment variable.
        /// </summary>
        /* CORE */
        public const string NoAnnounceInteractiveLoopEnvVarName =
            "NoAnnounceInteractiveLoop"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the interactive loop configuration command.
        /// </summary>
        /* CORE */
        public const string InteractiveLoopCommandName =
            "interactiveLoop"; /* MAY NOT BE NULL */

        /// <summary>
        /// The interactive loop name.
        /// </summary>
        /* CORE */
        public const string InteractiveLoopName =
            "Harpy Configuration"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// The name of the plugin shell flags interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginShellFlagsVariableName =
            "pluginShellFlags"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require shell configuration command.
        /// </summary>
        /* CORE? */
        public const string RequireShellCommandName =
            "requireShell"; /* MAY NOT BE NULL */
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        /// <summary>
        /// The name of the change features configuration command.
        /// </summary>
        /* CORE */
        public const string ChangeFeaturesCommandName =
            "changeFeatures"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have features configuration command.
        /// </summary>
        /* CORE */
        public const string HaveFeaturesCommandName =
            "haveFeatures"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require features configuration command.
        /// </summary>
        /* CORE */
        public const string RequireFeaturesCommandName =
            "requireFeatures"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin features interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginFeaturesVariableName =
            "pluginFeatures"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The name of the script flags property.
        /// </summary>
        /* CORE? */
        public const string ScriptFlagsPropertyName = "ScriptFlags";

        /// <summary>
        /// The name of the path flags property.
        /// </summary>
        /* CORE? */
        public const string PathFlagsPropertyName = "PathFlags";

        /// <summary>
        /// The name of the network flags property.
        /// </summary>
        /* CORE? */
        public const string NetworkFlagsPropertyName = "NetworkFlags";

        /// <summary>
        /// The name of the key name property.
        /// </summary>
        /* CORE? */
        public const string KeyNamePropertyName = "KeyName";

        /// <summary>
        /// The name of the key ring name property.
        /// </summary>
        /* CORE? */
        public const string KeyRingNamePropertyName = "KeyRingName";

        /// <summary>
        /// The name of the current policy property.
        /// </summary>
        /* CORE? */
        public const string CurrentPolicyPropertyName = "CurrentPolicy";
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // HACK: For now, allow only the global license execution policy
        //       to be changed via configuration subsystem commands.
        //       Perhaps changing the local license execution policy can
        //       be allowed in the future; however, since it defaults to
        //       being undefined (i.e. and thus falls back to the global
        //       license execution policy anyhow), that seems a lot less
        //       useful.
        //
        // HACK: *UPDATE* The above comment now (also) applies to various
        //       local policy property values and their associated global
        //       policy property values.
        //
        /// <summary>
        /// The change local policies.
        /// </summary>
        /* CORE? */
        public const bool ChangeLocalPolicies = false;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the script public key token interpreter variable.
        /// </summary>
        /* CORE? */
        public const string ScriptPublicKeyTokenVariableName =
            "scriptPublicKeyToken"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the script key pairs interpreter variable.
        /// </summary>
        /* CORE? */
        public const string ScriptKeyPairsVariableName =
            "scriptKeyPairs"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the enable security configuration command.
        /// </summary>
        /* CORE? */
        public const string EnableSecurityCommandName =
            "enableSecurity"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate file configuration command.
        /// </summary>
        /* CORE? */
        public const string EvaluateFileCommandName =
            "evaluateFile"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the evaluate stream configuration command.
        /// </summary>
        /* CORE? */
        public const string EvaluateStreamCommandName =
            "evaluateStream"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the add certificates configuration command.
        /// </summary>
        /* CORE? */
        public const string AddCertificatesCommandName =
            "addCertificates"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the add public key configuration command.
        /// </summary>
        /* CORE? */
        public const string AddPublicKeyCommandName =
            "addPublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the add ring public key configuration command.
        /// </summary>
        /* CORE? */
        public const string AddRingPublicKeyCommandName =
            "addRingPublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have public key configuration command.
        /// </summary>
        /* CORE? */
        public const string HavePublicKeyCommandName =
            "havePublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the have ring public key configuration command.
        /// </summary>
        /* CORE? */
        public const string HaveRingPublicKeyCommandName =
            "haveRingPublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require public key configuration command.
        /// </summary>
        /* CORE? */
        public const string RequirePublicKeyCommandName =
            "requirePublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the for each policy type configuration command.
        /// </summary>
        /* CORE? */
        public const string ForEachPolicyTypeCommandName =
            "forEachPolicyType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the remove public key configuration command.
        /// </summary>
        /* CORE? */
        public const string RemovePublicKeyCommandName =
            "removePublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the remove ring public key configuration command.
        /// </summary>
        /* CORE? */
        public const string RemoveRingPublicKeyCommandName =
            "removeRingPublicKey"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the require machine configuration command.
        /// </summary>
        /* CORE? */
        public const string RequireMachineCommandName =
            "requireMachine"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the change path flags configuration command.
        /// </summary>
        /* CORE? */
        public const string ChangePathFlagsCommandName =
            "changePathFlags"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the change network flags configuration command.
        /// </summary>
        /* CORE? */
        public const string ChangeNetworkFlagsCommandName =
            "changeNetworkFlags"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin machine interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginMachineVariableName =
            "pluginMachine"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin path flags
        /// interpreter variable name.
        /// </summary>
        /* CORE? */
        public const string PluginPathFlagsVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin path flags interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginPathFlagsVariableName =
            "pluginPathFlags"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin network flags
        /// interpreter variable name.
        /// </summary>
        /* CORE? */
        public const string PluginNetworkFlagsVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin network flags interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginNetworkFlagsVariableName =
            "pluginNetworkFlags"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the change license policy configuration command.
        /// </summary>
        /* CORE? */
        public const string ChangeLicensePolicyCommandName =
            "changeLicensePolicy"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin license policy interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginLicensePolicyVariableName =
            "pluginLicensePolicy"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to construct the interpreter creation
        /// interpreter variable name.
        /// </summary>
        /* CORE? */
        public const string InterpreterCreationVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the interpreter creation interpreter variable.
        /// </summary>
        /* CORE? */
        public const string InterpreterCreationVariableName =
            "interpreterCreation"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the persistent configuration element.
        /// </summary>
        /* CORE? */
        public const string PersistentElementName =
            "persistent"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the change policy property configuration command.
        /// </summary>
        /* CORE? */
        public const string ChangePolicyPropertyCommandName =
            "changePolicyProperty"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin policy property interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginPolicyPropertyVariableName =
            "pluginPolicyProperty"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the disable interpreter creation configuration
        /// command.
        /// </summary>
        /* CORE? */
        public const string DisableInterpreterCreationCommandName =
            "disableInterpreterCreation"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the enable interpreter creation configuration command.
        /// </summary>
        /* CORE? */
        public const string EnableInterpreterCreationCommandName =
            "enableInterpreterCreation"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the enable local policy configuration command.
        /// </summary>
        /* CORE? */
        public const string EnableLocalPolicyCommandName =
            "enableLocalPolicy"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the plugin change count interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginChangeCountVariableName =
            "pluginChangeCount"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The verify engine flags.
        /// </summary>
        /* CORE */
        public const EngineFlags VerifyEngineFlags =
            EngineFlags.NoPolicy |
#if TEST
            EngineFlags.SetSecurityProtocol |
#endif
            EngineFlags.None;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The read engine flags.
        /// </summary>
        /* CORE */
        public const EngineFlags ReadEngineFlags =
            VerifyEngineFlags |
#if XML
            EngineFlags.NoXml |
#endif
            EngineFlags.None;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the name value pair.
        /// </summary>
        /* CORE */
        public const string NameValuePairFormat =
            "{0}{1}{2}{1}{3}{1}{4}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script type interpreter variable.
        /// </summary>
        /* CORE */
        public const string ScriptTypeVariableName =
            "scriptType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script type unknown.
        /// </summary>
        /* CORE */
        public const string ScriptTypeUnknown =
            "UNKNOWN"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script type unsigned.
        /// </summary>
        /* CORE */
        public const string ScriptTypeUnsigned =
            "UNSIGNED"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script type signed.
        /// </summary>
        /* CORE */
        public const string ScriptTypeSigned =
            "SIGNED"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script type trusted.
        /// </summary>
        /* CORE */
        public const string ScriptTypeTrusted =
            "TRUSTED"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script sub type interpreter variable.
        /// </summary>
        /* CORE */
        public const string ScriptSubTypeVariableName =
            "scriptSubType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script sub type text.
        /// </summary>
        /* CORE */
        public const string ScriptSubTypeText =
            "text"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script sub type file.
        /// </summary>
        /* CORE */
        public const string ScriptSubTypeFile =
            "file"; /* MAY NOT BE NULL */

        /// <summary>
        /// The script sub type resource.
        /// </summary>
        /* CORE */
        public const string ScriptSubTypeResource =
            "resource"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script directory interpreter variable.
        /// </summary>
        /* CORE */
        public const string ScriptDirectoryVariableName =
            "scriptDirectory"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script file ID interpreter variable.
        /// </summary>
        /* CORE */
        public const string ScriptFileIdVariableName =
            "scriptFileId"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script file name interpreter variable.
        /// </summary>
        /* CORE */
        public const string ScriptFileNameVariableName =
            "scriptFileName"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin type interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginTypeVariableName =
            "pluginType"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin context interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginContextVariableName =
            "pluginContext"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin configuration phase interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginConfigurationPhaseVariableName =
            "pluginConfigurationPhase"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin variant interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginVariantVariableName =
            "pluginVariant"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the plugin isolated interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginIsolatedVariableName =
            "pluginIsolated"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the status time servers.
        /// </summary>
        /* CORE */
        public const string StatusTimeServersFormat =
            "i{0}: {1}Plugin {2} {3}time servers are now " +
            "{4} via {5} configuration {6} {7} ({8}) with " +
            "a change count of {9}."; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// The format string used for the status shell flags.
        /// </summary>
        /* CORE */
        public const string StatusShellFlagsFormat =
            "i{0}: {1}Plugin {2} {3}shell flags are now " +
            "{4} via {5} configuration {6} {7} ({8}) with " +
            "a change count of {9}."; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        /// <summary>
        /// The format string used for the status extra features.
        /// </summary>
        /* CORE */
        public const string StatusExtraFeaturesFormat =
            "i{0}: {1}Plugin {2} {3}extra features are now " +
            "{4} via {5} configuration {6} {7} ({8}) with " +
            "a change count of {9}."; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The format string used for the status policy key ring.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyKeyRingFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy key ring is now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the status policy execution flags.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyExecutionFlagsFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy execution flags are now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the status policy key name.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyKeyNameFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy key name is now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the status policy key ring name.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyKeyRingNameFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy key ring name is now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the status policy script flags name.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyScriptFlagsNameFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy script flags are now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the status policy path flags name.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyPathFlagsNameFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy path flags are now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used for the status policy network flags name.
        /// </summary>
        /* CORE? */
        public const string StatusPolicyNetworkFlagsNameFormat =
            "i{0}: {1}Plugin {2} {3} {4}policy network flags are now " +
            "{5} via {6} configuration {7} {8} ({9}) with a change " +
            "count of {10}."; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// The name of the plugin count interpreter variable.
        /// </summary>
        /* CORE */
        public const string PluginCountVariableName =
            "pluginCount"; /* MAY NOT BE NULL */

#if CERTIFICATE_POLICY
        /// <summary>
        /// The name of the plugin pending interpreter variable.
        /// </summary>
        /* CORE? */
        public const string PluginPendingVariableName =
            "pluginPending"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the plugin pending interpreter
        /// variable name.
        /// </summary>
        /* CORE? */
        public const string PluginPendingVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the any key ring configuration element.
        /// </summary>
        /* CORE? */
        public const string AnyKeyRingElementName =
            "anyKeyRing"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key ring configuration element.
        /// </summary>
        /* CORE? */
        public const string KeyRingElementName =
            "keyRing"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the license key ring configuration element.
        /// </summary>
        /* CORE? */
        public const string LicenseKeyRingElementName =
            "licenseKeyRing"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the license configuration element.
        /// </summary>
        /* CORE? */
        public const string LicenseElementName =
            "license"; /* MAY NOT BE NULL */

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The name of the demo license configuration element.
        /// </summary>
        /* CORE? */
        public const string DemoLicenseElementName =
            "demoLicense"; /* MAY NOT BE NULL */
#endif

#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// The name of the renewal configuration element.
        /// </summary>
        /* CORE? */
        public const string RenewalElementName =
            "renewal"; /* MAY NOT BE NULL */
#endif
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants
        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will be disabled.
        //
        /// <summary>
        /// The name of the no configuration environment variable.
        /// </summary>
        /* CORE */
        public const string NoConfigurationEnvVarName =
            "NoConfiguration"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the value
        //       will be used to alter the flags used by the [notAfter] and
        //       [notBefore] configuration commands.
        //
        /// <summary>
        /// The name of the not command flags environment variable.
        /// </summary>
        /* CORE */
        public const string NotCommandFlagsEnvVarName =
            "NotCommandFlags"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will only consider configuration
        //       scripts specified via the process environment.
        //
        /// <summary>
        /// The name of the configuration environment only environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationEnvironmentOnlyEnvVarName =
            "ConfigurationEnvironmentOnly"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will not load embedded resources
        //       containing configuration file data.
        //
        /// <summary>
        /// The name of the configuration skip embedded environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationSkipEmbeddedEnvVarName =
            "ConfigurationSkipEmbedded"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will stop (and fail) upon getting
        //       a script error.
        //
        /// <summary>
        /// The name of the configuration fail on error environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationFailOnErrorEnvVarName =
            "ConfigurationFailOnError"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], any
        //       preexisting commands in the configuration interpreter
        //       with a name that matches a configuration command will
        //       be automatically removed.  These removed commands will
        //       NOT be readded to the interpreter after configuration
        //       is completed.
        //
        /// <summary>
        /// The name of the configuration force commands environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationForceCommandsEnvVarName =
            "ConfigurationForceCommands"; /* MAY NOT BE NULL */

        //
        // HACK: If this environment variable is set [to anything], any
        //       preexisting commands in the configuration interpreter
        //       will be temporarily removed.  These removed commands
        //       will be readded to the interpreter after configuration
        //       is completed.
        //
        /// <summary>
        /// The name of the configuration swap commands environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationSwapCommandsEnvVarName =
            "ConfigurationSwapCommands"; /* MAY NOT BE NULL */

        //
        // NOTE: If this annotation is present within a configuration
        //       (script) file, the existing set of commands for the
        //       interpreter will be temporarily swapped out while
        //       that configuration file is being evaluated.  This
        //       means that only the set of Harpy configuration
        //       commands will be available.
        //
        /// <summary>
        /// The configuration annotation token for swap commands.
        /// </summary>
        /* CORE */
        public const string SwapCommandsAnnotation =
            "swapCommands"; /* MAY BE NULL */

        //
        // WARNING: This annotation is EXTREMELY POWERFUL.  Please do
        //          not use it unless you know exactly what you are
        //          doing.
        //
        // NOTE: If this annotation is present within a configuration
        //       (script) file, all further interpreter creation for
        //       the process will be halted.  This cannot be undone
        //       easily or directly.  Instead, the following process
        //       must be followed to restore interpreter creation
        //       capabilities:
        //
        //       0. In order to be able to successfully get past the
        //          "persistent interpreter creation is disabled"
        //          checking, the currently in-use primary license
        //          certificate for Harpy itself must include either
        //          the "EnableCreation" ("E") feature and/or the
        //          "All" ("X") feature.  Without at least one of
        //          these features present all attempts to reenable
        //          interpreter creation will fail.
        //
        //       1. For each configuration file that processed this
        //          annotation, call to the following method within
        //          the core library exactly once:
        //
        //              Utility.EnableInterpreterCreation(
        //                  DisableFlags.AllowNop);
        //
        //          For example, if there were two configuration
        //          files that had this annotation, the above method
        //          call would need to be done twice.
        //
        /// <summary>
        /// The configuration annotation token for disable interpreter
        /// creation.
        /// </summary>
        /* CORE */
        public const string DisableInterpreterCreationAnnotation =
            "disableInterpreterCreation"; /* MAY BE NULL */

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will emit trace messages for each
        //       (configuration) command executed.
        //
        /// <summary>
        /// The name of the configuration trace commands environment variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationTraceCommandsEnvVarName =
            "ConfigurationTraceCommands"; /* MAY NOT BE NULL */

        //
        // HACK: If this environment variable is set [to anything], it
        //       will be interpreted as the directory where all command
        //       trace log files will be stored.
        //
        /// <summary>
        /// The name of the configuration trace directory environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationTraceDirectoryEnvVarName =
            "ConfigurationTraceDirectory"; /* MAY NOT BE NULL */

        //
        // HACK: If this environment variable is set [to anything], it
        //       will allow line-ending characters to be emitted in the
        //       command trace log files.
        //
        /// <summary>
        /// The name of the configuration trace new lines environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationTraceNewLinesEnvVarName =
            "ConfigurationTraceNewLines"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration subsystem may write some diagnostic
        //       messages via its [writeWithoutFail] command.  If this
        //       environment varialbe is not set, the [writeWithoutFail]
        //       configuration command will simply do nothing.
        //
        /// <summary>
        /// The name of the write without fail environment variable.
        /// </summary>
        /* CORE */
        public const string WriteWithoutFailEnvVarName =
            "WriteWithoutFail"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will avoid emitting diagnostic
        //       messages to the configured interpreter host output via
        //       its [writeWithoutFail] command.
        //
        /// <summary>
        /// The name of the no write without fail via host environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string NoWriteWithoutFailViaHostEnvVarName =
            "NoWriteWithoutFailViaHost"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will use an interpreter that
        //       is created specifically for that purpose and disposed.
        //
        /// <summary>
        /// The name of the isolated configuration environment variable.
        /// </summary>
        /* CORE */
        public const string IsolatedConfigurationEnvVarName =
            "IsolatedConfiguration"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If this environment variable is set [to anything], the
        //       configuration loader will use a non-primary thread to
        //       perform its work.
        //
        /// <summary>
        /// The name of the asynchronous configuration environment variable.
        /// </summary>
        /* CORE */
        public const string AsynchronousConfigurationEnvVarName =
            "AsynchronousConfiguration"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If these environment variables are set [to anything],
        //       the configuration loader will attempt to the value as
        //       the name of a (signed) settings file containing the
        //       settings to use when creating isolated interpreters.
        //
        /// <summary>
        /// The composite format string used to construct the configuration
        /// interpreter settings environment variable name.
        /// </summary>
        /* CORE */
        public const string ConfigurationInterpreterSettingsEnvVarFormat =
            "ConfigurationInterpreterSettings_{0}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the configuration interpreter settings environment
        /// variable.
        /// </summary>
        /* CORE */
        public const string ConfigurationInterpreterSettingsEnvVarName =
            "ConfigurationInterpreterSettings"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: If these environment variables are set [to anything],
        //       the signed script evaluator will attempt to the value
        //       as the name of a (signed) settings file containing the
        //       settings to use when creating isolated interpreters.
        //
        /// <summary>
        /// The composite format string used to construct the script
        /// interpreter settings environment variable name.
        /// </summary>
        /* CORE */
        public const string ScriptInterpreterSettingsEnvVarFormat =
            "ScriptInterpreterSettings_{0}"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the script interpreter settings environment variable.
        /// </summary>
        /* CORE */
        public const string ScriptInterpreterSettingsEnvVarName =
            "ScriptInterpreterSettings"; /* MAY NOT BE NULL */
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Object Comparer Constants
        //
        // NOTE: This value is used to initialize the return value from the
        //       Licensing.Comparers.Object.GetHashCode method.
        //
        /// <summary>
        /// The hash code magic.
        /// </summary>
        public const int HashCodeMagic = 0x32686420;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Ring Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The name of the key ring URI.
        /// </summary>
        /* CORE? */
        public const string KeyRingUriName = "keyring"; /* MAY BE NULL */

        /// <summary>
        /// The key ring file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingFileName =
            "keyRing.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring zero file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingZeroFileName =
            "keyRing.zero.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring one file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingOneFileName =
            "keyRing.one.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring general file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingGeneralFileName =
            "keyRing.General.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring license file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingLicenseFileName =
            "keyRing.License.eagle"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
        /// <summary>
        /// The key ring demo general file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingDemoGeneralFileName =
            "keyRing.General.demo.eagle"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring demo license file name.
        /// </summary>
        /* CORE? */
        public const string KeyRingDemoLicenseFileName =
            "keyRing.License.demo.eagle"; /* MAY NOT BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 1).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern1 =
            "keyRing.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 2).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern2 =
            "keyRing.*.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 3).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern3 =
            "keyRing.{0}.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 4).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern4 =
            "keyRing.{0}.*.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 5).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern5 =
            "keyRing*.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 6).
        /// </summary>
        /* CORE? */
        public static readonly string KeyRingFileNamePattern6 =
            "keyRing*.eagle" + FileExtension.Signature; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 7).
        /// </summary>
        /* CORE? */
        public const string KeyRingFileNamePattern7 =
            "*/keyRing*.eagle"; /* MAY BE NULL */

        /// <summary>
        /// The pattern string used to match the key ring file name (variant
        /// 8).
        /// </summary>
        /* CORE? */
        public static readonly string KeyRingFileNamePattern8 =
            "*/keyRing*.eagle" + FileExtension.Signature; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key ring name (variant 1).
        /// </summary>
        /* CORE? */
        public const string KeyRingName1 = "default"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring name (variant 2).
        /// </summary>
        /* CORE? */
        public const string KeyRingName2 = "license"; /* MAY NOT BE NULL */

        /// <summary>
        /// The key ring name (variant 3).
        /// </summary>
        /* CORE? */
        public const string KeyRingName3 = "auxiliary"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the bootstrap directories environment variable.
        /// </summary>
        /* CORE? */
        public const string BootstrapDirectoriesEnvVarName =
            "BootstrapDirectories"; /* MAY NOT BE NULL */

        /// <summary>
        /// The suffix used when constructing bootstrap environment variable
        /// names.
        /// </summary>
        /* CORE? */
        public const string BootstrapEnvVarSuffix =
            "Bootstrap"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key pairs interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyPairsVariableName =
            "keyPairs"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key metadata interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyMetadataVariableName =
            "keyMetadata"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key usage interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyUsageVariableName =
            "keyUsage"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key expiration interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyExpirationVariableName =
            "keyExpiration"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key domains interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyDomainsVariableName =
            "keyDomains"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the key groups interpreter variable.
        /// </summary>
        /* CORE? */
        public const string KeyGroupsVariableName =
            "keyGroups"; /* MAY NOT BE NULL */

        /// <summary>
        /// The format string used to construct the settings interpreter
        /// variable name.
        /// </summary>
        /* CORE? */
        public const string SettingsVariableFormat =
            "{0}({1})"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The require core package script.
        /// </summary>
        /* CORE? */
        public static readonly string RequireCorePackageScript =
            String.Format("{0}; ::set {1} 1; ::package require Security.Core;",
            "{0}", SkipAutoKeyRingBootstrap); /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The skip auto key ring bootstrap.
        /// </summary>
        /* CORE? */
        public const string SkipAutoKeyRingBootstrap =
            "SkipAutoKeyRingBootstrap"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The with usage.
        /// </summary>
        /* CORE? */
        public const string WithUsage = "With Usage"; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix string used for the host name wildcard.
        /// </summary>
        /* CORE */
        public static readonly string HostNameWildcardPrefix =
            Characters.Asterisk.ToString() +
            Characters.Period.ToString(); /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the identifier metadata resource name.
        /// </summary>
        /* CORE */
        public const string IdentifierMetadataResourceNameFormat =
            "{0}.{1}.Public{2}"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The signature key name.
        /// </summary>
        /* CORE */
        public static readonly string SignatureKeyName =
            "EagleEnterprisePluginRootPublic" +
            FileExtension.StrongNameKey; /* MAY BE NULL */

        /// <summary>
        /// The assembly key name.
        /// </summary>
        /* CORE */
        public static readonly string AssemblyKeyName =
            "AssemblyPublic" +
            FileExtension.StrongNameKey; /* MAY NOT BE NULL */

        /// <summary>
        /// The license key name.
        /// </summary>
        /* CORE */
        public static readonly string LicenseKeyName =
            "LicensePublic" +
            FileExtension.StrongNameKey; /* MAY NOT BE NULL */

        /// <summary>
        /// The time key name.
        /// </summary>
        /* CORE */
        public static readonly string TimeKeyName =
            "TimePublic" +
            FileExtension.StrongNameKey; /* MAY NOT BE NULL */

        /// <summary>
        /// The auxiliary key name.
        /// </summary>
        /* CORE */
        public static readonly string AuxiliaryKeyName =
            "AuxiliaryPublic" +
            FileExtension.StrongNameKey; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The signature key metadata.
        /// </summary>
        /* CORE */
        public static readonly string SignatureKeyMetadata =
            "EagleEnterprisePluginRootPublic" +
            FileExtension.Text; /* MAY NOT BE NULL */

        /// <summary>
        /// The assembly key metadata.
        /// </summary>
        /* CORE */
        public static readonly string AssemblyKeyMetadata =
            "AssemblyPublic" +
            FileExtension.Text; /* MAY NOT BE NULL */

        /// <summary>
        /// The license key metadata.
        /// </summary>
        /* CORE */
        public static readonly string LicenseKeyMetadata =
            "LicensePublic" +
            FileExtension.Text; /* MAY NOT BE NULL */

        /// <summary>
        /// The time key metadata.
        /// </summary>
        /* CORE */
        public static readonly string TimeKeyMetadata =
            "TimePublic" +
            FileExtension.Text; /* MAY NOT BE NULL */

        /// <summary>
        /// The auxiliary key metadata.
        /// </summary>
        /* CORE */
        public static readonly string AuxiliaryKeyMetadata =
            "AuxiliaryPublic" +
            FileExtension.Text; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The trust root key revocation list.
        /// </summary>
        /* CORE */
        public static readonly string TrustRootKeyRevocationList =
            "EagleEnterpriseTrustRootRevokedKeys" +
            FileExtension.Text; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The trust root certificate revocation list.
        /// </summary>
        /* CORE */
        public static readonly string TrustRootCertificateRevocationList =
            "EagleEnterpriseTrustRootRevokedCertificates" +
            FileExtension.Text; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// The pattern string used to match the trust root key.
        /// </summary>
        public static readonly string TrustRootKeyPattern =
            "EagleEnterprise*RootPublic" +
            FileExtension.StrongNameKey; /* MAY BE NULL */
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// The script key name.
        /// </summary>
        public static readonly string ScriptKeyName =
            "EagleEnterpriseClass0RootPublic" +
            FileExtension.StrongNameKey; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Time Protocol Constants
#if NETWORK
        //
        // NOTE: When this value is non-zero, only the primary time servers
        //       will be trusted for purposes of certificate verification;
        //       otherwise, a random time server will be used (and trusted).
        //
        /// <summary>
        /// The network time force primary.
        /// </summary>
        /* CORE */
        public const bool NetworkTimeForcePrimary = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the number of times to retry querying the network
        //       time server for a successful response.  Using zero (or less)
        //       here means that no retries will occur.
        //
        /// <summary>
        /// The network time default retries.
        /// </summary>
        /* CORE */
        public const int NetworkTimeDefaultRetries = 20;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this is non-zero, the time stamp received from via the
        //       network must be signed, if applicable.
        //
        /// <summary>
        /// The network time must be signed.
        /// </summary>
        /* CORE */
        public const bool NetworkTimeMustBeSigned = true;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum allowed (absolute) difference in ticks
        //       between the network time server and the local system clock,
        //       when both are adjusted to UTC.  Higher differences will
        //       cause certificates that can expire to be considered "bad".
        //
        /// <summary>
        /// The network time difference maximum ticks.
        /// </summary>
        /* CORE */
        public const long NetworkTimeDifferenceMaximumTicks =
            TimeSpan.TicksPerHour; /* 1 hour */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Guid Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //    Changing these values MOST LIKELY WILL NOT break ALL existing  //
        //    certificates.                                                  //
        //                                                                   //
        //    Changing these values WILL break ALL existing certificate      //
        //    renewal requests.                                              //
        //                                                                   //
        //    Do not change any of these values unless you know exactly      //
        //    what they do.                                                  //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Dashes only.
        //
        /// <summary>
        /// The format string used for the default GUID.
        /// </summary>
        /* CORE */
        public const string DefaultGuidFormat = "D"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Without any formatting.
        //
        /// <summary>
        /// The format string used for the raw GUID.
        /// </summary>
        /* CORE */
        public const string RawGuidFormat = "N"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The machine path flags.
        /// </summary>
        /* CORE? */
        public const PathFlags MachinePathFlags =
            PathFlags.MachineForHarpy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The verify path flags.
        /// </summary>
        /* CORE? */
        public const PathFlags VerifyPathFlags =
            PathFlags.VerifyForHarpy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script network flags.
        /// </summary>
        /* CORE? */
        public const NetworkFlags ScriptNetworkFlags =
            NetworkFlags.Default;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The license network flags.
        /// </summary>
        /* CORE? */
        public const NetworkFlags LicenseNetworkFlags =
            NetworkFlags.Default;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The size of GUID.
        /// </summary>
        /* CORE? */
        public const int SizeOfGuid = 16;
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Constants (Debugging Only)
#if DEBUG || EXTRA_DIAGNOSTICS
        //
        // NOTE: Always use the "empty" Guid (i.e. the one consisting of
        //       all zeros) instead of creating a new one?
        //
        /// <summary>
        /// The name of the use empty ID environment variable.
        /// </summary>
        public const string UseEmptyIdEnvVarName =
            "UseEmptyId"; /* MAY NOT BE NULL */
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region DateTime Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //    Changing these values WILL break ALL existing certificates.    //
        //                                                                   //
        //    Do not change any of these values unless you know exactly      //
        //    what they do.                                                  //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The is time stamp UTC.
        /// </summary>
        /* CORE */
        public const bool IsTimeStampUtc = true;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the default time stamp.
        /// </summary>
        /* CORE */
        public const string DefaultTimeStampFormat =
            "yyyy-MM-ddTHH:mm:ss.fffffffK"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default date time styles.
        /// </summary>
        /* CORE */
        public const DateTimeStyles DefaultDateTimeStyles =
            DateTimeStyles.RoundtripKind;

        /// <summary>
        /// The format string used for the annotation date time.
        /// </summary>
        /* CORE */
        public const string AnnotationDateTimeFormat =
            "yyyy_MM_ddTHH_mm_ssK"; /* MAY NOT BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The placeholder text displayed for always values.
        /// </summary>
        /* CORE */
        public const string DisplayAlways = "<always>"; /* MAY BE NULL */

        /// <summary>
        /// The placeholder text displayed for never values.
        /// </summary>
        /* CORE */
        public const string DisplayNever = "<never>"; /* MAY BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TimeSpan Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: What is the TimeSpan value used to indicate that a
        //       certificate is valid forever?
        //
        /// <summary>
        /// The forever duration.
        /// </summary>
        public static readonly TimeSpan ForeverDuration = TimeSpan.Parse("-1");
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This is hard-coded to approximately 45 days.  It may need
        //       to be adjusted in the future.
        //
        /// <summary>
        /// The limited duration.
        /// </summary>
        public static readonly TimeSpan LimitedDuration = TimeSpan.FromDays(45);

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This is hard-coded to approximately 180 days.  It may need
        //       to be adjusted in the future.
        //
        /// <summary>
        /// The grace duration.
        /// </summary>
        public static readonly TimeSpan GraceDuration = TimeSpan.FromDays(180);

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This is the web page that may be shown if a configuration
        //       file is expired or not yet valid.
        //
        /// <summary>
        /// The out of time URI.
        /// </summary>
        public static readonly string OutOfTimeUri =
            "https://urn.to/r/harpy_exp_cfg";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum allowed (absolute) difference in ticks
        //       between the certificate creation date and the local system
        //       clock, when both are adjusted to UTC.  Higher differences
        //       will cause certificates that can expire to be considered
        //       "bad".  This only applies to certificates that appear to
        //       have been created in the future.
        //
        /// <summary>
        /// The created time difference maximum ticks.
        /// </summary>
        /* CORE */
        public const long CreatedTimeDifferenceMaximumTicks =
            TimeSpan.TicksPerMinute; /* 1 minute */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum allowed (absolute) difference in days
        //       between the plugin creation (compilation?) time and the
        //       plugin installation time.  Only used when calculating the
        //       expiration DateTime for trial certificates.
        //
        /// <summary>
        /// The install time maximum days.
        /// </summary>
        /* CORE */
        public const long InstallTimeMaximumDays = 366; /* ~1 leap year */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Entity Name Constants
        //
        // NOTE: This is the minimum length possible for a string that has a
        //       "process" unique identifier (GUID).
        //
        /// <summary>
        /// The minimum process length.
        /// </summary>
        /* CORE? */
        public const int MinimumProcessLength = 44; /* "process ..." */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used to extract a process unique identifier (GUID)
        //       from file version info resources.
        //
        /// <summary>
        /// The regular expression used to match the process.
        /// </summary>
        /* CORE */
        public static readonly Regex ProcessRegEx = new Regex(
            "(?:^|, )process ([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]" +
            "{4}-[0-9a-f]{12})(?:, |$)");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The process plugin flags.
        /// </summary>
        /* CORE */
        public const PluginFlags ProcessPluginFlags =
#if DEBUG
            PluginFlags.VerifiedOnly;
#else
            PluginFlags.VerifiedAndTrustedOnlyMask;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the minimum length possible for a string that has a
        //       "requirement" unique identifier (GUID).
        //
        /// <summary>
        /// The minimum requirement length.
        /// </summary>
        /* CORE */
        public const int MinimumRequirementLength = 44; /* "require ..." */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used to extract a "requirement" unique identifier
        //       (GUID) from certificate entity names.  When present, this
        //       will require a license certificate to have been verified
        //       prior to the current one, e.g. via a configuration script.
        //
        /// <summary>
        /// The regular expression used to match the requirement.
        /// </summary>
        /* CORE */
        public static readonly Regex RequirementRegEx = new Regex(
            "(?:^|, )require ([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]" +
            "{4}-[0-9a-f]{12})(?:, |$)");

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: This is the minimum length possible for an (email?) string
        //       that has a user name and a domain name.
        //
        /// <summary>
        /// The minimum email length.
        /// </summary>
        /* CORE? */
        public const int MinimumEmailLength = 6; /* "x@y.cc" */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used to extract an email address from certificate
        //       entity names.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The regular expression used to match the entity email.
        /// </summary>
        /* CORE? */
        public static readonly Regex EntityEmailRegEx = new Regex(
            " \\<[^@]+@([\\w]+(?:\\.[\\w]+)+)\\>$"); /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Quantity Constants
        //
        // HACK: *COMPAT* For compatibility with existing certificates, the
        //       quantity of negative one is used to indicate "unlimited".
        //
        /// <summary>
        /// The quantity unlimited.
        /// </summary>
        /* CORE */
        public const long QuantityUnlimited = -1;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the registry value name for the quantity remaining.
        //
        /// <summary>
        /// The quantity value name.
        /// </summary>
        /* CORE */
        public const string QuantityValueName = "Quantity"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When non-zero the CryptProtectData / CryptUnprotectData
        //       functions will be used in per-machine mode and also the
        //       per-machine registry hive will be used.
        //
        /// <summary>
        /// The quantity per machine.
        /// </summary>
        public static readonly bool? QuantityPerMachine =
            null; /* MAY BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Extra Features Constants
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        //
        // NOTE: When non-zero the CryptProtectData / CryptUnprotectData
        //       functions will be used for the extra features data.
        //
        /// <summary>
        /// The protect extra features.
        /// </summary>
        public const bool ProtectExtraFeatures = true;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When non-zero the CryptProtectData / CryptUnprotectData
        //       functions will be used in per-machine mode and also the
        //       per-machine registry hive will be used.
        //
        /// <summary>
        /// The extra features per machine.
        /// </summary>
        public static readonly bool? ExtraFeaturesPerMachine =
            null; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the registry value name for the extra features.
        //
        /// <summary>
        /// The extra features value name.
        /// </summary>
        public const string ExtraFeaturesValueName =
            "ExtraFeatures"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Registry Constants
        /// <summary>
        /// The default value.
        /// </summary>
        /* CORE */
        public static readonly byte[] DefaultValue = new byte[0];

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key name separator.
        /// </summary>
        /* CORE */
        public const char KeyNameSeparator = Characters.Backslash;

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// The key name separators.
        /// </summary>
        public static readonly char[] KeyNameSeparators = {
            KeyNameSeparator
        }; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default value name.
        /// </summary>
        /* CORE */
        public const string DefaultValueName = "<default>"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The base key name.
        /// </summary>
        /* CORE */
        public const string BaseKeyName = "Software"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The version components for key name.
        /// </summary>
        /* CORE */
        public const int VersionComponentsForKeyName = 2; /* <MAJOR>.<MINOR> */

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// The machine GUID key name.
        /// </summary>
        public const string MachineGuidKeyName =
            "Software\\Microsoft\\Cryptography"; /* MAY BE NULL */

        /// <summary>
        /// The machine GUID value name.
        /// </summary>
        public const string MachineGuidValueName =
            "MachineGuid"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Subsystem Constants
        //
        // NOTE: The name of the boolean datum within the clientData that
        //       will permit skipping of AppDomain authorization checks.
        //
        /// <summary>
        /// The name of the skip authorization data item.
        /// </summary>
        /* CORE */
        public const string SkipAuthorizationDataName =
            "skipAuthorization"; /* MAY NOT BE NULL */

        /// <summary>
        /// The name of the license failure count environment variable.
        /// </summary>
        /* CORE */
        public const string LicenseFailureCountEnvVarName =
            "LicenseFailureCount"; /* MAY NOT BE NULL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Manager Constants
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
        /// <summary>
        /// The name of the support URI.
        /// </summary>
        public const string SupportUriName = "support"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Agreement Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     Changing these values MAY break ALL existing certificates.    //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && LICENSING
        /// <summary>
        /// The URI of the core license agreement.
        /// </summary>
        public static readonly Uri CoreAgreement = new Uri(
            "https://eagle.to/standard/license.html");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The URI of the core license agreement (variant 2).
        /// </summary>
        public static readonly Uri CoreAgreement2 = new Uri(
            "https://urn.to/r/ece_license");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The URI of the standard license agreement.
        /// </summary>
        public static readonly Uri StandardAgreement = new Uri(
            "https://eagle.to/standard/license.html");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The URI of the standard license agreement (variant 2).
        /// </summary>
        public static readonly Uri StandardAgreement2 = new Uri(
            "https://urn.to/r/ese_license");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The URI of the enterprise license agreement.
        /// </summary>
        public static readonly Uri EnterpriseAgreement = new Uri(
            "https://eagle.to/enterprise/license.html");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The URI of the enterprise license agreement (variant 2).
        /// </summary>
        public static readonly Uri EnterpriseAgreement2 = new Uri(
            "https://urn.to/r/eee_license");
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Warning Constants
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The license warning type.
        /// </summary>
        public const string LicenseWarningType = "License"; /* MAY BE NULL */
        /// <summary>
        /// The script warning type.
        /// </summary>
        public const string ScriptWarningType = "Script"; /* MAY BE NULL */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used for the warning suffix.
        /// </summary>
        public const string WarningSuffixFormat =
            "{0}{0}    {1} HASH: {2}"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Shell Callback Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The no file name.
        /// </summary>
        public const string NoFileName = "NoFileName";
#endif

        ///////////////////////////////////////////////////////////////////////

#if SHELL && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// The fallback engine flags.
        /// </summary>
        public const EngineFlags FallbackEngineFlags =
            VerifyEngineFlags |
            EngineFlags.None;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Type Constants
        /// <summary>
        /// The command policy type.
        /// </summary>
        /* CORE */
        public const PolicyType CommandPolicyType = PolicyType.Script;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This is the default policy type for [cryptography] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default cryptography command policy type.
        /// </summary>
        public const PolicyType DefaultCryptographyCommandPolicyType =
            CommandPolicyType;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default policy type for [certificate] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default certificate other command policy type.
        /// </summary>
        public const PolicyType DefaultCertificateOtherCommandPolicyType =
            CommandPolicyType;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default policy type for [certificate verify]
        //       and [certificate loadandverify] sub-command options only.
        //       Do not use it in any other context.
        //
        /// <summary>
        /// The default certificate verify command policy type.
        /// </summary>
        public const PolicyType DefaultCertificateVerifyCommandPolicyType =
            PolicyType.License;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default policy type for [keypair] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default key pair command policy type.
        /// </summary>
        public const PolicyType DefaultKeyPairCommandPolicyType =
            CommandPolicyType;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default policy type for [secret] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default secret command policy type.
        /// </summary>
        public const PolicyType DefaultSecretCommandPolicyType =
            CommandPolicyType;
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: This is the default policy type for [keyring] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default key ring command policy type.
        /// </summary>
        public const PolicyType DefaultKeyRingCommandPolicyType =
            CommandPolicyType;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default policy type for [certificate] command
        //       options only.  Do not use it in any other context.
        //
        /// <summary>
        /// The default ksource command policy type.
        /// </summary>
        public const PolicyType DefaultKsourceCommandPolicyType =
            CommandPolicyType;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Execution Policy Constants
        #region Default Execution Policy Constants
        /// <summary>
        /// The bit mask describing the enable policy tracing limit flags.
        /// </summary>
        /* CORE */
        public const ExecutionPolicy EnablePolicyTracingLimitMask =
            ExecutionPolicy.EnableTracing |
            ExecutionPolicy.AppendTracing |
            ExecutionPolicy.SharedTracing |
            ExecutionPolicy.ForceTracing |
            ExecutionPolicy.VerboseTracing |
            ExecutionPolicy.AutoTraceFile |
            ExecutionPolicy.FullTracing |
            ExecutionPolicy.ResetTracing;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The bit mask describing the enable policy tracing default flags.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy EnablePolicyTracingDefaultMask =
            ExecutionPolicy.EnableTracing |
            ExecutionPolicy.AppendTracing |
            ExecutionPolicy.SharedTracing |
            ExecutionPolicy.ForceTracing |
            ExecutionPolicy.AutoTraceFile |
            ExecutionPolicy.ResetTracing;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The bit mask describing the enable full plugin policy tracing
        /// flags.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy EnableFullPluginPolicyTracingMask =
            ExecutionPolicy.EnableTracing |
            ExecutionPolicy.AppendTracing |
            ExecutionPolicy.SharedTracing |
            ExecutionPolicy.ForceTracing |
            ExecutionPolicy.VerboseTracing |
            ExecutionPolicy.AutoTraceFile |
            ExecutionPolicy.FullTracing |
            ExecutionPolicy.ResetTracing;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default common execution policy.
        /// </summary>
        /* CORE */
        private const ExecutionPolicy DefaultCommonExecutionPolicy =
            ExecutionPolicy.None;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The default script execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy DefaultScriptExecutionPolicy =
            DefaultCommonExecutionPolicy |
            ExecutionPolicy.TraceKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default file execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy DefaultFileExecutionPolicy =
            DefaultCommonExecutionPolicy |
            ExecutionPolicy.TrustSignedOnly |
            ExecutionPolicy.TraceKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default stream execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy DefaultStreamExecutionPolicy =
            DefaultCommonExecutionPolicy |
            ExecutionPolicy.TraceKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Currently, the execution policy flags listed here are the
        //       only ones supported by the license verification subsystem.
        //
        /// <summary>
        /// The default license execution policy.
        /// </summary>
        /* CORE */
        public const ExecutionPolicy DefaultLicenseExecutionPolicy =
            DefaultCommonExecutionPolicy |
            /* ExecutionPolicy.SkipThisAssembly | */
            /* ExecutionPolicy.ExplicitOnly | */
            /* ExecutionPolicy.PreferEmbedded | */
            ExecutionPolicy.AnyResourcePublicKey |
            ExecutionPolicy.CheckPublicKeyToken |
            ExecutionPolicy.EnforceKeyGroup |
            ExecutionPolicy.EnforceKeyUsage |
            ExecutionPolicy.CheckRevocation |
            ExecutionPolicy.CheckDomains |
            ExecutionPolicy.AllowRemoteUri |
            ExecutionPolicy.LooksLikeXml |
            ExecutionPolicy.PreValidateXml |
            ExecutionPolicy.MaybeNoFileSearch |
            ExecutionPolicy.AllowAssemblyPublicKey |
            ExecutionPolicy.AllowEmbeddedPublicKey |
            ExecutionPolicy.AllowRingPublicKey |
            ExecutionPolicy.AllowAnyPublicKey |
            ExecutionPolicy.IgnoreKeyRingError |
            ExecutionPolicy.CacheKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The default key pair execution policy.
        /// </summary>
        /* CORE */
        public const ExecutionPolicy DefaultKeyPairExecutionPolicy =
            DefaultCommonExecutionPolicy |
            ExecutionPolicy.TraceKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This purposely excludes the associated "common" execution
        //       policy flags.  This could be modified to contain the value
        //       EmitDiagnostics.
        //
        /// <summary>
        /// The default trace execution policy.
        /// </summary>
        /* CORE */
        public const ExecutionPolicy DefaultTraceExecutionPolicy =
            ExecutionPolicy.None;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Default to the policy that contains the flags we need for
        //       X509 certificate subject matching.  This purposely excludes
        //       the associated "common" execution policy flags.
        //
        /// <summary>
        /// The default other execution policy.
        /// </summary>
        /* CORE */
        public const ExecutionPolicy DefaultOtherExecutionPolicy =
            ExecutionPolicy.MatchSubjectSimpleName |
            ExecutionPolicy.MatchSubjectPrefix;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Simple Execution Policy Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: This is the "simple" execution policy for all editions of the
        //       Harpy plugin.  For the "Limited Edition", only scripts signed
        //       with the trust root will initially be allowed.  For all other
        //       editions, the assembly signing key will also be recognized as
        //       valid for signing scripts.  For all editions, [any] keys on
        //       the trusted key ring will be recognized as valid for signing
        //       scripts.
        //
        // NOTE: Only scripts included with Eagle Enterprise Edition itself
        //       will be signed with the assembly signing key.  This usage for
        //       the assembly signing key being phased out.  In the future, it
        //       will only be used to sign license certificates as well as the
        //       assembly itself.
        //
        /// <summary>
        /// The simple common execution policy.
        /// </summary>
        /* CORE? */
        private const ExecutionPolicy SimpleCommonExecutionPolicy =
            DefaultCommonExecutionPolicy |
            ExecutionPolicy.AllowSignedOnly |
            ExecutionPolicy.CheckExpiry |
            ExecutionPolicy.CheckEntityType |
            ExecutionPolicy.CheckPublicKeyToken |
            ExecutionPolicy.AllowEmbeddedPublicKey |
            ExecutionPolicy.AllowRingPublicKey |
            ExecutionPolicy.AllowAnyPublicKey |
            ExecutionPolicy.CheckDomains |
            ExecutionPolicy.CheckQuantity |
            ExecutionPolicy.ProtectQuantity |
            ExecutionPolicy.AllowEmbedded |
            ExecutionPolicy.EnforceKeyUsage |
            ExecutionPolicy.CheckRevocation |
            ExecutionPolicy.CacheKeyRings |
            ExecutionPolicy.SaveApprovedData |
            ExecutionPolicy.UseApprovedData;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The simple script execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleScriptExecutionPolicy =
            SimpleCommonExecutionPolicy | DefaultScriptExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The simple file execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleFileExecutionPolicy =
            SimpleCommonExecutionPolicy | DefaultFileExecutionPolicy |
            ExecutionPolicy.SkipExists |
            ExecutionPolicy.AllowRemoteUri |
            ExecutionPolicy.ValidateXml |
            ExecutionPolicy.VerifyString |
            ExecutionPolicy.VerifyFile;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The simple stream execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleStreamExecutionPolicy =
            SimpleCommonExecutionPolicy | DefaultStreamExecutionPolicy |
            ExecutionPolicy.SkipExists |
            ExecutionPolicy.AllowRemoteUri |
            ExecutionPolicy.VerifyString;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The simple license execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleLicenseExecutionPolicy =
            SimpleCommonExecutionPolicy | DefaultLicenseExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The simple key pair execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleKeyPairExecutionPolicy =
            SimpleCommonExecutionPolicy | DefaultKeyPairExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This purposely excludes the associated "common" execution
        //       policy flags.
        //
        /// <summary>
        /// The simple trace execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleTraceExecutionPolicy =
            DefaultTraceExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This purposely excludes the associated "common" execution
        //       policy flags.
        //
        /// <summary>
        /// The simple other execution policy.
        /// </summary>
        /* CORE? */
        public const ExecutionPolicy SimpleOtherExecutionPolicy =
            DefaultOtherExecutionPolicy;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Limited Edition Execution Policy Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && LIMITED_EDITION
        /// <summary>
        /// The limited script execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedScriptExecutionPolicy =
            SimpleScriptExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited file execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedFileExecutionPolicy =
            SimpleFileExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited stream execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedStreamExecutionPolicy =
            SimpleStreamExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited license execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedLicenseExecutionPolicy =
            SimpleLicenseExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited key pair execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedKeyPairExecutionPolicy =
            SimpleKeyPairExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited trace execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedTraceExecutionPolicy =
            SimpleTraceExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The limited other execution policy.
        /// </summary>
        public const ExecutionPolicy LimitedOtherExecutionPolicy =
            SimpleOtherExecutionPolicy;
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Security Manager Constants
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
#if DEBUG
        //
        // NOTE: If this runtime option is set, the [security] command can be
        //       used only via the "Security.Core" plugin.  In release builds,
        //       this runtime option has no effect.
        //
        /// <summary>
        /// The name of the forbid non security core option.
        /// </summary>
        /* CORE? */
        public const string ForbidNonSecurityCoreOption =
            "forbidNonSecurityCore"; /* MAY BE NULL */
#else
        /// <summary>
        /// The name of the allow non security core option.
        /// </summary>
        /* CORE? */
        public const string AllowNonSecurityCoreOption =
            "allowNonSecurityCore"; /* MAY BE NULL */
#endif

        /// <summary>
        /// The error message text used for the security core only.
        /// </summary>
        /* CORE? */
        public const string SecurityCoreOnlyError =
            "for use with security plugins only"; /* MAY BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Storage Manager Constants
        //
        // NOTE: This is the default value for the MustHaveSecurity property
        //       of the IStorageManager interface.  When non-zero, this will
        //       require the interpreter to have its security enabled.
        //
        /// <summary>
        /// The default must have security.
        /// </summary>
        /* CORE */
        public const bool DefaultMustHaveSecurity = false; /* COMPAT: beta */
        #endregion
    }
}
