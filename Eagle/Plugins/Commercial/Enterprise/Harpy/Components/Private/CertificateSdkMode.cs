/*
 * CertificateSdkMode.cs --
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
    /// Provides a process-wide flag indicating whether "SDK mode" is enabled
    /// for this plugin.  When enabled, other subsystems may forbid certain
    /// operations that would otherwise be permitted.
    /// </summary>
    [ObjectId("e33328f4-cda1-4c95-a7b3-6e98227cc855")]
    internal static class CertificateSdkMode
    {
        #region Private Constants
        //
        // NOTE: This is the default (initial) value for the "enableCount"
        //       static field, which is just below.
        //
        /// <summary>
        /// The default (initial) value for the <see cref="enableCount" />
        /// field.
        /// </summary>
        private const int DefaultEnableCount = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: When this field is greater than zero, "SDK mode" for this
        //       plugin is enabled.  Other subsystems may consult this flag
        //       in order to forbid certain things that would otherwise be
        //       allowed, e.g. custom key usage flags.
        //
        /// <summary>
        /// Tracks how many times "SDK mode" has been enabled.  When this
        /// field is greater than zero, "SDK mode" for this plugin is enabled.
        /// </summary>
        private static int enableCount = DefaultEnableCount;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether "SDK mode" is currently enabled.
        /// </summary>
        /// <returns>
        /// Non-zero if "SDK mode" is enabled; otherwise, zero.
        /// </returns>
        public static bool IsEnabled() /* CORE */
        {
            return Interlocked.CompareExchange(
                ref enableCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of whether "SDK mode" is currently
        /// enabled.
        /// </summary>
        /// <returns>
        /// The string representation of <see langword="true" /> when "SDK
        /// mode" is enabled; otherwise, null.
        /// </returns>
        public static string IsEnabledToString() /* CORE */
        {
            return IsEnabled() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables "SDK mode" by incrementing the enable count.
        /// </summary>
        public static void Enable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Increment(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables "SDK mode" by decrementing the enable count.
        /// </summary>
        public static void Disable() /* CORE */
        {
            /* IGNORED */
            Interlocked.Decrement(ref enableCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the enable count is at its default value.
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
        /// Resets the enable count to its default value, disabling "SDK
        /// mode".
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
