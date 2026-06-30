/*
 * CertificateSandboxOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !TEST
#error "This file cannot be compiled or used properly with test code disabled."
#endif

using System;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;

using SandboxPair = Eagle._Components.Public.AnyPair<
    Licensing.Components.Private.EvaluateClientData, long>;

using SDCD = Licensing.Components.Private.CertificateSandboxOps.ShutdownClientData;

using ShutdownPair = Eagle._Components.Public.AnyPair<
    Licensing.Components.Private.CertificateSandboxOps.ShutdownClientData, int>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper routines for creating, refreshing, and shutting down
    /// sandbox interpreters used to evaluate license certificate scripts.
    /// </summary>
    [ObjectId("654707da-e360-4a68-ab65-a056d07d9192")]
    internal static class CertificateSandboxOps
    {
        #region ShutdownClientData Helper Class
        /// <summary>
        /// Carries the state needed to asynchronously shut down a sandbox
        /// interpreter once its evaluation has completed.
        /// </summary>
        [ObjectId("4f7d1779-e08e-46a5-af78-43e065477815")]
        internal sealed class ShutdownClientData :
                ClientData, IGetInterpreter, IMaybeDisposed
        {
            #region Public Constructors
            /// <summary>
            /// Constructs an instance used to carry sandbox shutdown state.
            /// </summary>
            /// <param name="data">
            /// The optional caller-specific data for this object.
            /// </param>
            /// <param name="interpreter">
            /// The optional sandbox interpreter to be shut down.
            /// </param>
            /// <param name="configuration">
            /// The optional configuration used to track the sandbox token.
            /// </param>
            /// <param name="refreshEvent">
            /// The optional event used to signal that the sandbox should be
            /// refreshed instead of shut down.
            /// </param>
            /// <param name="sandboxToken">
            /// The optional token identifying the sandbox interpreter.
            /// </param>
            public ShutdownClientData(
                object data,                        /* in: OPTIONAL */
                Interpreter interpreter,            /* in: OPTIONAL */
                IConfiguration configuration,       /* in: OPTIONAL */
                SharedEventWaitHandle refreshEvent, /* in: OPTIONAL */
                ulong? sandboxToken                 /* in: OPTIONAL */
                )
                : base(data)
            {
                this.interpreter = interpreter;
                this.configuration = configuration;
                this.refreshEvent = refreshEvent;
                this.sandboxToken = sandboxToken;
            }
            #endregion

            //////////////////////////////////////////////////////////////////

            #region Public Properties
            /// <summary>
            /// Stores the configuration used to track the sandbox token.
            /// </summary>
            private IConfiguration configuration;
            /// <summary>
            /// Gets the configuration used to track the sandbox token.
            /// </summary>
            public IConfiguration Configuration
            {
                get { return configuration; }
            }

            //////////////////////////////////////////////////////////////////

            /// <summary>
            /// Stores the event used to signal a sandbox refresh.
            /// </summary>
            private SharedEventWaitHandle refreshEvent;
            /// <summary>
            /// Gets the event used to signal a sandbox refresh.
            /// </summary>
            public SharedEventWaitHandle RefreshEvent
            {
                get { return refreshEvent; }
            }

            //////////////////////////////////////////////////////////////////

            /// <summary>
            /// Stores the token identifying the sandbox interpreter.
            /// </summary>
            private ulong? sandboxToken;
            /// <summary>
            /// Gets the token identifying the sandbox interpreter.
            /// </summary>
            public ulong? SandboxToken
            {
                get { return sandboxToken; }
            }
            #endregion

            //////////////////////////////////////////////////////////////////

            #region IGetInterpreter Members
            /// <summary>
            /// Stores the sandbox interpreter to be shut down.
            /// </summary>
            private Interpreter interpreter;
            /// <summary>
            /// Gets the sandbox interpreter to be shut down.
            /// </summary>
            public Interpreter Interpreter
            {
                get { return interpreter; }
            }
            #endregion

            //////////////////////////////////////////////////////////////////

            #region IMaybeDisposed Members
            /// <summary>
            /// Gets a value indicating whether this object has been disposed.
            /// </summary>
            public bool Disposed
            {
                get
                {
                    //
                    // HACK: Actually, we cannot be disposed; therefore,
                    //       always return false.
                    //
                    return false;
                }
            }

            //////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets a value indicating whether this object is being disposed.
            /// </summary>
            public bool Disposing
            {
                get { throw new NotImplementedException(); }
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits for the specified event to be signaled, or sleeps for the
        /// given timeout when no event is supplied, while honoring
        /// application domain and interpreter shutdown.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter checked for pending disposal while
        /// waiting.
        /// </param>
        /// <param name="event">
        /// The optional event to wait on; when null, the method simply
        /// sleeps for the timeout.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait or sleep.
        /// </param>
        /// <returns>
        /// Non-zero if the wait or sleep completed normally; otherwise,
        /// zero.
        /// </returns>
        private static bool WaitOrSleep( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            SharedEventWaitHandle @event, /* in: OPTIONAL */
            int timeout                   /* in */
            )
        {
            try
            {
                if (timeout < 0)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "WaitOrSleep: invalid timeout of {0}",
                        timeout), typeof(CertificateSandboxOps).Name,
                        TracePriority.MediumHigh);
#endif

                    return false;
                }

                if (@event != null)
                {
                    /* IGNORED */
                    @event.Reset(); /* throw */

                    while (@event.WaitOne(timeout, false)) /* throw */
                    {
                        if (Utility.AppDomainIsStoppingSoon())
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(String.Format(
                                "WaitOrSleep: application domain {0} " +
                                "shutdown with timeout {1}",
                                Utility.GetCurrentAppDomainId(), timeout),
                                typeof(CertificateSandboxOps).Name,
                                TracePriority.High);
#endif

                            return false;
                        }

                        if (Interpreter.IsPendingDispose(interpreter))
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(String.Format(
                                "WaitOrSleep: interpreter {0} disposal " +
                                "with timeout {1}",
                                CertificateDataOps.FormatInterpreter(
                                    interpreter, true, false), timeout),
                                typeof(CertificateSandboxOps).Name,
                                TracePriority.High);
#endif

                            return false;
                        }

                        /* IGNORED */
                        @event.Reset(); /* throw */
                    }

#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "WaitOrSleep: event {0} set with timeout of {1}",
                        Utility.GetHashCode(@event), timeout),
                        typeof(CertificateSandboxOps).Name,
                        TracePriority.Low);
#endif
                }
                else
                {
                    /* NO RESULT */
                    Thread.Sleep(timeout); /* throw */

#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "WaitOrSleep: waited for timeout of {0}",
                        timeout), typeof(CertificateSandboxOps).Name,
                        TracePriority.Low);
#endif
                }

                return true;
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "WaitOrSleep: aborted with timeout of {0}",
                    timeout), typeof(CertificateSandboxOps).Name,
                    TracePriority.MediumLow);
#endif

                return false;
            }
            catch (ThreadInterruptedException)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "WaitOrSleep: interrupted with timeout of {0}",
                    timeout), typeof(CertificateSandboxOps).Name,
                    TracePriority.MediumLow);
#endif

                return false;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateSandboxOps).Name,
                    TracePriority.Highest);
#endif

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if WINFORMS
        /// <summary>
        /// Signals the refresh event carried by the given client data so a
        /// sandbox interpreter is refreshed rather than recreated.
        /// </summary>
        /// <param name="clientData">
        /// The shutdown client data containing the refresh event to set.
        /// </param>
        /// <returns>
        /// Non-zero if the refresh event was successfully set; otherwise,
        /// zero.
        /// </returns>
        private static bool SetRefreshEvent( /* CORE */
            SDCD clientData /* in */
            )
        {
            if (clientData == null)
                return false;

            if (clientData.Disposed)
                return false;

            SharedEventWaitHandle @event = clientData.RefreshEvent;

            if (@event == null)
                return false;

            bool result = false;

            try
            {
                result = @event.Set(); /* throw */

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "SetRefreshEvent: result = {0}", result),
                    typeof(CertificateSandboxOps).Name,
                    TracePriority.Low);
#endif
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateSandboxOps).Name,
                    TracePriority.Highest);
#endif
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports the given status text to the interpreter, automatically
        /// starting the status display and sleeping afterward.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose status display is updated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the status display.
        /// </param>
        /// <param name="text">
        /// The optional status text to report.
        /// </param>
        private static void MaybeReportStatus( /* CORE */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            string text              /* in: OPTIONAL */
            )
        {
            MaybeReportStatus(
                interpreter, clientData, text, false, true, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports the given status text to the interpreter, optionally
        /// checking for an existing status display, starting one, and
        /// sleeping so the user can see the text.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose status display is updated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the status display.
        /// </param>
        /// <param name="text">
        /// The optional status text to report.
        /// </param>
        /// <param name="noCheck">
        /// Non-zero to skip checking whether the status display is already
        /// active.
        /// </param>
        /// <param name="autoStart">
        /// Non-zero to automatically start the status display when needed.
        /// </param>
        /// <param name="noSleep">
        /// Non-zero to skip sleeping after reporting the status text.
        /// </param>
        public static void MaybeReportStatus( /* CORE */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            string text,             /* in: OPTIONAL */
            bool noCheck,            /* in */
            bool autoStart,          /* in */
            bool noSleep             /* in */
            )
        {
            ReturnCode code = ReturnCode.Ok; /* REUSED */
            Result error = null; /* REUSED */

            try
            {
                if (interpreter == null)
                {
                    error = "invalid interpreter";
                    code = ReturnCode.Error;

                    return;
                }

                if (Configuration.DoesVariableExist(
                        Constants.NoSandboxStatusEnvVarName))
                {
                    error = "sandbox status disabled";
                    code = ReturnCode.Error;

                    return;
                }

                //
                // HACK: When "noCheck" mode, skip calling to
                //       check if the status form is already
                //       active.
                //
                error = null;

                if (noCheck ||
                    (interpreter.CheckStatus(clientData,
                        Constants.SandboxReportStatusTimeout,
                        ref error) != ReturnCode.Ok))
                {
                    if (autoStart)
                    {
                        error = null;

                        code = interpreter.StartStatus(
                            clientData, null, ref error);

                        if (code != ReturnCode.Ok)
                        {
                            //
                            // HACK: When "noCheck" mode,
                            //       just fake it.
                            //
                            if (noCheck)
                                code = ReturnCode.Ok;
                            else
                                return;
                        }
                    }
                    else
                    {
                        code = ReturnCode.Error;
                        return;
                    }
                }

                if (text != null)
                {
                    error = null;

                    code = interpreter.ReportStatus(
                        clientData, String.Format(
                        "{0}: {1}{2}",
                        Utility.FormatTraceDateTime(
                            Utility.GetNow(), true),
                        text, Environment.NewLine),
                        null, ref error);

                    if (code != ReturnCode.Ok)
                        return;
                }

                //
                // HACK: Attempt to make sure the interactive
                //       user always has a chance to actually
                //       see the status text unless this has
                //       been disabled by the caller.
                //
                if (!noSleep)
                {
                    int milliseconds =
                        Constants.SandboxReportStatusSleep;

                    if (milliseconds >= 0)
                    {
                        /* IGNORED */
                        WaitOrSleep(interpreter, null, milliseconds);
                    }
                }
            }
            finally
            {
#if DEBUG || FORCE_TRACE
                if (code != ReturnCode.Ok)
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "MaybeReportStatus: interpreter = {0}, " +
                        "noCheck = {1}, autoStart = {2}, " +
                        "noSleep = {3}, code = {4}, error = {5}",
                        CertificateDataOps.FormatInterpreter(
                            interpreter, true, false),
                        noCheck, autoStart, noSleep, code,
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateSandboxOps).Name,
                        TracePriority.MediumLow);
                }
#endif
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to set an interpreter variable whose name is built from
        /// the given format, variable name, and identifier, while holding
        /// the interpreter lock.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter whose variable is set.
        /// </param>
        /// <param name="format">
        /// The optional format string used to build the variable name.
        /// </param>
        /// <param name="varName">
        /// The optional base variable name.
        /// </param>
        /// <param name="varValue">
        /// The optional value to assign to the variable.
        /// </param>
        /// <param name="id">
        /// The identifier combined with the format to build the variable
        /// name.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait for the interpreter lock.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while setting the variable.
        /// </param>
        /// <returns>
        /// Non-zero if the variable was successfully set; otherwise, zero.
        /// </returns>
        public static bool MaybeSetVariableValue( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            string format,           /* in: OPTIONAL */
            string varName,          /* in: OPTIONAL */
            string varValue,         /* in: OPTIONAL */
            long id,                 /* in */
            int timeout,             /* in */
            ref ResultList errors    /* in, out */
            )
        {
            if ((interpreter != null) && (format != null) &&
                (varName != null) && (varValue != null) &&
                (id != 0))
            {
                bool locked = false;

                try
                {
                    interpreter.TryLock(
                        timeout, ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        Result error = null;

                        if (interpreter.SetVariableValue(
                                String.Format(format, varName, id),
                                varValue, ref error) == ReturnCode.Ok)
                        {
                            return true;
                        }
                        else
                        {
                            if (error != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(error);
                            }
                        }
                    }
                    else
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add("interpreter is locked");
                    }
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(e);
                }
                finally
                {
                    interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to unset an interpreter variable whose name is built
        /// from the given format, variable name, and identifier, while
        /// holding the interpreter lock.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter whose variable is unset.
        /// </param>
        /// <param name="format">
        /// The optional format string used to build the variable name.
        /// </param>
        /// <param name="varName">
        /// The optional base variable name.
        /// </param>
        /// <param name="id">
        /// The identifier combined with the format to build the variable
        /// name.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait for the interpreter lock.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while unsetting the variable.
        /// </param>
        /// <returns>
        /// Non-zero if the variable was successfully unset; otherwise,
        /// zero.
        /// </returns>
        public static bool MaybeUnsetVariable( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            string format,           /* in: OPTIONAL */
            string varName,          /* in: OPTIONAL */
            long id,                 /* in */
            int timeout,             /* in */
            ref ResultList errors    /* in, out */
            )
        {
            if ((interpreter != null) && (format != null) &&
                (varName != null) && (id != 0))
            {
                bool locked = false;

                try
                {
                    interpreter.TryLock(
                        timeout, ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        Result error = null;

                        if (interpreter.UnsetVariable(
                                String.Format(format, varName, id),
                                ref error) == ReturnCode.Ok)
                        {
                            return true;
                        }
                        else
                        {
                            if (error != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(error);
                            }
                        }
                    }
                    else
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add("interpreter is locked");
                    }
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(e);
                }
                finally
                {
                    interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits on the given event, optionally with a timeout, and reports
        /// a textual description of the operation.
        /// </summary>
        /// <param name="event">
        /// The event to wait on.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait, or a negative value to wait
        /// indefinitely.
        /// </param>
        /// <param name="toString">
        /// Receives a textual description of the wait operation.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while waiting.
        /// </param>
        /// <returns>
        /// Non-zero if the event was signaled; otherwise, zero.
        /// </returns>
        public static bool MaybeWaitEvent( /* CORE */
            EventWaitHandle @event, /* in, out */
            int timeout,            /* in */
            ref string toString,    /* out */
            ref ResultList errors   /* in, out */
            )
        {
            if (@event != null)
            {
                try
                {
                    bool result;

                    if (timeout >= 0)
                    {
                        result = @event.WaitOne(
                            timeout, false); /* throw */
                    }
                    else
                    {
                        result = @event.WaitOne(); /* throw */
                    }

                    toString = String.Format(
                        "wait: {0}", @event);

                    return result;
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(e);
                }
                finally
                {
                    @event = null;
                }
            }
            else
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid sandbox event");
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the given event and reports a textual description of the
        /// operation, clearing the reference afterward.
        /// </summary>
        /// <param name="event">
        /// The event to set; the reference is cleared on return.
        /// </param>
        /// <param name="toString">
        /// Receives a textual description of the set operation.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while setting the event.
        /// </param>
        /// <returns>
        /// Non-zero if the event was successfully set; otherwise, zero.
        /// </returns>
        public static bool MaybeSetEvent( /* CORE */
            ref EventWaitHandle @event, /* in, out */
            ref string toString,        /* out */
            ref ResultList errors       /* in, out */
            )
        {
            if (@event != null)
            {
                try
                {
                    bool result = @event.Set(); /* throw */

                    toString = String.Format(
                        "set: {0}", @event);

                    return result;
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(e);
                }
                finally
                {
                    @event = null;
                }
            }
            else
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid sandbox event");
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the given event and reports a textual description of the
        /// operation, clearing the reference afterward.
        /// </summary>
        /// <param name="event">
        /// The event to close; the reference is cleared on return.
        /// </param>
        /// <param name="toString">
        /// Receives a textual description of the close operation.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while closing the event.
        /// </param>
        /// <returns>
        /// Non-zero if the event was successfully closed; otherwise, zero.
        /// </returns>
        public static bool MaybeCloseEvent( /* CORE */
            ref EventWaitHandle @event, /* in, out */
            ref string toString,        /* out */
            ref ResultList errors       /* in, out */
            )
        {
            if (@event != null)
            {
                try
                {
                    /* NO RESULT */
                    @event.Close(); /* throw */

                    toString = String.Format(
                        "close: {0}", @event);

                    return true;
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(e);
                }
                finally
                {
                    @event = null;
                }
            }
            else
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid sandbox event");
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits for the sandbox shutdown timeout or refresh event and then
        /// cleans up the sandbox interpreter and removes its token.  This is
        /// intended to be queued as a <see cref="WaitCallback" />.
        /// </summary>
        /// <param name="state">
        /// The shutdown pair carrying the shutdown client data and timeout.
        /// </param>
        /* System.Threading.WaitCallback */
        private static void Shutdown( /* CORE */
            object state /* in */
            )
        {
            ReturnCode code = ReturnCode.Ok; /* REUSED */
            Result result = null; /* REUSED */
            Interpreter interpreter = null;
            SharedEventWaitHandle @event = null;
            ulong? token = null;
            IConfiguration configuration = null;
            int timeout = 0;

            try
            {
                try
                {
                    // do nothing.
                }
                finally /* NOTE: Thread.Abort() protection. */
                {
                    ShutdownPair anyPair = state as ShutdownPair;

                    if (anyPair == null)
                    {
                        result = "invalid shutdown pair";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    SDCD clientData = anyPair.X;

                    if (clientData == null)
                    {
                        result = "invalid clientData";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    if (clientData.Disposed)
                    {
                        result = "disposed clientData";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    interpreter = clientData.Interpreter;

                    if (interpreter == null)
                    {
                        result = "invalid interpreter";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    @event = clientData.RefreshEvent;

                    if (@event == null)
                    {
                        result = "invalid refresh event";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    token = clientData.SandboxToken;

                    if (token == null)
                    {
                        result = "invalid sandbox token";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    configuration = clientData.Configuration;

                    if (configuration == null)
                    {
                        result = "configuration unavailable";
                        code = ReturnCode.Error;

                        goto done;
                    }

                    timeout = anyPair.Y;

                    if (timeout <= 0)
                    {
                        result = "invalid shutdown timeout";
                        code = ReturnCode.Error;

                        goto done;
                    }

                done:

#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "Shutdown: SLEEP, token = {0}, " +
                        "interpreter = {1}, event = {2}, " +
                        "timeout = {3}, code = {4}, result = {5}",
                        Utility.FormatWrapOrNull(token),
                        CertificateDataOps.FormatInterpreter(
                            interpreter, true, false),
                        Utility.GetHashCode(@event), timeout,
                        code, Utility.FormatWrapOrNull(result)),
                        typeof(CertificateSandboxOps).Name,
                        TracePriority.Low);
#else
                    ; /* HACK: Do not remove this empty statement. */
#endif
                }

                /* IGNORED */
                WaitOrSleep(interpreter, @event, timeout); /* throw */

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "Shutdown: TRY, token = {0}, " +
                    "interpreter = {1}, event = {2}, " +
                    "timeout = {3}, code = {4}, result = {5}",
                    Utility.FormatWrapOrNull(token),
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    Utility.GetHashCode(@event), timeout,
                    code, Utility.FormatWrapOrNull(result)),
                    typeof(CertificateSandboxOps).Name,
                    TracePriority.Highest);
#endif
            }
            catch (ThreadAbortException e)
            {
                Thread.ResetAbort();

                result = e;
                code = ReturnCode.Error;
            }
            catch (ThreadInterruptedException e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            catch (Exception e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            finally /* NOTE: Thread.Abort() protection. */
            {
                ResultList errors = null;

                if (@event != null)
                {
                    Result localError = null;

                    if (Utility.TryDisposeObject<SharedEventWaitHandle>(
                            ref @event, ref localError) != ReturnCode.Ok)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }
                    }
                }

                if (token != null)
                {
                    ulong localToken = (ulong)token;

                    if (!CertificateScriptOps.CleanupInterpreter(
                            localToken))
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(String.Format(
                            "could not cleanup interpreter {0}",
                            localToken));

                        goto done;
                    }

                    if ((configuration != null) &&
                        !configuration.RemoveSandboxToken(localToken))
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(String.Format(
                            "could not remove sandbox token {0}",
                            localToken));

                        goto done;
                    }
                }

            done:

#if DEBUG || FORCE_TRACE
                if ((code != ReturnCode.Ok) || (errors != null))
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "Shutdown: FINALLY, token = {0}, " +
                        "interpreter = {1}, event = {2}, " +
                        "timeout = {3}, code = {4}, result = {5}, " +
                        "errors = {6}",
                        Utility.FormatWrapOrNull(token),
                        CertificateDataOps.FormatInterpreter(
                            interpreter, true, false),
                        Utility.GetHashCode(@event), timeout,
                        code, Utility.FormatWrapOrNull(result),
                        Utility.FormatWrapOrNull(errors)),
                        typeof(CertificateSandboxOps).Name,
                        TracePriority.MediumLow);
                }
#else
                ; /* HACK: Do not remove this empty statement. */
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a sandbox interpreter and evaluates a license certificate
        /// script within it, reporting status and propagating the result back
        /// to the parent interpreter.  This is intended to be queued as a
        /// <see cref="WaitCallback" />.
        /// </summary>
        /// <param name="state">
        /// The sandbox pair carrying the evaluation client data and the
        /// identifier of the sandbox to evaluate.
        /// </param>
        /* System.Threading.WaitCallback */
        public static void EvaluateWithin( /* CORE */
            object state /* in */
            )
        {
            ReturnCode code = ReturnCode.Ok; /* REUSED */
            Result result = null; /* REUSED */
            EvaluateClientData clientData = null;
            SandboxData sandboxData = null;
            long nextId = 0;
            Interpreter parentInterpreter = null;
            string parentVarName = null;
            long parentNextId = 0;
            EventWaitHandle parentEvent = null;

            try
            {
                SandboxPair anyPair = state as SandboxPair;

                if (anyPair == null)
                {
                    result = "invalid sandbox pair";
                    code = ReturnCode.Error;

                    return;
                }

                clientData = anyPair.X;

                if (clientData == null)
                {
                    result = "invalid clientData";
                    code = ReturnCode.Error;

                    return;
                }

                if (clientData.Disposed)
                {
                    result = "disposed clientData";
                    code = ReturnCode.Error;

                    return;
                }

                nextId = anyPair.Y;

                if (!clientData.TryGetSandbox(
                        nextId, out sandboxData))
                {
                    result = String.Format(
                        "missing evaluation sandbox {0}", nextId);

                    code = ReturnCode.Error;
                    return;
                }

                if (sandboxData == null)
                {
                    result = String.Format(
                        "invalid evaluation sandbox {0}", nextId);

                    code = ReturnCode.Error;
                    return;
                }

                parentInterpreter = clientData.Interpreter;
                parentVarName = sandboxData.VarName;
                parentNextId = sandboxData.NextId;
                parentEvent = sandboxData.Event;

                ulong? sandboxToken = clientData.SandboxToken;
                IPluginData parentPluginData = clientData.Plugin;
                bool createdInterpreter = false;

                result = null;

                using (Interpreter interpreter =
                    CertificateScriptOps.CreateInterpreter(
                        sandboxToken, clientData.SettingsCallback,
                        clientData.RuleSet, parentInterpreter,
                        parentPluginData, clientData.HashAlgorithmName,
                        clientData.HashKey, clientData.Encoding,
                        clientData.KeyPairs, clientData.KeyUsage,
                        clientData.CultureInfo, clientData.Timeout,
                        clientData.AllowRemoteUri, ref createdInterpreter,
                        ref result))
                {
                    if (interpreter == null)
                    {
                        code = ReturnCode.Error;
                        return;
                    }

                    SharedEventWaitHandle @event = clientData.RefreshEvent;

                    if (@event != null)
                        @event = @event.Clone() as SharedEventWaitHandle;

                    SDCD shutdownClientData = new SDCD(null,
                        interpreter, parentPluginData as IConfiguration,
                        @event, sandboxToken);

                    bool primaryToken;

                    /* IGNORED */
                    Configuration.MaybeKeepTrackOfSandboxToken(
                        sandboxToken, parentPluginData, interpreter,
                        createdInterpreter, out primaryToken);

#if WINFORMS
                    string sandboxName = String.Format(
                        Constants.SandboxNameFormat,
                        Utility.FormatWrapOrNull(
                            clientData.FileName),
                        Utility.FormatWrapOrNull(
                            parentPluginData),
                        CertificateDataOps.FormatAppDomainId(
                            parentInterpreter, true, true),
                        CertificateDataOps.FormatInterpreter(
                            parentInterpreter, true, true),
                        CertificateDataOps.FormatVarName(
                            parentVarName, true, true),
                        CertificateDataOps.FormatId(
                            parentNextId, true));

                    if (createdInterpreter)
                    {
                        MaybeReportStatus(
                            interpreter, clientData, String.Format(
                            Constants.SandboxActivateStatusText,
                            sandboxName, primaryToken ? "PRIMARY " :
                            String.Empty, Environment.NewLine));
                    }
                    else if (SetRefreshEvent(shutdownClientData))
                    {
                        MaybeReportStatus(
                            interpreter, clientData, String.Format(
                            Constants.SandboxRefreshStatusText,
                            sandboxName, primaryToken ? "PRIMARY " :
                            String.Empty, Environment.NewLine));
                    }
#endif

                    clientData.Interpreter = interpreter;

                    if (createdInterpreter)
                    {
                        clientData.ResetReferences();

                        if (!Configuration.DoesVariableExist(
                                Constants.NoSandboxShutdownEnvVarName))
                        {
                            int timeout = Constants.ShutdownSandboxTimeout;

#if WINFORMS
                            MaybeReportStatus(
                                interpreter, clientData, String.Format(
                                Constants.SandboxShutdownStatusText,
                                timeout / 1000, primaryToken ? "PRIMARY " :
                                String.Empty, Environment.NewLine));
#endif

                            Utility.QueueUserWorkItem(
                                new WaitCallback(Shutdown),
                                new ShutdownPair(
                                    shutdownClientData, timeout),
                                QueueFlags.Default);
                        }
                    }

                    result = null;

                    code = CertificateScriptOps.EvaluateFile(
                        clientData, ref result);
                }
            }
            catch (ThreadAbortException e)
            {
                Thread.ResetAbort();

                result = e;
                code = ReturnCode.Error;
            }
            catch (ThreadInterruptedException e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            catch (Exception e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            finally
            {
                ResultList errors = null;

                if (!clientData.RemoveSandbox(sandboxData))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Insert(0, String.Format(
                        "could not remove evaluation sandbox {0}",
                        nextId));
                }

                clientData.Dispose(); /* NOTE: Asynchronous. */

                /* IGNORED */
                MaybeSetVariableValue(parentInterpreter,
                    Constants.SandboxReturnCodeVarNameFormat,
                    parentVarName, code.ToString(), parentNextId,
                    Constants.SandboxVariableTimeout, ref errors);

                /* IGNORED */
                MaybeSetVariableValue(parentInterpreter,
                    Constants.SandboxResultVarNameFormat,
                    parentVarName, result, parentNextId,
                    Constants.SandboxVariableTimeout, ref errors);

                string parentEventToString = null;

                /* IGNORED */
                MaybeSetEvent(ref parentEvent,
                    ref parentEventToString, ref errors);

#if DEBUG || FORCE_TRACE
                if ((code != ReturnCode.Ok) || (errors != null))
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "EvaluateWithin: interpreter = {0}, " +
                        "varName = {1}, nextId = {2}, event = {3}, " +
                        "code = {4}, result = {5}, errors = {6}",
                        CertificateDataOps.FormatInterpreter(
                            parentInterpreter, true, false),
                        Utility.FormatWrapOrNull(parentVarName),
                        parentNextId,
                        Utility.FormatWrapOrNull(parentEventToString),
                        code, Utility.FormatWrapOrNull(result),
                        Utility.FormatWrapOrNull(errors)),
                        typeof(CertificateSandboxOps).Name,
                        TracePriority.MediumLow);
                }
#endif
            }
        }
    }
}
