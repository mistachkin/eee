/*
 * CertificateTestMode.cs --
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
    /// Provides a thread-safe, process-wide flag indicating whether the
    /// certificate "test-mode" for this plugin is currently enabled.  Other
    /// subsystems consult this state to permit operations that would
    /// otherwise be forbidden, such as using key pairs marked with the
    /// "TestOnly" key usage flag in release builds.
    /// </summary>
    [ObjectId("57db336f-827c-41c2-8e41-da64c3e55799")]
    internal static class CertificateTestMode
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
        // NOTE: When this field is greater than zero, "test-mode" for this
        //       plugin is enabled.  Other subsystems may consult this flag
        //       in concert with other global state (e.g. a global "feature"
        //       flag) in order to allow certain things that would otherwise
        //       be forbidden, e.g. allowing key pairs to be used in release
        //       builds when they are marked with the "TestOnly" key usage
        //       flag.
        //
        /// <summary>
        /// When this field is greater than zero, "test-mode" for this plugin
        /// is enabled.  It is manipulated atomically so that concurrent
        /// callers may safely enable and disable test-mode.
        /// </summary>
        private static int enableCount = DefaultEnableCount;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether certificate "test-mode" is currently enabled.
        /// </summary>
        /// <returns>
        /// Non-zero if test-mode is enabled (i.e. it has been enabled more
        /// times than it has been disabled); otherwise, zero.
        /// </returns>
        public static bool IsEnabled() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of whether certificate "test-mode"
        /// is currently enabled.
        /// </summary>
        /// <returns>
        /// The string form of <see langword="true" /> when test-mode is
        /// enabled; otherwise, null.
        /// </returns>
        public static string IsEnabledToString() /* CORE */
        {
            return IsEnabled() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables certificate "test-mode" by atomically incrementing the
        /// internal enable count.
        /// </summary>
        public static void Enable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Increment(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables certificate "test-mode" by atomically decrementing the
        /// internal enable count.
        /// </summary>
        public static void Disable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Decrement(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the internal enable count is at its default
        /// (initial) value.
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
        /// Resets the internal enable count to its default (initial) value,
        /// thereby disabling certificate "test-mode".
        /// </summary>
        public static void ResetToDefault()
        {
            /* IGNORED */
            Interlocked.Exchange(
                ref enableCount, DefaultEnableCount);
        }
        #endregion
    }
}
