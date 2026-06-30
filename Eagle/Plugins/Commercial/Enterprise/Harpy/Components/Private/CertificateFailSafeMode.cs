/*
 * CertificateFailSafeMode.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Threading;
using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static tracking of the "fail-safe" mode for this plugin,
    /// including whether the mode is currently enabled and whether a
    /// fail-safe has been tripped.  All state is maintained using
    /// interlocked operations so that it may be safely consulted and
    /// modified from multiple threads.
    /// </summary>
    [ObjectId("1c0b58ef-c528-4d06-ab76-0c1c0ad86b10")]
    internal static class CertificateFailSafeMode
    {
        #region Private Constants
        //
        // NOTE: This is the default (initial) value for the "enableCount"
        //       static field, which is just below.
        //
        /// <summary>
        /// The default (initial) value for the <see cref="enableCount" />
        /// static field.
        /// </summary>
        private const int DefaultEnableCount = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: When this field is greater than zero, "fail-safe" for this
        //       plugin is enabled.  Other subsystems may consult this flag
        //       in concert with other global state (e.g. a global "feature"
        //       flag) in order to forbid certain things that would otherwise
        //       be allowed, e.g. for revocation checking to be disabled by
        //       default.
        //
        /// <summary>
        /// When this field is greater than zero, "fail-safe" for this plugin
        /// is enabled.  It is maintained using interlocked operations.
        /// </summary>
        private static int enableCount = DefaultEnableCount;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If a fail-safe is tripped, this count will be incremented.
        //
        /// <summary>
        /// The number of times a fail-safe has been tripped.  It is
        /// incremented each time a fail-safe is tripped and maintained using
        /// interlocked operations.
        /// </summary>
        private static int tripCount = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether "fail-safe" mode is currently enabled.
        /// </summary>
        /// <returns>
        /// Non-zero if the enable count is greater than zero; otherwise,
        /// zero.
        /// </returns>
        public static bool IsEnabled() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of whether "fail-safe" mode is
        /// currently enabled.
        /// </summary>
        /// <returns>
        /// The string form of <c>true</c> if "fail-safe" mode is enabled;
        /// otherwise, null.
        /// </returns>
        public static string IsEnabledToString() /* CORE */
        {
            return IsEnabled() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables "fail-safe" mode by incrementing the enable count.
        /// </summary>
        public static void Enable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Increment(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables "fail-safe" mode by decrementing the enable count.
        /// </summary>
        public static void Disable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Decrement(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the enable count is currently at its default
        /// value.
        /// </summary>
        /// <returns>
        /// Non-zero if the enable count equals
        /// <see cref="DefaultEnableCount" />; otherwise, zero.
        /// </returns>
        public static bool IsDefault() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) == DefaultEnableCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the enable count to its default value,
        /// <see cref="DefaultEnableCount" />.
        /// </summary>
        public static void ResetToDefault()
        {
            /* IGNORED */
            Interlocked.Exchange(
                ref enableCount, DefaultEnableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current number of times a fail-safe has been tripped.
        /// </summary>
        /// <returns>
        /// The current value of the trip count.
        /// </returns>
        public static int TripCount() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref tripCount, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of the current trip count.
        /// </summary>
        /// <returns>
        /// The string form of the trip count if it is greater than zero;
        /// otherwise, null.
        /// </returns>
        public static string TripCountToString() /* CORE */
        {
            int count = TripCount();
            return (count > 0) ? count.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a fail-safe has been tripped at least once.
        /// </summary>
        /// <returns>
        /// Non-zero if the trip count is greater than zero; otherwise, zero.
        /// </returns>
        public static bool WasTripped() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref tripCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of whether a fail-safe has been
        /// tripped.
        /// </summary>
        /// <returns>
        /// The string form of <c>true</c> if a fail-safe has been tripped;
        /// otherwise, null.
        /// </returns>
        public static string WasTrippedToString() /* CORE */
        {
            return WasTripped() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records that a fail-safe has been tripped by incrementing the trip
        /// count.
        /// </summary>
        public static void Trip()
        {
            /* IGNORED */
            Interlocked.Increment(ref tripCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the trip count to zero, clearing any record that a
        /// fail-safe has been tripped.
        /// </summary>
        public static void Untrip()
        {
            /* IGNORED */
            Interlocked.Exchange(ref tripCount, 0);
        }
        #endregion
    }
}
