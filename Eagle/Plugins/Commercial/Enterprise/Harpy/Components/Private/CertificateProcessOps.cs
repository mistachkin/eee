/*
 * CertificateProcessOps.cs --
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

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods for tracking the pending state of certificate
    /// processing operations using a per-prefix reference count.
    /// </summary>
    [ObjectId("837fbf09-84c0-4cd6-b8e8-c903f50ddf86")]
    internal static class CertificateProcessOps
    {
        /// <summary>
        /// Determines whether there are any pending certificate processing
        /// operations associated with the specified prefix.
        /// </summary>
        /// <param name="prefix">
        /// The prefix that identifies the group of certificate processing
        /// operations to query.
        /// </param>
        /// <returns>
        /// Non-zero if one or more operations are currently pending for the
        /// specified prefix; otherwise, zero.
        /// </returns>
        public static bool IsPending(
            string prefix /* in */
            ) /* CORE? */
        {
            long referenceCount;
            Result error = null;

            if (Utility.CheckAndMaybeModifyProcessReferenceCount(
                    prefix, null, null, out referenceCount,
                    ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "IsPending: prefix = {0}, error = {1}",
                    Utility.FormatWrapOrNull(prefix),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateProcessOps).Name,
                    TracePriority.MediumHigh);
#endif
            }

            return referenceCount > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of pending certificate processing operations
        /// associated with the specified prefix.
        /// </summary>
        /// <param name="prefix">
        /// The prefix that identifies the group of certificate processing
        /// operations to query.
        /// </param>
        /// <returns>
        /// The current reference count representing the number of pending
        /// operations for the specified prefix.
        /// </returns>
        public static long GetPendingCount(
            string prefix /* in */
            ) /* CORE? */
        {
            long referenceCount;
            Result error = null;

            if (Utility.CheckAndMaybeModifyProcessReferenceCount(
                    prefix, null, null, out referenceCount,
                    ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetPendingCount: prefix = {0}, error = {1}",
                    Utility.FormatWrapOrNull(prefix),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateProcessOps).Name,
                    TracePriority.MediumHigh);
#endif
            }

            return referenceCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the start of a pending certificate processing operation for
        /// the specified prefix by incrementing its reference count.
        /// </summary>
        /// <param name="prefix">
        /// The prefix that identifies the group of certificate processing
        /// operations being started.
        /// </param>
        public static void BeginPending(
            string prefix /* in */
            ) /* CORE? */
        {
            Result error = null;

            if (Utility.CheckAndMaybeModifyProcessReferenceCount(
                    prefix, null, true, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "BeginPending: prefix = {0}, error = {1}",
                    Utility.FormatWrapOrNull(prefix),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateProcessOps).Name,
                    TracePriority.MediumHigh);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the completion of a pending certificate processing operation
        /// for the specified prefix by decrementing its reference count.
        /// </summary>
        /// <param name="prefix">
        /// The prefix that identifies the group of certificate processing
        /// operations being completed.
        /// </param>
        public static void EndPending(
            string prefix /* in */
            ) /* CORE? */
        {
            Result error = null;

            if (Utility.CheckAndMaybeModifyProcessReferenceCount(
                    prefix, null, false, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "EndPending: prefix = {0}, error = {1}",
                    Utility.FormatWrapOrNull(prefix),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateProcessOps).Name,
                    TracePriority.MediumHigh);
#endif
            }
        }
    }
}
