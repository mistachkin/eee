/*
 * DemoHost.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.IO;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Demo.Interfaces.Public
{
    /// <summary>
    /// Describes the demo host, which simulates interactive input by playing
    /// back a script.
    /// </summary>
    [ObjectId("150d744f-6532-4272-9125-a7f6dbd5e7b5")]
    public interface IDemoHost : IHost
    {
        /// <summary>
        /// Gets or sets the object used to synchronize access to the playback
        /// state.
        /// </summary>
        object PlaySyncRoot { get; set; }
        /// <summary>
        /// Gets or sets the object used to synchronize access to the timeout
        /// state.
        /// </summary>
        object TimeoutSyncRoot { get; set; }

        /// <summary>
        /// Gets or sets the reader supplying the simulated input being played
        /// back.
        /// </summary>
        TextReader PlayInput { get; set; }
        /// <summary>
        /// Gets a value indicating whether playback is currently active.
        /// </summary>
        bool PlayActive { get; }

        /// <summary>
        /// Gets or sets the number of milliseconds to wait between simulated
        /// key presses.
        /// </summary>
        int PlayMilliseconds { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to pause after each line of
        /// non-comment input.
        /// </summary>
        bool PlayUsePause { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to beep before pausing.
        /// </summary>
        bool PlayPauseBeep { get; set; }
        /// <summary>
        /// Gets or sets the debugging level for diagnostic messages emitted
        /// during playback.
        /// </summary>
        int PlayDebugLevel { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds to wait after attempting to
        /// stop playback.
        /// </summary>
        int StopMilliseconds { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether playback stops when the
        /// Control-C key is pressed.
        /// </summary>
        bool StopOnCancel { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether playback stops at the end
        /// of the input stream.
        /// </summary>
        bool StopOnEndOfStream { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether calling the base ReadLine
        /// method without active input is treated as a failure.
        /// </summary>
        bool FailOnBaseReadLine { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the host reports as closed
        /// when there is no active play input.
        /// </summary>
        bool ClosedOnInactive { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds before the demo times out.
        /// </summary>
        int TimeoutMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the event used to request that playback stop.
        /// </summary>
        EventWaitHandle PlayStopEvent { get; set; }
        /// <summary>
        /// Gets or sets the event signaled when playback is done.
        /// </summary>
        EventWaitHandle PlayDoneEvent { get; set; }

        /// <summary>
        /// Determines whether playback should pause before the specified input
        /// line.
        /// </summary>
        /// <param name="value">
        /// The input line about to be played back.
        /// </param>
        /// <returns>
        /// Non-zero if playback should pause; otherwise, zero.
        /// </returns>
        bool PlayNeedsPause(string value);
        /// <summary>
        /// Refreshes the demo timeout, restarting the timeout interval.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool RefreshTimeout();

        /// <summary>
        /// Begins playing back the specified input as simulated interactive
        /// input.
        /// </summary>
        /// <param name="value">
        /// The input text to play back.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, for the playback operation.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        ReturnCode Play(string value, int timeout, ref Result error);
        /// <summary>
        /// Stops the active playback.
        /// </summary>
        /// <param name="timeout">
        /// The timeout, in milliseconds, for the stop operation.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        ReturnCode Stop(int timeout, ref Result error);
    }
}
