/*
 * CertificateDemoMode.cs --
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
    /// Provides a thread-safe, reference-counted toggle that tracks whether
    /// "demo-mode" is enabled for this plugin. Other subsystems consult this
    /// state to decide whether to permit operations (e.g. using key pairs
    /// intended for demonstration purposes only) that would otherwise be
    /// forbidden.
    /// </summary>
    [ObjectId("cc679f0f-57b4-48f2-a5b9-de102e93dd7c")]
    internal static class CertificateDemoMode
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
        private const int DefaultEnableCount = 1;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: When this field is greater than zero, "demo-mode" for this
        //       plugin is enabled.  Other subsystems may consult this flag
        //       in concert with other global state (e.g. a global "feature"
        //       flag) in order to allow certain things that would otherwise
        //       be forbidden, e.g. allowing key pairs to be used in release
        //       builds that are otherwise for demonstration purposes only.
        //
        /// <summary>
        /// The reference count controlling whether "demo-mode" is enabled.
        /// When this field is greater than zero, "demo-mode" for this plugin
        /// is enabled.
        /// </summary>
        private static int enableCount = DefaultEnableCount;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether "demo-mode" is currently enabled by checking
        /// whether the <see cref="enableCount" /> reference count is greater
        /// than zero.
        /// </summary>
        /// <returns>
        /// Non-zero if "demo-mode" is currently enabled; otherwise, zero.
        /// </returns>
        public static bool IsEnabled() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of whether "demo-mode" is
        /// currently enabled.
        /// </summary>
        /// <returns>
        /// The string representation of <see langword="true" /> when
        /// "demo-mode" is enabled; otherwise, null.
        /// </returns>
        public static string IsEnabledToString() /* CORE */
        {
            return IsEnabled() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables "demo-mode" by incrementing the <see cref="enableCount" />
        /// reference count.
        /// </summary>
        public static void Enable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Increment(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables "demo-mode" by decrementing the
        /// <see cref="enableCount" /> reference count.
        /// </summary>
        public static void Disable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Decrement(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the <see cref="enableCount" /> reference count
        /// is still at its default value of
        /// <see cref="DefaultEnableCount" />.
        /// </summary>
        /// <returns>
        /// Non-zero if the reference count equals
        /// <see cref="DefaultEnableCount" />; otherwise, zero.
        /// </returns>
        public static bool IsDefault() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) == DefaultEnableCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the <see cref="enableCount" /> reference count back to its
        /// default value of <see cref="DefaultEnableCount" />.
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
