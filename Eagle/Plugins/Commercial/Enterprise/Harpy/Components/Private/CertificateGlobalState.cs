/*
 * CertificateGlobalState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Components.Public;
using Utility = Eagle._Components.Public.Utility;

using KeyUsagePair = System.Collections.Generic.KeyValuePair<
    Licensing.Components.Public.EntityType,
    Eagle._Components.Public.AnyTriplet<string, bool, bool>>;

using KeyUsageTriplet = Eagle._Components.Public.AnyTriplet<
    string, bool, bool>;

using KeyUsageDictionary = System.Collections.Generic.Dictionary<
    Licensing.Components.Public.EntityType,
    Eagle._Components.Public.AnyTriplet<string, bool, bool>>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains process-wide global state for the certificate licensing
    /// subsystem, including the configured storage type, security
    /// requirements, extra feature settings, change tracking, and the
    /// per-<see cref="EntityType" /> key usage customizations used by the
    /// policy subsystem.
    /// </summary>
    [ObjectId("275efb99-6da3-4ac2-aa45-84ae49f9f46f")]
    internal static class CertificateGlobalState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private key ring
        //       and key pair data in this class (i.e. which is used by the
        //       policy subsystem).
        //
        /// <summary>
        /// The object used to synchronize access to the global state
        /// maintained by this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of the configured storage type for
        //       license manager data accessed via the IStorageManager, e.g.
        //       extra features.
        //
        /// <summary>
        /// The configured storage type for license manager data accessed
        /// via the storage manager, or null when none has been set.
        /// </summary>
        private static StorageType? storageType = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of the configured security flag for
        //       the license manager data accessed via the IStorageManager,
        //       e.g. extra features.
        //
        /// <summary>
        /// The configured security flag for license manager data accessed
        /// via the storage manager, or null when none has been set.
        /// </summary>
        private static bool? mustHaveSecurity = null;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: This field is the trace priority to use when emitting the
        //       diagnostic messages related to leftover global state.
        //
        /// <summary>
        /// The trace priority used when emitting diagnostic messages
        /// related to leftover global state.
        /// </summary>
        private static TracePriority tracePriority = TracePriority.Default;
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Extra Features Data
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        //
        // HACK: This is used to cache the "ExtraFeatures" setting value
        //       that is queried from the registry.
        //
        /// <summary>
        /// Caches the "ExtraFeatures" setting value that is queried from
        /// the registry.
        /// </summary>
        private static string extraFeatures = null;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field is used to keep track of how many changes to
        //       state (local and/or global) have been made via commands
        //       in the configuration subsystem.
        //
        /// <summary>
        /// Tracks how many changes to state (local and/or global) have
        /// been made via commands in the configuration subsystem.
        /// </summary>
        private static long changeCount = 0;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used to customize required key usage flags on a
        //       per-EntityType basis.
        //
        /// <summary>
        /// Stores the required key usage flags customized on a
        /// per-<see cref="EntityType" /> basis, or null when none have
        /// been configured.
        /// </summary>
        private static KeyUsageDictionary keyUsages = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Global Storage Type Methods
        /// <summary>
        /// Determines whether a storage type has been configured.
        /// </summary>
        /// <returns>
        /// Non-zero if a storage type has been configured; otherwise,
        /// zero.
        /// </returns>
        public static bool HaveStorageType() /* CORE */
        {
            lock (syncRoot)
            {
                return storageType != null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured storage type.
        /// </summary>
        /// <returns>
        /// The configured storage type, or null when none has been set.
        /// </returns>
        public static StorageType? GetStorageType() /* CORE */
        {
            lock (syncRoot)
            {
                return storageType;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configured storage type.
        /// </summary>
        /// <param name="value">
        /// The storage type to use, or null to indicate none.
        /// </param>
        public static void SetStorageType( /* CORE */
            StorageType? value /* in */
            )
        {
            lock (syncRoot)
            {
                storageType = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the configured storage type.
        /// </summary>
        public static void UnsetStorageType() /* CORE */
        {
            lock (syncRoot)
            {
                storageType = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured storage type formatted as a string.
        /// </summary>
        /// <returns>
        /// The string representation of the configured storage type, or
        /// null when none has been set.
        /// </returns>
        public static string GetStorageTypeAsString() /* CORE */
        {
            lock (syncRoot)
            {
                return (storageType != null) ?
                    storageType.ToString() : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured security flag.
        /// </summary>
        /// <returns>
        /// The configured security flag, or null when none has been set.
        /// </returns>
        public static bool? GetMustHaveSecurity() /* CORE */
        {
            lock (syncRoot)
            {
                return mustHaveSecurity;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configured security flag.
        /// </summary>
        /// <param name="value">
        /// The security flag to use, or null to indicate none.
        /// </param>
        public static void SetMustHaveSecurity( /* CORE */
            bool? value /* in */
            )
        {
            lock (syncRoot)
            {
                mustHaveSecurity = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured security flag formatted as a string.
        /// </summary>
        /// <returns>
        /// The string representation of the configured security flag, or
        /// null when none has been set.
        /// </returns>
        public static string GetMustHaveSecurityAsString() /* CORE */
        {
            lock (syncRoot)
            {
                return (mustHaveSecurity != null) ?
                    mustHaveSecurity.ToString() : null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Extra Features Methods
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
        /// <summary>
        /// Determines whether an extra features setting value has been
        /// cached.
        /// </summary>
        /// <returns>
        /// Non-zero if an extra features value has been cached; otherwise,
        /// zero.
        /// </returns>
        public static bool HaveExtraFeatures()
        {
            lock (syncRoot)
            {
                return extraFeatures != null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the cached extra features setting value.
        /// </summary>
        /// <returns>
        /// The cached extra features value, or null when none has been
        /// cached.
        /// </returns>
        public static string GetExtraFeatures()
        {
            lock (syncRoot)
            {
                return extraFeatures;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cached extra features setting value.
        /// </summary>
        /// <param name="value">
        /// The extra features value to cache.
        /// </param>
        public static void SetExtraFeatures(
            string value /* in */
            )
        {
            lock (syncRoot)
            {
                extraFeatures = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the cached extra features setting value.
        /// </summary>
        public static void UnsetExtraFeatures()
        {
            lock (syncRoot)
            {
                extraFeatures = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the cached extra features include the
        /// promotional feature or all features.
        /// </summary>
        /// <returns>
        /// Non-zero if the promotional feature (or all features) is
        /// present; otherwise, zero.
        /// </returns>
        public static bool IsPromotionalOrAll()
        {
            return HaveExtraFeatures(Features.PromotionalOrAll, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the cached extra features include the test
        /// mode feature or all features.
        /// </summary>
        /// <returns>
        /// Non-zero if the test mode feature (or all features) is present;
        /// otherwise, zero.
        /// </returns>
        public static bool IsEnableTestModeOrAll()
        {
            return HaveExtraFeatures(Features.EnableTestModeOrAll, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the cached extra features match the
        /// specified features.
        /// </summary>
        /// <param name="hasFeatures">
        /// The features to test for within the cached extra features.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the specified features be
        /// present.
        /// </param>
        /// <returns>
        /// Non-zero if the cached extra features match; otherwise, zero.
        /// </returns>
        public static bool HaveExtraFeatures(
            string hasFeatures, /* in */
            bool hasAll         /* in */
            )
        {
            Result result = null;

            return HaveExtraFeatures(hasFeatures, hasAll, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the cached extra features match the
        /// specified features.
        /// </summary>
        /// <param name="hasFeatures">
        /// The features to test for within the cached extra features.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the specified features be
        /// present.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the matched features or any error
        /// information.
        /// </param>
        /// <returns>
        /// Non-zero if the cached extra features match; otherwise, zero.
        /// </returns>
        public static bool HaveExtraFeatures(
            string hasFeatures, /* in */
            bool hasAll,        /* in */
            ref Result result   /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if (CertificateSharedOps.MatchFlags(
                        extraFeatures, FlagType.Feature, flagsKey,
                        hasFeatures, null, hasAll, false, true,
                        ref result) == ReturnCode.Ok)
                {
                    return true;
                }

                return false;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Change Count Methods
        /// <summary>
        /// Gets the current number of tracked state changes.
        /// </summary>
        /// <returns>
        /// The current change count.
        /// </returns>
        public static long GetChangeCount() /* CORE */
        {
            return Interlocked.CompareExchange(ref changeCount, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically increments the tracked state change count.
        /// </summary>
        /// <returns>
        /// The incremented change count.
        /// </returns>
        public static long IncrementChangeCount() /* CORE */
        {
            return Interlocked.Increment(ref changeCount);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region KeyUsage Methods
        /// <summary>
        /// Removes all customized per-<see cref="EntityType" /> key usage
        /// entries.
        /// </summary>
        /// <returns>
        /// The number of key usage entries that were removed.
        /// </returns>
        public static int ClearKeyUsages() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (keyUsages != null)
                {
                    count += keyUsages.Count;
                    keyUsages.Clear();
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default key usage for the specified entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose default key usage is required.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives error information.
        /// </param>
        /// <returns>
        /// The default key usage for the entity type, or null on failure.
        /// </returns>
        public static KeyUsageTriplet DefaultKeyUsage( /* CORE */
            EntityType entityType, /* in */
            ref Result error       /* out */
            )
        {
            string hasFlags = null;
            bool hasAll = false;
            bool mayNeedRootKeyUsage = false;

            if (CertificateSharedOps.DefaultEntityTypeToKeyUsage(
                    entityType, ref hasFlags, ref hasAll,
                    ref mayNeedRootKeyUsage, ref error))
            {
                return new KeyUsageTriplet(
                    hasFlags, hasAll, mayNeedRootKeyUsage);
            }
            else
            {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the effective key usage for the specified entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose key usage is to be resolved.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives error information.
        /// </param>
        /// <returns>
        /// The resolved key usage for the entity type, or null on failure.
        /// </returns>
        public static KeyUsageTriplet ResolveKeyUsage( /* CORE */
            EntityType entityType, /* in */
            ref Result error       /* out */
            )
        {
            string hasFlags = null;
            bool hasAll = false;
            bool mayNeedRootKeyUsage = false;

            if (CertificateSharedOps.EntityTypeToKeyUsage(
                    entityType, ref hasFlags, ref hasAll,
                    ref mayNeedRootKeyUsage, ref error))
            {
                return new KeyUsageTriplet(
                    hasFlags, hasAll, mayNeedRootKeyUsage);
            }
            else
            {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a list describing the customized per-entity-type key usage
        /// entries.
        /// </summary>
        /// <returns>
        /// A list describing the configured key usages, or null when none
        /// have been configured.
        /// </returns>
        public static StringList ListKeyUsages() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyUsages == null)
                    return null;

                StringList list = new StringList();

                foreach (KeyUsagePair pair in keyUsages)
                    list.Add(StringList.MakeList(pair.Key, pair.Value));

                return list;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the customized key usage for the specified
        /// entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose key usage is required.
        /// </param>
        /// <param name="anyTriplet">
        /// Upon success, receives the key usage configured for the entity
        /// type.
        /// </param>
        /// <returns>
        /// Non-zero if a key usage was found for the entity type;
        /// otherwise, zero.
        /// </returns>
        public static bool TryGetKeyUsage( /* CORE */
            EntityType entityType,         /* in */
            out KeyUsageTriplet anyTriplet /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                anyTriplet = null;

                if (keyUsages == null)
                    return false;

                return keyUsages.TryGetValue(
                    entityType, out anyTriplet);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or updates the customized key usage for the specified
        /// entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose key usage is to be set.
        /// </param>
        /// <param name="hasFlags">
        /// The required key usage flags for the entity type.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the specified flags be present.
        /// </param>
        /// <param name="mayNeedRootKeyUsage">
        /// Non-zero if root key usage may also be required.
        /// </param>
        /// <returns>
        /// Non-zero upon success.
        /// </returns>
        public static bool MergeKeyUsage( /* CORE */
            EntityType entityType,   /* in */
            string hasFlags,         /* out */
            bool hasAll,             /* out */
            bool mayNeedRootKeyUsage /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyUsages == null)
                    keyUsages = new KeyUsageDictionary();

                keyUsages[entityType] = new KeyUsageTriplet(
                    hasFlags, hasAll, mayNeedRootKeyUsage);

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Forbids any key usage for the specified entity type by storing
        /// a null entry for it.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose key usage is to be forbidden.
        /// </param>
        /// <returns>
        /// Non-zero upon success.
        /// </returns>
        public static bool ForbidKeyUsage( /* CORE */
            EntityType entityType /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyUsages == null)
                    keyUsages = new KeyUsageDictionary();

                keyUsages[entityType] = null;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the customized key usage for the specified entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type whose key usage is to be removed.
        /// </param>
        /// <returns>
        /// Non-zero if a key usage entry was removed; otherwise, zero.
        /// </returns>
        public static bool RemoveKeyUsage( /* CORE */
            EntityType entityType /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyUsages == null)
                    return false;

                return keyUsages.Remove(entityType);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cleanup Methods
#if CERTIFICATE_PLUGIN && (LICENSING || CERTIFICATE_POLICY)
        /// <summary>
        /// Gets the trace priority to use when reporting cleanup activity
        /// for the specified number of leftover items.
        /// </summary>
        /// <param name="count">
        /// The number of leftover items that were cleaned up.
        /// </param>
        /// <returns>
        /// The trace priority appropriate for the specified count.
        /// </returns>
        private static TracePriority GetCleanupTracePriority( /* CORE? */
            int count /* in */
            )
        {
            TracePriority priority = TracePriority.Low; /* EXEMPT */

            if (count > 3)
                priority = TracePriority.MediumHigh; /* EXEMPT */
            else if (count > 2)
                priority = TracePriority.Medium; /* EXEMPT */
            else if (count > 1)
                priority = TracePriority.MediumLow; /* EXEMPT */

            return priority;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cleans up all leftover global state, emitting a diagnostic
        /// trace message when any items are cleaned up.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method, used for diagnostic purposes.
        /// </param>
        public static void MaybeCleanupAll( /* CORE? */
            string methodName /* in */
            )
        {
            int count = 0; /* TRACE ONLY */
            Result result = null; /* TRACE ONLY */

            count = MaybeCleanupAll(ref result);

#if DEBUG || FORCE_TRACE
            if (count > 0)
            {
                CertificateTraceOps.DebugTrace(String.Format(
                    "MaybeCleanupAll({0}): count {1} greater than zero: {2}",
                    Utility.FormatWrapOrNull(methodName),
                    count, Utility.FormatWrapOrNull(result)),
                    typeof(CertificateGlobalState).Name,
                    GetCleanupTracePriority(count));
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cleans up all leftover global state, including pending plugins,
        /// trusted key rings, and approved key pairs.
        /// </summary>
        /// <param name="result">
        /// Upon return, receives a description of the items that were
        /// cleaned up, or null when none were.
        /// </param>
        /// <returns>
        /// The total number of items that were cleaned up.
        /// </returns>
        private static int MaybeCleanupAll( /* CORE? */
            ref Result result /* out */
            ) /* CORE? */
        {
            int totalCount = 0;
            StringBuilder builder = null;

            ///////////////////////////////////////////////////////////////////

            lock (syncRoot) /* TRANSACTIONAL */
            {
                #region Pending Plugins
#if LICENSING && CERTIFICATE_POLICY
                CertificatePluginState.MaybeCountPending(
                    tracePriority, ref builder, ref totalCount);

                CertificatePluginState.MaybeCleanupPending(
                    ref builder, ref totalCount);
#endif
                #endregion

                ///////////////////////////////////////////////////////////////

                #region Trusted Key Rings
#if CERTIFICATE_POLICY
                CertificateKeyRingState.MaybeCountAll(
                    tracePriority, ref builder, ref totalCount);

                CertificateKeyRingState.MaybeCleanupAll(
                    ref builder, ref totalCount);
#endif
                #endregion

                ///////////////////////////////////////////////////////////////

                #region Approved Key Pairs
#if CERTIFICATE_POLICY
                CertificateKeyPairState.MaybeCountAll(
                    tracePriority, ref builder, ref totalCount);

                CertificateKeyPairState.MaybeCleanupAll(
                    ref builder, ref totalCount);
#endif
                #endregion
            }

            ///////////////////////////////////////////////////////////////////

            result = (builder != null) ?
                builder.ToString() : null;

            return totalCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cleans up leftover global state associated with the specified
        /// interpreter, emitting a diagnostic trace message when any items
        /// are cleaned up.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose associated global state is to be cleaned
        /// up.
        /// </param>
        public static void CleanupOne( /* CORE? */
            Interpreter interpreter /* in */
            )
        {
            int count; /* TRACE ONLY */
            Result result = null; /* TRACE ONLY */

            count = CleanupOne(interpreter, true, ref result);

#if DEBUG || FORCE_TRACE
            if (count > 0)
            {
                CertificateTraceOps.DebugTrace(String.Format(
                    "CleanupOne: count {0} greater than zero: {1}",
                    count, Utility.FormatWrapOrNull(result)),
                    typeof(CertificateGlobalState).Name,
                    GetCleanupTracePriority(count));
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cleans up leftover global state associated with the specified
        /// interpreter, including pending plugins, trusted key rings, and
        /// approved key pairs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose associated global state is to be cleaned
        /// up.
        /// </param>
        /// <param name="force">
        /// Non-zero to clean up key rings and key pairs even when key ring
        /// state is pending.
        /// </param>
        /// <param name="result">
        /// Upon return, receives a description of the items that were
        /// cleaned up, or null when none were.
        /// </param>
        /// <returns>
        /// The total number of items that were cleaned up.
        /// </returns>
        private static int CleanupOne( /* CORE? */
            Interpreter interpreter, /* in */
            bool force,              /* in */
            ref Result result        /* out */
            )
        {
#if CERTIFICATE_POLICY
            int count; /* REUSED */
#endif

            int totalCount = 0;
            StringBuilder builder = null;

            ///////////////////////////////////////////////////////////////////

            #region Pending Plugins
#if LICENSING && CERTIFICATE_POLICY
            count = 0;

            if (CertificatePluginState.CountPending(
                    interpreter, true, ref count) && (count > 0))
            {
                if (builder == null)
                    builder = new StringBuilder();

                if (builder.Length > 0)
                    builder.Append(Characters.Space);

                builder.AppendFormat(
                    "pendingPlugins(interpreter, {0})", count);

                totalCount += count;
            }

            ///////////////////////////////////////////////////////////////////

            if (CertificatePluginState.RemovePending(
                    interpreter))
            {
                if (builder == null)
                    builder = new StringBuilder();

                if (builder.Length > 0)
                    builder.Append(Characters.Space);

                builder.Append("pendingPlugins");
                totalCount++;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Trusted Key Rings & Approved Key Pairs
#if CERTIFICATE_POLICY
            if (force || !CertificateKeyRingState.IsPending())
            {
                count = 0;

                if (CertificateKeyRingState.CountAllTrusted(
                        interpreter, true, ref count) && (count > 0))
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "trustedKeyRings(interpreter, {0})", count);

                    totalCount += count;
                }

                ///////////////////////////////////////////////////////////////

                if (CertificateKeyRingState.RemoveAllTrusted(
                        interpreter, true))
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.Append("trustedKeyRings");
                    totalCount++;
                }

                ///////////////////////////////////////////////////////////////

                count = 0;

                if (CertificateKeyPairState.CountAllApproved(
                        interpreter, true, ref count) && (count > 0))
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "approvedKeyPairs(interpreter, {0})", count);

                    totalCount += count;
                }

                ///////////////////////////////////////////////////////////////

                if (CertificateKeyPairState.RemoveAllApproved(
                        interpreter, true))
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.Append("approvedKeyPairs");
                    totalCount++;
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            result = (builder != null) ?
                builder.ToString() : null;

            return totalCount;
        }
#endif
        #endregion
    }
}
