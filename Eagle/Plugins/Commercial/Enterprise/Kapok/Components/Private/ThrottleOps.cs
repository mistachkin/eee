/*
 * ThrottleOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

#if KAPOK_PRIVATE || EAGLE_BETA_56
using SlidingThrottleKey =
    Eagle._Containers.Public.ThrottleDictionary.ThrottleKey;

using SlidingThrottleValue = System.Nullable<System.UInt64>;

using SlidingThrottlePair = System.Collections.Generic.KeyValuePair<
    Eagle._Containers.Public.ThrottleDictionary.ThrottleKey, ulong>;

using SlidingThrottleDictionary = Eagle._Containers.Public.ThrottleDictionary;
#endif

using FixedThrottleValue = Eagle._Components.Public.MutableAnyTriplet<
    System.DateTime, long, long>;

using FixedThrottlePair = System.Collections.Generic.KeyValuePair<
    string, Eagle._Components.Public.MutableAnyTriplet<
        System.DateTime, long, long>>;

using FixedThrottleDictionary = System.Collections.Generic.Dictionary<
    string, Eagle._Components.Public.MutableAnyTriplet<
        System.DateTime, long, long>>;

#if KAPOK_PRIVATE
using TokenManagement = Kapok.Components.Shared.SandboxOps.TokenManagement;
#endif

#if KAPOK_PRIVATE
namespace Kapok.Components.Private
#else
namespace LangDemo
#endif
{
    #region API Key Status Enumeration (Shared)
    /// <summary>
    /// Identifies the access status of an API key as determined by the
    /// throttle subsystem, ranging from error and unknown through banned,
    /// anonymous, restricted, standard, and administrator.
    /// </summary>
    [Flags()]
    [ObjectId("ce064a95-b3b6-4adc-a791-0576e0dc0132")]
    internal enum ApiKeyStatus
    {
        /// <summary>
        /// None; implicit only, do not use.
        /// </summary>
        None = 0x0,            /* None, implicit only, do not use. */
        /// <summary>
        /// Explicitly invalid; do not use.
        /// </summary>
        Invalid = 0x1,         /* Explicitly invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// No keys are configured, so the status cannot be determined.
        /// </summary>
        Error = 0x2,           /* No keys configured, etc, cannot determine. */
        /// <summary>
        /// The key was not found; access is denied.
        /// </summary>
        Unknown = 0x4,         /* Key not found, etc, access denied. */
        /// <summary>
        /// The key was found but is administratively banned.
        /// </summary>
        Banned = 0x8,          /* Key found, use administratively banned. */
        /// <summary>
        /// No key was specified; anonymous access is used.
        /// </summary>
        Anonymous = 0x10,      /* No key specified, use anonymous access. */
        /// <summary>
        /// The key was found with restricted (non-administrator) access.
        /// </summary>
        Restricted = 0x20,     /* Key found, no administrator access. */
        /// <summary>
        /// The key was found with standard user access.
        /// </summary>
        Standard = 0x40,       /* Key found, use standard user access. */
        /// <summary>
        /// The key was found with administrator access.
        /// </summary>
        Administrator = 0x80,  /* Key found, use administrator user access. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Error: the internal API key state is null.
        /// </summary>
        NullState = 0x10000,   /* Error: Internal API key state is null. */
        /// <summary>
        /// Error: the internal API key state is empty.
        /// </summary>
        EmptyState = 0x20000,  /* Error: Internal API key state is empty. */

        /// <summary>
        /// Error: the caller-specified API key is null.
        /// </summary>
        NullKey = 0x40000,     /* Error: Caller specified API key is null. */
        /// <summary>
        /// Error: the caller-specified API key is empty.
        /// </summary>
        EmptyKey = 0x80000,    /* Error: Caller specified API key is empty. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default status (standard user access).
        /// </summary>
        Default = Standard     /* Key found, use default user access. */
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Request Throttle Flags Enumeration (Shared)
    /// <summary>
    /// Flags controlling a throttle check, such as which request markers were
    /// present, whether the key is anonymous, and whether to use the
    /// sliding-window algorithm.
    /// </summary>
    [ObjectId("f84e94cb-b521-43c4-9a9c-5cfca3ce9bec")]
    internal enum ThrottleFlags
    {
        /// <summary>
        /// None; implicit only, do not use.
        /// </summary>
        None = 0x0,              /* None, implicit only, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The "raw" query parameter was found.
        /// </summary>
        HaveRaw = 0x1000,        /* The "raw" query parameter found. */
        /// <summary>
        /// The "superRaw" query parameter was found.
        /// </summary>
        HaveSuperRaw = 0x2000,   /* The "superRaw" query parameter found. */
        /// <summary>
        /// The API key in use is not anonymous.
        /// </summary>
        NoAnonymous = 0x4000,    /* The API key in use is not anonymous. */
        /// <summary>
        /// Use the (newer) sliding-window algorithm.
        /// </summary>
        Sliding = 0x8000,        /* Use the (new) sliding-window algorithm. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Indicates that the default limits are in use.
        /// </summary>
        ForDefault = 0x10000000, /* Indicator that defaults are in use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default throttle flags.
        /// </summary>
        Default = ForDefault     /* Enables use of the default flags. */
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Request Throttle Helper Class (Shared)
    /// <summary>
    /// Provides request rate-limiting for the script-evaluation server using
    /// both a fixed-window and a sliding-window algorithm, tracking requests
    /// per client and per server and rejecting requests that exceed the
    /// configured limits.
    /// </summary>
    [ObjectId("0a7694b2-8793-453e-9e08-5d869c4b65dc")]
    internal static class ThrottleOps
    {
        #region Private Constants
        //
        // NOTE: This is the default request type, which is used when a
        //       null request type is not specified by the caller.
        //
        /// <summary>
        /// The default request type used when none is supplied.
        /// </summary>
        private const string DefaultRequestType = "default";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the request key that represents all clients that
        //       are currently using the server.  The associated counts
        //       will be used to make sure the server is not experiencing
        //       a DDoS attack.
        //
        /// <summary>
        /// The request key used for the overall (per-server) limits.
        /// </summary>
        private static readonly string OverallRequestKey = String.Empty;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This is the maximum number of clients that are supported
        //       at one time.  This value is being kept purposely small
        //       for this web application because it is for demonstration
        //       purposes only.  Also, since the interpreter is shared by
        //       all clients, concurrency is naturally limited.
        //
        /// <summary>
        /// The default maximum number of concurrent clients.
        /// </summary>
        private const long DefaultMaximumClientCount = 1;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: *WARNING* This is the maximum number of tracked requests
        //       that a client is allowed to submit between restarts of
        //       the containing process.  This assumes the This count is
        //       designed to last about a month (i.e. monthly patches for
        //       security from Microsoft).  In the future, this assumption
        //       may no longer be valid.
        //
        /// <summary>
        /// The default maximum lifetime request count (fixed window).
        /// </summary>
        private const long DefaultFixedMaximumLifetimeCount = 32;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default number of tracked requests a client is
        //       allowed to submit within a given interval.  It is used when
        //       the specified count is zero.
        //
        /// <summary>
        /// The default maximum request count per window (fixed window).
        /// </summary>
        private const long DefaultFixedMaximumCount = 7;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the default number of seconds considered to be in
        //       the interval for request tracking on a per-client basis.
        //       It is used when the specified number of seconds is zero.
        //
        /// <summary>
        /// The default time window duration, in seconds (fixed window).
        /// </summary>
        private const long DefaultFixedMaximumSeconds = 604800; /* 7 days */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum difference allowed for two "seconds"
        //       quantities to be considered "equal" for the purposes of
        //       this module.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The small tolerance, in seconds, used in time comparisons.
        /// </summary>
        private static double SecondsEpsilon = 0.00001;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the "requests" static
        //       field (below).
        //
        /// <summary>
        /// The object used to synchronize access to the request tables.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This fixed-window "list" holds per-client request tracking
        //       data that is used to prevent requests from being processed
        //       "too fast" for a particular client.  Clients are identified
        //       by their IP address.
        //
        // TODO: Maybe this should be per-thread instead of locking?  That
        //       seems a bit wasteful as per-thread variables are slow and
        //       and require more storage.  Also, that will not work right,
        //       unless client requests always have thread affinity.
        //
        /// <summary>
        /// The fixed-window request tracking table, keyed by client request
        /// key.
        /// </summary>
        private static FixedThrottleDictionary fixedRequests = null;

        ///////////////////////////////////////////////////////////////////////

#if KAPOK_PRIVATE || EAGLE_BETA_56
        //
        // NOTE: This sliding-window "list" holds per-client request tracking
        //       data that is used to prevent requests from being processed
        //       "too fast" for a particular client.  Clients are identified
        //       by their IP address.
        //
        // TODO: Maybe this should be per-thread instead of locking?  That
        //       seems a bit wasteful as per-thread variables are slow and
        //       and require more storage.  Also, that will not work right,
        //       unless client requests always have thread affinity.
        //
        /// <summary>
        /// The sliding-window request tracking table.
        /// </summary>
        private static SlidingThrottleDictionary slidingRequests = null;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Gets the elapsed seconds between two times.
        /// </summary>
        /// <param name="now">
        /// The current time.
        /// </param>
        /// <param name="dateTime">
        /// The earlier time.
        /// </param>
        /// <returns>
        /// The elapsed seconds.
        /// </returns>
        private static long GetTotalSeconds(
            DateTime now,     /* in */
            DateTime dateTime /* in */
            )
        {
            return (long)now.Subtract(dateTime).TotalSeconds;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the effective request type, substituting the default when none
        /// is supplied.
        /// </summary>
        /// <param name="requestType">
        /// The requested request type, if any.
        /// </param>
        /// <returns>
        /// The effective request type.
        /// </returns>
        private static string GetRequestType(
            string requestType /* in: OPTIONAL */
            )
        {
            return !String.IsNullOrEmpty(requestType) ?
                requestType : DefaultRequestType;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the client request key from the host address and request
        /// type.
        /// </summary>
        /// <param name="userHostAddress">
        /// The client host or IP address.
        /// </param>
        /// <param name="requestType">
        /// The request type.
        /// </param>
        /// <returns>
        /// The request key.
        /// </returns>
        private static string GetRequestKey(
            string userHostAddress, /* in */
            string requestType      /* in: OPTIONAL */
            )
        {
            if (userHostAddress == null)
            {
#if KAPOK_PRIVATE
                userHostAddress = Utility.FormatMaybeNull(null) as string;
#else
                userHostAddress = FormatOps.NullResult;
#endif
            }

            return String.Format(
                "{0}-{1}", userHostAddress, GetRequestType(requestType));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds an error to the error list when a result is present.
        /// </summary>
        /// <param name="result">
        /// The result to add, if any.
        /// </param>
        /// <param name="errors">
        /// The error list to add to.
        /// </param>
        /// <param name="error">
        /// The error to add.
        /// </param>
        private static void MaybeAddError(
            bool result,           /* in */
            ref ResultList errors, /* in, out */
            Result error           /* in */
            )
        {
            if (result)
                return;

            AddError(ref errors, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds an error to the error list.
        /// </summary>
        /// <param name="errors">
        /// The error list to add to.
        /// </param>
        /// <param name="error">
        /// The error to add.
        /// </param>
        private static void AddError(
            ref ResultList errors, /* in, out */
            Result error           /* in */
            )
        {
            if (error != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied throttle flags contain the given
        /// flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        private static bool HasFlags(
            ThrottleFlags flags,    /* in */
            ThrottleFlags hasFlags, /* in */
            bool all                /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != ThrottleFlags.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a request appears to be non-human (missing the
        /// expected query markers and using a non-standard key).
        /// </summary>
        /// <param name="throttleFlags">
        /// The throttle flags for the request.
        /// </param>
        /// <param name="apiKeyStatus">
        /// The API key status for the request.
        /// </param>
        /// <returns>
        /// Non-zero when the request appears non-human; otherwise, zero.
        /// </returns>
        private static bool IsNonHumanRequest(
            ThrottleFlags throttleFlags, /* in */
            ApiKeyStatus apiKeyStatus    /* in */
            )
        {
            //
            // NOTE: Was the request submitted via the public API endpoint?
            //       As of this writing (2018-08-14), bots are not known to
            //       use this.
            //
            if (HasFlags(throttleFlags, ThrottleFlags.HaveRaw, true))
                return false;

            //
            // NOTE: Was this request submitted via the JavaScript snippet
            //       on the web page?  As of this writing (2018-08-14), no
            //       bots are known to use this.
            //
            if (HasFlags(throttleFlags, ThrottleFlags.HaveSuperRaw, true))
                return false;

            //
            // NOTE: If the API key associated with the request is standard
            //       or better (i.e. not "anonymous"), then the request is
            //       almost certainly from a real human or official client
            //       code.
            //
#if KAPOK_PRIVATE
            if (HasFlags(throttleFlags, ThrottleFlags.NoAnonymous, true))
                return false;
#else
            if (ApiKeyOps.IsStandardOrBetter(apiKeyStatus))
                return false;
#endif

            //
            // NOTE: By default, assume the worst.
            //
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a count has exceeded its maximum.
        /// </summary>
        /// <param name="count">
        /// The current count.
        /// </param>
        /// <param name="maximumCount">
        /// The maximum request count per window.
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <returns>
        /// Non-zero when the count is exceeded; otherwise, zero.
        /// </returns>
        private static bool IsCountExceeded(
            long count,        /* in */
            long maximumCount, /* in */
            bool inclusive     /* in */
            )
        {
            return inclusive ?
                (count >= maximumCount) : (count > maximumCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether an elapsed time has exceeded its maximum.
        /// </summary>
        /// <param name="seconds">
        /// The elapsed seconds.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <returns>
        /// Non-zero when the time is exceeded; otherwise, zero.
        /// </returns>
        private static bool AreSecondsExceeded(
            double seconds,        /* in */
            double maximumSeconds, /* in */
            bool inclusive         /* in */
            )
        {
            if (seconds > maximumSeconds)
                return true;

            if (inclusive && (Math.Abs( /* EQUALS (?) */
                    maximumSeconds - seconds) <= SecondsEpsilon))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a fixed-window request exceeds the configured
        /// limits, recording any violation.
        /// </summary>
        /// <param name="requestKey">
        /// The client request key being checked.
        /// </param>
        /// <param name="maximumLifetimeCount">
        /// The maximum lifetime request count.
        /// </param>
        /// <param name="maximumCount">
        /// The maximum request count per window.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="exempt">
        /// Non-zero when the caller is exempt from the limits (for example, an
        /// administrator).
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <param name="server">
        /// Non-zero to apply the per-server limits rather than the per-client
        /// limits.
        /// </param>
        /// <param name="errors">
        /// On a bad request, receives the throttle violation errors.
        /// </param>
        /// <returns>
        /// Non-zero when the request is bad (throttled); otherwise, zero.
        /// </returns>
        private static bool IsBadFixedRequest(
            string requestKey,         /* in */
            long maximumLifetimeCount, /* in */
            long maximumCount,         /* in */
            long maximumSeconds,       /* in */
            bool exempt,               /* in */
            bool inclusive,            /* in */
            bool server,               /* in */
            ref ResultList errors      /* out */
            )
        {
            //
            // NOTE: What is the "reference time" now?  This should be UTC.
            //
            DateTime now = Utility.GetUtcNow();

            //
            // NOTE: If the caller specified a maximum number of seconds as
            //       zero, use the default instead.
            //
            long fixedMaximumSeconds;

            if (maximumSeconds == 0)
                fixedMaximumSeconds = DefaultFixedMaximumSeconds;
            else
                fixedMaximumSeconds = maximumSeconds;

            //
            // HACK: Hold the lock on the requests dictionary for the entire
            //       time we are checking the request.  In theory, this will
            //       limit overall concurrency of the web server; however,
            //       that being said, nothing within this block should be
            //       that time-consuming.  Perhaps a lock-free dictionary
            //       could be used here?
            //
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fixedRequests == null)
                {
                    //
                    // NOTE: The request cannot possibly be too fast
                    //       because there are not any requests yet;
                    //       however, we still need to keep track of
                    //       this request.
                    //
                    fixedRequests = new FixedThrottleDictionary();
                }

                //
                // NOTE: If the caller specified an invalid request
                //       key, we cannot proceed.
                //
                if (requestKey == null)
                {
                    MaybeAddError(
                        exempt, ref errors,
                        "invalid request key");

                    RemoveStaleFixedRequests(
                        now, fixedMaximumSeconds, inclusive);

                    return !exempt;
                }

                //
                // NOTE: Grab the request count information for this
                //       client, if any.
                //
                FixedThrottleValue value;

                if (fixedRequests.TryGetValue(requestKey, out value))
                {
                    //
                    // NOTE: If the request information is null, that
                    //       may mean this client has been banned by
                    //       an administrator.  If the client is also
                    //       an administrator, this has no effect.
                    //
                    if (value == null)
                    {
                        MaybeAddError(
                            exempt, ref errors, String.Format(
                            "invalid throttle value for request key {0}",
                            Utility.FormatWrapOrNull(requestKey)));

                        RemoveStaleFixedRequests(
                            now, fixedMaximumSeconds, inclusive);

                        return !exempt;
                    }

                    //
                    // NOTE: At this point, include the current request
                    //       in lifetime tracking data for this client.
                    //
                    value.Z++;

                    //
                    // NOTE: If the caller specified a maximum lifetime
                    //       count of zero, use the default instead.
                    //
                    if (maximumLifetimeCount == 0)
                        maximumLifetimeCount = DefaultFixedMaximumLifetimeCount;

                    //
                    // NOTE: If the lifetime number of requests for
                    //       this client exceeds the maximum lifetime
                    //       limit, this request is "too fast".  This
                    //       assumes the web server will be restarted
                    //       as the count is hard-coded and does not
                    //       depend on any DateTime values.  Handling
                    //       for this is disabled when the maximum
                    //       lifetime count is zero (or less).
                    //
                    if ((maximumLifetimeCount > 0) && IsCountExceeded(
                            value.Z, maximumLifetimeCount, inclusive))
                    {
                        MaybeAddError(
                            exempt, ref errors, String.Format(
                            "lifetime count exceeded: {0} {2} {1}",
                            value.Z, maximumLifetimeCount, inclusive ?
                            ">=" : ">"));

                        RemoveStaleFixedRequests(
                            now, fixedMaximumSeconds, inclusive);

                        return !exempt;
                    }

                    //
                    // NOTE: Attempt to figure out how many seconds
                    //       have elapsed since the first tracked
                    //       request for this client.
                    //
                    long seconds = GetTotalSeconds(now, value.X);

                    //
                    // NOTE: If the elapsed number of seconds is zero,
                    //       this request is obviously (?) "too fast";
                    //       if the number of seconds is somehow less
                    //       than zero, which should be impossible, it
                    //       will also be considered "too fast".
                    //
                    if (!server && (seconds <= 0))
                    {
                        MaybeAddError(
                            exempt, ref errors, String.Format(
                            "total seconds: {0} <= zero", seconds));

                        RemoveStaleFixedRequests(
                            now, fixedMaximumSeconds, inclusive);

                        return !exempt;
                    }

                    //
                    // NOTE: Check the elapsed number of seconds against
                    //       the maximum number of seconds specified by
                    //       the caller.  If it is greater, adjustments
                    //       need to be made for the request tracking
                    //       data for this client.  Handling for this is
                    //       disabled when the maximum seconds is zero
                    //       (or less).
                    //
                    if ((fixedMaximumSeconds > 0) && AreSecondsExceeded(
                            seconds, fixedMaximumSeconds, inclusive))
                    {
                        //
                        // NOTE: Reset the interval starting point to
                        //       now.  This is relatively safe because
                        //       the count will also be adjusted.
                        //
                        value.X = now;

                        //
                        // NOTE: Since the elapsed number of seconds is
                        //       now known to be higher than the maximum
                        //       number of seconds, we divide it by the
                        //       maximum number of seconds, which should
                        //       result in a number of "intervals" (an
                        //       integer) greater than or equal to one.
                        //       The resulting value will be used as the
                        //       divisor to reduce the overall tracked
                        //       request count for this client.
                        //
                        long intervals = seconds / fixedMaximumSeconds;

                        //
                        // NOTE: Divide request count for this interval
                        //       and client by a value.  The lifetime
                        //       value for this client is NOT adjusted.
                        //       Since we know the number of seconds is
                        //       (at least one) greater than the maximum
                        //       number of seconds, we should always at
                        //       least reduce the request count by one,
                        //       even if the number of whole intervals
                        //       is only one; otherwise, the client will
                        //       always have to wait at least double the
                        //       interval time in order to issue another
                        //       request.
                        //
                        if (intervals == 1)
                        {
                            //
                            // NOTE: Subtract one from the request count
                            //       -AND- one to preemptively undo the
                            //       subsequent increment below.  If the
                            //       new value would be below zero, use
                            //       zero instead.
                            //
                            value.Y = Math.Max(0, value.Y - 2);
                        }
                        else if (intervals > 0) /* SANITY */
                        {
                            //
                            // NOTE: Divide the request count by the
                            //       number of whole intervals, which
                            //       must be greater than one at this
                            //       point.
                            //
                            value.Y /= intervals;
                        }
                    }

                    //
                    // NOTE: At this point, include the current request
                    //       in the tracking data for this client.
                    //
                    // TODO: This is actually very strict.  The current
                    //       request may (yet) not be allowed; however,
                    //       we are counting it against the client for
                    //       the fixed window anyhow?
                    //
                    value.Y++;

                    //
                    // NOTE: If the caller specified a maximum count of
                    //       zero, use the default instead.
                    //
                    long fixedMaximumCount = 0;

                    if (maximumCount == 0)
                        fixedMaximumCount = DefaultFixedMaximumCount;
                    else
                        fixedMaximumCount = maximumCount;

                    //
                    // NOTE: If the incremented (and possibly adjusted)
                    //       request count exceeds the maximum specified
                    //       by the caller, this request is "too fast".
                    //       Handling for this is disabled when the
                    //       maximum count is zero (or less).
                    //
                    if ((fixedMaximumCount > 0) && IsCountExceeded(
                            value.Y, fixedMaximumCount, inclusive))
                    {
                        MaybeAddError(
                            exempt, ref errors, String.Format(
                            "count exceeded: {0} {2} {1}",
                            value.Y, fixedMaximumCount, inclusive ?
                            ">=" : ">"));

                        RemoveStaleFixedRequests(
                            now, fixedMaximumSeconds, inclusive);

                        return !exempt;
                    }
                }
                else
                {
                    //
                    // NOTE: No previous requests match this one, so
                    //       it cannot possibly be too fast.
                    //
                    value = new FixedThrottleValue(true, now, 1, 1);

                    fixedRequests.Add(requestKey, value);
                }
            }

            RemoveStaleFixedRequests(
                now, fixedMaximumSeconds, inclusive);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes stale fixed-window request entries older than the maximum
        /// seconds.
        /// </summary>
        /// <param name="now">
        /// The current time.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <returns>
        /// The number of entries removed.
        /// </returns>
        private static int RemoveStaleFixedRequests(
            DateTime now,        /* in */
            long maximumSeconds, /* in */
            bool inclusive       /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (fixedRequests != null)
                {
                    FixedThrottleDictionary localRequests =
                        new FixedThrottleDictionary(fixedRequests);

                    foreach (FixedThrottlePair pair in localRequests)
                    {
                        FixedThrottleValue value = pair.Value;

                        if (value == null)
                            continue;

                        TimeSpan age = now.Subtract(value.X);

                        if (AreSecondsExceeded(age.TotalSeconds,
                                maximumSeconds, inclusive))
                        {
                            string key = pair.Key;

                            if (fixedRequests.Remove(key))
                                count++;
                        }
                    }
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets (clears) the fixed-window request table.
        /// </summary>
        /// <returns>
        /// The number of entries removed.
        /// </returns>
        private static int ResetFixedRequests()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (fixedRequests != null)
                {
                    count += fixedRequests.Count;

                    fixedRequests.Clear();
                    fixedRequests = null;
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a snapshot copy of the fixed-window request table.
        /// </summary>
        /// <returns>
        /// A copy of the fixed-window request table.
        /// </returns>
        private static FixedThrottleDictionary CopyFixedRequests()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                FixedThrottleDictionary localRequests = null;

                if (fixedRequests != null)
                {
                    localRequests = new FixedThrottleDictionary();

                    foreach (FixedThrottlePair pair in fixedRequests)
                    {
                        FixedThrottleValue value = pair.Value;

                        if (value == null)
                            continue;

                        string key = pair.Key;

                        localRequests.Add(key,
                            new FixedThrottleValue(false, value.X,
                            value.Y, value.Z)); /* DEEP COPY */
                    }
                }

                return localRequests;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if KAPOK_PRIVATE || EAGLE_BETA_56
        /// <summary>
        /// Determines whether a sliding-window request exceeds the configured
        /// limits, recording any violation.
        /// </summary>
        /// <param name="requestKey">
        /// The client request key being checked.
        /// </param>
        /// <param name="maximumLifetimeCount">
        /// The maximum lifetime request count.
        /// </param>
        /// <param name="maximumCount">
        /// The maximum request count per window.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="exempt">
        /// Non-zero when the caller is exempt from the limits (for example, an
        /// administrator).
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <param name="server">
        /// Non-zero to apply the per-server limits rather than the per-client
        /// limits.
        /// </param>
        /// <param name="errors">
        /// On a bad request, receives the throttle violation errors.
        /// </param>
        /// <returns>
        /// Non-zero when the request is bad (throttled); otherwise, zero.
        /// </returns>
        private static bool IsBadSlidingRequest(
            string requestKey,         /* in */
            long maximumLifetimeCount, /* in: NOT USED */
            long maximumCount,         /* in */
            long maximumSeconds,       /* in */
            bool exempt,               /* in */
            bool inclusive,            /* in */
            bool server,               /* in: NOT USED */
            ref ResultList errors      /* out */
            )
        {
            //
            // NOTE: What is the "reference time" now?  This should be UTC.
            //
            DateTime now = Utility.GetUtcNow();

            //
            // NOTE: If the caller specified a maximum number of seconds as
            //       zero, use the default instead.
            //
            ulong? slidingMaximumSeconds;

            if (maximumSeconds == 0)
                slidingMaximumSeconds = null;
            else
                slidingMaximumSeconds = (ulong)maximumSeconds;

            //
            // HACK: Hold the lock on the requests dictionary for the entire
            //       time we are checking the request.  In theory, this will
            //       limit overall concurrency of the web server; however,
            //       that being said, nothing within this block should be
            //       that time-consuming.  Perhaps a lock-free dictionary
            //       could be used here?
            //
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (slidingRequests == null)
                {
                    //
                    // NOTE: The request cannot possibly be too fast
                    //       because there are not any requests yet;
                    //       however, we still need to keep track of
                    //       this request.
                    //
                    slidingRequests = new SlidingThrottleDictionary();
                }

                //
                // NOTE: If the caller specified an invalid request
                //       key, we cannot proceed.
                //
                if (requestKey == null)
                {
                    MaybeAddError(
                        exempt, ref errors,
                        "invalid request key");

                    RemoveStaleSlidingRequests(
                        now, slidingMaximumSeconds, inclusive);

                    return !exempt;
                }

                //
                // NOTE: If the caller specified a maximum count of
                //       zero, use the default instead.
                //
                ulong? slidingMaximumCount;

                if (maximumCount == 0)
                    slidingMaximumCount = null;
                else
                    slidingMaximumCount = (ulong)maximumCount;

                //
                // NOTE: Attempt to increment the request count for
                //       the client now, based on the sliding-window
                //       between X seconds ago and now.
                //
                SlidingThrottleValue value;

                if (!slidingRequests.TryIncrement(
                        requestKey, now, slidingMaximumSeconds,
                        slidingMaximumCount, inclusive, out value))
                {
                    string valueString;

#if KAPOK_PRIVATE
                    valueString = Utility.FormatMaybeNull(
                        value).ToString();
#else
                    valueString = FormatOps.NullResult;
#endif

                    string countString;

#if KAPOK_PRIVATE
                    countString = Utility.FormatMaybeNull(
                        slidingMaximumCount).ToString();
#else
                    countString = FormatOps.NullResult;
#endif

                    MaybeAddError(
                        exempt, ref errors, String.Format(
                        "count exceeded: {0} {2} {1}",
                        valueString, countString, inclusive ?
                        ">=" : ">"));

                    RemoveStaleSlidingRequests(
                        now, slidingMaximumSeconds, inclusive);

                    return !exempt;
                }
            }

            RemoveStaleSlidingRequests(
                now, slidingMaximumSeconds, inclusive);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes stale sliding-window request entries older than the maximum
        /// seconds.
        /// </summary>
        /// <param name="now">
        /// The current time.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="inclusive">
        /// Non-zero to treat the maximum as inclusive.
        /// </param>
        /// <returns>
        /// The number of entries removed.
        /// </returns>
        private static int RemoveStaleSlidingRequests(
            DateTime now,          /* in */
            ulong? maximumSeconds, /* in */
            bool inclusive         /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (slidingRequests != null)
                {
                    SlidingThrottleDictionary localRequests =
                        new SlidingThrottleDictionary(slidingRequests);

                    foreach (SlidingThrottlePair pair in localRequests)
                    {
                        SlidingThrottleKey key = pair.Key;

                        if (key == null) /* IMPOSSIBLE? */
                            continue;

                        TimeSpan age = now.Subtract(key.Y);

                        if (((maximumSeconds == null) ||
                            AreSecondsExceeded(age.TotalSeconds,
                                (ulong)maximumSeconds, inclusive)) &&
                            slidingRequests.Remove(key))
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets (clears) the sliding-window request table.
        /// </summary>
        /// <returns>
        /// The number of entries removed.
        /// </returns>
        private static int ResetSlidingRequests()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (slidingRequests != null)
                {
                    count += slidingRequests.Count;

                    slidingRequests.Clear();
                    slidingRequests = null;
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a snapshot copy of the sliding-window request table.
        /// </summary>
        /// <returns>
        /// A copy of the sliding-window request table.
        /// </returns>
        private static SlidingThrottleDictionary CopySlidingRequests()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                SlidingThrottleDictionary localRequests = null;

                if (slidingRequests != null)
                {
                    localRequests = new SlidingThrottleDictionary();

                    foreach (SlidingThrottlePair pair in slidingRequests)
                    {
                        SlidingThrottleValue value = pair.Value;

                        if (value == null)
                            continue;

                        SlidingThrottleKey key = pair.Key;

                        if (key == null) /* IMPOSSIBLE? */
                            continue;

                        localRequests.Add(key, (ulong)value);
                    }
                }

                return localRequests;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Resets (clears) the fixed- or sliding-window request table.
        /// </summary>
        /// <param name="sliding">
        /// Non-zero to reset the sliding-window table; zero for the
        /// fixed-window table.
        /// </param>
        /// <returns>
        /// The number of entries removed.
        /// </returns>
        public static int ResetRequests(
            bool sliding /* in */
            )
        {
            if (sliding)
            {
#if KAPOK_PRIVATE || EAGLE_BETA_56
                return ResetSlidingRequests();
#else
                return 0;
#endif
            }
            else
            {
                return ResetFixedRequests();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a snapshot copy of the fixed- or sliding-window request table.
        /// </summary>
        /// <param name="sliding">
        /// Non-zero to copy the sliding-window table; zero for the
        /// fixed-window table.
        /// </param>
        /// <returns>
        /// A copy of the requested table.
        /// </returns>
        public static IDictionary CopyRequests(
            bool sliding /* in */
            )
        {
            if (sliding)
            {
#if KAPOK_PRIVATE || EAGLE_BETA_56
                return CopySlidingRequests();
#else
                return null;
#endif
            }
            else
            {
                return CopyFixedRequests();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a fixed-window tracking value for display.
        /// </summary>
        /// <param name="value">
        /// The fixed-window value to format.
        /// </param>
        /// <returns>
        /// The formatted value.
        /// </returns>
        public static string FormatFixedValue(
            FixedThrottleValue value /* in: OPTIONAL */
            )
        {
            if (value == null)
            {
#if KAPOK_PRIVATE
                return Utility.FormatMaybeNull(null) as string;
#else
                return FormatOps.NullResult;
#endif
            }

            string dateTimeString;

#if KAPOK_PRIVATE
            dateTimeString = Utility.FormatIso8601FullDateTime(value.X);
#else
            dateTimeString = FormatOps.DateTime(value.X, true);
#endif

            return StringList.MakeList(dateTimeString, value.Y, value.Z);
        }

        ///////////////////////////////////////////////////////////////////////

#if KAPOK_PRIVATE || EAGLE_BETA_56
        /// <summary>
        /// Formats a sliding-window tracking entry for display.
        /// </summary>
        /// <param name="key">
        /// The sliding-window entry key.
        /// </param>
        /// <param name="value">
        /// The sliding-window entry value.
        /// </param>
        /// <returns>
        /// The formatted entry.
        /// </returns>
        public static string FormatSlidingValue(
            SlidingThrottleKey key,    /* in: OPTIONAL */
            SlidingThrottleValue value /* in: OPTIONAL */
            )
        {
            string valueString;

#if KAPOK_PRIVATE
            valueString = Utility.FormatMaybeNull(value).ToString();
#else
            valueString = (value != null) ?
                value.ToString() : FormatOps.NullResult;
#endif

            string keyString; /* REUSED */

            if (key == null)
            {
#if KAPOK_PRIVATE
                keyString = Utility.FormatMaybeNull(null) as string;
#else
                keyString = FormatOps.NullResult;
#endif

                return StringList.MakeList(keyString, valueString);
            }

            string dateTimeString;

#if KAPOK_PRIVATE
            keyString = Utility.FormatMaybeNull(key.X) as string;
            dateTimeString = Utility.FormatIso8601FullDateTime(key.Y);
#else
            keyString = key.X;

            if (keyString == null)
                keyString = FormatOps.NullResult;

            dateTimeString = FormatOps.DateTime(key.Y, true);
#endif

            return StringList.MakeList(
                keyString, dateTimeString, valueString);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a request should be rejected by the throttle
        /// subsystem, checking the key status, the per-client limits, and the
        /// per-server limits, while exempting administrators.
        /// </summary>
        /// <param name="apiKeyId">
        /// The API key identifier, if any.
        /// </param>
        /// <param name="userHostAddress">
        /// The client host or IP address.
        /// </param>
        /// <param name="throttleFlags">
        /// The throttle flags for the request.
        /// </param>
        /// <param name="apiKeyStatus">
        /// The API key status for the request.
        /// </param>
        /// <param name="requestType">
        /// The request type.
        /// </param>
        /// <param name="maximumLifetimeCount">
        /// The maximum lifetime request count.
        /// </param>
        /// <param name="maximumCount">
        /// The maximum request count per window.
        /// </param>
        /// <param name="maximumSeconds">
        /// The time window duration, in seconds.
        /// </param>
        /// <param name="maximumClientCount">
        /// The maximum number of concurrent clients.
        /// </param>
        /// <param name="errors">
        /// On a bad request, receives the throttle violation errors.
        /// </param>
        /// <returns>
        /// Non-zero when the request is bad (should be rejected); otherwise,
        /// zero.
        /// </returns>
        public static bool IsBadRequest(
            Guid? apiKeyId,              /* in: OPTIONAL */
            string userHostAddress,      /* in */
            ThrottleFlags throttleFlags, /* in */
            ApiKeyStatus apiKeyStatus,   /* in */
            string requestType,          /* in: OPTIONAL */
            long maximumLifetimeCount,   /* in */
            long maximumCount,           /* in */
            long maximumSeconds,         /* in */
            long maximumClientCount,     /* in */
            ref ResultList errors        /* out */
            )
        {
            //
            // NOTE: Is API key status "error" or "unknown"?  If so, this
            //       must always be considered to be an invalid request.
            //
#if KAPOK_PRIVATE
            if ((apiKeyId == null) ||
                TokenManagement.IsDenied((Guid)apiKeyId))
            {
                AddError(ref errors, String.Format(
                    "unknown or denied API key identifier {0}",
                    Utility.FormatMaybeNull(apiKeyId)));

                return true;
            }
#else
            if (ApiKeyOps.IsErrorOrUnknown(apiKeyStatus))
            {
                AddError(ref errors, String.Format(
                    "bad API key status {0}", apiKeyStatus));

                return true;
            }
#endif

            //
            // NOTE: Check if this is an administrator.  If so, the request
            //       counts will still be tracked; however, limits will not
            //       be enforced.
            //
            bool administrator;

#if KAPOK_PRIVATE
            administrator = TokenManagement.IsAdministrator((Guid)apiKeyId);
#else
            administrator = ApiKeyOps.IsAdministrator(apiKeyStatus);
#endif

            //
            // NOTE: If the request appears to be a bot of some kind, prevent
            //       the caller from actually handling the request unless the
            //       API key also happens to belong to an administrator.
            //
            // HACK: This non-human check is valid because the front-end web
            //       page now always sends scripts via XMLHttpRequest objects
            //       with the "superRaw" query parameter set.  Therefore, the
            //       "superRaw" query parameter being missing indicates some
            //       kind of custom front-end is being used (i.e. perhaps an
            //       automated script or bot?).  The "raw" query parameter is
            //       also considered here as that is always used by the Eagle
            //       core script library procedure [evaluateInRemoteSandbox].
            //
            if (IsNonHumanRequest(throttleFlags, apiKeyStatus))
            {
                MaybeAddError(
                    administrator, ref errors,
                    "detected non-human request");

                return !administrator;
            }

            //
            // NOTE: Use the IP address of the client to lookup the count of
            //       requests we are interested in.  If it is null or an empty
            //       string, the request cannot be considered valid unless the
            //       API key belongs to an administrator.  This may need to be
            //       changed in the future.
            //
            string requestKey = GetRequestKey(userHostAddress, requestType);

            if (String.IsNullOrEmpty(requestKey))
            {
                MaybeAddError(
                    administrator, ref errors,
                    "request key is invalid or empty");

                return !administrator;
            }

            //
            // NOTE: If the caller specified a maximum client count count of
            //       zero, use the default instead.
            //
            if (maximumClientCount == 0)
                maximumClientCount = DefaultMaximumClientCount;

            //
            // NOTE: Make sure that requests are not being submitted too fast
            //       for this client -AND- for the overall server itself.  It
            //       should be noted that the number of seconds is fixed here
            //       because the overall server limit is for concurrency, not
            //       a usage limit.  Also, there is no lifetime limit for the
            //       overall server itself (i.e. as that is not concurrency).
            //
            bool sliding = HasFlags(
                throttleFlags, ThrottleFlags.Sliding, true);

            bool clientOk;
            bool serverOk;

            if (sliding)
            {
#if KAPOK_PRIVATE || EAGLE_BETA_56
                clientOk = !IsBadSlidingRequest( /* SIDE-EFFECTS */
                    requestKey, maximumLifetimeCount, maximumCount,
                    maximumSeconds, administrator, true, false,
                    ref errors);

                serverOk = !IsBadSlidingRequest( /* SIDE-EFFECTS */
                    GetRequestType(requestType), -1 /* UNLIMITED */,
                    maximumCount * maximumClientCount, maximumSeconds,
                    administrator, true, true, ref errors);
#else
                clientOk = false;
                serverOk = false;
#endif
            }
            else
            {
                clientOk = !IsBadFixedRequest( /* SIDE-EFFECTS */
                    requestKey, maximumLifetimeCount, maximumCount,
                    maximumSeconds, administrator, true, false,
                    ref errors);

                serverOk = !IsBadFixedRequest( /* SIDE-EFFECTS */
                    GetRequestType(requestType), -1 /* UNLIMITED */,
                    maximumCount * maximumClientCount, maximumSeconds,
                    administrator, true, true, ref errors);
            }

            if (!clientOk || !serverOk)
            {
                MaybeAddError(
                    administrator, ref errors, String.Format(
                    "{0} client status: {1}, {0} server status: {2}",
                    sliding ? "SLIDING" : "FIXED", clientOk ?
                    "GOOD" : "BAD", serverOk ? "GOOD" : "BAD"));

                return !administrator;
            }

            return false;
        }
        #endregion
    }
    #endregion
}
