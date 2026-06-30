/*
 * Demo.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !CONSOLE
#error "This file cannot be compiled or used properly with console support disabled."
#endif

using System;
using System.IO;

#if OBFUSCATION
using System.Reflection;
#endif

using System.Runtime.CompilerServices;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Demo.Components.Private;
using Demo.Interfaces.Public;
using _Hosts = Eagle._Hosts;
using _Beep = Eagle._Constants.Beep;

namespace Demo.Hosts
{
    /// <summary>
    /// Implements the demo host, a console host that replays a script as
    /// simulated interactive input.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("49581ca5-a689-4638-8562-d708d7e70ca1")]
    public class Demo : _Hosts.Console, IDemoHost, IDisposable
    {
        #region Private Constants
        //
        // NOTE: Used with the PlayDebugLevel property.  This means that any
        //       caught exception will be emitted to the tracing subsystem.
        //
        /// <summary>
        /// The play debug level at which caught exceptions are emitted to the
        /// tracing subsystem.
        /// </summary>
        private const int ErrorLevel = 1;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Used with the PlayDebugLevel property.  This means that all
        //       key points in this host will be emitted as trace messages.
        //
        /// <summary>
        /// The play debug level at which all key points in this host are
        /// emitted as trace messages.
        /// </summary>
        private const int TraceLevel = 2;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: When this field is not null, it should contain a handle to a
        //       thread that is used to call the ShutdownAndStop method after
        //       the specified timeout period has elapsed.
        //
        /// <summary>
        /// The thread, when not null, that calls the shutdown-and-stop method
        /// after the timeout period elapses.
        /// </summary>
        private Thread timeoutThread;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The cached console cancel-key-press event handler.
        /// </summary>
        private ConsoleCancelEventHandler consoleCancelEventHandler;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Demo" /> host class.
        /// </summary>
        /// <param name="hostData">
        /// The data used to create and configure the host.
        /// </param>
        public Demo(
            IHostData hostData /* in */
            )
            : base(hostData)
        {
            /* NO RESULT */
            SetupStopAndDoneEvents();

            /* IGNORED */
            SetupDemoCancelKeyPressHandler(true, true);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Host Flags Support
        /// <summary>
        /// Resets the cached host flags and the base host flags.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool PrivateResetHostFlags()
        {
            hostFlags = HostFlags.Invalid;

            return base.ResetHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the cached host flags on first use, adding the flags
        /// specific to this host.
        /// </summary>
        /// <returns>
        /// The host flags for this host.
        /// </returns>
        protected override HostFlags MaybeInitializeHostFlags()
        {
            DebugTrace("MaybeInitializeHostFlags: entered");

            if (hostFlags == HostFlags.Invalid)
            {
                DebugTrace("MaybeInitializeHostFlags: invalid flags");

                //
                // NOTE: Always display the prompt.  Also, this host
                //       supports recording and playing back commands.
                //
                hostFlags = HostFlags.ForcePrompt | HostFlags.Recording |
                            HostFlags.Playback |
                            base.MaybeInitializeHostFlags();
            }

            return hostFlags;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IInteractiveHost Members
        /// <summary>
        /// Determines whether host input is redirected, treating active
        /// playback as redirected input.
        /// </summary>
        /// <returns>
        /// Non-zero if input is redirected; otherwise, zero.
        /// </returns>
        public override bool IsInputRedirected()
        {
            CheckDisposed();

            DebugTrace("IsInputRedirected: entered");

            if (PlayActive)
            {
                DebugTrace("IsInputRedirected: play active");

                return true;
            }

            return base.IsInputRedirected();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the host is open, optionally reporting closed
        /// when there is no active playback.
        /// </summary>
        /// <returns>
        /// Non-zero if the host is open; otherwise, zero.
        /// </returns>
        public override bool IsOpen()
        {
            CheckDisposed();

            DebugTrace("IsOpen: entered");

            if (ClosedOnInactive)
            {
                DebugTrace("IsOpen: closed on inactive");

                return PlayActive;
            }

            return base.IsOpen();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Pauses the host, optionally beeping first.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Pause()
        {
            CheckDisposed();

            DebugTrace("Pause: entered");

            if (PlayPauseBeep)
            {
                DebugTrace("Pause: play pause beep");

                Beep(_Beep.Frequency, _Beep.Duration);
            }

            return base.Pause();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The cached host flags for this host.
        /// </summary>
        private HostFlags hostFlags = HostFlags.Invalid;
        /// <summary>
        /// Gets the host flags for this host.
        /// </summary>
        /// <returns>
        /// The host flags for this host.
        /// </returns>
        public override HostFlags GetHostFlags()
        {
            CheckDisposed();

            return MaybeInitializeHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the next line of input, playing back the active demo input
        /// when present and otherwise deferring to the base host.
        /// </summary>
        /// <param name="value">
        /// Upon success, receives the line that was read.
        /// </param>
        /// <returns>
        /// Non-zero if a line was read; otherwise, zero.
        /// </returns>
        public override bool ReadLine(
            ref string value /* out */
            )
        {
            CheckDisposed();

            DebugTrace("ReadLine: entered");

            try
            {
                TextReader input = PlayInput;

                if (input != null)
                {
                    string localValue = input.ReadLine();

                    if (localValue != null)
                    {
                        EventWaitHandle stopEvent = PlayStopEvent;
                        EventWaitHandle doneEvent = PlayDoneEvent;

                        if (stopEvent != null)
                            stopEvent.Reset();

                        if (doneEvent != null)
                            doneEvent.Reset();

                        ReturnCode code;
                        Result error = null;

                        code = Play(
                            localValue, PlayMilliseconds,
                            stopEvent, doneEvent, ref error);

                        if (code == ReturnCode.Ok)
                        {
                            if (doneEvent != null)
                                doneEvent.WaitOne();

                            if (PlayUsePause &&
                                PlayNeedsPause(localValue))
                            {
                                Discard();
                                Pause();
                            }

                            WriteLine();
                        }
                        else
                        {
                            Complain(code, error);
                        }
                    }
                    else
                    {
                        if (StopOnEndOfStream)
                        {
                            DebugTrace(
                                "ReadLine: end-of-stream, inactive");

                            goto inactive;
                        }
                        else
                        {
                            DebugTrace(
                                "ReadLine: end-of-stream, active");
                        }
                    }

                    DebugTrace("ReadLine: exiting, success");

                    value = localValue;
                    return true;
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);

                DebugTrace("ReadLine: exiting, exception");

                return false;
            }

        inactive:

            if (FailOnBaseReadLine)
            {
                DebugTrace("ReadLine: exiting, failure");

                return false;
            }

            DebugTrace("ReadLine: exiting, inactive");

            return base.ReadLine(ref value);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStreamHost Members
        /// <summary>
        /// Gets the default input stream, or null when the host is in a
        /// different application domain.
        /// </summary>
        public override Stream DefaultIn
        {
            get
            {
                CheckDisposed();

                return IsSameAppDomain() ? base.DefaultIn : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default output stream, or null when the host is in a
        /// different application domain.
        /// </summary>
        public override Stream DefaultOut
        {
            get
            {
                CheckDisposed();

                return IsSameAppDomain() ? base.DefaultOut : null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default error stream, or null when the host is in a
        /// different application domain.
        /// </summary>
        public override Stream DefaultError
        {
            get
            {
                CheckDisposed();

                return IsSameAppDomain() ? base.DefaultError : null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHost Members
        /// <summary>
        /// Queries the host state, appending the demo playback settings to the
        /// base state.
        /// </summary>
        /// <param name="detailFlags">
        /// The flags controlling how much detail is reported.
        /// </param>
        /// <returns>
        /// The list of host state name and value pairs.
        /// </returns>
        public override StringList QueryState(
            DetailFlags detailFlags /* in */
            )
        {
            CheckDisposed();

            StringList result = base.QueryState(detailFlags);

            if (result == null)
                result = new StringList();

            lock (playSyncRoot) /* TRANSACTIONAL */
            {
                result.Add("PlayActive", PlayActive.ToString());
                result.Add("PlayMilliseconds", playMilliseconds.ToString());
                result.Add("PlayUsePause", playUsePause.ToString());
                result.Add("PlayPauseBeep", playPauseBeep.ToString());
                result.Add("PlayDebugLevel", playDebugLevel.ToString());
                result.Add("StopMilliseconds", stopMilliseconds.ToString());
                result.Add("StopOnCancel", stopOnCancel.ToString());
                result.Add("StopOnEndOfStream", stopOnEndOfStream.ToString());
                result.Add("FailOnBaseReadLine", failOnBaseReadLine.ToString());

                result.Add("TimeoutMilliseconds",
                    timeoutMilliseconds.ToString());
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the host flags.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool ResetHostFlags()
        {
            CheckDisposed();

            return PrivateResetHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the host, including its flags.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Reset(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            if (base.Reset(ref error) == ReturnCode.Ok)
            {
                if (!PrivateResetHostFlags()) /* NON-VIRTUAL */
                {
                    error = "failed to reset flags";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDemoHost Members
        /// <summary>
        /// Used to synchronize access to the playback state.
        /// </summary>
        private object playSyncRoot = new object();
        /// <summary>
        /// Gets or sets the object used to synchronize access to the playback
        /// state.
        /// </summary>
        public virtual object PlaySyncRoot
        {
            get { CheckDisposed(); return playSyncRoot; }
            set { CheckDisposed(); playSyncRoot = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Used to synchronize access to the timeout state.
        /// </summary>
        private object timeoutSyncRoot = new object();
        /// <summary>
        /// Gets or sets the object used to synchronize access to the timeout
        /// state.
        /// </summary>
        public virtual object TimeoutSyncRoot
        {
            get { CheckDisposed(); return timeoutSyncRoot; }
            set { CheckDisposed(); timeoutSyncRoot = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The reader supplying the simulated input being played back.
        /// </summary>
        private TextReader playInput;
        /// <summary>
        /// Gets or sets the reader supplying the simulated input being played
        /// back.
        /// </summary>
        public virtual TextReader PlayInput
        {
            get { CheckDisposed(); return playInput; }
            set { CheckDisposed(); playInput = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether playback is currently active.
        /// </summary>
        public virtual bool PlayActive
        {
            get { CheckDisposed(); return playInput != null; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of milliseconds to wait between simulated key presses.
        /// </summary>
        private int playMilliseconds;
        /// <summary>
        /// Gets or sets the number of milliseconds to wait between simulated
        /// key presses.
        /// </summary>
        public virtual int PlayMilliseconds
        {
            get { CheckDisposed(); return SafePlayMilliseconds; }
            set { CheckDisposed(); SafePlayMilliseconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to pause after each line of non-comment input.
        /// </summary>
        private bool playUsePause;
        /// <summary>
        /// Gets or sets a value indicating whether to pause after each line of
        /// non-comment input.
        /// </summary>
        public bool PlayUsePause
        {
            get { CheckDisposed(); return playUsePause; }
            set { CheckDisposed(); playUsePause = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to beep before pausing.
        /// </summary>
        private bool playPauseBeep;
        /// <summary>
        /// Gets or sets a value indicating whether to beep before pausing.
        /// </summary>
        public bool PlayPauseBeep
        {
            get { CheckDisposed(); return playPauseBeep; }
            set { CheckDisposed(); playPauseBeep = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The debugging level for diagnostic messages emitted during
        /// playback.
        /// </summary>
        private int playDebugLevel;
        /// <summary>
        /// Gets or sets the debugging level for diagnostic messages emitted
        /// during playback.
        /// </summary>
        public virtual int PlayDebugLevel
        {
            get { CheckDisposed(); return SafePlayDebugLevel; }
            set { CheckDisposed(); SafePlayDebugLevel = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of milliseconds to wait after attempting to stop
        /// playback.
        /// </summary>
        private int stopMilliseconds;
        /// <summary>
        /// Gets or sets the number of milliseconds to wait after attempting to
        /// stop playback.
        /// </summary>
        public virtual int StopMilliseconds
        {
            get { CheckDisposed(); return SafeStopMilliseconds; }
            set { CheckDisposed(); SafeStopMilliseconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to stop playback when the Control-C key is pressed.
        /// </summary>
        private bool stopOnCancel;
        /// <summary>
        /// Gets or sets a value indicating whether playback stops when the
        /// Control-C key is pressed.
        /// </summary>
        public virtual bool StopOnCancel
        {
            get { CheckDisposed(); return stopOnCancel; }
            set { CheckDisposed(); stopOnCancel = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to stop playback at the end of the input stream.
        /// </summary>
        private bool stopOnEndOfStream;
        /// <summary>
        /// Gets or sets a value indicating whether playback stops at the end
        /// of the input stream.
        /// </summary>
        public virtual bool StopOnEndOfStream
        {
            get { CheckDisposed(); return stopOnEndOfStream; }
            set { CheckDisposed(); stopOnEndOfStream = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to treat calling the base ReadLine method without active
        /// input as a failure.
        /// </summary>
        private bool failOnBaseReadLine;
        /// <summary>
        /// Gets or sets a value indicating whether calling the base ReadLine
        /// method without active input is treated as a failure.
        /// </summary>
        public virtual bool FailOnBaseReadLine
        {
            get { CheckDisposed(); return failOnBaseReadLine; }
            set { CheckDisposed(); failOnBaseReadLine = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to report the host as closed when there is no active play
        /// input.
        /// </summary>
        private bool closedOnInactive;
        /// <summary>
        /// Gets or sets a value indicating whether the host reports as closed
        /// when there is no active play input.
        /// </summary>
        public virtual bool ClosedOnInactive
        {
            get { CheckDisposed(); return closedOnInactive; }
            set { CheckDisposed(); closedOnInactive = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of milliseconds before the demo times out.
        /// </summary>
        private int timeoutMilliseconds;
        /// <summary>
        /// Gets or sets the number of milliseconds before the demo times out.
        /// </summary>
        public virtual int TimeoutMilliseconds
        {
            get
            {
                CheckDisposed();

                lock (timeoutSyncRoot)
                {
                    return SafeTimeoutMilliseconds;
                }
            }
            set
            {
                CheckDisposed();

                lock (timeoutSyncRoot)
                {
                    SafeTimeoutMilliseconds = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The event used to request that playback stop.
        /// </summary>
        private EventWaitHandle playStopEvent;
        /// <summary>
        /// Gets or sets the event used to request that playback stop.
        /// </summary>
        public virtual EventWaitHandle PlayStopEvent
        {
            get { CheckDisposed(); return playStopEvent; }
            set { CheckDisposed(); playStopEvent = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The event signaled when playback is done.
        /// </summary>
        private EventWaitHandle playDoneEvent;
        /// <summary>
        /// Gets or sets the event signaled when playback is done.
        /// </summary>
        public virtual EventWaitHandle PlayDoneEvent
        {
            get { CheckDisposed(); return playDoneEvent; }
            set { CheckDisposed(); playDoneEvent = value; }
        }

        ///////////////////////////////////////////////////////////////////////

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
        public virtual bool PlayNeedsPause(
            string value /* in */
            )
        {
            CheckDisposed();

            return !String.IsNullOrEmpty(value) &&
                (value[0] != Characters.Comment);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Refreshes the demo timeout, restarting the timeout interval.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool RefreshTimeout()
        {
            CheckDisposed();

            try
            {
                lock (timeoutSyncRoot) /* TRANSACTIONAL */
                {
                    if (!CleanupTimeoutThread())
                    {
                        DebugTrace("RefreshTimeout: cleanup failed");
                        return false;
                    }

                    int timeoutMilliseconds = SafeTimeoutMilliseconds;

                    if (timeoutMilliseconds > 0)
                    {
                        Utility.CreateAndOrStartThread(
                            null, "demoHostTimeout", TimeoutThreadStart,
                            timeoutMilliseconds, false, 0, true, true,
                            true, ref timeoutThread);

                        if (timeoutThread != null)
                            return true;
                    }
                    else
                    {
                        if (timeoutMilliseconds == 0)
                            ShutdownAndStop("RefreshTimeout", true, false);

                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugTrace(e);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

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
        public virtual ReturnCode Play(
            string value,    /* in */
            int timeout,     /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            return Play(
                value, timeout, PlayStopEvent, PlayDoneEvent, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

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
        public virtual ReturnCode Stop(
            int timeout,     /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            return Stop(
                timeout, PlayStopEvent, PlayDoneEvent, ref error);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Creates the stop and done events, when not already present.
        /// </summary>
        private void SetupStopAndDoneEvents()
        {
            if (playStopEvent == null)
                playStopEvent = new ManualResetEvent(false);

            if (playDoneEvent == null)
                playDoneEvent = new ManualResetEvent(false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shuts down the demo and halts playback when appropriate.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method, used for tracing.
        /// </param>
        /// <param name="force">
        /// Non-zero to shut down even when stop-on-cancel is disabled.
        /// </param>
        /// <param name="reset">
        /// Non-zero to reset the demo settings after shutting down.
        /// </param>
        private void ShutdownAndStop(
            string methodName, /* in */
            bool force,        /* in */
            bool reset         /* in */
            ) /* THREAD-SAFE */
        {
            //
            // NOTE: Check if the stop-on-cancel flag is set.  If so,
            //       shutdown the demo right now and then halt the
            //       current playback.
            //
            DebugTrace(String.Format("{0}: entered", methodName));

            if (CommonOps.DemoShutdown(this, force, reset))
            {
                DebugTrace(String.Format("{0}: shutdown", methodName));

                ReturnCode code;
                Result error = null;

                code = Stop(SafeStopMilliseconds, ref error);

                if (code == ReturnCode.Ok)
                    DebugTrace(String.Format("{0}: stopped", methodName));
                else
                    Complain(code, error);
            }

            DebugTrace(String.Format("{0}: exited", methodName));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The thread routine that shuts down the demo after the timeout
        /// period elapses.
        /// </summary>
        /// <param name="obj">
        /// The timeout, in milliseconds, as a boxed integer.
        /// </param>
        private void TimeoutThreadStart(
            object obj /* in */
            ) /* System.Threading.ParameterizedThreadStart */
        {
            try
            {
                if (obj is int)
                {
                    int timeoutMilliseconds = (int)obj;

                    if (timeoutMilliseconds >= 0)
                        Sleep(timeoutMilliseconds);
                }

                ShutdownAndStop(
                    "TimeoutThreadStart", true, false);
            }
            catch (ThreadAbortException e)
            {
                Thread.ResetAbort();

                DebugTrace(e);
            }
            catch (ThreadInterruptedException e)
            {
                DebugTrace(e);
            }
            catch (Exception e)
            {
                DebugTrace(e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shuts down and clears the timeout thread, when present.
        /// </summary>
        /// <returns>
        /// Non-zero if no timeout thread remains; otherwise, zero.
        /// </returns>
        private bool CleanupTimeoutThread()
        {
            lock (timeoutSyncRoot) /* TRANSACTIONAL */
            {
                if (timeoutThread != null)
                {
                    Utility.MaybeShutdownThread(
                        null, null, ShutdownFlags.Cancel,
                        ref timeoutThread);
                }

                return (timeoutThread == null);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports a failure using the host's interpreter.
        /// </summary>
        /// <param name="code">
        /// The return code of the failure.
        /// </param>
        /// <param name="result">
        /// The result or error describing the failure.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Complain(
            ReturnCode code, /* in */
            Result result    /* in */
            )
        {
            Complain(SafeGetInterpreter(), code, result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Gets or sets the play delay, in milliseconds, without checking for
        /// disposal.
        /// </summary>
        protected virtual int SafePlayMilliseconds
        {
            get { return playMilliseconds; }
            set { playMilliseconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the play debug level without checking for disposal.
        /// </summary>
        protected virtual int SafePlayDebugLevel
        {
            get { return playDebugLevel; }
            set { playDebugLevel = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the stop delay, in milliseconds, without checking for
        /// disposal.
        /// </summary>
        protected virtual int SafeStopMilliseconds
        {
            get { return stopMilliseconds; }
            set { stopMilliseconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the timeout, in milliseconds, without checking for
        /// disposal.
        /// </summary>
        protected virtual int SafeTimeoutMilliseconds
        {
            get { return timeoutMilliseconds; }
            set { timeoutMilliseconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports a failure using the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to report the failure.
        /// </param>
        /// <param name="code">
        /// The return code of the failure.
        /// </param>
        /// <param name="result">
        /// The result or error describing the failure.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void Complain(
            Interpreter interpreter, /* in */
            ReturnCode code,         /* in */
            Result result            /* in */
            )
        {
            Utility.Complain(interpreter, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits a diagnostic trace message when the play debug level is high
        /// enough.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void DebugTrace(
            string message /* in */
            )
        {
            try
            {
                int level = SafePlayDebugLevel;

                if (level >= TraceLevel)
                {
                    Utility.DebugTrace(
                        message, typeof(Demo).Name,
                        TracePriority.Medium |
                            TracePriority.ViaWrapperFromPlugin);
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits a diagnostic trace for an exception when the play debug level
        /// is high enough.
        /// </summary>
        /// <param name="exception">
        /// The exception to trace.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void DebugTrace(
            Exception exception /* in */
            )
        {
            try
            {
                int level = SafePlayDebugLevel;

                if (level >= ErrorLevel)
                {
                    Utility.DebugTrace(
                        exception, typeof(Demo).Name,
                        TracePriority.MediumHigh |
                            TracePriority.ViaWrapperFromPlugin);
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Discards any pending host input, faking success when input is
        /// redirected.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected virtual bool Discard()
        {
            if (!IsInputRedirected())
            {
                ReturnCode code;
                Result error = null;

                code = base.Discard(ref error);

                if (code == ReturnCode.Ok)
                    return true;

                Complain(code, error);

                return false;
            }
            else
            {
                return true; /* NOTE: Fake success. */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the host is in the same application domain as
        /// its interpreter.
        /// </summary>
        /// <returns>
        /// Non-zero if in the same application domain; otherwise, zero.
        /// </returns>
        protected virtual bool IsSameAppDomain()
        {
            return Utility.IsSameAppDomain(SafeGetInterpreter());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the cached console cancel-key-press event handler, creating it
        /// on demand.
        /// </summary>
        /// <returns>
        /// The console cancel-key-press event handler.
        /// </returns>
        protected ConsoleCancelEventHandler GetConsoleCancelEventHandler()
        {
            if (consoleCancelEventHandler == null)
            {
                consoleCancelEventHandler = new ConsoleCancelEventHandler(
                    ConsoleCancelEventHandler);
            }

            return consoleCancelEventHandler;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the console cancel-key-press event by shutting down the
        /// demo and stopping playback.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The data for the cancel-key-press event.
        /// </param>
        protected virtual void ConsoleCancelEventHandler(
            object sender,           /* in */
            ConsoleCancelEventArgs e /* in */
            ) /* THREAD-SAFE */
        {
            ShutdownAndStop("ConsoleCancelEventHandler", false, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or removes the cancel-key-press handlers for this host.
        /// </summary>
        /// <param name="setup">
        /// Non-zero to add the handlers; zero to remove them.
        /// </param>
        /// <param name="forceAppDomain">
        /// Non-zero to force the application-domain handler.
        /// </param>
        /// <param name="forcePending">
        /// Non-zero to force pending handler processing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected override bool SetupCancelKeyPressHandler(
            bool setup,          /* in */
            bool forceAppDomain, /* in */
            bool forcePending    /* in */
            )
        {
            if (!NoCancel)
            {
                if (!SetupDemoCancelKeyPressHandler(setup, false))
                    return false;

                if (IsSameAppDomain())
                {
                    return base.SetupCancelKeyPressHandler(
                        setup, forceAppDomain, forcePending);
                }
                else
                {
                    Complain(ReturnCode.Error, String.Format(
                        "cannot {0} cancel key press handler: wrong " +
                        "application domain", setup ? "add" : "remove"));

                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or removes the demo's console cancel-key-press handler.
        /// </summary>
        /// <param name="setup">
        /// Non-zero to add the handler; zero to remove it.
        /// </param>
        /// <param name="forceNoCancel">
        /// Non-zero to set up the handler even when cancellation is disabled.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected virtual bool SetupDemoCancelKeyPressHandler(
            bool setup,        /* in */
            bool forceNoCancel /* in */
            )
        {
            try
            {
                SystemConsoleMustBeOpen(false); /* throw */

                //
                // NOTE: Has setting up the script cancellation
                //       keypress been explicitly disabled?
                //
                if (forceNoCancel || !NoCancel)
                {
                    ConsoleCancelEventHandler handler =
                        GetConsoleCancelEventHandler();

                    if (handler != null)
                    {
                        if (setup)
                            Console.CancelKeyPress += handler;
                        else
                            Console.CancelKeyPress -= handler;

                        return true; // success.
                    }
                    else
                    {
                        return false; // no handler.
                    }
                }
                else
                {
                    return true; // fake success.
                }
            }
            catch
            {
                return false; // failure.
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Queues a work item that plays back the specified input one
        /// character at a time.
        /// </summary>
        /// <param name="value">
        /// The input text to play back.
        /// </param>
        /// <param name="timeout">
        /// The delay, in milliseconds, between characters.
        /// </param>
        /// <param name="stopEvent">
        /// The event that, when signaled, halts playback.
        /// </param>
        /// <param name="doneEvent">
        /// The event signaled when playback is done.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        protected virtual ReturnCode Play(
            string value,              /* in */
            int timeout,               /* in */
            EventWaitHandle stopEvent, /* in */
            EventWaitHandle doneEvent, /* in */
            ref Result error           /* out */
            )
        {
            EventWaitHandle[] events = new EventWaitHandle[] {
                stopEvent, doneEvent
            };

            DebugTrace("Play: entered");

            //
            // NOTE: Queue a work-item to write the text with the given
            //       delay in milliseconds in between each character.
            //
            return QueueWorkItem(PlayThreadStart,
                new AnyTriplet<string, int, EventWaitHandle[]>(
                    value, timeout, events), QueueFlags.Default,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals the stop event and waits for playback to finish.
        /// </summary>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait for completion.
        /// </param>
        /// <param name="stopEvent">
        /// The event used to request that playback stop.
        /// </param>
        /// <param name="doneEvent">
        /// The event signaled when playback is done.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        protected virtual ReturnCode Stop(
            int timeout,               /* in */
            EventWaitHandle stopEvent, /* in */
            EventWaitHandle doneEvent, /* in */
            ref Result error           /* out */
            )
        {
            DebugTrace("Stop: entered");

            try
            {
                if (stopEvent != null)
                {
                    stopEvent.Set();

                    DebugTrace("Stop: event set");

                    if (doneEvent != null)
                    {
                        if (doneEvent.WaitOne(timeout, false))
                            DebugTrace("Stop: done");
                        else
                            DebugTrace("Stop: timeout");
                    }
                    else
                    {
                        DebugTrace("Stop: asynchronous");
                    }

                    return ReturnCode.Ok;
                }
                else
                {
                    error = "invalid stop event";
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The work-item routine that writes the playback text to the host one
        /// character at a time.
        /// </summary>
        /// <param name="obj">
        /// A triplet containing the text, the per-character delay, and the
        /// stop and done events.
        /// </param>
        protected virtual void PlayThreadStart(
            object obj /* in */
            )
        {
            DebugTrace("PlayThreadStart: entered");

            IAnyTriplet<string, int, EventWaitHandle[]> anyTriplet = obj as
                IAnyTriplet<string, int, EventWaitHandle[]>;

            if (anyTriplet != null)
            {
                //
                // NOTE: Hold the lock for this object to prevent us from being
                //       disposed while we are using it.
                //
                lock (playSyncRoot) /* TRANSACTIONAL */
                {
                    //
                    // NOTE: Make sure we have not already been disposed.
                    //
                    if (!disposed)
                    {
                        //
                        // NOTE: Grab the event array provided by our caller.
                        //
                        EventWaitHandle[] events = anyTriplet.Z;

                        //
                        // NOTE: Extract the necessary events from the array,
                        //       if available.
                        //
                        EventWaitHandle stopEvent = null;
                        EventWaitHandle doneEvent = null;

                        if (events != null)
                        {
                            if (events.Length >= 1)
                                stopEvent = events[0];

                            if (events.Length >= 2)
                                doneEvent = events[1];
                        }

                        //
                        // NOTE: Grab the text to write.  If the text is null
                        //       or empty, we do nothing.
                        //
                        string value = anyTriplet.X;

                        if (value != null)
                        {
                            //
                            // NOTE: Get the number of milliseconds to wait
                            //       before writing each character.
                            //
                            int timeout = anyTriplet.Y;

                            //
                            // NOTE: Make sure we do not pass any negative
                            //       integers to the sleep method.
                            //
                            if (timeout < 0)
                                timeout = 0;

                            //
                            // NOTE: Get the length of the string now because
                            //       strings are immutable and we do not want
                            //       to access the property each time through
                            //       the loop.
                            //
                            int length = value.Length;

                            for (int index = 0; index < length; index++)
                            {
                                //
                                // NOTE: Block the current [worker] thread for
                                //       the specified number of milliseconds.
                                //
                                if (stopEvent != null)
                                {
                                    if (stopEvent.WaitOne(timeout, false))
                                    {
                                        DebugTrace("PlayThreadStart: stopped");

                                        break;
                                    }
                                }
                                else
                                {
                                    Sleep(timeout);
                                }

                                //
                                // NOTE: Write the specified character to the
                                //       host.
                                //
                                Write(value[index]);
                            }
                        }

                        //
                        // NOTE: If possible, signal the "done" event now.
                        //
                        if (doneEvent != null)
                        {
                            doneEvent.Set();

                            DebugTrace("PlayThreadStart: event set");
                        }
                    }
                    else
                    {
                        DebugTrace("PlayThreadStart: disposed");
                    }
                }
            }

            DebugTrace("PlayThreadStart: exited");
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IMaybeDisposed Members
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        public override bool Disposed
        {
            get { return disposed; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(
                    SafeGetInterpreter(), null))
            {
                throw new ObjectDisposedException(typeof(Demo).Name);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                lock (playSyncRoot) /* TRANSACTIONAL */
                {
                    if (!disposed)
                    {
                        if (disposing)
                        {
                            ////////////////////////////////////
                            // dispose managed resources here...
                            ////////////////////////////////////

                            if (playInput != null)
                            {
                                playInput.Close();
                                playInput = null;
                            }

                            if (playDoneEvent != null)
                            {
                                playDoneEvent.Close();
                                playDoneEvent = null;
                            }

                            if (playStopEvent != null)
                            {
                                playStopEvent.Close();
                                playStopEvent = null;
                            }

                            /* IGNORED */
                            SetupDemoCancelKeyPressHandler(
                                false, true);
                        }

                        //////////////////////////////////////
                        // release unmanaged resources here...
                        //////////////////////////////////////
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
}
