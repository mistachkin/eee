/*
 * LogOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Runtime.CompilerServices;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using HotKey.Interfaces.Private;

namespace HotKey.Components.Private
{
    #region Private Delegates
    /// <summary>
    /// Represents a callback that appends a line of text to a log and reports
    /// whether it was logged.
    /// </summary>
    /// <param name="text">
    /// The text to log.
    /// </param>
    /// <returns>
    /// Non-zero if the text was logged; otherwise, zero.
    /// </returns>
    [ObjectId("c096fd81-0b1f-4464-b688-d37bef3381b8")]
    internal delegate bool LoggingCallback(string text);
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides logging and diagnostic helpers for the HotKey plugin:
    /// formatting timestamps, log entries, registration entries, and hot-key
    /// results, and routing messages to the hot-key log or, failing that, the
    /// complaint mechanism.
    /// </summary>
    [ObjectId("ebc1d05e-199b-4b61-90a2-9e85ea1ed834")]
    internal static class LogOps
    {
        #region Private Constants
        /// <summary>
        /// The format string used for a general log entry (thread id,
        /// timestamp, text, newline).
        /// </summary>
        private const string LogEntryFormat = "{0:000000}: [{1}]: {2}{3}";

        /// <summary>
        /// The format string used for a hot-key registration log entry.
        /// </summary>
        private const string RegistrationLogEntryFormat1 =
            "{0} hot-key {1} with keys {2}{3}";

        /// <summary>
        /// The format string appended to a registration log entry reporting
        /// the current registered hot-key count.
        /// </summary>
        private const string RegistrationLogEntryFormat2 =
            ", there are now {0} registered hot-keys";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to render timestamps in log entries.
        /// </summary>
        private const string TimeStampFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Diagnostic Helper Methods
        #region Formatting Helper Methods
        /// <summary>
        /// Gets the current time, formatted for a log entry.
        /// </summary>
        /// <param name="utc">
        /// Non-zero to use UTC; zero to use local time.
        /// </param>
        /// <returns>
        /// The formatted current time.
        /// </returns>
        public static string GetNowString(
            bool utc /* in */
            )
        {
            return FormatHotKeyDateTime(
                utc ? Utility.GetUtcNow() : Utility.GetNow());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a nullable date/time using the log timestamp format.
        /// </summary>
        /// <param name="value">
        /// The date/time to format, or null.
        /// </param>
        /// <returns>
        /// The formatted timestamp, or null when the value is null.
        /// </returns>
        public static string FormatHotKeyDateTime(
            DateTime? value /* in */
            )
        {
            if (value == null)
                return null;

            return ((DateTime)value).ToString(TimeStampFormat);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a complete log entry for the supplied text, prefixing the
        /// current thread id and UTC timestamp and appending a newline.
        /// </summary>
        /// <param name="value">
        /// The text of the log entry.
        /// </param>
        /// <returns>
        /// The formatted log entry.
        /// </returns>
        public static string FormatHotKeyLogEntry(
            string value /* in */
            )
        {
            return String.Format(LogEntryFormat,
                Utility.GetCurrentThreadId(), GetNowString(true), value,
                Environment.NewLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a registration log entry for the supplied hot-key,
        /// describing whether it was registered or unregistered, its id and
        /// keys, and the current registered hot-key count.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key whose registration is described.
        /// </param>
        /// <returns>
        /// The formatted registration log entry, or null when the hot-key is
        /// null.
        /// </returns>
        public static string FormatHotKeyRegistrationLogEntry(
            IHotKey hotKey /* in */
            )
        {
            if (hotKey == null)
                return null;

            int count = Shell.Form.GetHotKeyRegisteredCount(true);

            return String.Format(
                RegistrationLogEntryFormat1, hotKey.Registered ?
                "Registered" : "Unregistered", hotKey.Id,
                Utility.FormatWrapOrNull(WinFormsOps.GetKeysToShow(
                hotKey.Keys)), (count != Count.Invalid) ?
                String.Format(RegistrationLogEntryFormat2, count) :
                String.Empty);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the captured result of a hot-key's most recent script
        /// evaluation (its return code, result text, and error line).
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key whose result is formatted.
        /// </param>
        /// <returns>
        /// The formatted result.
        /// </returns>
        public static string FormatHotKeyResult(
            IHotKey hotKey /* in */
            )
        {
            if (hotKey != null)
            {
                return Utility.FormatResult(hotKey.ReturnCode,
                    Utility.FormatWrapOrNull(true, false, hotKey.Result),
                    hotKey.ErrorLine);
            }

            return Utility.FormatWrapOrNull(null);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs, via the supplied callback, a message describing a temporary
        /// safe child interpreter created for the named operation.
        /// </summary>
        /// <param name="interpreter">
        /// The temporary interpreter being described.
        /// </param>
        /// <param name="loggingCallback">
        /// The callback used to emit the log message, if any.
        /// </param>
        /// <param name="operation">
        /// The name of the operation the interpreter was created for.
        /// </param>
        public static void MaybeLogInterpreter(
            Interpreter interpreter,         /* in */
            LoggingCallback loggingCallback, /* in */
            string operation                 /* in */
            )
        {
            if (loggingCallback != null)
            {
                loggingCallback(String.Format(
                    "{0}: temporary safe child interpreter {1}",
                    operation, HotKeyOps.ToString(interpreter)));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Complain Helper Methods
        /// <summary>
        /// Raises a complaint about a failed operation, without a specific
        /// interpreter or hot-key.
        /// </summary>
        /// <param name="code">
        /// The return code of the failed operation.
        /// </param>
        /// <param name="result">
        /// The result describing the failure.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Complain(
            ReturnCode code, /* in */
            Result result    /* in */
            )
        {
            Complain(null, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises a complaint about a failed operation in the context of the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the failure, if any.
        /// </param>
        /// <param name="code">
        /// The return code of the failed operation.
        /// </param>
        /// <param name="result">
        /// The result describing the failure.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Complain(
            Interpreter interpreter, /* in */
            ReturnCode code,         /* in */
            Result result            /* in */
            )
        {
            Complain(null, interpreter, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises a complaint about a failed operation, suppressed when the
        /// associated hot-key has the no-complain flag set.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key associated with the failure, if any; complaints are
        /// suppressed when it has the no-complain flag.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the failure, if any.
        /// </param>
        /// <param name="code">
        /// The return code of the failed operation.
        /// </param>
        /// <param name="result">
        /// The result describing the failure.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Complain(
            IHotKey hotKey,          /* in */
            Interpreter interpreter, /* in */
            ReturnCode code,         /* in */
            Result result            /* in */
            )
        {
            if ((hotKey != null) &&
                hotKey.HasFlags(HotKeyFlags.NoComplain, true))
            {
                return;
            }

            Utility.Complain(interpreter, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log when a log is
        /// available within the timeout, complaining on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry, if any.
        /// </param>
        /// <param name="text">
        /// The text to log.
        /// </param>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait for the log.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void MaybeLogOrComplain(
            Interpreter interpreter, /* in */
            string text,             /* in */
            int timeout              /* in */
            )
        {
            if (Shell.Form.HaveHotKeyLog(timeout))
                LogOrComplain(interpreter, text, timeout);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log, complaining on
        /// failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry, if any.
        /// </param>
        /// <param name="text">
        /// The text to log.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogOrComplain(
            Interpreter interpreter, /* in */
            string text              /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Shell.Form.AppendToHotKeyLog(
                interpreter, text, ref error);

            if (code != ReturnCode.Ok)
                Complain(interpreter, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log, waiting up to the
        /// specified timeout and complaining on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry, if any.
        /// </param>
        /// <param name="text">
        /// The text to log.
        /// </param>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait for the log.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogOrComplain(
            Interpreter interpreter, /* in */
            string text,             /* in */
            int timeout              /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Shell.Form.AppendToHotKeyLog(
                interpreter, text, timeout, ref error);

            if (code != ReturnCode.Ok)
                Complain(interpreter, code, error);
        }
        #endregion
        #endregion
    }
}
