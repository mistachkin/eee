/*
 * CommonOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Constants;
using Demo.Interfaces.Public;

namespace Demo.Components.Private
{
    /// <summary>
    /// Provides common helper operations shared by the demo plugin and demo
    /// host.
    /// </summary>
    [ObjectId("2bde1afd-cbf4-4c13-8190-b498a8b65aec")]
    internal static class CommonOps
    {
        #region Public Methods
        /// <summary>
        /// Gets the list of conditional compilation options that were active
        /// when the plugin was built.
        /// </summary>
        /// <param name="result">
        /// Upon success, receives the list of options; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode GetDefineConstants(
            ref Result result /* out */
            )
        {
            StringList list = DefineConstants.OptionList;

            if (list != null)
            {
                result = new StringList(list, false);
                return ReturnCode.Ok;
            }
            else
            {
                result = "define constants not available";
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a demo script file and returns a reader over its preprocessed
        /// script text.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to read the script.
        /// </param>
        /// <param name="path">
        /// The file to read the demo script from.
        /// </param>
        /// <param name="textReader">
        /// Upon success, receives a reader over the script text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode GetDemoStream(
            Interpreter interpreter,   /* in */
            string path,               /* in */
            ref TextReader textReader, /* out */
            ref Result error           /* out */
            )
        {
            StreamReader streamReader = null;

            try
            {
                streamReader = new StreamReader(path);

                string text = null;

                if (Engine.ReadScriptStream(
                        interpreter, path, streamReader,
                        0, Count.Invalid, ref text,
                        ref error) == ReturnCode.Ok)
                {
                    if (text == null)
                    {
                        error = "invalid script";
                        return ReturnCode.Error;
                    }

                    textReader = new StringReader(text);
                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                if (streamReader != null)
                {
                    streamReader.Close();
                    streamReader = null;
                }
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals the stop event and optionally waits for the done event to
        /// be signaled.
        /// </summary>
        /// <param name="stopEvent">
        /// The event to signal, or null to skip signaling.
        /// </param>
        /// <param name="doneEvent">
        /// The event to wait on, or null to skip waiting.
        /// </param>
        /// <param name="milliseconds">
        /// The maximum number of milliseconds to wait, or null to skip
        /// waiting.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the combined stop and wait results.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode DemoSignalAndOrWait(
            EventWaitHandle stopEvent, /* in */
            EventWaitHandle doneEvent, /* in */
            int? milliseconds,         /* in */
            ref Result result          /* out */
            )
        {
            ReturnCode stopCode;
            Result stopResult;

            if (stopEvent != null)
            {
                try
                {
                    if (stopEvent.Set())
                    {
                        stopResult = "stop";
                        stopCode = ReturnCode.Ok;
                    }
                    else
                    {
                        stopResult = "timeout";
                        stopCode = ReturnCode.Error;
                    }
                }
                catch (ObjectDisposedException)
                {
                    stopResult = "disposed";
                    stopCode = ReturnCode.Error;
                }
                catch (Exception e)
                {
                    stopResult = e;
                    stopCode = ReturnCode.Error;
                }
            }
            else
            {
                stopResult = null;
                stopCode = ReturnCode.Continue;
            }

            ReturnCode doneCode;
            Result doneResult;

            if ((doneEvent != null) && (milliseconds != null))
            {
                try
                {
                    if (doneEvent.WaitOne(
                            (int)milliseconds, false))
                    {
                        doneResult = "done";
                        doneCode = ReturnCode.Ok;
                    }
                    else
                    {
                        doneResult = "timeout";
                        doneCode = ReturnCode.Error;
                    }
                }
                catch (ObjectDisposedException)
                {
                    doneResult = "disposed";
                    doneCode = ReturnCode.Error;
                }
                catch (Exception e)
                {
                    doneResult = e;
                    doneCode = ReturnCode.Error;
                }
            }
            else
            {
                doneResult = null;
                doneCode = ReturnCode.Continue;
            }

            ResultList results = new ResultList();

            if (stopResult != null)
                results.Add(stopResult);

            if (doneResult != null)
                results.Add(doneResult);

            result = results;

            if (((stopCode == ReturnCode.Ok) ||
                    (stopCode == ReturnCode.Continue)) &&
                ((doneCode == ReturnCode.Ok) ||
                    (doneCode == ReturnCode.Continue)))
            {
                return ReturnCode.Ok;
            }
            else
            {
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shuts down an active demo, closing its input and optionally
        /// resetting the demo settings.
        /// </summary>
        /// <param name="demoHost">
        /// The demo host to shut down.
        /// </param>
        /// <param name="force">
        /// Non-zero to shut down even when stop-on-cancel is disabled.
        /// </param>
        /// <param name="reset">
        /// Non-zero to reset the demo settings after shutting down.
        /// </param>
        /// <returns>
        /// Non-zero if the demo was shut down; otherwise, zero.
        /// </returns>
        public static bool DemoShutdown(
            IDemoHost demoHost, /* in */
            bool force,         /* in */
            bool reset          /* in */
            )
        {
            if (demoHost != null)
            {
                try
                {
                    lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                    {
                        if (force || demoHost.StopOnCancel)
                        {
                            TextReader input = demoHost.PlayInput;

                            if (input != null)
                                demoHost.PlayInput = null;

                            //
                            // TODO: Why is the input closed only
                            //       when it is a StreamReader?
                            //
                            StreamReader streamReader =
                                input as StreamReader;

                            if (streamReader != null)
                            {
                                streamReader.Close(); /* throw */
                                streamReader = null;
                            }

                            if (reset)
                                ResetDemoSettings(demoHost);

                            return true;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // do nothing.
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the demo host settings from the supplied values,
        /// optionally falling back to the built-in defaults.
        /// </summary>
        /// <param name="demoHost">
        /// The demo host to configure.
        /// </param>
        /// <param name="playMilliseconds">
        /// The delay between simulated key presses, or null.
        /// </param>
        /// <param name="pause">
        /// Whether to pause after each input line, or null.
        /// </param>
        /// <param name="beep">
        /// Whether to beep before pausing, or null.
        /// </param>
        /// <param name="stopMilliseconds">
        /// The delay after attempting to stop, or null.
        /// </param>
        /// <param name="cancel">
        /// Whether to stop on the Control-C key, or null.
        /// </param>
        /// <param name="endOfStream">
        /// Whether to stop at the end of the input stream, or null.
        /// </param>
        /// <param name="baseReadLine">
        /// Whether calling the base ReadLine method without active input
        /// fails, or null.
        /// </param>
        /// <param name="closed">
        /// Whether the host reports as closed when inactive, or null.
        /// </param>
        /// <param name="timeoutMilliseconds">
        /// The demo timeout, in milliseconds, or null.
        /// </param>
        /// <param name="fallbackToDefaults">
        /// Non-zero to use the built-in defaults for any unspecified value.
        /// </param>
        public static void InitializeDemoSettings(
            IDemoHost demoHost,       /* in */
            int? playMilliseconds,    /* in */
            bool? pause,              /* in */
            bool? beep,               /* in */
            int? stopMilliseconds,    /* in */
            bool? cancel,             /* in */
            bool? endOfStream,        /* in */
            bool? baseReadLine,       /* in */
            bool? closed,             /* in */
            int? timeoutMilliseconds, /* in */
            bool fallbackToDefaults   /* in */
            )
        {
            if (demoHost != null)
            {
                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                {
                    if (playMilliseconds != null)
                        demoHost.PlayMilliseconds = (int)playMilliseconds;
                    else if (fallbackToDefaults)
                        demoHost.PlayMilliseconds = Defaults.PlayMilliseconds;

                    if (pause != null)
                        demoHost.PlayUsePause = (bool)pause;
                    else if (fallbackToDefaults)
                        demoHost.PlayUsePause = Defaults.PlayUsePause;

                    if (beep != null)
                        demoHost.PlayPauseBeep = (bool)beep;
                    else if (fallbackToDefaults)
                        demoHost.PlayPauseBeep = Defaults.PlayPauseBeep;

                    if (stopMilliseconds != null)
                        demoHost.StopMilliseconds = (int)stopMilliseconds;
                    else if (fallbackToDefaults)
                        demoHost.StopMilliseconds = Defaults.StopMilliseconds;

                    if (cancel != null)
                        demoHost.StopOnCancel = (bool)cancel;
                    else if (fallbackToDefaults)
                        demoHost.StopOnCancel = Defaults.StopOnCancel;

                    if (endOfStream != null)
                        demoHost.StopOnEndOfStream = (bool)endOfStream;
                    else if (fallbackToDefaults)
                        demoHost.StopOnEndOfStream = Defaults.StopOnEndOfStream;

                    if (baseReadLine != null)
                        demoHost.FailOnBaseReadLine = (bool)baseReadLine;
                    else if (fallbackToDefaults)
                        demoHost.FailOnBaseReadLine = Defaults.FailOnBaseReadLine;

                    if (baseReadLine != null)
                        demoHost.ClosedOnInactive = (bool)closed;
                    else if (fallbackToDefaults)
                        demoHost.ClosedOnInactive = Defaults.ClosedOnInactive;

                    if (timeoutMilliseconds != null)
                        demoHost.TimeoutMilliseconds = (int)timeoutMilliseconds;
                    else if (fallbackToDefaults)
                        demoHost.TimeoutMilliseconds = Defaults.TimeoutMilliseconds;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the demo host settings to their zero or disabled state.
        /// </summary>
        /// <param name="demoHost">
        /// The demo host to reset.
        /// </param>
        public static void ResetDemoSettings(
            IDemoHost demoHost /* in */
            )
        {
            if (demoHost != null)
            {
                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                {
                    demoHost.PlayMilliseconds = 0;
                    demoHost.PlayUsePause = false;
                    demoHost.PlayPauseBeep = false;
                    demoHost.PlayDebugLevel = 0;

                    demoHost.StopMilliseconds = 0;
                    demoHost.StopOnCancel = false;
                    demoHost.StopOnEndOfStream = false;

                    demoHost.FailOnBaseReadLine = false;
                    demoHost.ClosedOnInactive = false;

                    demoHost.TimeoutMilliseconds = 0;
                }
            }
        }
        #endregion
    }
}
