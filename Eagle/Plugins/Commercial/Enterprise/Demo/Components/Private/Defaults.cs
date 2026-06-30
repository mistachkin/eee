/*
 * Defaults.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Demo.Components.Private
{
    /// <summary>
    /// Provides the default values for the demo host settings.
    /// </summary>
    [ObjectId("9a055db9-cf1c-4353-8e60-b7684eb136aa")]
    internal static class Defaults
    {
        #region Public Constants
        //
        // NOTE: This is the default number of milliseconds to wait between
        //       simulated key presses.
        //
        /// <summary>
        /// The default number of milliseconds to wait between simulated key
        /// presses.
        /// </summary>
        public const int PlayMilliseconds = 50;

        //
        // NOTE: This is the default number of milliseconds to wait after
        //       attempting to stop the simulated key presses.
        //
        /// <summary>
        /// The default number of milliseconds to wait after attempting to stop
        /// the simulated key presses.
        /// </summary>
        public const int StopMilliseconds = 2000;

        //
        // NOTE: This is the default number of milliseconds to timeout after
        //       starting the demo.  Any value less than zero will disable
        //       the timeout.
        //
        /// <summary>
        /// The default number of milliseconds before the demo times out.  Any
        /// value less than zero disables the timeout.
        /// </summary>
        public const int TimeoutMilliseconds = -1; /* _Timeout.Infinite */

        //
        // NOTE: This is the default setting that determines if we should pause
        //       after each line of [non-comment] input during the demo.
        //
        /// <summary>
        /// The default setting that determines whether to pause after each
        /// line of non-comment input during the demo.
        /// </summary>
        public const bool PlayUsePause = false;

        //
        // NOTE: This is the default setting that determines if we should beep
        //       before pausing.
        //
        /// <summary>
        /// The default setting that determines whether to beep before pausing.
        /// </summary>
        public const bool PlayPauseBeep = false;

        //
        // NOTE: This is the default debugging level for diagnostic messages
        //       emitted from the demo host.
        //
        /// <summary>
        /// The default debugging level for diagnostic messages emitted from
        /// the demo host.
        /// </summary>
        public const int PlayDebugLevel = -1; /* Level.Invalid */

        //
        // NOTE: Stop playback of the demo when the Control-C key has been
        //       pressed.
        //
        /// <summary>
        /// The default setting that determines whether playback stops when the
        /// Control-C key has been pressed.
        /// </summary>
        public const bool StopOnCancel = false;

        //
        // NOTE: Stop playback of the demo when the end of the input stream is
        //       hit.
        //
        /// <summary>
        /// The default setting that determines whether playback stops when the
        /// end of the input stream is reached.
        /// </summary>
        public const bool StopOnEndOfStream = true;

        //
        // NOTE: Permit the base ReadLine method to be called when there is no
        //       active input.
        //
        /// <summary>
        /// The default setting that determines whether calling the base
        /// ReadLine method without active input is treated as a failure.
        /// </summary>
        public const bool FailOnBaseReadLine = false;

        //
        // NOTE: Return false when IInteractiveHost.IsOpen is called without
        //       any active play input.
        //
        /// <summary>
        /// The default setting that determines whether the host reports as
        /// closed when there is no active play input.
        /// </summary>
        public const bool ClosedOnInactive = false;

        //
        // NOTE: When available, use the native keyboard API provided by the
        //       operating system in order to simulate input.
        //
        /// <summary>
        /// The default setting that determines whether to use the native
        /// operating system keyboard API to simulate input, when available.
        /// </summary>
        public const bool Native = false;

        //
        // NOTE: After shutting down the demo, set the interpreter "Exit"
        //       property to true.
        //
        /// <summary>
        /// The default setting that determines whether the interpreter Exit
        /// property is set after the demo shuts down.
        /// </summary>
        public const bool Exit = false;

        //
        // NOTE: This is the default file name to use when starting a demo.
        //
        /// <summary>
        /// The default file name to use when starting a demo.
        /// </summary>
        public const string FileName = "demo.eagle";
        #endregion
    }
}
