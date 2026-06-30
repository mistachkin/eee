/*
 * CertificatePluginOps.cs --
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
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using IsolatedState = Licensing.Components.Private.CertificateIsolatedState;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the private helper operations used by the certificate plugin,
    /// including license manager discovery, anti-tampering checks,
    /// certificate summary formatting, and define constant queries.
    /// </summary>
    [ObjectId("857f050b-e629-4726-9bc7-f59e5172b74e")]
    internal static class CertificatePluginOps
    {
        #region License Manager Support
        /// <summary>
        /// Attempts to locate the <see cref="ILicenseManager" /> associated
        /// with the specified plugin, searching the license plugin manager
        /// data, the plugin client data, and the plugin auxiliary data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context.  This parameter is not used.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to search for an associated license manager.
        /// </param>
        /// <param name="initialize">
        /// Non-zero if the plugin is being constructed; when set, the cached
        /// license manager from the license plugin manager data is skipped.
        /// </param>
        /// <returns>
        /// The discovered <see cref="ILicenseManager" />, or null if one
        /// could not be found.
        /// </returns>
        public static ILicenseManager FindLicenseManager(
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData,  /* in */
            bool initialize          /* in */
            )
        {
            if (pluginData == null)
                return null;

            if (!initialize) /* NOTE: Are we constructing the plugin? */
            {
                ILicensePluginManagerData licensePluginManagerData =
                    SharedOps.GetLicensePluginManagerData(pluginData);

                if (licensePluginManagerData != null)
                {
                    ILicenseManager licenseManager =
                        licensePluginManagerData.LicenseManager;

                    if (licenseManager != null)
                        return licenseManager;
                }
            }

            IClientData clientData = pluginData.ClientData;

            if (clientData != null)
            {
                object data = null;

                /* IGNORED */
                clientData = ClientData.UnwrapOrReturn(
                    clientData, ref data);

                ILicenseManager licenseManager = data as ILicenseManager;

                if (licenseManager != null)
                    return licenseManager;
            }

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData != null)
            {
                string name = typeof(ILicenseManager).Name;
                object value;

                if (auxiliaryData.TryGetValue(name, out value))
                {
                    ILicenseManager licenseManager = value as ILicenseManager;

                    if (licenseManager != null)
                        return licenseManager;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the file name selection callback configured on the license
        /// manager associated with the specified plugin, optionally falling
        /// back to <see cref="DefaultFileNameCallback" /> when none is set.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to locate the associated license manager.
        /// </param>
        /// <param name="defaultOnNull">
        /// Non-zero to return the default callback when the license manager
        /// has no configured file name callback.
        /// </param>
        /// <returns>
        /// The configured file name selection callback, or the default
        /// callback when one is not available.
        /// </returns>
        public static ElementSelectionCallback GetFileNameCallback(
            IPluginData pluginData, /* in */
            bool defaultOnNull      /* in */
            )
        {
            if (pluginData == null)
                goto fallback;

            ILicensePluginManagerData licensePluginManagerData =
                SharedOps.GetLicensePluginManagerData(pluginData);

            if (licensePluginManagerData == null)
                goto fallback;

            ILicenseManager licenseManager =
                licensePluginManagerData.LicenseManager;

            if (licenseManager == null)
                goto fallback;

            ElementSelectionCallback fileNameCallback =
                licenseManager.FileNameCallback;

            if (defaultOnNull && (fileNameCallback == null))
                goto fallback;

            return fileNameCallback;

        fallback:

            return DefaultFileNameCallback;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default file name selection callback, which returns the first
        /// valid file name from the specified set of candidate file names.
        /// </summary>
        /// <param name="fileNames">
        /// The candidate file names to select from.
        /// </param>
        /// <param name="clientData">
        /// The optional client data associated with the selection.
        /// </param>
        /// <returns>
        /// The first valid file name, or null if none is valid.
        /// </returns>
        public static string DefaultFileNameCallback(
            IEnumerable<string> fileNames, /* in */
            IClientData clientData         /* in: OPTIONAL */
            ) /* EComPD.ElementSelectionCallback */
        {
            return CertificateVerifyOps.GetFirstValidFileName(
                fileNames, clientData);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Extra Features Support
#if !LIMITED_EDITION
        /// <summary>
        /// Reads and decodes the persisted extra features string for the
        /// specified certificate from the storage manager, optionally
        /// decrypting the stored value when protection is enabled.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter context used to locate the storage
        /// manager.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data used to locate the storage manager.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used to hash the value
        /// name.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing the value name.
        /// </param>
        /// <param name="certificate">
        /// The optional certificate used to scope the value name and provide
        /// the key for decryption.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when reading the stored value.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags controlling how the certificate contributes to
        /// the hash.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to decode the stored value bytes into a string.
        /// </param>
        /// <param name="storageType">
        /// The optional storage type identifying the storage manager to use.
        /// </param>
        /// <param name="protect">
        /// Non-zero if the stored value is protected and must be decrypted.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to read the value from per-machine storage instead of
        /// per-user storage.
        /// </param>
        /// <param name="features">
        /// Upon success, receives the decoded extra features string, or null
        /// if no value has been stored.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode GetExtraFeatures(
            Interpreter interpreter,                    /* in: OPTIONAL */
            IPluginData pluginData,                     /* in: OPTIONAL */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CultureInfo cultureInfo,                    /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in */
            StorageType? storageType,                   /* in: OPTIONAL */
            bool protect,                               /* in */
            bool perMachine,                            /* in */
            ref string features,                        /* out */
            ref Result error                            /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            string name = CertificateDataOps.FormatValueName(
                certificate, Constants.ExtraFeaturesValueName);

            byte[] nameData = null;

            if (SharedOps.HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, name,
                    ref nameData, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IStorageManager storageManager = SharedOps.GetStorageManager(
                    interpreter, pluginData, storageType, true, true);

            if (storageManager == null)
            {
                error = "storage manager not available";
                return ReturnCode.Error;
            }

            string valueName = CertificateDataOps.FormatHexadecimal(
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
                //
                features = null;
            }
#if NATIVE
            else if (protect)
            {
                //
                // NOTE: Decrypt the value read from the registry to obtain
                //       the extra features.
                //
                if (ProtectOps.UnprotectData(
                        (certificate != null) ? certificate.Key : null,
                        perMachine, false, true, ref description,
                        ref valueData, ref error) == ReturnCode.Ok)
                {
                    features = encoding.GetString(valueData);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
#endif
            else
            {
                features = encoding.GetString(valueData);
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if false
        //
        // NOTE: This method is purposely omitted from all the production
        //       builds of Harpy in all SKU build configurations.  It should
        //       only be used on a bonafide licensed developer machine where
        //       the fully licensed Eagle and Harpy source code is present
        //       and then only when the Harpy extra features registry key
        //       needs to be created or changed.  The registry key will be
        //       cryptographically locked (i.e. via CryptProtectData) to the
        //       user who created or changed it.
        //
        //       WARNING: DO NOT DISTRIBUTE A COMPILED HARPY ASSEMBLY WITH
        //                THIS METHOD INCLUDED.  DOING SO WOULD BE A DIRECT
        //                VIOLATION OF THE LICENSE AGREEMENT.
        //
        /// <summary>
        /// Encodes, optionally encrypts, and persists the extra features
        /// string for the specified certificate via the storage manager,
        /// deleting the stored value when no features are supplied.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter context used to locate the storage
        /// manager.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data used to locate the storage manager.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used to hash the value
        /// name.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing the value name.
        /// </param>
        /// <param name="certificate">
        /// The optional certificate used to scope the value name and provide
        /// the key for encryption.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when writing the stored value.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags controlling how the certificate contributes to
        /// the hash.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to encode the features string into bytes.
        /// </param>
        /// <param name="storageType">
        /// The optional storage type identifying the storage manager to use.
        /// </param>
        /// <param name="protect">
        /// Non-zero to encrypt the value before it is stored.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to write the value to per-machine storage instead of
        /// per-user storage.
        /// </param>
        /// <param name="features">
        /// The optional extra features string to store; when null, any
        /// existing stored value is deleted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode SetExtraFeatures(
            Interpreter interpreter,                    /* in: OPTIONAL */
            IPluginData pluginData,                     /* in: OPTIONAL */
            string hashAlgorithmName,                   /* in: OPTIONAL */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CultureInfo cultureInfo,                    /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in */
            StorageType? storageType,                   /* in: OPTIONAL */
            bool protect,                               /* in */
            bool perMachine,                            /* in */
            ref string features,                        /* in: OPTIONAL */
            ref Result error                            /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            string name = CertificateDataOps.FormatValueName(
                certificate, Constants.ExtraFeaturesValueName);

            byte[] nameData = null;

            if (SharedOps.HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, name,
                    ref nameData, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IStorageManager storageManager = SharedOps.GetStorageManager(
                interpreter, pluginData, storageType, true, true);

            if (storageManager == null)
            {
                error = "license data manager not available";
                return ReturnCode.Error;
            }

            string valueName = CertificateDataOps.FormatHexadecimal(
                nameData);

            byte[] valueData = null;

            if (features != null)
            {
                valueData = encoding.GetBytes(features);

#if NATIVE
                if (protect)
                {
                    string description = CertificateDataOps.FormatId(
                        (certificate != null) ? certificate.Id : Guid.Empty);

                    if (ProtectOps.ProtectData(
                            (certificate != null) ? certificate.Key : null,
                            false, false, true, description, ref valueData,
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
            else
            {
                if (storageManager.DeleteValue(
                        valueName, cultureInfo, perMachine, true,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hacking Prevention Support
        /// <summary>
        /// Compares the subjects of two X509 certificates, optionally
        /// allowing a prefix match and/or a simple name match according to
        /// the supplied execution policy.
        /// </summary>
        /// <param name="thisX509Certificate2">
        /// The first certificate to compare.
        /// </param>
        /// <param name="coreX509Certificate2">
        /// The second certificate to compare against.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy controlling whether prefix matching
        /// and simple name matching are permitted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the matching failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the subjects match; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchX509CertificateSubjects(
            X509Certificate2 thisX509Certificate2, /* in */
            X509Certificate2 coreX509Certificate2, /* in */
            ExecutionPolicy? policy,               /* in: OPTIONAL */
            ref Result error                       /* out */
            )
        {
            if (thisX509Certificate2 == null)
            {
                error = "invalid certificate (1)";
                return ReturnCode.Error;
            }

            if (coreX509Certificate2 == null)
            {
                error = "invalid certificate (2)";
                return ReturnCode.Error;
            }

            bool usePrefix;
            bool useSimpleName;

            SharedOps.ExtractSubjectExecutionPolicy(
                policy, out usePrefix, out useSimpleName);

            string subject1 = thisX509Certificate2.Subject;
            string subject2 = coreX509Certificate2.Subject;

            if (CertificateDataOps.StringEquals(subject1, subject2))
                return ReturnCode.Ok;

            if (usePrefix && CertificateDataOps.StringStartsWith(
                    subject1, subject2))
            {
                return ReturnCode.Ok;
            }

            if (!useSimpleName)
            {
                error = String.Format(
                    "full ({0} prefix) certificate subject matching failed",
                    usePrefix ? "with" : "without");

                return ReturnCode.Error;
            }

            string simpleName1 = thisX509Certificate2.GetNameInfo(
                X509NameType.SimpleName, false);

            string simpleName2 = coreX509Certificate2.GetNameInfo(
                X509NameType.SimpleName, false);

            if (CertificateDataOps.StringEquals(simpleName1, simpleName2))
                return ReturnCode.Ok;

            if (usePrefix && CertificateDataOps.StringStartsWith(
                    simpleName1, simpleName2))
            {
                return ReturnCode.Ok;
            }

            error = String.Format(
                "full and simple ({0} prefix) certificate subject matching failed",
                usePrefix ? "with" : "without");

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the Authenticode certificate of the executing
        /// assembly matches that of the Eagle core assembly by comparing
        /// their X509 certificate subjects.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the verification failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the certificates match; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyAssemblyCertificates(
            ref Result error /* out */
            )
        {
            Assembly thisAssembly = CertificateAssemblyOps.GetObject();

            if (thisAssembly == null)
            {
                error = "invalid executing assembly";
                return ReturnCode.Error;
            }

            Assembly coreAssembly = typeof(Engine).Assembly;

            if (coreAssembly == null)
            {
                error = "invalid core assembly";
                return ReturnCode.Error;
            }

            X509Certificate2 thisX509Certificate2 = null;

            if (Utility.GetAssemblyCertificate2(
                    thisAssembly, false, ref thisX509Certificate2,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            X509Certificate2 coreX509Certificate2 = null;

            if (Utility.GetAssemblyCertificate2(
                    coreAssembly, false, ref coreX509Certificate2,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return MatchX509CertificateSubjects(
                thisX509Certificate2, coreX509Certificate2, null,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified extra features are present, using
        /// the supplied features string when available or the cached global
        /// extra features otherwise.
        /// </summary>
        /// <param name="features">
        /// The optional extra features string to test; when null, the cached
        /// global extra features are consulted instead.
        /// </param>
        /// <param name="hasFeatures">
        /// The feature flags to test for.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require all of the specified features; zero to require
        /// any of them.
        /// </param>
        /// <param name="result">
        /// Receives the result of the feature matching operation.
        /// </param>
        /// <returns>
        /// Non-zero if the requested extra features are present; otherwise,
        /// zero.
        /// </returns>
        private static bool HasExtraFeatures(
            string features,    /* in: OPTIONAL */
            string hasFeatures, /* in */
            bool hasAll,        /* in */
            ref Result result   /* out */
            )
        {
            if (features != null)
            {
                //
                // NOTE: Use the specified extra features passed by
                //       the caller.
                //
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if (SharedOps.MatchFlags(
                        features, FlagType.Feature, flagsKey,
                        hasFeatures, null, hasAll, false, true,
                        ref result) == ReturnCode.Ok)
                {
                    return true;
                }
            }
#if !LIMITED_EDITION
            else
            {
                //
                // NOTE: Use the cached extra features kept in the
                //       global state.
                //
                if (CertificateGlobalState.HaveExtraFeatures(
                        hasFeatures, hasAll, ref result))
                {
                    return true;
                }
            }
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the core anti-tampering and licensing integrity checks,
        /// including extra feature detection, native and managed debugger
        /// detection, strong name and Authenticode evidence demands, and
        /// matching of assembly certificates.  Individual checks may be
        /// relaxed by the applicable extra feature flags.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter context; may be null.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data; may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the failed check.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if all checks pass; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode InnerCheck(
            Interpreter interpreter, /* in, OPTIONAL: May be null. */
            IPluginData pluginData,  /* in, OPTIONAL: May be null. */
            ref Result error         /* out */
            )
        {
#if DEBUG || (OFFICIAL && STABLE)
            bool nativeDebuggerOk = false;
            bool managedDebuggerOk = false;
            bool noStrongNameOk = false;
            bool noVerifiedOk = false;
            bool noCertificateOk = false;
            bool skipCertificateOk = false;
            bool noTrustedOk = false;
            bool promotionalOk = false;
            bool enableTestModeOk = false;
            bool relaxedSecretsKeyUsageOk = false;
            bool asynchronousLicensingOk = false;
            Result result; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: For official release builds, these are blocking errors
            //       without the necessary "extra feature" flag; otherwise,
            //       they are totally harmless.
            //
            TracePriority priority1 = TracePriority.Lowest;
            TracePriority priority2 = TracePriority.MediumHigh;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: For all official release builds, we absolutely expect
            //       there to be X509 certificates and signatures for all
            //       managed assemblies; however, when running on the .NET
            //       Core runtime on non-Windows (e.g. Linux), certificate
            //       trust checking will not be performed in the usual way;
            //       instead, the list of trusted hashes will be consulted.
            //       No "real" trust checking against the X509 certificate
            //       chain will not be performed in those cases.  Something
            //       is better than nothing, no?  For "Debug" builds, we do
            //       not expect there to be X509 certificates or signatures
            //       on any managed assemblies.
            //
            TracePriority priority3;

#if DEBUG
            priority3 = priority1;
#else
            priority3 = priority2;
#endif

            ///////////////////////////////////////////////////////////////////
            // **************** PHASE 0: CHECK EXTRA FEATURES *****************
            ///////////////////////////////////////////////////////////////////

            #region Phase 0: Check Extra Features
#if !LIMITED_EDITION
            //
            // WARNING: Since there is no certificate instance available at
            //          this point in the plugin initialization process, the
            //          only possible way the "debug.eagle" tool works is by
            //          setting the extra feature globally, i.e. without the
            //          use of a specific certificate file name.
            //
            string features = CertificateGlobalState.GetExtraFeatures();

            if (features == null)
            {
                string hashAlgorithmName = SharedOps.GetHashAlgorithm(
                    null, null, null, HashAlgorithmType.LocalUse);

                bool perMachine = SharedOps.ShouldUsePerMachine(
                    Constants.ExtraFeaturesPerMachine);

                result = null;

                if (GetExtraFeatures(interpreter,
                        pluginData, hashAlgorithmName, null, null,
                        null, null, CertificateDataOps.GetRawEncoding(),
                        null, Constants.ProtectExtraFeatures, perMachine,
                        ref features, ref result) == ReturnCode.Ok)
                {
                    CertificateGlobalState.SetExtraFeatures(features);
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: GetExtraFeatures {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

#if false
                features = Features.AnyDebuggerOk;
                result = null;

                if (SetExtraFeatures(interpreter,
                        pluginData, hashAlgorithmName, null, null,
                        null, null, CertificateDataOps.GetRawEncoding(),
                        null, Constants.ProtectExtraFeatures, perMachine,
                        ref features, ref result) == ReturnCode.Ok)
                {
                    CertificateGlobalState.SetExtraFeatures(features);
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: SetExtraFeatures {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif
#endif
            }

            ///////////////////////////////////////////////////////////////

            if (features != null)
            {
                result = null;

                if (HasExtraFeatures(
                        features, Features.AnyOrNativeDebuggerOkOrAll,
                        false, ref result))
                {
                    nativeDebuggerOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures1 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.AnyOrManagedDebuggerOkOrAll,
                        false, ref result))
                {
                    managedDebuggerOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {

                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures2 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.NoStrongNameOrAll,
                        false, ref result))
                {
                    noStrongNameOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures3 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.NoVerifiedOrAll,
                        false, ref result))
                {
                    noVerifiedOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures4 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.NoCertificateOrAll,
                        false, ref result))
                {
                    noCertificateOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures5 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.SkipCertificateOrAll,
                        false, ref result))
                {
                    skipCertificateOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures6 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.NoTrustedOrAll,
                        false, ref result))
                {
                    noTrustedOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures7 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.PromotionalOrAll,
                        false, ref result))
                {
                    promotionalOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures8 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.EnableTestModeOrAll,
                        false, ref result))
                {
                    enableTestModeOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures9 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

                result = null;

                if (HasExtraFeatures(
                        features, Features.AsynchronousLicensingOrAll,
                        false, ref result))
                {
                    asynchronousLicensingOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures10 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif

                ///////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                result = null;

                if (HasExtraFeatures(
                        features, Features.RelaxedSecretsKeyUsageOrAll,
                        false, ref result))
                {
                    relaxedSecretsKeyUsageOk = true;
                }
#if DEBUG || FORCE_TRACE
                else
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: HasExtraFeatures11 {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificatePluginOps).Name,
                        TracePriority.Lower);
                }
#endif
#endif
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////
            // **************** PHASE 0.5: SHOW EXTRA FEATURES ****************
            ///////////////////////////////////////////////////////////////////

            #region Phase 0.5: Show Extra Features
#if DEBUG || FORCE_TRACE
            StringDictionary flags = new StringDictionary();

            flags.Add("nativeDebuggerOk", nativeDebuggerOk.ToString());
            flags.Add("managedDebuggerOk", managedDebuggerOk.ToString());
            flags.Add("noStrongNameOk", noStrongNameOk.ToString());
            flags.Add("noVerifiedOk", noVerifiedOk.ToString());
            flags.Add("noCertificateOk", noCertificateOk.ToString());
            flags.Add("skipCertificateOk", skipCertificateOk.ToString());
            flags.Add("noTrustedOk", noTrustedOk.ToString());
            flags.Add("promotionalOk", promotionalOk.ToString());
            flags.Add("enableTestModeOk", enableTestModeOk.ToString());

            flags.Add("relaxedSecretsKeyUsageOk",
                relaxedSecretsKeyUsageOk.ToString());

            flags.Add("asynchronousLicensingOk",
                asynchronousLicensingOk.ToString());

            CertificateTraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "InnerCheck: FLAGS {0}",
                    flags.KeysAndValuesToString(null, false)),
                typeof(CertificatePluginOps).Name,
                TracePriority.MediumLow, 0);
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ******************* PHASE 1: NATIVE DEBUGGER *******************
            ///////////////////////////////////////////////////////////////////

            #region Phase 1: Native Debugger
#if NATIVE && WINDOWS
            if (Utility.IsDebuggerPresent())
            {
                result = "native method forbidden by license";

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: IsDebuggerPresent {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificatePluginOps).Name,
                    nativeDebuggerOk ? priority1 : priority2);
#endif

#if !DEBUG
                if (!nativeDebuggerOk)
                {
                    error = result;
                    return ReturnCode.Error;
                }
#endif
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ****************** PHASE 2: MANAGED DEBUGGER *******************
            ///////////////////////////////////////////////////////////////////

            #region Phase 2: Managed Debugger
            if (Debugger.IsAttached)
            {
                result = "managed method forbidden by license";

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: Debugger.IsAttached {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificatePluginOps).Name,
                    managedDebuggerOk ? priority1 : priority2);
#endif

#if !DEBUG
                if (!managedDebuggerOk)
                {
                    error = result;
                    return ReturnCode.Error;
                }
#endif
            }
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ***** PHASE 2.5: CORE LIBRARY STRONG NAME EVIDENCE DEMAND ******
            ///////////////////////////////////////////////////////////////////

            #region Phase 2.5: Core Library Strong Name Evidence Demand
            try
            {
                if (interpreter == null)
                    interpreter = Interpreter.GetAny();

                if (interpreter != null)
                {
                    try
                    {
                        interpreter.DemandStrongName(); /* throw */
                    }
                    catch (NotImplementedException)
                    {
                        // do nothing.
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: DemandStrongName {0}",
                    Utility.FormatWrapOrNull(e)),
                    typeof(CertificatePluginOps).Name,
                    noStrongNameOk ? priority1 : priority2);
#endif

#if !DEBUG
                if (!noStrongNameOk)
                {
                    error = e;
                    return ReturnCode.Error;
                }
#endif
            }
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ************** PHASE 3: PLUGIN ASSEMBLY FILE NAME **************
            ///////////////////////////////////////////////////////////////////

            #region Phase 3: Plugin Assembly File Name
            string fileName = Utility.GetAssemblyLocation();
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ***** PHASE 3.5: CORE LIBRARY STRONG NAME SIGNATURE CHECK ******
            ///////////////////////////////////////////////////////////////////

            #region Phase 3.5: Core Library Strong Name Signature Check
            if (!Utility.IsFileStrongNameVerified(fileName))
            {
                result = "core library assembly file is not verified";

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: IsFileStrongNameVerified {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificatePluginOps).Name,
                    noVerifiedOk ? priority1 : priority2);
#endif

#if !DEBUG
                if (!noVerifiedOk)
                {
                    error = result;
                    return ReturnCode.Error;
                }
#endif
            }
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ****** PHASE 4: CORE LIBRARY AUTHENTICODE EVIDENCE DEMAND ******
            ///////////////////////////////////////////////////////////////////

            #region Phase 4: Core Library Authenticode Evidence Demand
            if (!skipCertificateOk ||
                !Utility.IsFileTrusted(interpreter, fileName))
            {
                try
                {
                    if (interpreter == null)
                        interpreter = Interpreter.GetAny();

                    if (interpreter != null)
                    {
                        try
                        {
                            interpreter.DemandCertificate(); /* throw */
                        }
                        catch (NotImplementedException)
                        {
                            // do nothing.
                        }
                    }
                }
                catch (Exception e)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "InnerCheck: DemandCertificate {0}",
                        Utility.FormatWrapOrNull(e)),
                        typeof(CertificatePluginOps).Name,
                        noCertificateOk ? priority1 : priority3);
#endif

#if !DEBUG
                    if (!noCertificateOk)
                    {
                        error = e;
                        return ReturnCode.Error;
                    }
#endif
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ***** PHASE 4.5: CORE LIBRARY AUTHENTICODE SIGNATURE CHECK *****
            ///////////////////////////////////////////////////////////////////

            #region Phase 4.5: Core Library Authenticode Signature Check
            if (!Utility.IsFileTrusted(interpreter, fileName))
            {
                result = "core library assembly file is not trusted";

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: IsFileTrusted {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificatePluginOps).Name,
                    noTrustedOk ? priority1 : priority3);
#endif

#if !DEBUG
                if (!noTrustedOk)
                {
                    error = result;
                    return ReturnCode.Error;
                }
#endif
            }
            #endregion

            ///////////////////////////////////////////////////////////////////
            // ********* PHASE 5: MATCHING AUTHENTICODE CERTIFICATES **********
            ///////////////////////////////////////////////////////////////////

            #region Phase 5: Matching Authenticode Certificates
            result = null;

            if (!skipCertificateOk &&
                VerifyAssemblyCertificates(ref result) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "InnerCheck: VerifyAssemblyCertificates {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificatePluginOps).Name,
                    noCertificateOk ? priority1 : priority3);
#endif

#if !DEBUG
                if (!noCertificateOk)
                {
                    error = result;
                    return ReturnCode.Error;
                }
#endif
            }
            #endregion
#endif

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the licensing integrity checks for the specified plugin,
        /// setting up the active interpreter context and optional license
        /// logging before delegating to <see cref="InnerCheck" />.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter context; may be null.
        /// </param>
        /// <param name="plugin">
        /// The optional plugin being checked; may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the failed check.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if all checks pass; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Check(
            Interpreter interpreter, /* in, OPTIONAL: May be null. */
            IPlugin plugin,          /* in, OPTIONAL: May be null. */
            ref Result error         /* out */
            )
        {
            ILogClientData logClientData = null;

            try
            {
                if (Configuration.DoesVariableExist(
                        Constants.ForceLogLicenseEnvVarName))
                {
                    logClientData = new ScriptLogClientData(
                        interpreter, plugin, null, PolicyType.License,
                        null);
                }

                try
                {
                    Utility.PushActiveInterpreter(
                        interpreter, logClientData);

                    return InnerCheck(
                        interpreter, plugin, ref error);
                }
                finally
                {
                    Utility.PopActiveInterpreter();
                }
            }
            finally
            {
                if (logClientData != null)
                {
                    logClientData.Dispose();
                    logClientData = null;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Summary Support
        /// <summary>
        /// Determines whether the specified certificate represents a
        /// supported enterprise product, and if so produces the product
        /// name, appending the support suffix when the support feature is
        /// present.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to examine.
        /// </param>
        /// <param name="product">
        /// Upon success, receives the resolved product name, optionally
        /// including the support suffix.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate represents a known, supported product;
        /// otherwise, zero.
        /// </returns>
        private static bool CheckForSupport(
            ICertificate certificate, /* in */
            ref string product        /* out */
            )
        {
            if (certificate == null)
                return false;

            if (!CertificateDataOps.MatchPublicKeyToken(
                    certificate.Key, Constants.EnterprisePublicKeyTokenBytes))
            {
                return false;
            }

            long flagsKey = Utility.DefaultAttributeFlagsKey();

            if (SharedOps.MatchFlags(
                    certificate, FlagType.Restriction,
                    flagsKey, null, Restrictions.Engineering,
                    false, false, true) != ReturnCode.Ok)
            {
                return false;
            }

            string suffix = null;

            if (SharedOps.MatchFlags(
                    certificate, FlagType.Feature,
                    flagsKey, Features.SupportOrAll, null,
                    false, false, true) == ReturnCode.Ok)
            {
                suffix = Constants.SupportProductSuffix;
            }

            string localProduct = certificate.Product;

            if (localProduct != null)
            {
                StringDictionary products = Constants.Products;

                if ((products != null) &&
                    products.ContainsKey(localProduct))
                {
                    if (!String.IsNullOrEmpty(suffix))
                        product = localProduct + suffix;
                    else
                        product = localProduct;

                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines, from the relevant environment configuration variables,
        /// whether restriction flags should be excluded from and whether
        /// feature flags should be included in the certificate summary.
        /// </summary>
        /// <param name="excludeRestrictions">
        /// Upon return, non-zero if restriction flags should be excluded from
        /// the summary.
        /// </param>
        /// <param name="includeFeatures">
        /// Upon return, non-zero if feature flags should be included in the
        /// summary.
        /// </param>
        private static void GetToListFlags(
            out bool excludeRestrictions, /* out */
            out bool includeFeatures      /* out */
            )
        {
            if (Configuration.DoesVariableExist(
                    Constants.NoCertificateSummaryRestrictionsEnvVarName))
            {
                excludeRestrictions = true;
            }
            else
            {
                excludeRestrictions = false;
            }

            if (Configuration.DoesVariableExist(
                    Constants.CertificateSummaryFeaturesEnvVarName))
            {
                includeFeatures = true;
            }
            else
            {
                includeFeatures = false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines a human-readable signature algorithm name for the
        /// specified certificate, derived from the matching key pair when
        /// available, or falling back to the hard-coded public key algorithm
        /// name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when resolving key pairs.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to obtain the assembly public key when no
        /// certificate key pair is available.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose signature algorithm name is to be
        /// determined.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when resolving the certificate key pair.
        /// </param>
        /// <returns>
        /// A string describing the signature algorithm, such as the key pair
        /// type and bit length, or the default public key algorithm name.
        /// </returns>
        private static string GetSignatureAlgorithmName(
            Interpreter interpreter,  /* in */
            IPluginData pluginData,   /* in */
            ICertificate certificate, /* in */
            PolicyType policyType     /* in */
            )
        {
            #region Full Algorithm Name with Plugin Commands
#if CERTIFICATE_POLICY && PLUGIN_COMMANDS
            if (certificate != null)
            {
                string keyRingName = CertificateKeyRingOps.GetName(
                    null, policyType); /* EXEMPT */

                string objectName = CertificateDataOps.FormatPublicKeyToken(
                    certificate.Key, false, false);

                IKeyPair keyPair = null;

                if ((CertificateKeyPairOps.GetOne( /* OK */
                        keyRingName, policyType, false,
                        CertificateAssemblyOps.GetObject(),
                        CertificateAssemblyOps.GetName(),
                        interpreter, objectName, false, false,
                        ref keyPair) == ReturnCode.Ok) &&
                    (keyPair != null))
                {
                    return String.Format(
                        "{0}-{1}", keyPair.KeyPairType.ToString(),
                        keyPair.BitLength);
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Full Algorithm Name with Plugin Data
            if (pluginData != null)
            {
                Assembly assembly = SharedOps.IsCrossAppDomain(
                    interpreter, pluginData) ? null : pluginData.Assembly;

                IKeyPair keyPair = null;

                if ((CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                        assembly, pluginData.AssemblyName,
                        ref keyPair) == ReturnCode.Ok) &&
                    (keyPair != null))
                {
                    return String.Format(
                        "{0}-{1}", keyPair.KeyPairType.ToString(),
                        keyPair.BitLength);
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: This is hard-coded.
            //
            return Constants.PublicKeyAlgorithmName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Examines the specified feature flags and appends a human-readable
        /// description for each recognized feature to the supplied list,
        /// using either the global or non-global feature prefix.
        /// </summary>
        /// <param name="features">
        /// The feature flags string to examine.
        /// </param>
        /// <param name="key">
        /// The attribute flags key used when matching the feature flags.
        /// </param>
        /// <param name="withGlobal">
        /// Non-zero to use the global feature prefix; zero to use the regular
        /// feature prefix.
        /// </param>
        /// <param name="specificOnly">
        /// Non-zero to match only the specific feature flag; zero to also
        /// match the corresponding "or all" flag.
        /// </param>
        /// <param name="list">
        /// The list to which feature descriptions are appended; created if
        /// null.
        /// </param>
        private static void FeaturesToList(
            string features,        /* in */
            long key,               /* in */
            bool withGlobal,        /* in */
            bool specificOnly,      /* in */
            ref StringPairList list /* in, out */
            )
        {
            string prefix = withGlobal ?
                Constants.WithGlobalFeature : Constants.WithFeature;

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key,
                    Features.All, null, false, false,
                    true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ALL FEATURES");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key,
                    Features.Vendor, null, false, false,
                    true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "VENDOR PARTNER");
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.Support : Features.SupportOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "SUPPORT CONTRACT");
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.Renewal : Features.RenewalOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "AUTOMATIC RENEWAL");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoSubject : Features.NoSubjectOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO X509 SUBJECT");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoStrongName : Features.NoStrongNameOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO STRONG NAME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoVerified : Features.NoVerifiedOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO STRONG NAME VERIFIED");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoCertificate : Features.NoCertificateOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO X509 CERTIFICATE");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.SkipCertificate : Features.SkipCertificateOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "SKIP X509 CERTIFICATE");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoTrusted : Features.NoTrustedOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO X509 CERTIFICATE TRUSTED");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.NoNetworkTime : Features.NoNetworkTimeOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO NETWORK TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.CreatedAnyTime : Features.CreatedAnyTimeOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "CREATED ANY TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.Promotional : Features.PromotionalOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NO PROMOTIONAL CHECK");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.RelaxedNetworkTime :
                        Features.RelaxedNetworkTimeOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "RELAXED NETWORK TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.RelaxedRevocation :
                        Features.RelaxedRevocationOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "RELAXED NETWORK REVOCATION");
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.WellKnownNeverExpired :
                        Features.WellKnownNeverExpiredOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "WELL KNOWN NEVER EXPIRED");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.EnablePolicyTracing :
                        Features.EnablePolicyTracingOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ENABLE POLICY TRACING");
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                    Features.AnyDebuggerOk : Features.AnyDebuggerOkOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ANY DEBUGGER OK");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.NativeDebuggerOk :
                        Features.NativeDebuggerOkOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NATIVE DEBUGGER OK");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.ManagedDebuggerOk :
                        Features.ManagedDebuggerOkOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "MANAGED DEBUGGER OK");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.EnableCreation :
                        Features.EnableCreationOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ENABLE INTERPRETER CREATION");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.EnableTestMode :
                        Features.EnableTestModeOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ENABLE TEST MODE");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.SkipAuthorization :
                        Features.SkipAuthorizationOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "SKIP AUTHORIZATION");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.AutoProvision :
                        Features.AutoProvisionOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "AUTOMATIC PROVISIONING");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.ForceConfiguration :
                        Features.ForceConfigurationOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FORCE CONFIGURATION");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.UseVersionForExpiration :
                        Features.UseVersionForExpirationOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "USE VERSION FOR EXPIRATION");
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.RelaxedSecretsKeyUsage :
                        Features.RelaxedSecretsKeyUsageOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "RELAXED SECRETS KEY USAGE");
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.AsynchronousLicensing :
                        Features.AsynchronousLicensingOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "ASYNCHRONOUS LICENSING");
            }

            ///////////////////////////////////////////////////////////////////

            if ((features != null) && (SharedOps.MatchFlags(
                    features, FlagType.Feature, key, specificOnly ?
                        Features.SkipFailSafeMode :
                        Features.SkipFailSafeModeOrAll,
                    null, false, false, true) == ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "SKIP SDK FAIL-SAFE");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Examines the specified restriction flags and appends a
        /// human-readable description for each recognized restriction to the
        /// supplied list, using either the global or non-global restriction
        /// prefix.
        /// </summary>
        /// <param name="restrictions">
        /// The restriction flags string to examine.
        /// </param>
        /// <param name="key">
        /// The attribute flags key used when matching the restriction flags.
        /// </param>
        /// <param name="quantity">
        /// The quantity value included in the limited quantity restriction
        /// description.
        /// </param>
        /// <param name="withGlobal">
        /// Non-zero to use the global restriction prefix; zero to use the
        /// regular restriction prefix.
        /// </param>
        /// <param name="specificOnly">
        /// Reserved for symmetry with feature matching.  This parameter is
        /// not used.
        /// </param>
        /// <param name="list">
        /// The list to which restriction descriptions are appended; created
        /// if null.
        /// </param>
        private static void RestrictionsToList(
            string restrictions,    /* in */
            long key,               /* in */
            long quantity,          /* in */
            bool withGlobal,        /* in */
            bool specificOnly,      /* in: NOT USED */
            ref StringPairList list /* in, out */
            )
        {
            string prefix = withGlobal ?
                Constants.WithGlobalRestriction : Constants.WithRestriction;

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.Activation, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "REQUIRES ACTIVATION");
            }

            ///////////////////////////////////////////////////////////////////

#if FOR_TEST_USE_ONLY
            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.Test, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FOR TEST USE ONLY");
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.Engineering, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FOR ENGINEERING USE ONLY");
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.Promotional, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FOR PROMOTIONAL USE ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.Revocation, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "NETWORK REVOCATION");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.LimitedQuantity, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, String.Format(
                    "LIMITED QUANTITY ({0})", quantity));
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.ForceNetworkTime, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FORCE NETWORK TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.StrictNetworkTime, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "STRICT NETWORK TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.HttpNetworkTime, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "HTTP NETWORK TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.ExpiredFromInstall, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "EXPIRED FROM INSTALL");
            }

            ///////////////////////////////////////////////////////////////////

            if ((restrictions != null) && (SharedOps.MatchFlags(
                    restrictions, FlagType.Restriction, key, null,
                    Restrictions.FullyTrustedKey, false, false,
                    true) != ReturnCode.Ok))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(prefix, "FULLY TRUSTED KEY");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the list of name and value pairs that summarizes the
        /// specified certificate, using a temporary item count.  This is a
        /// convenience overload of <c>ToList</c> that discards the
        /// item count.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when resolving certificate details.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used when resolving certificate details.
        /// </param>
        /// <param name="certificate">
        /// The certificate to summarize.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when resolving the signature algorithm.
        /// </param>
        /// <param name="excludeRestrictions">
        /// Non-zero to exclude restriction flags from the summary.
        /// </param>
        /// <param name="includeFeatures">
        /// Non-zero to include feature flags in the summary.
        /// </param>
        /// <param name="list">
        /// The list to which the summary pairs are appended; created if null.
        /// </param>
        private static void ToList(
            Interpreter interpreter,  /* in */
            IPluginData pluginData,   /* in */
            ICertificate certificate, /* in */
            PolicyType policyType,    /* in */
            bool excludeRestrictions, /* in */
            bool includeFeatures,     /* in */
            ref StringPairList list   /* in, out */
            )
        {
            int count = 0;

            ToList(
                interpreter, pluginData, certificate, policyType,
                excludeRestrictions, includeFeatures, ref list,
                ref count);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the list of name and value pairs that summarizes the
        /// specified certificate, including subject and issuer information,
        /// important flags, key and number, creation and expiration, hash and
        /// signature algorithms, and product details.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when resolving certificate details.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used when resolving certificate details.
        /// </param>
        /// <param name="certificate">
        /// The certificate to summarize.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when resolving the signature algorithm.
        /// </param>
        /// <param name="excludeRestrictions">
        /// Non-zero to exclude restriction flags from the summary.
        /// </param>
        /// <param name="includeFeatures">
        /// Non-zero to include feature flags in the summary.
        /// </param>
        /// <param name="list">
        /// The list to which the summary pairs are appended; created if null.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of summary items added to the list.
        /// </param>
        private static void ToList(
            Interpreter interpreter,  /* in */
            IPluginData pluginData,   /* in */
            ICertificate certificate, /* in */
            PolicyType policyType,    /* in */
            bool excludeRestrictions, /* in */
            bool includeFeatures,     /* in */
            ref StringPairList list,  /* in, out */
            ref int count             /* in, out */
            )
        {
            if (certificate == null)
                return;

            ///////////////////////////////////////////////////////////////////

            #region Subject & Issuer
            string entityName = certificate.EntityName;

            if (!String.IsNullOrEmpty(entityName))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Licensed To", entityName);

                count++;
            }

            string vendor = certificate.Vendor;

            if (!String.IsNullOrEmpty(vendor))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Issued By", vendor);

                count++;
            }

            string type = certificate.Type;

            if (!String.IsNullOrEmpty(type))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Type", type);

                count++;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Important Flags
            long flagsKey = Utility.DefaultAttributeFlagsKey();

            ///////////////////////////////////////////////////////////////////

#if !LIMITED_EDITION
            #region Important Global Feature Flags
            if (includeFeatures)
            {
                if (CertificateAssemblyOps.MatchObjectOrName(pluginData))
                {
                    FeaturesToList(
                        CertificateGlobalState.GetExtraFeatures(),
                        flagsKey, true, true, ref list);
                }
            }
            #endregion
#endif

            ///////////////////////////////////////////////////////////////////

            #region Important Restriction Flags
            if (!excludeRestrictions)
            {
                RestrictionsToList(
                    certificate.Restrictions, flagsKey,
                    certificate.Quantity, false, true, ref list);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Important Feature Flags
            if (includeFeatures)
            {
                FeaturesToList(
                    certificate.Features, flagsKey, false, true,
                    ref list);
            }
            #endregion
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Key & Number
            byte[] key = certificate.Key;

            if ((key != null) && (key.Length > 0))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("With Public Key",
                    CertificateDataOps.FormatPublicKeyToken(
                        key, false, false));

                count++;
            }

            ulong number = certificate.Number;

            if (number != 0)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Identified By Number",
                    CertificateDataOps.FormatHexadecimal(number));

                count++;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Creation & Expiration
            DateTime created = certificate.TimeStamp;

            if (created != DateTime.MinValue)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Created On",
                    CertificateDataOps.FormatTimeStamp(created));

                count++;

                TimeSpan duration = certificate.Duration;

                if ((duration != TimeSpan.Zero) &&
                    (duration.Ticks > 0))
                {
                    DateTime expired = created.Add(duration);

                    list.Add("Expires On",
                        CertificateDataOps.FormatTimeStamp(expired));

                    count++;
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Hash & Signature
            string hashAlgorithmName = certificate.HashAlgorithm;

            if (!String.IsNullOrEmpty(hashAlgorithmName))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Hash Algorithm", hashAlgorithmName);

                count++;
            }

            byte[] signature = certificate.Signature;

            if ((signature != null) && (signature.Length > 0))
            {
                string signatureAlgorithmName = GetSignatureAlgorithmName(
                    interpreter, pluginData, certificate, policyType);

                if (list == null)
                    list = new StringPairList();

                list.Add("Signature Algorithm", signatureAlgorithmName);

                count++;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Product & Support
            string product = null;

            if (!CheckForSupport(certificate, ref product))
                product = null;

            if (!String.IsNullOrEmpty(product))
            {
                if (list == null)
                    list = new StringPairList();

                list.Add("Product", product);

                count++;
            }
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the base length, in characters, of the
        /// trailing separator line already present in the specified string
        /// builder.
        /// </summary>
        /// <param name="builder">
        /// The string builder whose existing content is examined.
        /// </param>
        /// <param name="baseLength">
        /// Upon success, receives the length of the existing separator;
        /// otherwise, zero.
        /// </param>
        /// <returns>
        /// Non-zero if a separator was found and its base length determined;
        /// otherwise, zero.
        /// </returns>
        private static bool TryGetBaseLength(
            StringBuilder builder, /* in */
            out int baseLength     /* out */
            )
        {
            if (builder == null)
            {
                baseLength = 0;
                return false;
            }

            string value = builder.ToString();

            if (String.IsNullOrEmpty(value))
            {
                baseLength = 0;
                return false;
            }

            int startIndex = value.IndexOf(Constants.BaseLengthSeparator);

            if (startIndex == Index.Invalid)
            {
                baseLength = 0;
                return false;
            }

            int index = startIndex + Constants.BaseLengthSeparator.Length;

            for (; index < value.Length; index++)
                if (value[index] != Characters.MinusSign)
                    break;

            baseLength = index - startIndex;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the maximum rendered length, in characters, of any name
        /// and value pair in the specified list, accounting for the separator
        /// inserted between a name and its value.
        /// </summary>
        /// <param name="list">
        /// The list of name and value pairs to examine.
        /// </param>
        /// <param name="maximumLength">
        /// Upon return, receives the maximum rendered length found, or zero
        /// when the list is null or empty.
        /// </param>
        /// <returns>
        /// Non-zero if the list was examined; zero if the list was null.
        /// </returns>
        private static bool TryGetMaximumLength(
            StringPairList list,  /* in */
            out int maximumLength /* out */
            )
        {
            maximumLength = 0;

            if (list == null)
                return false;

            foreach (IPair<string> pair in list)
            {
                string name = pair.X;

                if (name != null)
                    name = name.Trim();

                string value = pair.Y;

                if (value != null)
                    value = value.Trim();

                bool haveName = !String.IsNullOrEmpty(name);
                bool haveValue = !String.IsNullOrEmpty(value);

                if (!haveName && !haveValue)
                    continue;

                int length = 0;

                if (haveName)
                {
                    length += name.Length;

                    if (haveValue)
                        length += 2; /* ": " */
                }

                if (haveValue)
                    length += value.Length;

                if ((maximumLength == 0) || (length > maximumLength))
                    maximumLength = length;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a separator line, consisting of a tab followed by a run of
        /// minus signs, to the specified string builder.
        /// </summary>
        /// <param name="builder">
        /// The string builder to which the separator line is appended.
        /// </param>
        /// <param name="baseLength">
        /// The desired length of the separator, clamped to the configured
        /// minimum summary length.
        /// </param>
        private static void AppendSeparatorLine(
            StringBuilder builder, /* in, out */
            int baseLength         /* in */
            )
        {
            if ((builder != null) && (baseLength > 0))
            {
                builder.Append(Environment.NewLine);
                builder.Append(Characters.HorizontalTab);
                builder.Append(Characters.MinusSign, Math.Max(
                    baseLength, Constants.MinimumSummaryLength));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a human-readable, separator-delimited summary of the
        /// specified certificate to the supplied string builder.  This is the
        /// legacy, plain-text summary format.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when building the summary.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used when building the summary.
        /// </param>
        /// <param name="certificate">
        /// The certificate to summarize.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when resolving the signature algorithm.
        /// </param>
        /// <param name="builder">
        /// The string builder to which the summary is appended; created if
        /// null.
        /// </param>
        private static void AppendSummary( /* HUMAN READABLE: LEGACY */
            Interpreter interpreter,  /* in */
            IPluginData pluginData,   /* in */
            ICertificate certificate, /* in */
            PolicyType policyType,    /* in */
            ref StringBuilder builder /* in, out */
            )
        {
            if (certificate == null)
                return;

            bool excludeRestrictions;
            bool includeFeatures;

            GetToListFlags(out excludeRestrictions, out includeFeatures);

            StringPairList list = null;

            ToList(
                interpreter, pluginData, certificate, policyType,
                excludeRestrictions, includeFeatures, ref list);

            if ((list != null) && (list.Count > 0))
            {
                if (builder == null)
                    builder = new StringBuilder();

                int baseLength;

                if (!TryGetBaseLength(builder, out baseLength))
                {
                    int length = builder.Length;

                    baseLength = (length > 0) ? length - 1 : 0;
                }

                int maximumLength;

                if (TryGetMaximumLength(list, out maximumLength))
                    maximumLength = Math.Max(baseLength, maximumLength);
                else
                    maximumLength = baseLength;

                int count = 0;

                AppendSeparatorLine(builder, maximumLength);

                foreach (IPair<string> pair in list)
                {
                    string name = pair.X;

                    if (name != null)
                        name = name.Trim();

                    string value = pair.Y;

                    if (value != null)
                        value = value.Trim();

                    bool haveName = !String.IsNullOrEmpty(name);
                    bool haveValue = !String.IsNullOrEmpty(value);

                    if (!haveName && !haveValue)
                        continue;

                    if (builder.Length > 0)
                        builder.Append(Environment.NewLine);

                    builder.Append(Characters.HorizontalTab);

                    if (haveName)
                    {
                        builder.Append(name);

                        count++;

                        if (haveValue)
                            builder.Append(": ");
                    }

                    if (haveValue)
                    {
                        builder.Append(value);

                        count++;
                    }
                }

                if (count > 0)
                    AppendSeparatorLine(builder, maximumLength);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Augments the specified result with a human-readable summary of the
        /// certificate, either as appended text or as a list of name and
        /// value pairs, unless suppressed by the relevant configuration
        /// variables.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when building the summary.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used when building the summary.
        /// </param>
        /// <param name="certificate">
        /// The certificate to summarize.
        /// </param>
        /// <param name="result">
        /// On input, the existing result to augment; on output, the result
        /// with the certificate summary applied.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" /> when the certificate is invalid.
        /// </returns>
        public static ReturnCode About( /* HUMAN READABLE */
            Interpreter interpreter,  /* in */
            IPluginData pluginData,   /* in */
            ICertificate certificate, /* in */
            ref Result result         /* in, out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            if (!Configuration.DoesVariableExist(
                    Constants.NoCertificateSummaryEnvVarName))
            {
                if (!Configuration.DoesVariableExist(
                        Constants.CertificateSummaryPairsEnvVarName))
                {
                    StringBuilder builder = new StringBuilder(result);

                    AppendSummary(
                        interpreter, pluginData, certificate,
                        PolicyType.License, ref builder);

                    result = builder;
                }
                else
                {
                    bool excludeRestrictions;
                    bool includeFeatures;

                    GetToListFlags(
                        out excludeRestrictions, out includeFeatures);

                    StringPairList list = new StringPairList();
                    int count = 0;

                    ToList(
                        interpreter, pluginData, certificate,
                        PolicyType.License, excludeRestrictions,
                        includeFeatures, ref list, ref count);

                    if (result != null)
                    {
                        list.Insert(0, new StringPair(result));

                        if (count > 0)
                        {
                            list.Insert(1, null);

                            count++;
                        }

                        count++;
                    }

                    result = list;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces a human-readable "about" result for the specified plugin,
        /// combining the formatted plugin information with a summary of the
        /// plugin's certificate, falling back to the plugin information alone
        /// on error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used when building the summary.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose "about" information is produced.
        /// </param>
        /// <returns>
        /// A result containing the plugin "about" text, including the
        /// certificate summary when available.
        /// </returns>
        public static Result About(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            StringBuilder builder = new StringBuilder(
                Utility.FormatPluginAbout(pluginData, true));

            ICertificate certificate = SharedOps.GetViaPlugin(pluginData);
            Result result = builder;

            if (About(
                    interpreter, pluginData, certificate,
                    ref result) == ReturnCode.Ok)
            {
                return result;
            }
            else
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "About certificate {0} for plugin {1} error: {2}",
                        DebugOnlyOps.FormatCertificate(certificate),
                        Utility.FormatWrapOrNull(pluginData),
                        Utility.FormatWrapOrNull(true, false, result)),
                    typeof(CertificatePluginOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                //
                // HACK: Fallback to just leaving out the certificate
                //       information.  This should never happen when
                //       in production.
                //
                return builder;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Options Support
        /// <summary>
        /// Gets the list of compile-time define constants that were in effect
        /// when the assembly was built.
        /// </summary>
        /// <param name="result">
        /// Upon success, receives the list of define constants; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" /> when the define constants are not
        /// available.
        /// </returns>
        public static ReturnCode GetDefineConstants(
            ref Result result /* out */
            )
        {
            StringList list = DefineConstants.OptionList;

            if (list != null)
            {
                result = new StringList(list, false);
                return ReturnCode.Ok;
            }
            else
            {
                result = "define constants not available";
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified compile-time define constant was
        /// in effect when the assembly was built.
        /// </summary>
        /// <param name="name">
        /// The name of the define constant to check for.
        /// </param>
        /// <returns>
        /// Non-zero if the define constant is present; otherwise, zero.
        /// </returns>
        public static bool HaveDefineConstant(
            string name /* in */
            )
        {
            if (name == null)
                return false;

            StringList list = DefineConstants.OptionList;

            if (list == null)
                return false;

            return list.Contains(name, StringComparison.Ordinal);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the sorted, non-empty compile-time define constants to the
        /// specified list.
        /// </summary>
        /// <param name="list">
        /// The list to which the define constants are appended; created if
        /// null.
        /// </param>
        public static void AddDefineConstants(
            ref StringList list /* in, out */
            )
        {
            StringList localList = DefineConstants.OptionList;

            if (localList != null)
            {
                localList = new StringList(localList);
                localList.Sort(); /* O(N log N) */

                foreach (string element in localList)
                {
                    if (String.IsNullOrEmpty(element))
                        continue;

                    if (list == null)
                        list = new StringList();

                    list.Add(element);
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Support
#if CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the specified plugin data is an instance of the
        /// <see cref="Security.Core" /> plugin type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to test.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin data is the security core plugin;
        /// otherwise, zero.
        /// </returns>
        public static bool IsSecurityCore(
            IPluginData pluginData /* in */
            )
        {
            if (pluginData == null)
                return false;

            return Object.ReferenceEquals(
                pluginData.GetType(), typeof(Security.Core));
        }
#endif
        #endregion
    }
}
