/*
 * CertificateTimeState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

using DurationPair = System.Collections.Generic.KeyValuePair<
    Eagle._Components.Public.PolicyType, System.TimeSpan?>;

using DurationDictionary = System.Collections.Generic.Dictionary<
    Eagle._Components.Public.PolicyType, System.TimeSpan?>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-policy certificate time durations, along with the
    /// associated default duration and the flag indicating whether queries
    /// should be performed via HTTP. All access is synchronized.
    /// </summary>
    [ObjectId("bd834be4-a656-4733-ba33-2c141ae0cc99")]
    internal static class CertificateTimeState
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the static state of this
        /// class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The mapping of policy types to their associated time durations.
        /// </summary>
        private static DurationDictionary durations;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if queries should be performed via HTTP.
        /// </summary>
        private static bool queryViaHttp;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default time duration used when no policy-specific duration is
        /// available.
        /// </summary>
        private static TimeSpan? defaultDuration;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Policy Duration Methods
        /// <summary>
        /// Attempts to look up the time duration associated with the
        /// specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type for which the associated duration is being
        /// queried.
        /// </param>
        /// <param name="duration">
        /// Upon success, receives the duration associated with
        /// <paramref name="policyType" />; otherwise, receives null.
        /// </param>
        /// <returns>
        /// Non-zero if the duration was found; otherwise, zero.
        /// </returns>
        private static bool TryGetDuration( /* CORE */
            PolicyType policyType, /* in */
            out TimeSpan? duration /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                duration = null;

                if (durations == null)
                    return false;

                if (durations.TryGetValue(policyType, out duration))
                    return true;

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to associate the specified time duration with the
        /// specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type for which the duration is being set.
        /// </param>
        /// <param name="duration">
        /// The duration to associate with <paramref name="policyType" />.
        /// This value may be null.
        /// </param>
        /// <param name="allowOverwrite">
        /// Non-zero to permit overwriting an existing duration for the
        /// specified policy type; otherwise, an existing entry causes
        /// failure.
        /// </param>
        /// <returns>
        /// Non-zero if the duration was set; otherwise, zero.
        /// </returns>
        private static bool TrySetDuration( /* CORE */
            PolicyType policyType, /* in */
            TimeSpan? duration,    /* in: OPTIONAL */
            bool allowOverwrite    /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (durations == null)
                    return false;

                if (!allowOverwrite && durations.ContainsKey(policyType))
                    return false;

                durations[policyType] = duration;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to remove the time duration associated with the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose associated duration is to be removed.
        /// </param>
        /// <returns>
        /// Non-zero if the duration was removed; otherwise, zero.
        /// </returns>
        private static bool TryUnsetDuration( /* CORE */
            PolicyType policyType /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (durations == null)
                    return false;

                return durations.Remove(policyType);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Policy Duration Methods
        /// <summary>
        /// Initializes the policy duration mapping and the default duration,
        /// optionally forcing re-initialization of existing state.
        /// </summary>
        /// <param name="force">
        /// Non-zero to re-initialize the duration mapping and default
        /// duration even if they have already been initialized.
        /// </param>
        public static void InitializeDurations( /* CORE */
            bool force /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || (durations == null))
                    durations = new DurationDictionary();

                if (force || (defaultDuration == null))
                    defaultDuration = Constants.LimitedDuration;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a flat list of the policy types and their associated
        /// durations and returns it as a string.
        /// </summary>
        /// <returns>
        /// A string representation of the policy type and duration pairs.
        /// </returns>
        public static string GetDurations() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                StringList list = new StringList();

                foreach (DurationPair pair in durations)
                {
                    list.Add(pair.Key.ToString());

                    TimeSpan? timeSpan = pair.Value;

                    list.Add((timeSpan != null) ?
                        ((TimeSpan)timeSpan).ToString() : null);
                }

                return list.ToString();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a duration is available for the specified
        /// policy type, or whether a default duration is available when no
        /// policy type is specified.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to query, or null to query the default duration.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to treat a null policy-specific duration as available;
        /// otherwise, a null duration is treated as unavailable.
        /// </param>
        /// <returns>
        /// Non-zero if a suitable duration is available; otherwise, zero.
        /// </returns>
        public static bool HaveDurationOrDefault( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            bool allowNull          /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    TimeSpan? duration; /* NOT USED */

                    if (TryGetDuration(
                            (PolicyType)policyType, out duration))
                    {
                        return allowNull || (duration != null);
                    }

                    return false;
                }
                else
                {
                    return defaultDuration != null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the duration associated with the specified policy type,
        /// falling back to the default duration when appropriate.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to query, or null to use the default duration.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to accept a null policy-specific duration as a valid
        /// result; otherwise, a null duration causes the fallback behavior.
        /// </param>
        /// <param name="nullOnNotFound">
        /// Non-zero to return null when no policy-specific duration is found;
        /// otherwise, the default duration is returned in that case.
        /// </param>
        /// <returns>
        /// The resolved duration, the default duration, or null, depending on
        /// the specified arguments.
        /// </returns>
        public static TimeSpan? GetDurationOrDefault( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            bool allowNull,         /* in */
            bool nullOnNotFound     /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    TimeSpan? duration;

                    if (TryGetDuration(
                            (PolicyType)policyType, out duration) &&
                        (allowNull || (duration != null)))
                    {
                        return duration;
                    }
                }

                return nullOnNotFound ? null : defaultDuration;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the duration associated with the specified policy type, or
        /// sets the default duration when no policy type is specified.
        /// </summary>
        /// <param name="policyType">
        /// The policy type for which the duration is being set, or null to
        /// set the default duration.
        /// </param>
        /// <param name="duration">
        /// The duration to set. This value may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the duration was set; otherwise, zero.
        /// </returns>
        public static bool SetDurationOrDefault( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            TimeSpan? duration      /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    return TrySetDuration(
                        (PolicyType)policyType, duration, true);
                }
                else
                {
                    defaultDuration = duration;
                    return true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the duration associated with the specified policy type, or
        /// clears the default duration when no policy type is specified.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose duration is to be removed, or null to clear
        /// the default duration.
        /// </param>
        /// <returns>
        /// Non-zero if the duration was removed or cleared; otherwise, zero.
        /// </returns>
        public static bool UnsetDurationOrDefault( /* CORE */
            PolicyType? policyType /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (policyType != null)
                {
                    return TryUnsetDuration((PolicyType)policyType);
                }
                else
                {
                    defaultDuration = null;
                    return true;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public ViaHttp Flag Methods
        /// <summary>
        /// Determines whether queries should be performed via HTTP.
        /// </summary>
        /// <returns>
        /// Non-zero if queries should be performed via HTTP; otherwise, zero.
        /// </returns>
        public static bool ShouldQueryViaHttp() /* CORE */
        {
            lock (syncRoot)
            {
                return queryViaHttp;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the flag indicating whether queries should be performed via
        /// HTTP.
        /// </summary>
        /// <param name="viaHttp">
        /// Non-zero if queries should be performed via HTTP; otherwise, zero.
        /// </param>
        public static void SetQueryViaHttp( /* CORE */
            bool viaHttp /* in */
            )
        {
            lock (syncRoot)
            {
                queryViaHttp = viaHttp;
            }
        }
        #endregion
    }
}
