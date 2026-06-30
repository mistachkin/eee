/*
 * Shell.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;
using HotKey.Forms;
using HotKey.Interfaces.Private;

namespace HotKey.Shell
{
    /// <summary>
    /// Manages the application-domain-wide hot-key manager form and its
    /// dedicated WinForms message-loop thread.  It starts and stops the
    /// thread, exposes the manager and its log, tracks the owning plugin, and
    /// holds the hot-key root directory.  All access is synchronized.
    /// </summary>
    [ObjectId("8405da21-51b7-47ff-91d2-ecce3749d627")]
    internal static class Form
    {
        #region Private Constants
        //
        // NOTE: This is the maximum number of milliseconds to wait for the
        //       hot-key manager thread (i.e. the one managed by this class)
        //       to die.  If this is negative, we will wait forever.
        //
        /// <summary>
        /// The maximum number of milliseconds to wait for the hot-key manager
        /// thread to die; a negative value waits forever.
        /// </summary>
        private static readonly int ThreadJoinTimeout = -1;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum number of milliseconds to wait when an
        //       attempt is being made to send a log request to the hot-key
        //       manager (i.e. from this class).  This value must be greater
        //       than zero to actually be used.
        //
        /// <summary>
        /// The maximum number of milliseconds to wait when sending a log
        /// request to the hot-key manager; must be greater than zero to be
        /// used.
        /// </summary>
        private static readonly int LogTimeout = 1000;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: Used to synchronize access to the contained private data.
        //
        /// <summary>
        /// The object used to synchronize access to the private data of this
        /// class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The plugin that started us [directly].
        //
        /// <summary>
        /// The plugin that directly started the hot-key manager thread.
        /// </summary>
        private static IPlugin startPlugin;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The thread that we started [directly].
        //
        /// <summary>
        /// The hot-key manager thread that this class started.
        /// </summary>
        private static Thread startThread;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the event wait handle that will be signaled upon
        //       the hot-key manager starting up.
        //
        /// <summary>
        /// The event signaled when the hot-key manager has started up.
        /// </summary>
        private static EventWaitHandle startEvent;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The hot-key manager that is responsible for receiving and
        //       dispatching hot-key messages from Windows.
        //
        /// <summary>
        /// The hot-key manager responsible for receiving and dispatching
        /// hot-key messages from Windows.
        /// </summary>
        private static IHotKeyManager hotKeyManager;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The root directory where hot-key and template scripts, et
        //       al, should search for executable files and other content.
        //
        /// <summary>
        /// The root directory searched by hot-key and template scripts for
        /// executable files and other content.
        /// </summary>
        private static string rootDirectory;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Management Methods
        /// <summary>
        /// Gets the number of currently registered hot-keys.
        /// </summary>
        /// <param name="quiet">
        /// Non-zero to suppress complaints on failure.
        /// </param>
        /// <returns>
        /// The registered hot-key count, or an invalid count on failure.
        /// </returns>
        public static int GetHotKeyRegisteredCount(
            bool quiet /* in */
            )
        {
            int result = Count.Invalid;

            lock (syncRoot)
            {
                if (hotKeyManager != null)
                {
                    ReturnCode code;
                    Result error = null;

                    code = hotKeyManager.CountHotKeys(
                        true, ref result, ref error);

                    if (!quiet && (code != ReturnCode.Ok))
                        LogOps.Complain(code, error);
                }
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Thread Management Methods
        /// <summary>
        /// Determines whether the hot-key manager thread exists and is alive.
        /// Thread-safe.
        /// </summary>
        /// <returns>
        /// Non-zero when the thread is alive; otherwise, zero.
        /// </returns>
        public static bool HaveHotKeyManagerThread() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                return ((startThread != null) && startThread.IsAlive);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Starts the dedicated hot-key manager thread for the specified
        /// plugin, unless it is already running.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the manager is associated with.
        /// </param>
        /// <param name="plugin">
        /// The plugin starting the thread; must implement the started
        /// interface.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when the thread is already started.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode StartHotKeyManagerThread(
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            IClientData clientData,  /* in */
            bool strict,             /* in */
            ref Result error         /* out */
            ) /* THREAD-SAFE */
        {
            IStarted started = plugin as IStarted;

            if (started == null)
            {
                error = "invalid start";
                return ReturnCode.Error;
            }

            ReturnCode code;

            lock (syncRoot)
            {
                if ((startThread == null) || !startThread.IsAlive)
                {
                    //
                    // NOTE: We will start the thread next; therefore,
                    //       make sure to flag the plugin first.
                    //
                    started.Started = true;

                    startThread = Engine.CreateThread(interpreter,
                        HotKeyManagerThreadStart, 0, true, false, true);

                    if (startThread != null)
                    {
                        if (startEvent != null)
                            startEvent.Reset();
                        else
                            startEvent = new ManualResetEvent(false);

                        IAnyPair<Interpreter, EventWaitHandle> anyPair =
                            new AnyPair<Interpreter, EventWaitHandle>(
                                interpreter, startEvent);

                        startThread.Name = String.Format(
                            "{0}: {1}", typeof(Form).FullName,
                            HotKeyOps.ToString(interpreter));

                        startThread.Start(anyPair); /* throw */
                        startPlugin = started as IPlugin;

                        code = ReturnCode.Ok;
                    }
                    else
                    {
                        error = "could not create thread";
                        code = ReturnCode.Error;
                    }
                }
                else if (strict)
                {
                    error = "thread has already been started";
                    code = ReturnCode.Error;
                }
                else
                {
                    code = ReturnCode.Ok;
                }
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stops the dedicated hot-key manager thread, closing the form and
        /// joining the thread, when the specified plugin started it.
        /// Thread-safe.
        /// </summary>
        /// <param name="plugin">
        /// The plugin that started the thread; must implement the started
        /// interface.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when the thread was not started.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode StopHotKeyManagerThread(
            IPlugin plugin,  /* in */
            bool strict,     /* in */
            ref Result error /* out */
            ) /* THREAD-SAFE */
        {
            IStarted started = plugin as IStarted;

            if (started == null)
            {
                error = "invalid start";
                return ReturnCode.Error;
            }

            //
            // NOTE: If the plugin instance specified by the caller was not
            //       directly responsible for starting the hot-key manager
            //       form thread, just return success now.
            //
            if (!started.Started)
                return ReturnCode.Ok;

            CloseForm();

            Thread thread;

            lock (syncRoot)
            {
                thread = startThread;
            }

            ReturnCode code;

            if (thread != null)
            {
                try
                {
                    if (!thread.IsAlive ||
                        thread.Join(ThreadJoinTimeout)) /* throw */
                    {
                        thread = null; /* DEAD */

                        lock (syncRoot)
                        {
                            startThread = null; /* DEAD */

                            if (startEvent != null)
                            {
                                startEvent.Close();
                                startEvent = null;
                            }

                            startPlugin = null;
                        }

                        code = ReturnCode.Ok;
                    }
                    else
                    {
                        error = "timeout waiting for thread to exit";
                        code = ReturnCode.Error;
                    }
                }
                catch (Exception e)
                {
                    error = e;
                    code = ReturnCode.Error;
                }
            }
            else if (strict)
            {
                error = "thread has not been started";
                code = ReturnCode.Error;
            }
            else
            {
                code = ReturnCode.Ok;
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Form Thread Start Routine
        /// <summary>
        /// The thread entry point that creates the hot-key manager form and
        /// runs its WinForms message loop.
        /// </summary>
        /// <param name="obj">
        /// The thread parameter, an interpreter/event pair used to create the
        /// form and signal startup.
        /// </param>
        private static void HotKeyManagerThreadStart(
            object obj /* in */
            ) /* ParameterizedThreadStart */
        {
            try
            {
                Utility.DebugTrace(
                    "HotKeyManagerThreadStart: entered",
                    typeof(Form).Name, TracePriority.MediumLow |
                        TracePriority.FromPlugin);

                ///////////////////////////////////////////////////////////////

                IHotKeyManager localHotKeyManager;

                ///////////////////////////////////////////////////////////////

                lock (syncRoot)
                {
                    localHotKeyManager = hotKeyManager;

                    if (localHotKeyManager != null)
                    {
                        Utility.DebugTrace(
                            "HotKeyManagerThreadStart: form already created",
                            typeof(Form).Name, TracePriority.MediumLow |
                                TracePriority.FromPlugin);

                        return;
                    }
                    else
                    {
                        IAnyPair<Interpreter, EventWaitHandle> anyPair =
                            obj as IAnyPair<Interpreter, EventWaitHandle>;

                        if (anyPair != null)
                        {
                            hotKeyManager = localHotKeyManager =
                                new HotKeyManagerForm(FormId.GetNext(),
                                    anyPair.X, null, anyPair.Y);

                            Utility.DebugTrace(String.Format(
                                "HotKeyManagerThreadStart: form created, " +
                                "handle = {0}, interpreter = {1}, event = {2}",
                                localHotKeyManager.GetHotKeyHandle(),
                                HotKeyOps.ToString(anyPair.X),
                                Utility.FormatWrapOrNull(anyPair.Y)),
                                typeof(Form).Name, TracePriority.MediumLow |
                                    TracePriority.FromPlugin);
                        }
                        else
                        {
                            Utility.DebugTrace(
                                "HotKeyManagerThreadStart: cannot create form, " +
                                "invalid argument", typeof(Form).Name,
                                TracePriority.MediumHigh |
                                    TracePriority.FromPlugin);

                            return;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                /* IGNORED */
                Application.Run(
                    localHotKeyManager as System.Windows.Forms.Form);

                ///////////////////////////////////////////////////////////////
            }
            catch (Exception e)
            {
                LogOps.Complain(ReturnCode.Error, e);
            }
            finally
            {
                Utility.DebugTrace(
                    "HotKeyManagerThreadStart: exited",
                    typeof(Form).Name, TracePriority.MediumLow |
                        TracePriority.FromPlugin);
            }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Management Methods
        /// <summary>
        /// Determines whether a starting plugin is currently associated with
        /// the hot-key manager.  Thread-safe.
        /// </summary>
        /// <returns>
        /// Non-zero when a plugin is associated; otherwise, zero.
        /// </returns>
        public static bool HaveHotKeyPlugin() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                return (startPlugin != null);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the hot-key plugin is isolated in a different
        /// application domain than the specified interpreter.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to compare against, or null to compare against the
        /// current domain.
        /// </param>
        /// <returns>
        /// Non-zero when the plugin is isolated; otherwise, zero.
        /// </returns>
        public static bool IsHotKeyIsolated(
            Interpreter interpreter /* in */
            ) /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                return (interpreter != null) ?
                    Utility.IsCrossAppDomain(interpreter, startPlugin) :
                    Utility.IsCrossAppDomain(startPlugin);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Form Management Methods
        /// <summary>
        /// Determines whether the hot-key manager exists and has not been
        /// closed.  Thread-safe.
        /// </summary>
        /// <returns>
        /// Non-zero when the manager is available; otherwise, zero.
        /// </returns>
        public static bool HaveHotKeyManager() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                return (hotKeyManager != null) &&
                    !hotKeyManager.IsClosed;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the hot-key manager, or null when it does not exist or has
        /// been closed.  Thread-safe.
        /// </summary>
        /// <returns>
        /// The hot-key manager, or null when unavailable.
        /// </returns>
        public static IHotKeyManager GetHotKeyManager() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                //
                // BUGFIX: Do not return the hot-key manager form if it has
                //         been closed.
                //
                if ((hotKeyManager == null) ||
                    hotKeyManager.IsClosed)
                {
                    return null;
                }

                return hotKeyManager;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is for use by the HotKeyForm_FormClosed private
        //       event handler method only.
        //
        /// <summary>
        /// Clears the cached hot-key manager reference.  Intended for use by
        /// the form-closed event handler only.  Thread-safe.
        /// </summary>
        public static void ClearHotKeyManager() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                hotKeyManager = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits up to the specified timeout for the hot-key manager to start
        /// up.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter; not used by this method.
        /// </param>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail on timeout or a missing event.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode WaitHotKeyManager(
            Interpreter interpreter, /* in: NOT USED */
            int timeout,             /* in */
            bool strict,             /* in */
            ref Result error         /* out */
            ) /* THREAD-SAFE */
        {
            try
            {
                EventWaitHandle localEvent;

                lock (syncRoot)
                {
                    localEvent = startEvent;
                }

                if (localEvent != null)
                {
                    if (localEvent.WaitOne(timeout))
                    {
                        return ReturnCode.Ok;
                    }
                    else if (strict)
                    {
                        error = String.Format(
                            "hot-key manager timeout of {0} milliseconds",
                            timeout);
                    }
                    else
                    {
                        return ReturnCode.Ok;
                    }
                }
                else if (strict)
                {
                    error = "invalid hot-key manager event";
                }
                else
                {
                    return ReturnCode.Ok;
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
        /// Safely closes the hot-key manager form.  Thread-safe.
        /// </summary>
        private static void CloseForm() /* THREAD-SAFE */
        {
            try
            {
                Utility.DebugTrace(
                    "CloseForm: entered", typeof(Form).Name,
                    TracePriority.MediumLow | TracePriority.FromPlugin);

                ///////////////////////////////////////////////////////////////

                ISafeClose safeClose;

                ///////////////////////////////////////////////////////////////

                lock (syncRoot)
                {
                    safeClose = hotKeyManager as ISafeClose;

                    if (safeClose == null)
                    {
                        Utility.DebugTrace(
                            "CloseForm: form already shutdown or invalid",
                            typeof(Form).Name, TracePriority.MediumLow |
                                TracePriority.FromPlugin);

                        return;
                    }
                }

                ///////////////////////////////////////////////////////////////

                safeClose.SafeClose();
                safeClose = null;
            }
            catch (Exception e)
            {
                LogOps.Complain(ReturnCode.Error, e);
            }
            finally
            {
                Utility.DebugTrace(
                    "CloseForm: exited", typeof(Form).Name,
                    TracePriority.MediumLow | TracePriority.FromPlugin);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Log Management Methods
        /// <summary>
        /// Determines whether a hot-key log is available (optionally requiring
        /// the start event when a positive timeout is given).
        /// </summary>
        /// <param name="timeout">
        /// The timeout that determines whether the start event is required.
        /// </param>
        /// <returns>
        /// Non-zero when a log is available; otherwise, zero.
        /// </returns>
        public static bool HaveHotKeyLog(
            int timeout /* in */
            )
        {
            lock (syncRoot)
            {
                if ((timeout > 0) && (startEvent == null))
                    return false;

                if (hotKeyManager == null)
                    return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the hot-key manager's log.  Thread-safe.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ClearHotKeyLog(
            ref Result error /* out */
            ) /* THREAD-SAFE */
        {
            ISafeResult safeResult;

            lock (syncRoot)
            {
                safeResult = hotKeyManager as ISafeResult;
            }

            if (safeResult == null)
            {
                error = "invalid safe result";
                return ReturnCode.Error;
            }

            if (!safeResult.SafeClearResult())
            {
                error = "could not clear log";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log using the default log
        /// timeout.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        /// <param name="text">
        /// The text to append.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode AppendToHotKeyLog(
            Interpreter interpreter, /* in */
            string text,             /* in */
            ref Result error         /* out */
            ) /* THREAD-SAFE */
        {
            return AppendToHotKeyLog(interpreter, text, LogTimeout, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log, waiting up to the
        /// specified timeout for the manager.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        /// <param name="text">
        /// The text to append.
        /// </param>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait for the manager.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode AppendToHotKeyLog(
            Interpreter interpreter, /* in */
            string text,             /* in */
            int timeout,             /* in */
            ref Result error         /* out */
            ) /* THREAD-SAFE */
        {
            //
            // NOTE: Wait for the specified number of milliseconds prior to
            //       attempting to log to the hot-key manager?
            //
            if ((timeout > 0) && (WaitHotKeyManager(
                    interpreter, timeout, true, ref error) != ReturnCode.Ok))
            {
                //
                // NOTE: The timeout expired, fail now.  The caller should
                //       (read: must) be setup to complain about this.
                //
                return ReturnCode.Error;
            }

            ISafeResult safeResult;

            lock (syncRoot)
            {
                safeResult = hotKeyManager as ISafeResult;
            }

            if (safeResult == null)
            {
                error = "invalid hot-key form";
                return ReturnCode.Error;
            }

            if (!safeResult.SafeAppendLogEntry(text))
            {
                error = "could not append to log";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the hot-key manager's log to the clipboard.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode CopyLogToClipboard(
            ref Result error /* out */
            )
        {
            ISafeResult safeResult;

            lock (syncRoot)
            {
                safeResult = hotKeyManager as ISafeResult;
            }

            if (safeResult == null)
            {
                error = "invalid safe result";
                return ReturnCode.Error;
            }

            if (!safeResult.SafeCopyResultToClipboard())
            {
                error = "could not copy log to clipboard";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a formatted log entry describing the result of a hot-key's
        /// script evaluation.  Thread-safe.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        /// <param name="hotKey">
        /// The hot-key whose result is logged.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode LogHotKeyResult(
            Interpreter interpreter, /* in */
            IHotKey hotKey,          /* in */
            ref Result error         /* out */
            ) /* THREAD-SAFE */
        {
            if (hotKey == null)
            {
                error = "invalid hot-key";
                return ReturnCode.Error;
            }

            bool asynchronous;
            int id;
            HotKeyFlags flags;
            string text;

            lock (hotKey.SyncRoot)
            {
                asynchronous = hotKey.HasFlags(
                    HotKeyFlags.Asynchronous, true);

                id = hotKey.Id;
                flags = hotKey.Flags;
                text = LogOps.FormatHotKeyResult(hotKey);
            }

            return AppendToHotKeyLog(interpreter, String.Format(
                "{0} RESULT: interpreter = {1}, keyId = {2}, " +
                "keyFlags = {3}, result = {4}", asynchronous ?
                "ASYNCHRONOUS" : "SYNCHRONOUS", HotKeyOps.ToString(
                interpreter), id, Utility.FormatWrapOrNull(flags),
                text), ref error);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Root Directory Management Methods
        /// <summary>
        /// Gets the hot-key root directory.  Thread-safe.
        /// </summary>
        /// <returns>
        /// The root directory, or null when none is set.
        /// </returns>
        public static string GetHotKeyRootDirectory() /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                return rootDirectory;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the hot-key root directory.  Thread-safe.
        /// </summary>
        /// <param name="directory">
        /// The root directory to set, or null to clear it.
        /// </param>
        public static void SetHotKeyRootDirectory(
            string directory /* in */
            ) /* THREAD-SAFE */
        {
            lock (syncRoot)
            {
                rootDirectory = directory;
            }
        }
        #endregion
    }
}
