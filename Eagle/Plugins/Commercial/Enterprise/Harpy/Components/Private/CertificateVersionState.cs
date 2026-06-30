/*
 * CertificateVersionState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

using VersionRange = Eagle._Components.Public.Pair<System.Version>;

using VersionPair = System.Collections.Generic.KeyValuePair<
    Eagle._Components.Public.PolicyType, Eagle._Components.Public.Pair<
    System.Version>>;

using VersionRangeDictionary = System.Collections.Generic.Dictionary<
    Eagle._Components.Public.PolicyType, Eagle._Components.Public.Pair<
    System.Version>>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-<see cref="PolicyType" /> certificate version ranges
    /// in a thread-safe manner. Provides operations to initialize, query,
    /// set, and remove the version range associated with each policy type.
    /// </summary>
    [ObjectId("70b8b3e1-3139-46a0-985b-2bff8d3824ba")]
    internal static class CertificateVersionState
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the version range data.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The collection of version ranges, keyed by their associated
        /// <see cref="PolicyType" />.
        /// </summary>
        private static VersionRangeDictionary versionRanges;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to obtain the version range associated with the specified
        /// <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be retrieved.
        /// </param>
        /// <param name="versionRange">
        /// Upon success, receives the version range associated with
        /// <paramref name="policyType" />; otherwise, null.
        /// </param>
        /// <returns>
        /// Non-zero if a version range was found; otherwise, zero.
        /// </returns>
        private static bool TryGetRange( /* CORE */
            PolicyType policyType,        /* in */
            out VersionRange versionRange /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                versionRange = null;

                if (versionRanges == null)
                    return false;

                if (versionRanges.TryGetValue(
                        policyType, out versionRange))
                {
                    return true;
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to associate the specified version range with the
        /// specified <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be set.
        /// </param>
        /// <param name="versionRange">
        /// The version range to associate with
        /// <paramref name="policyType" />. This value may be null.
        /// </param>
        /// <param name="allowOverwrite">
        /// Non-zero to overwrite an existing version range for
        /// <paramref name="policyType" />; otherwise, an existing entry is
        /// left unchanged.
        /// </param>
        /// <returns>
        /// Non-zero if the version range was set; otherwise, zero.
        /// </returns>
        private static bool TrySetRange( /* CORE */
            PolicyType policyType,     /* in */
            VersionRange versionRange, /* in: OPTIONAL */
            bool allowOverwrite        /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (versionRanges == null)
                    return false;

                if (!allowOverwrite &&
                    versionRanges.ContainsKey(policyType))
                {
                    return false;
                }

                versionRanges[policyType] = versionRange;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to remove the version range associated with the specified
        /// <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be removed.
        /// </param>
        /// <returns>
        /// Non-zero if the version range was removed; otherwise, zero.
        /// </returns>
        private static bool TryUnsetRange( /* CORE */
            PolicyType policyType /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (versionRanges == null)
                    return false;

                return versionRanges.Remove(policyType);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Initializes the collection of version ranges, optionally forcing
        /// it to be recreated even when it already exists.
        /// </summary>
        /// <param name="force">
        /// Non-zero to recreate the collection even when it has already been
        /// initialized.
        /// </param>
        public static void InitializeRanges( /* CORE */
            bool force /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || (versionRanges == null))
                    versionRanges = new VersionRangeDictionary();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a string representation of all configured version ranges,
        /// including the associated <see cref="PolicyType" /> for each entry.
        /// </summary>
        /// <returns>
        /// A string containing the formatted version ranges.
        /// </returns>
        public static string GetRanges() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                StringList list = new StringList();

                foreach (VersionPair pair in versionRanges)
                {
                    list.Add(pair.Key.ToString());

                    list.Add(CertificateDataOps.FormatVersionRange(
                        pair.Value));
                }

                return list.ToString();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a version range is configured for the specified
        /// <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check. This value may be null.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to treat a configured null version range as present;
        /// otherwise, a null version range is considered absent.
        /// </param>
        /// <returns>
        /// Non-zero if a matching version range is present; otherwise, zero.
        /// </returns>
        public static bool HaveRange( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            bool allowNull          /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    VersionRange versionRange; /* NOT USED */

                    if (TryGetRange(
                            (PolicyType)policyType, out versionRange))
                    {
                        return allowNull || (versionRange != null);
                    }
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the version range configured for the specified
        /// <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be retrieved. This value
        /// may be null.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to permit returning a configured null version range.
        /// </param>
        /// <returns>
        /// The version range associated with <paramref name="policyType" />,
        /// or null if none is available.
        /// </returns>
        public static VersionRange GetRange( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            bool allowNull          /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    VersionRange versionRange;

                    if (TryGetRange(
                            (PolicyType)policyType, out versionRange) &&
                        (allowNull || (versionRange != null)))
                    {
                        return versionRange;
                    }
                }

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Associates the specified version range with the specified
        /// <see cref="PolicyType" />, overwriting any existing range.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be set. This value may
        /// be null.
        /// </param>
        /// <param name="versionRange">
        /// The version range to associate with
        /// <paramref name="policyType" />. This value may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the version range was set; otherwise, zero.
        /// </returns>
        public static bool SetRange( /* CORE */
            PolicyType? policyType,   /* in: OPTIONAL */
            VersionRange versionRange /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    return TrySetRange(
                        (PolicyType)policyType, versionRange, true);
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the version range associated with the specified
        /// <see cref="PolicyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose version range is to be removed. This value
        /// may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the version range was removed; otherwise, zero.
        /// </returns>
        public static bool UnsetRange( /* CORE */
            PolicyType? policyType /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                    return TryUnsetRange((PolicyType)policyType);

                return false;
            }
        }
        #endregion
    }
}
