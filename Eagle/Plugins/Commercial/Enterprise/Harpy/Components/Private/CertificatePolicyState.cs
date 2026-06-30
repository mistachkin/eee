/*
 * CertificatePolicyState.cs --
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
using This = Licensing.Components.Private.CertificatePolicyState;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the global policy verification state used by the certificate
    /// policy subsystem, including the force-network flag together with the
    /// path and network flags.
    /// </summary>
    [ObjectId("e8608de8-9ffb-4149-84a3-2f5f9f079b08")]
    internal static class CertificatePolicyState
    {
        #region Private Data
        //
        // NOTE: This field is used to synchronize access to private data
        //       in this class.
        //
        /// <summary>
        /// This field is used to synchronize access to private data in this
        /// class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this is non-zero, all expiration date and revocation
        //       checking for policies will require network access.
        //
        /// <summary>
        /// When this is non-zero, all expiration date and revocation checking
        /// for policies will require network access.
        /// </summary>
        private static bool forceNetwork = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the (path) flags used machine identifiers within
        //       the policy verification subsystem, e.g. in support of the
        //       automatic provisioning feature, et al.  When this value is
        //       null, a suitable default value may be used.
        //
        /// <summary>
        /// These are the (path) flags used for machine identifiers within the
        /// policy verification subsystem, e.g. in support of the automatic
        /// provisioning feature, et al.  When this value is null, a suitable
        /// default value may be used.
        /// </summary>
        private static PathFlags? pathFlags = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the network (time) flags used within the policy
        //       verification subsystem .
        //
        /// <summary>
        /// These are the network (time) flags used within the policy
        /// verification subsystem.
        /// </summary>
        private static NetworkFlags? networkFlags = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether a key ring is pending by comparing the pending
        /// policy count with the pending key ring count.
        /// </summary>
        /// <returns>
        /// Non-zero if both pending counts are valid and equal; otherwise,
        /// zero.
        /// </returns>
        public static bool IsKeyRingPending() /* CORE? */
        {
            long policyCount = CertificateProcessOps.GetPendingCount(
                Constants.PendingPolicyCountEnvVarName);

            if (policyCount <= 0)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Key ring not pending, bad policy count {0}",
                        policyCount),
                    typeof(CertificatePolicyState).Name,
                    TracePriority.MediumLow, 0);
#endif

                return false;
            }

            long keyRingCount = CertificateProcessOps.GetPendingCount(
                Constants.PendingKeyRingCountEnvVarName);

            if (keyRingCount <= 0)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Key ring not pending, bad key ring count {0}",
                        policyCount),
                    typeof(CertificatePolicyState).Name,
                    TracePriority.MediumLow, 0);
#endif

                return false;
            }

            if (policyCount != keyRingCount)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Key ring not pending, policy count {0} versus " +
                        "key ring count {1}", policyCount, keyRingCount),
                    typeof(CertificatePolicyState).Name,
                    TracePriority.MediumLow, 0);
#endif

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Begins a pending block for the policy subsystem.
        /// </summary>
        public static void BeginPending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingPolicyCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ends a pending block for the policy subsystem.
        /// </summary>
        public static void EndPending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingPolicyCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the force-network flag.
        /// </summary>
        /// <returns>
        /// Non-zero if all expiration date and revocation checking for
        /// policies will require network access; otherwise, zero.
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
        /// Sets the value of the force-network flag.
        /// </summary>
        /// <param name="forceNetwork">
        /// Non-zero if all expiration date and revocation checking for
        /// policies should require network access; otherwise, zero.
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

        #region Path Flags Methods
        /// <summary>
        /// Determines whether the path flags have been set.
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
        /// Gets the configured path flags, if any.
        /// </summary>
        /// <returns>
        /// The configured path flags, or null if they have not been set.
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
        /// Gets the string representation of the configured path flags, if
        /// any.
        /// </summary>
        /// <returns>
        /// The string representation of the configured path flags, or null if
        /// they have not been set.
        /// </returns>
        public static string GetPathFlagsToString() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return (pathFlags != null) ?
                    pathFlags.ToString() : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configured path flags.
        /// </summary>
        /// <param name="pathFlags">
        /// The path flags to use within the policy verification subsystem, or
        /// null to clear them.
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
        /// Clears the configured path flags.
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
        /// Gets the configured path flags, falling back to a default value
        /// when they have not been set.
        /// </summary>
        /// <returns>
        /// The configured path flags, or the default machine path flags if
        /// they have not been set.
        /// </returns>
        public static PathFlags GetPathFlagsOrDefault() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (pathFlags != null)
                    return (PathFlags)pathFlags;

                return Constants.MachinePathFlags;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Flags Methods
        /// <summary>
        /// Determines whether the network flags have been set.
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
        /// Gets the configured network flags, if any.
        /// </summary>
        /// <returns>
        /// The configured network flags, or null if they have not been set.
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
        /// Gets the string representation of the configured network flags, if
        /// any.
        /// </summary>
        /// <returns>
        /// The string representation of the configured network flags, or null
        /// if they have not been set.
        /// </returns>
        public static string GetNetworkFlagsToString() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return (networkFlags != null) ?
                    networkFlags.ToString() : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configured network flags.
        /// </summary>
        /// <param name="networkFlags">
        /// The network flags to use within the policy verification subsystem,
        /// or null to clear them.
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
        /// Clears the configured network flags.
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
        /// Gets the configured network flags, falling back to a default value
        /// when they have not been set.
        /// </summary>
        /// <returns>
        /// The configured network flags, or the default script network flags
        /// if they have not been set.
        /// </returns>
        public static NetworkFlags GetNetworkFlagsOrDefault() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (networkFlags != null)
                    return (NetworkFlags)networkFlags;

                return Constants.ScriptNetworkFlags;
            }
        }
        #endregion
    }
}
