/*
 * CertificateNetworkState.cs --
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
using This = Licensing.Components.Private.CertificateNetworkState;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Tracks the state associated with periodic network access checks
    /// (e.g. NTP and HTTPS connectivity) performed by the plugin and/or
    /// the license manager.  Provides thread-safe access to the network
    /// state flags, the check count, the time of the last completed check,
    /// and the maximum number of retries passed into the core library.
    /// </summary>
    [ObjectId("e5ebbe90-98cd-43bf-a82b-e38dd90d555b")]
    internal static class CertificateNetworkState
    {
        #region Private Constants
        //
        // NOTE: This is the (minimum?) number of minutes between network
        //       access checks.
        //
        /// <summary>
        /// The (minimum?) number of minutes that must elapse between
        /// network access checks.
        /// </summary>
        private static int checkMinutes = 480; /* 8 hours */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the network state
        //       flags (below).
        //
        /// <summary>
        /// Used to synchronize access to the network state flags (below).
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These flags will be changed primarily in response to the
        //       network access checks performed by the plugin and/or the
        //       license manager.
        //
        /// <summary>
        /// The network state flags.  These are changed primarily in
        /// response to the network access checks performed by the plugin
        /// and/or the license manager.
        /// </summary>
        private static NetworkFlags networkFlags;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the number of times that network access checks
        //       have been performed.
        //
        /// <summary>
        /// The number of times that network access checks have been
        /// performed.
        /// </summary>
        private static int checkCount;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the last date/time when network access checks
        //       were actually completed.
        //
        /// <summary>
        /// The last date/time when network access checks were actually
        /// completed, or null if they have not been completed.
        /// </summary>
        private static DateTime? @checked;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This the default maximum number of retries value that
        //       will be passed into the core library.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The default maximum number of retries value that will be passed
        /// into the core library.  This is purposely not read-only.
        /// </summary>
        private static int? defaultMaximumRetries = 2;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This the maximum number of retries value that will be
        //       passed into the core library.
        //
        /// <summary>
        /// The maximum number of retries value that will be passed into
        /// the core library, or null to use
        /// <see cref="defaultMaximumRetries" />.
        /// </summary>
        private static int? maximumRetries;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether at least one network access check has been
        /// performed.
        /// </summary>
        /// <returns>
        /// Non-zero if the check count is greater than zero; otherwise,
        /// zero.
        /// </returns>
        private static bool HasBeenChecked()
        {
            return Interlocked.CompareExchange(
                ref checkCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Calculates the number of whole minutes that have elapsed since
        /// network access checks were last completed.
        /// </summary>
        /// <param name="now">
        /// The current date/time to measure against the last completed
        /// check time.
        /// </param>
        /// <returns>
        /// The number of minutes since the last completed check, or zero if
        /// no check has been completed.
        /// </returns>
        private static int MinutesSinceChecked(
            DateTime now /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (@checked == null)
                    return 0;

                return (int)now.Subtract(
                    (DateTime)@checked).TotalMinutes;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the last completed check date/time, indicating that no
        /// network access check has been completed.
        /// </summary>
        private static void ResetCheckedNow()
        {
            lock (syncRoot)
            {
                @checked = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the last completed check date/time to the specified value.
        /// </summary>
        /// <param name="now">
        /// The date/time to record as the last completed check time.
        /// </param>
        private static void SetCheckedNow(
            DateTime now /* in */
            )
        {
            lock (syncRoot)
            {
                @checked = now;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Checked Count / When Methods
        /// <summary>
        /// Determines whether network access checks have been performed
        /// recently, i.e. at least one check has been performed and not too
        /// many minutes have elapsed since the last completed check.
        /// </summary>
        /// <param name="now">
        /// The current date/time used to measure elapsed time since the
        /// last completed check, or null to skip the elapsed time test.
        /// </param>
        /// <param name="defaultAppDomainOnly">
        /// Non-zero if only the primary application domain should be
        /// considered; when set, non-primary application domains are
        /// treated as checked when other application domains exist.
        /// </param>
        /// <returns>
        /// Non-zero if a check has been performed recently; otherwise,
        /// zero.
        /// </returns>
        public static bool WasCheckedRecently(
            DateTime? now,            /* in */
            bool defaultAppDomainOnly /* in */
            )
        {
#if APPDOMAINS || ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            //
            // HACK: If only the primary AppDomain should
            //       be considered here, skip any further
            //       checking if this AppDomain is not
            //       primary -AND- other AppDomains have
            //       been created -OR- unloaded (via the
            //       core library).
            //
            if (defaultAppDomainOnly &&
                !Utility.IsDefaultAppDomain() &&
                CertificateSharedOps.HaveOtherAppDomains())
            {
                return true;
            }
#endif

            if (!HasBeenChecked())
                return false;

            if ((now != null) && (MinutesSinceChecked(
                    (DateTime)now) > checkMinutes))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the recently-checked state by clearing the check count
        /// and the last completed check date/time.
        /// </summary>
        public static void ResetCheckedRecently()
        {
            /* IGNORED */
            Interlocked.Exchange(ref checkCount, 0);

            /* NO RESULT */
            ResetCheckedNow();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records that a network access check has been performed by
        /// incrementing the check count and, when a date/time is supplied,
        /// updating the last completed check time.
        /// </summary>
        /// <param name="now">
        /// The date/time to record as the last completed check time, or
        /// null to leave the last completed check time unchanged.
        /// </param>
        public static void SetCheckedRecently(
            DateTime? now /* in */
            )
        {
            /* IGNORED */
            Interlocked.Increment(ref checkCount);

            if (now != null)
            {
                /* NO RESULT */
                SetCheckedNow((DateTime)now);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Flag Methods
        /// <summary>
        /// Determines whether the NTP network access check has succeeded.
        /// </summary>
        /// <returns>
        /// Non-zero if the NTP-OK flag is set; otherwise, zero.
        /// </returns>
        public static bool IsNtpOk()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.NtpOk, true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the NTP-OK flag, indicating that the NTP network access
        /// check has succeeded.
        /// </summary>
        public static void SetNtpOk()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                networkFlags |= NetworkFlags.NtpOk;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the HTTPS network access check has succeeded.
        /// </summary>
        /// <returns>
        /// Non-zero if the HTTPS-OK flag is set; otherwise, zero.
        /// </returns>
        public static bool IsHttpsOk()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.HttpsOk, true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the HTTPS-OK flag, indicating that the HTTPS network access
        /// check has succeeded.
        /// </summary>
        public static void SetHttpsOk()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                networkFlags |= NetworkFlags.HttpsOk;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Count Methods
        /// <summary>
        /// Determines whether a time-via-NTP operation is currently
        /// pending for this process.
        /// </summary>
        /// <returns>
        /// Non-zero if a time-via-NTP operation is pending; otherwise,
        /// zero.
        /// </returns>
        public static bool IsNtpPending() /* CORE? */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingTimeViaNtpCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending time-via-NTP operation for this
        /// process.
        /// </summary>
        public static void BeginNtpPending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingTimeViaNtpCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending time-via-NTP operation for this
        /// process.
        /// </summary>
        public static void EndNtpPending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingTimeViaNtpCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a time-via-HTTPS operation is currently
        /// pending for this process.
        /// </summary>
        /// <returns>
        /// Non-zero if a time-via-HTTPS operation is pending; otherwise,
        /// zero.
        /// </returns>
        public static bool IsHttpsPending() /* CORE? */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingTimeViaHttpsCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending time-via-HTTPS operation for
        /// this process.
        /// </summary>
        public static void BeginHttpsPending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingTimeViaHttpsCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending time-via-HTTPS operation for this
        /// process.
        /// </summary>
        public static void EndHttpsPending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingTimeViaHttpsCountEnvVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Maximum Retries Methods
        /// <summary>
        /// Gets the maximum number of retries value to be passed into the
        /// core library, falling back to the default value when no explicit
        /// value has been set.
        /// </summary>
        /// <returns>
        /// The configured maximum number of retries, or the default value
        /// when none has been set.
        /// </returns>
        public static int? GetMaximumRetries() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (maximumRetries != null)
                    return maximumRetries;

                return defaultMaximumRetries;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the maximum number of retries value to be passed into the
        /// core library.
        /// </summary>
        /// <param name="maximumRetries">
        /// The maximum number of retries value, or null to fall back to the
        /// default value.
        /// </param>
        public static void SetMaximumRetries( /* CORE */
            int? maximumRetries /* in */
            )
        {
            lock (syncRoot)
            {
                This.maximumRetries = maximumRetries;
            }
        }
        #endregion
    }
}
