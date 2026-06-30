/*
 * CertificateLicenseState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Interfaces.Public;
using _Utility = Eagle._Components.Public.Utility;
using This = Licensing.Components.Private.CertificateLicenseState;

using CertificateDictionary = System.Collections.Generic.Dictionary<
    System.Guid, Licensing.Interfaces.Public.ICertificate>;

using BinaryFileDictionary = System.Collections.Generic.Dictionary<string, byte[]>;
using TextFileDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-process licensing state used by the license
    /// certificate verification subsystem, including the loaded license
    /// certificate, the verified certificate cache, skip settings, file
    /// caches, and the path and network flags.
    /// </summary>
    [ObjectId("ce09b91f-9987-48f5-bbb7-908b0c0f2fc2")]
    internal static class CertificateLicenseState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private key ring
        //       and key pair data in this class (i.e. which is used by the
        //       policy subsystem).
        //
        /// <summary>
        /// Synchronizes access to the private key ring and key pair data
        /// in this class, which is used by the policy subsystem.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of the license certificate file name
        //       that was used to load this assembly (i.e. Harpy).
        //
        /// <summary>
        /// The license certificate file name that was used to load this
        /// assembly (i.e. Harpy).
        /// </summary>
        private static string fileName = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of the license certificate that was
        //       used to load this assembly (i.e. Harpy).
        //
        /// <summary>
        /// The license certificate that was used to load this assembly
        /// (i.e. Harpy).
        /// </summary>
        private static ICertificate certificate = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of all license certificates that were
        //       successfully verified by this assembly.
        //
        /// <summary>
        /// All license certificates that were successfully verified by
        /// this assembly, keyed by their unique identifier.
        /// </summary>
        private static CertificateDictionary certificates = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this is non-zero, all expiration date and revocation
        //       checking for licenses will require network access.
        //
        /// <summary>
        /// When non-zero, all expiration date and revocation checking for
        /// licenses will require network access.
        /// </summary>
        private static bool forceNetwork = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this field is greater than zero, license checks will be
        //       skipped for the Harpy assembly itself.
        //
        /// <summary>
        /// When greater than zero, license checks will be skipped for the
        /// Harpy assembly itself.
        /// </summary>
        private static int skipCount = 0;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the licensing types that may skip license checks
        //       (if skipping license checks is enabled for this module and
        //       other various other restrictions are met).
        //
        /// <summary>
        /// The licensing types that may skip license checks when skipping
        /// license checks is enabled for this module and other various
        /// restrictions are met.
        /// </summary>
        private static LicenseType skipTypes = LicenseType.None;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the cached bytes of "files" that (may) have been
        //       read from disk -OR- obtained via other means, e.g. through
        //       a configuration command, etc.
        //
        /// <summary>
        /// The cached bytes of "files" that may have been read from disk
        /// or obtained via other means, e.g. through a configuration
        /// command.
        /// </summary>
        private static BinaryFileDictionary binaryFileCache = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the cached texts of "files" that (may) have been
        //       read from disk -OR- obtained via other means, e.g. through
        //       a configuration command, etc.
        //
        /// <summary>
        /// The cached texts of "files" that may have been read from disk
        /// or obtained via other means, e.g. through a configuration
        /// command.
        /// </summary>
        private static TextFileDictionary textFileCache = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the (path) flags used machine identifiers within
        //       the license verification subsystem, e.g. in support of the
        //       automatic provisioning feature, et al.  When this value is
        //       null, a suitable default value may be used.
        //
        /// <summary>
        /// The (path) flags used for machine identifiers within the
        /// license verification subsystem, e.g. in support of the
        /// automatic provisioning feature; when null, a suitable default
        /// value may be used.
        /// </summary>
        private static PathFlags? pathFlags = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the network (time) flags used within the license
        //       verification subsystem .
        //
        /// <summary>
        /// The network (time) flags used within the license verification
        /// subsystem; when null, a suitable default value may be used.
        /// </summary>
        private static NetworkFlags? networkFlags = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Information Methods
        /// <summary>
        /// Gets the license certificate file name that was used to load
        /// this assembly.
        /// </summary>
        /// <returns>
        /// The license certificate file name, or null if none is set.
        /// </returns>
        public static string GetFileName() /* CORE */
        {
            lock (syncRoot)
            {
                return fileName;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the license certificate file name that was used to load
        /// this assembly.
        /// </summary>
        /// <param name="fileName">
        /// The license certificate file name to use.
        /// </param>
        public static void SetFileName( /* CORE */
            string fileName /* in */
            )
        {
            lock (syncRoot)
            {
                This.fileName = fileName;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the stored license certificate file name to null.
        /// </summary>
        public static void ResetFileName() /* CORE */
        {
            lock (syncRoot)
            {
                This.fileName = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the license certificate that was used to load this
        /// assembly.
        /// </summary>
        /// <returns>
        /// The license certificate, or null if none is set.
        /// </returns>
        public static ICertificate GetCertificate() /* CORE */
        {
            lock (syncRoot)
            {
                return certificate;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the license certificate that was used to load this
        /// assembly.
        /// </summary>
        /// <param name="certificate">
        /// The license certificate to use.
        /// </param>
        public static void SetCertificate( /* CORE */
            ICertificate certificate /* in */
            )
        {
            lock (syncRoot)
            {
                This.certificate = certificate;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the stored license certificate to null.
        /// </summary>
        public static void ResetCertificate() /* CORE */
        {
            lock (syncRoot)
            {
                This.certificate = null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Other Information Methods
        /// <summary>
        /// Determines whether a verified license certificate with the
        /// specified identifier is present.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the license certificate to check for.
        /// </param>
        /// <returns>
        /// Non-zero if a license certificate with the specified identifier
        /// is present; otherwise, zero.
        /// </returns>
        public static bool HaveCertificate( /* CORE */
            Guid id /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificates == null)
                    return false;

                return certificates.ContainsKey(id);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the verified license certificate with the specified
        /// identifier, discarding any error message.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the license certificate to get.
        /// </param>
        /// <returns>
        /// The license certificate with the specified identifier, or null
        /// if it cannot be found.
        /// </returns>
        public static ICertificate GetCertificate( /* CORE */
            Guid id /* in */
            )
        {
            Result error = null; /* NOT USED */

            return GetCertificate(id, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the verified license certificate with the specified
        /// identifier.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the license certificate to get.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing why the
        /// license certificate could not be returned.
        /// </param>
        /// <returns>
        /// The license certificate with the specified identifier, or null
        /// if it cannot be found.
        /// </returns>
        public static ICertificate GetCertificate( /* CORE */
            Guid id,         /* in */
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificates == null)
                {
                    error = "license certificates unavailable";
                    return null;
                }

                ICertificate certificate;

                if (!certificates.TryGetValue(id, out certificate))
                {
                    error = String.Format(
                        "license certificate {0} not found",
                        _Utility.FormatWrapOrNull(id));

                    return null;
                }

                if (certificate == null)
                {
                    error = String.Format(
                        "invalid license certificate for {0}",
                        _Utility.FormatWrapOrNull(id));

                    return null;
                }

                return certificate;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears all verified license certificates.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing why the
        /// license certificates could not be cleared.
        /// </param>
        /// <returns>
        /// Non-zero if the license certificates were cleared; otherwise,
        /// zero.
        /// </returns>
        public static bool ClearCertificates( /* CORE */
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificates == null)
                {
                    error = "license certificates unavailable";
                    return false;
                }

                certificates.Clear();
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds a verified license certificate, optionally overwriting any
        /// existing certificate with the same identifier.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the license certificate to add.
        /// </param>
        /// <param name="certificate">
        /// The license certificate to add.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite any existing license certificate with the
        /// same identifier.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing why the
        /// license certificate could not be added.
        /// </param>
        /// <returns>
        /// Non-zero if the license certificate was added; otherwise, zero.
        /// </returns>
        public static bool AddCertificate( /* CORE */
            Guid id,                  /* in */
            ICertificate certificate, /* in */
            bool overwrite,           /* in */
            ref Result error          /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificate == null)
                {
                    error = String.Format(
                        "invalid license certificate for {0}",
                        _Utility.FormatWrapOrNull(id));

                    return false;
                }

                if (certificates == null)
                    certificates = new CertificateDictionary();

                if (!overwrite && certificates.ContainsKey(id))
                {
                    error = String.Format(
                        "license certificate {0} already present",
                        _Utility.FormatWrapOrNull(id));

                    return false;
                }

                certificates[id] = certificate;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the verified license certificate with the specified
        /// identifier.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the license certificate to remove.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing why the
        /// license certificate could not be removed.
        /// </param>
        /// <returns>
        /// Non-zero if the license certificate was removed; otherwise,
        /// zero.
        /// </returns>
        public static bool RemoveCertificate( /* CORE */
            Guid id,         /* in */
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificates == null)
                {
                    error = "license certificates unavailable";
                    return false;
                }

                if (!certificates.ContainsKey(id))
                {
                    error = String.Format(
                        "license certificate {0} not found",
                        _Utility.FormatWrapOrNull(id));

                    return false;
                }

                if (!certificates.Remove(id))
                {
                    error = String.Format(
                        "license certificate {0} not removed",
                        _Utility.FormatWrapOrNull(id));

                    return false;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears and discards all verified license certificates.
        /// </summary>
        public static void ResetCertificates() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (certificates != null)
                {
                    certificates.Clear();
                    certificates = null;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Support Methods
        /// <summary>
        /// Gets a value indicating whether expiration date and revocation
        /// checking for licenses requires network access.
        /// </summary>
        /// <returns>
        /// Non-zero if network access is required; otherwise, zero.
        /// </returns>
        public static bool GetForceNetwork() /* CORE */
        {
            lock (syncRoot)
            {
                return forceNetwork;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets a value indicating whether expiration date and revocation
        /// checking for licenses requires network access.
        /// </summary>
        /// <param name="forceNetwork">
        /// Non-zero to require network access for expiration date and
        /// revocation checking.
        /// </param>
        public static void SetForceNetwork( /* CORE */
            bool forceNetwork /* in */
            )
        {
            lock (syncRoot)
            {
                This.forceNetwork = forceNetwork;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Skip Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether license checks may be skipped for the
        /// specified licensing type while a key ring load operation is
        /// pending.
        /// </summary>
        /// <param name="type">
        /// The licensing type to check.
        /// </param>
        /// <returns>
        /// Non-zero if license checks may be skipped for the specified
        /// licensing type; otherwise, zero.
        /// </returns>
        private static bool CanSkipForKeyRing( /* CORE? */
            LicenseType type /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!CertificateSharedOps.HasFlags(
                        type, LicenseType.KeyRingMask, false))
                {
                    //
                    // NOTE: The caller specified a licensing type
                    //       that is not permitted to skip license
                    //       checks for key rings; fail.
                    //
                    return false;
                }

                ///////////////////////////////////////////////////////////////

                if (CertificateSharedOps.HasFlags(
                        skipTypes, LicenseType.KeyRing, true))
                {
                    //
                    // NOTE: This module is configured to skip
                    //       licensing checks while loading key
                    //       rings.  Now, make sure that a key
                    //       ring load operation is pending.
                    //
                    if (!CertificateKeyRingState.IsPending())
                        return false;
                }

                ///////////////////////////////////////////////////////////////

                if (CertificateSharedOps.HasFlags(
                        skipTypes, LicenseType.LicenseKeyRing, true))
                {
                    //
                    // NOTE: This module is configured to skip
                    //       licensing checks while loading key
                    //       rings used by license certificates.
                    //       Now, make sure that a key ring load
                    //       operation is pending.
                    //
                    if (!CertificateKeyRingState.IsLicensePending())
                        return false;
                }

                ///////////////////////////////////////////////////////////////

                return true;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any licensing types are configured to skip
        /// license checks.
        /// </summary>
        /// <returns>
        /// Non-zero if one or more licensing types are configured to skip
        /// license checks; otherwise, zero.
        /// </returns>
        public static bool HaveSkipTypes() /* CORE */
        {
            lock (syncRoot)
            {
                return skipTypes != LicenseType.None;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the licensing types that are configured to skip license
        /// checks.
        /// </summary>
        /// <returns>
        /// The licensing types configured to skip license checks.
        /// </returns>
        public static LicenseType GetSkipTypes() /* CORE */
        {
            lock (syncRoot)
            {
                return skipTypes;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the string representation of the licensing types that are
        /// configured to skip license checks.
        /// </summary>
        /// <returns>
        /// The string representation of the licensing types configured to
        /// skip license checks.
        /// </returns>
        public static string GetSkipTypesToString() /* CORE */
        {
            return GetSkipTypes().ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the licensing types that are configured to skip license
        /// checks.
        /// </summary>
        /// <param name="skipTypes">
        /// The licensing types to configure to skip license checks.
        /// </param>
        public static void SetSkipTypes( /* CORE */
            LicenseType skipTypes /* in */
            )
        {
            lock (syncRoot)
            {
                This.skipTypes = skipTypes;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether skipping of license checks is currently
        /// enabled.
        /// </summary>
        /// <returns>
        /// Non-zero if skipping of license checks is enabled; otherwise,
        /// zero.
        /// </returns>
        public static bool HaveSkip() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref skipCount, 0, 0) > 0; /* NO-LOCK */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether license checks may be skipped for the
        /// specified licensing type.
        /// </summary>
        /// <param name="type">
        /// The licensing type to check.
        /// </param>
        /// <returns>
        /// Non-zero if license checks may be skipped for the specified
        /// licensing type; otherwise, zero.
        /// </returns>
        public static bool CanSkip( /* CORE */
            LicenseType type /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!HaveSkip())
                {
                    //
                    // NOTE: Skipping license checks is globally
                    //       disabled.
                    //
                    return false;
                }

                ///////////////////////////////////////////////////////////////

                if ((type == LicenseType.Context) ||
                    (type == LicenseType.Command))
                {
                    //
                    // HACK: Being called from the "skipLicense"
                    //       configuration command -AND- skipping
                    //       license checks is globally enabled.
                    //       For this case, no other types may be
                    //       combined with this one; the equality
                    //       operator is used here to verify this.
                    //
                    return true;
                }

                ///////////////////////////////////////////////////////////////

                if (CertificateSharedOps.HasFlags(
                        skipTypes, LicenseType.Any, true))
                {
                    //
                    // NOTE: Skipping license checks is globally
                    //       enabled (for any licensing type).
                    //
                    return true;
                }

                ///////////////////////////////////////////////////////////////

                if ((type == LicenseType.Assembly) &&
                    CertificateSharedOps.HasFlags(
                        skipTypes, LicenseType.Assembly, true))
                {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    //
                    // NOTE: Skipping license checks is globally
                    //       enabled -AND- for assembly loading
                    //       if a key ring loading operations is
                    //       allowed to skip license checks.
                    //
                    return CanSkipForKeyRing(type);
#endif
                }

                ///////////////////////////////////////////////////////////////

                if ((type == LicenseType.Feature) &&
                    CertificateSharedOps.HasFlags(
                        skipTypes, LicenseType.Feature, true))
                {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    //
                    // NOTE: Skipping license checks is globally
                    //       enabled -AND- for feature checking
                    //       if a key ring loading operations is
                    //       allowed to skip license checks.
                    //
                    return CanSkipForKeyRing(type);
#endif
                }

                ///////////////////////////////////////////////////////////////

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the string representation of whether license checks may be
        /// skipped for the specified licensing type.
        /// </summary>
        /// <param name="type">
        /// The licensing type to check.
        /// </param>
        /// <returns>
        /// The string representation of true if license checks may be
        /// skipped; otherwise, null.
        /// </returns>
        public static string CanSkipToString( /* CORE */
            LicenseType type /* in */
            )
        {
            return CanSkip(type) ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables skipping of license checks by incrementing the skip
        /// count.
        /// </summary>
        public static void EnableSkip() /* CORE */
        {
            /* IGNORED */
            Interlocked.Increment(ref skipCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables skipping of license checks by decrementing the skip
        /// count.
        /// </summary>
        public static void DisableSkip() /* CORE */
        {
            /* IGNORED */
            Interlocked.Decrement(ref skipCount);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Methods
        /// <summary>
        /// Determines whether a license certificate load operation is
        /// pending.
        /// </summary>
        /// <returns>
        /// Non-zero if a license certificate load operation is pending;
        /// otherwise, zero.
        /// </returns>
        public static bool IsPending() /* CORE */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingLicenseCertificateCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending license certificate load
        /// operation.
        /// </summary>
        public static void BeginPending() /* CORE */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingLicenseCertificateCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending license certificate load operation.
        /// </summary>
        public static void EndPending() /* CORE */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingLicenseCertificateCountEnvVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region File Cache Methods
        /// <summary>
        /// Clears all cached binary and text files.
        /// </summary>
        /// <returns>
        /// The total number of cached files that were cleared.
        /// </returns>
        public static int ClearCachedFiles() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (binaryFileCache != null)
                {
                    count += binaryFileCache.Count;

                    binaryFileCache.Clear();
                    binaryFileCache = null;
                }

                if (textFileCache != null)
                {
                    count += textFileCache.Count;

                    textFileCache.Clear();
                    textFileCache = null;
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a cached binary or text file with the
        /// specified name is present.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached file to check for.
        /// </param>
        /// <returns>
        /// Non-zero if a cached file with the specified name is present;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveCachedFile( /* CORE */
            string fileName /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileName == null)
                    return false;

                if ((binaryFileCache != null) &&
                    binaryFileCache.ContainsKey(fileName))
                {
                    return true;
                }

                if ((textFileCache != null) &&
                    textFileCache.ContainsKey(fileName))
                {
                    return true;
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the cached binary or text file with the
        /// specified name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached file to get.
        /// </param>
        /// <param name="data">
        /// Upon success, receives the cached file data as a byte array or
        /// a string; otherwise, null.
        /// </param>
        /// <returns>
        /// Non-zero if the cached file was found; otherwise, zero.
        /// </returns>
        public static bool TryGetCachedFile( /* CORE */
            string fileName, /* in */
            out object data  /* out */
            )
        {
            byte[] bytes;

            if (TryGetCachedBinaryFile(fileName, out bytes))
            {
                data = bytes;
                return true;
            }

            string text;

            if (TryGetCachedTextFile(fileName, out text))
            {
                data = text;
                return true;
            }

            data = null;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the cached binary file with the specified name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached binary file to get.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the cached binary file data; otherwise,
        /// null.
        /// </param>
        /// <returns>
        /// Non-zero if the cached binary file was found; otherwise, zero.
        /// </returns>
        public static bool TryGetCachedBinaryFile( /* CORE */
            string fileName, /* in */
            out byte[] bytes /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                bytes = null;

                if (fileName == null)
                    return false;

                if (binaryFileCache == null)
                    return false;

                return binaryFileCache.TryGetValue(
                    fileName, out bytes);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or updates the cached binary file with the specified name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached binary file to add or update.
        /// </param>
        /// <param name="bytes">
        /// The binary file data to cache.
        /// </param>
        /// <returns>
        /// Non-zero if the cached binary file was added or updated;
        /// otherwise, zero.
        /// </returns>
        public static bool MergeCachedBinaryFile( /* CORE */
            string fileName, /* in */
            byte[] bytes     /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (binaryFileCache == null)
                    binaryFileCache = new BinaryFileDictionary();

                if (fileName == null)
                    return false;

                binaryFileCache[fileName] = bytes;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the cached text file with the specified name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached text file to get.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the cached text file data; otherwise,
        /// null.
        /// </param>
        /// <returns>
        /// Non-zero if the cached text file was found; otherwise, zero.
        /// </returns>
        public static bool TryGetCachedTextFile( /* CORE */
            string fileName, /* in */
            out string text  /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                text = null;

                if (fileName == null)
                    return false;

                if (textFileCache == null)
                    return false;

                return textFileCache.TryGetValue(
                    fileName, out text);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or updates the cached text file with the specified name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the cached text file to add or update.
        /// </param>
        /// <param name="text">
        /// The text file data to cache.
        /// </param>
        /// <returns>
        /// Non-zero if the cached text file was added or updated;
        /// otherwise, zero.
        /// </returns>
        public static bool MergeCachedTextFile( /* CORE */
            string fileName, /* in */
            string text      /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (textFileCache == null)
                    textFileCache = new TextFileDictionary();

                if (fileName == null)
                    return false;

                textFileCache[fileName] = text;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the names of all cached binary and text files to the
        /// specified list.
        /// </summary>
        /// <param name="fileNames">
        /// The list to which cached file names are added; created when
        /// null.
        /// </param>
        /// <returns>
        /// The total number of cached file names that were added.
        /// </returns>
        public static int AddCachedFileNames( /* CORE */
            ref StringList fileNames /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (binaryFileCache != null)
                {
                    count += binaryFileCache.Count;

                    if (fileNames == null)
                        fileNames = new StringList();

                    fileNames.AddRange(binaryFileCache.Keys);
                }

                if (textFileCache != null)
                {
                    count += textFileCache.Count;

                    if (fileNames == null)
                        fileNames = new StringList();

                    fileNames.AddRange(textFileCache.Keys);
                }

                return count;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Path Flags Methods
        /// <summary>
        /// Determines whether the path flags used by the license
        /// verification subsystem have been set.
        /// </summary>
        /// <returns>
        /// Non-zero if the path flags have been set; otherwise, zero.
        /// </returns>
        public static bool HavePathFlags() /* CORE */
        {
            lock (syncRoot)
            {
                return pathFlags != null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the path flags used by the license verification subsystem.
        /// </summary>
        /// <returns>
        /// The path flags, or null if none have been set.
        /// </returns>
        public static PathFlags? GetPathFlags() /* CORE */
        {
            lock (syncRoot)
            {
                return pathFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the string representation of the path flags used by the
        /// license verification subsystem.
        /// </summary>
        /// <returns>
        /// The string representation of the path flags, or null if none
        /// have been set.
        /// </returns>
        public static string GetPathFlagsToString() /* CORE */
        {
            lock (syncRoot)
            {
                return (pathFlags != null) ?
                    pathFlags.ToString() : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the path flags used by the license verification subsystem.
        /// </summary>
        /// <param name="pathFlags">
        /// The path flags to use, or null to use a suitable default.
        /// </param>
        public static void SetPathFlags( /* CORE */
            PathFlags? pathFlags /* in */
            )
        {
            lock (syncRoot)
            {
                This.pathFlags = pathFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the path flags used by the license verification
        /// subsystem to null.
        /// </summary>
        public static void UnsetPathFlags() /* CORE */
        {
            lock (syncRoot)
            {
                pathFlags = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the path flags used by the license verification subsystem,
        /// returning a suitable default value when none have been set.
        /// </summary>
        /// <returns>
        /// The configured path flags, or a default value when none have
        /// been set.
        /// </returns>
        public static PathFlags GetPathFlagsOrDefault() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (pathFlags != null)
                    return (PathFlags)pathFlags;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                return Constants.VerifyPathFlags;
#else
                return PathFlags.None;
#endif
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Flags Methods
        /// <summary>
        /// Determines whether the network (time) flags used by the license
        /// verification subsystem have been set.
        /// </summary>
        /// <returns>
        /// Non-zero if the network flags have been set; otherwise, zero.
        /// </returns>
        public static bool HaveNetworkFlags() /* CORE */
        {
            lock (syncRoot)
            {
                return networkFlags != null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the network (time) flags used by the license verification
        /// subsystem.
        /// </summary>
        /// <returns>
        /// The network flags, or null if none have been set.
        /// </returns>
        public static NetworkFlags? GetNetworkFlags() /* CORE */
        {
            lock (syncRoot)
            {
                return networkFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the string representation of the network (time) flags used
        /// by the license verification subsystem.
        /// </summary>
        /// <returns>
        /// The string representation of the network flags, or null if none
        /// have been set.
        /// </returns>
        public static string GetNetworkFlagsToString() /* CORE */
        {
            lock (syncRoot)
            {
                return (networkFlags != null) ?
                    networkFlags.ToString() : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the network (time) flags used by the license verification
        /// subsystem.
        /// </summary>
        /// <param name="networkFlags">
        /// The network flags to use, or null to use a suitable default.
        /// </param>
        public static void SetNetworkFlags( /* CORE */
            NetworkFlags? networkFlags /* in */
            )
        {
            lock (syncRoot)
            {
                This.networkFlags = networkFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the network (time) flags used by the license
        /// verification subsystem to null.
        /// </summary>
        public static void UnsetNetworkFlags() /* CORE */
        {
            lock (syncRoot)
            {
                networkFlags = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the network (time) flags used by the license verification
        /// subsystem, returning a suitable default value when none have
        /// been set.
        /// </summary>
        /// <returns>
        /// The configured network flags, or a default value when none have
        /// been set.
        /// </returns>
        public static NetworkFlags GetNetworkFlagsOrDefault() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (networkFlags != null)
                    return (NetworkFlags)networkFlags;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                return Constants.LicenseNetworkFlags;
#else
                return NetworkFlags.None;
#endif
            }
        }
        #endregion
    }
}
