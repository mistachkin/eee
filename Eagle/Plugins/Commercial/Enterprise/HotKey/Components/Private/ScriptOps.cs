/*
 * ScriptOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using HotKey.Forms;
using HotKey.Interfaces.Private;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace HotKey.Components.Private
{
    #region Hot-Key Script Flags Enumeration
    /// <summary>
    /// Flags that control how a hot-key script is evaluated and that identify
    /// the source of the evaluation (the global hot-key event, the command, or
    /// the user interface).
    /// </summary>
    [Flags()]
    [ObjectId("ac132e42-feed-47a5-bd4b-e5a926e46f7f")]
    internal enum HotKeyScriptFlags
    {
        /// <summary>
        /// No special treatment.
        /// </summary>
        None = 0x0,               /* No special treatment.*/
        /// <summary>
        /// Invalid flag; do not use.
        /// </summary>
        Invalid = 0x1,            /* Invalid flag, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved; do not use.
        /// </summary>
        Reserved1 = 0x10,         /* Reserved, do not use. */
        /// <summary>
        /// Reserved; do not use.
        /// </summary>
        Reserved2 = 0x20,         /* Reserved, do not use. */
        /// <summary>
        /// Reserved; do not use.
        /// </summary>
        Reserved3 = 0x40,         /* Reserved, do not use. */
        /// <summary>
        /// Reserved; do not use.
        /// </summary>
        Reserved4 = 0x80,         /* Reserved, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Record the hot-key as being hit now.
        /// </summary>
        RecordAsHit = 0x100,      /* Record the hot-key as being "hit" now. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script is being evaluated by the global Windows hot-key event.
        /// </summary>
        ForHotKeyEvent = 0x200,   /* The script is being evaluated by the
                                   * global Windows hot-key event itself. */
        /// <summary>
        /// The script is being evaluated by the [hotkey] sub-command.
        /// </summary>
        ForCommand = 0x400,       /* The script is being evaluated by the
                                   * [hotkey] sub-command. */
        /// <summary>
        /// The script is being evaluated by the user interface in response to
        /// a button press or similar event.
        /// </summary>
        ForUserInterface = 0x800, /* The script is being evaluated by the
                                   * user-interface in response to a button
                                   * being pressed or some other similar
                                   * event. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The flag combination used when evaluating via the global hot-key
        /// event.
        /// </summary>
        ViaHotKeyEvent = Reserved1 | RecordAsHit | ForHotKeyEvent,
        /// <summary>
        /// The flag combination used when evaluating via the command.
        /// </summary>
        ViaCommand = Reserved2 | ForCommand,
        /// <summary>
        /// The flag combination used when evaluating via the user interface.
        /// </summary>
        ViaUserInterface = Reserved3 | ForUserInterface
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides the script-evaluation helpers for the HotKey plugin: resolving
    /// directories and trusted paths, evaluating startup, load, and hot-key
    /// scripts (optionally in isolated interpreters), managing script
    /// cancellation, extracting and formatting embedded tags, and auto-loading
    /// hot-key files.
    /// </summary>
    [ObjectId("b5ff810a-67f2-4c6f-91ac-d727af664cf9")]
    internal static class ScriptOps
    {
        #region Public Constants
        //
        // NOTE: This is the default name of the command for this plugin.
        //       This command name is used when creating the script that will
        //       re-add the configured hot-keys.
        //
        /// <summary>
        /// The script-visible name of the hot-key command.
        /// </summary>
        public static readonly string commandName =
            typeof(Commands._HotKey).Name.ToLowerInvariant().Substring(1);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the sub-command that is responsible for
        //       adding hot-keys.  This is used when creating the script that
        //       will re-add the configured hot-keys.
        //
        /// <summary>
        /// The name of the "add" sub-command (the only one allowed in a safe
        /// interpreter).
        /// </summary>
        public static readonly string addSubCommandName = "add";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        //
        // NOTE: This is the format string for the auto-load file names used
        //       by the [hotkey autoload] sub-command.
        //
        /// <summary>
        /// The format string used to build auto-load file names.
        /// </summary>
        private static readonly string AutoLoadFileNameFormat =
            "{0}-{1}{2}{3}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of names, in order of priority, used by the
        //       hot-key auto-loader when it needs to build the possible file
        //       names to be loaded.  This list is also used by the hot-key
        //       templating mechanism.
        //
        /// <summary>
        /// The candidate file-name part names searched when resolving script
        /// and template files.
        /// </summary>
        private static readonly string[] FileNamePartNames = {
            Environment.UserName, Environment.MachineName,
            Environment.UserDomainName, null
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Just use the command name for the child interpreter name.
        //
        /// <summary>
        /// The name used for temporary child interpreters.
        /// </summary>
        private static readonly string ChildInterpreterName =
            commandName;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The newline-plus-indent string used when formatting script text.
        /// </summary>
        private static readonly string NewLineWithIndent =
            Environment.NewLine + "  ";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to build an embedded tag marker.
        /// </summary>
        private const string EmbeddedTagFormat = "# <<{0}>> : ";
        /// <summary>
        /// The default tag name used for embedded script tags.
        /// </summary>
        private const string DefaultTagName = "name";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The characters treated as line terminators in script text.
        /// </summary>
        private static readonly char[] LineTerminatorChars = {
            Characters.CarriageReturn, Characters.LineFeed
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the startup script for the hot-key plugin.  If it exists, the
        //       contents of this script variable will be evaluated during
        //       initialization of the plugin instance.  If this script raises
        //       an error, the plugin will be unloaded.
        //
        /// <summary>
        /// The name of the variable that holds the hot-key startup script.
        /// </summary>
        private static readonly string StartupVariableName =
            "::" + typeof(Enterprise).FullName + "_Startup";

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        //
        // NOTE: These are the additional paths that should be searched for
        //       package index files before attempting to load the Harpy SDK.
        //
        /// <summary>
        /// The trusted search paths used when loading licensing scripts.
        /// </summary>
        private static readonly string[] TrustedLicensingPaths = new string[] {
            GetLicenseManagerDirectory(), GetLibraryManagerDirectory()
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the fully trusted script to be evaluated in the safe
        //       child interpreter that must make all the license manager
        //       component packages available.
        //
        /// <summary>
        /// The script evaluated to load trusted licensing support.
        /// </summary>
        private static readonly string TrustedLicensingScript =
            GetPackageScanCommand(TrustedLicensingPaths,
                "; package require Licensing.Core" +
                "; package require Badge.Enterprise;");
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the additional paths that should be searched for
        //       package index files for created "safe" child interpreters,
        //       i.e. so they can load the hot-key manager plugin.
        //
        /// <summary>
        /// The trusted search paths used when pre-loading scripts.
        /// </summary>
        private static readonly string[] TrustedPreLoadPaths = new string[] {
            ManagerOps.GetDirectory()
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the fully trusted script to be evaluated in "safe"
        //       child interpreters that make the [hotkey add] sub-command
        //       available to the untrusted hot-key script.
        //
        /// <summary>
        /// The script evaluated to perform trusted pre-loading.
        /// </summary>
        private static readonly string TrustedPreLoadScript =
            GetPackageScanCommand(TrustedPreLoadPaths, String.Format(
                "; set {0} true; package require HotKey.Enterprise;",
                HotKeyOps.NoThreadVariableName));

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default object flags applied to opaque objects created by this
        /// plugin.
        /// </summary>
        private static readonly ObjectFlags DefaultObjectFlags =
            ObjectFlags.Default | ObjectFlags.NoDispose |
            ObjectFlags.AddReference;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default object option type used when creating opaque objects.
        /// </summary>
        private static readonly ObjectOptionType DefaultObjectOptionType =
            ObjectOptionType.Default;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: Is this class in the middle of canceling one or more
        //       scripts?
        //
        /// <summary>
        /// The reference count tracking pending script cancellations.
        /// </summary>
        private static int pendingCancel;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Pending Cancel Helper Methods
        /// <summary>
        /// Determines whether a script cancellation is currently pending.
        /// </summary>
        /// <returns>
        /// Non-zero when a cancellation is pending; otherwise, zero.
        /// </returns>
        public static bool IsPendingCancel()
        {
            return Interlocked.CompareExchange(ref pendingCancel, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically increments the pending-cancellation count.
        /// </summary>
        /// <returns>
        /// The new pending-cancellation count.
        /// </returns>
        public static int EnterPendingCancel()
        {
            return Interlocked.Increment(ref pendingCancel);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically decrements the pending-cancellation count.
        /// </summary>
        /// <returns>
        /// The new pending-cancellation count.
        /// </returns>
        public static int ExitPendingCancel()
        {
            return Interlocked.Decrement(ref pendingCancel);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Directory Methods
        /// <summary>
        /// Gets the directory containing the hot-key scripts.
        /// </summary>
        /// <returns>
        /// The hot-key script directory.
        /// </returns>
        public static string GetDirectory()
        {
            return Path.Combine(
                ManagerOps.GetDirectory(), "Scripts");
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Gets the directory containing the license manager scripts.
        /// </summary>
        /// <returns>
        /// The license manager script directory.
        /// </returns>
        private static string GetLicenseManagerDirectory() /* Harpy */
        {
            string path = LicenseOps.GetManagerPackageDirectoryName(true);

            if (path == null)
                return null;

            string directory = ManagerOps.GetDirectory();

            if (directory == null)
                return null;

            return Path.Combine(Path.GetDirectoryName(directory), path);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory containing the library manager scripts.
        /// </summary>
        /// <returns>
        /// The library manager script directory.
        /// </returns>
        private static string GetLibraryManagerDirectory() /* Badge */
        {
            string path = LicenseOps.GetLibraryPackageDirectoryName(true);

            if (path == null)
                return null;

            string directory = ManagerOps.GetDirectory();

            if (directory == null)
                return null;

            return Path.Combine(Path.GetDirectoryName(directory), path);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Scripting Helper Methods
        /// <summary>
        /// Determines whether the specified interpreter is a safe interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to test.
        /// </param>
        /// <returns>
        /// Non-zero when the interpreter is safe; otherwise, zero.
        /// </returns>
        public static bool IsSafe(
            Interpreter interpreter /* in */
            )
        {
            return (interpreter != null) ? interpreter.IsSafe() : false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the package-scan command for the supplied paths and suffix.
        /// </summary>
        /// <param name="paths">
        /// The directories to scan for packages.
        /// </param>
        /// <param name="suffix">
        /// The optional command suffix.
        /// </param>
        /// <returns>
        /// The package-scan command script.
        /// </returns>
        private static string GetPackageScanCommand(
            IEnumerable<string> paths, /* in: OPTIONAL */
            string suffix              /* in: OPTIONAL */
            )
        {
            Result error = null; /* NOT USED */

            return String.Format(
                "{0}{1}", Utility.GetPackageScanCommand(
                null, null, paths, ref error), suffix);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the object flags to use when creating opaque objects, adjusted
        /// for isolation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the object belongs to.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when the object is created in an isolated context.
        /// </param>
        /// <returns>
        /// The object flags to use.
        /// </returns>
        private static ObjectFlags GetObjectFlags(
            Interpreter interpreter, /* in: NOT USED */
            bool isolated            /* in */
            )
        {
            ObjectFlags result = DefaultObjectFlags;

            if (isolated)
                result |= ObjectFlags.NoBinder;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Wraps a value as an opaque object in the interpreter and returns
        /// its object handle name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="objectName">
        /// The desired object name, or null to generate one.
        /// </param>
        /// <param name="value">
        /// The value to wrap.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when wrapping in an isolated context.
        /// </param>
        /// <param name="result">
        /// On success, receives the object handle name; on failure, an error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode FixupReturnValue(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            object value,            /* in */
            bool isolated,           /* in */
            ref Result result        /* out */
            )
        {
            return Utility.FixupReturnValue(
                interpreter, null, GetObjectFlags(interpreter, isolated),
                null, DefaultObjectOptionType, objectName, value, true,
                false, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the hot-key startup script, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode EvaluateStartup(
            Interpreter interpreter, /* in */
            bool resetCancel,        /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            Result value = null;
            Result error = null; /* NOT USED */

            if (interpreter.GetVariableValue(
                    VariableFlags.None, StartupVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                return Evaluate(
                    interpreter, value, false, true, resetCancel,
                    false, ref result);
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates a script for the purpose of loading hot-key definitions,
        /// logging via the supplied callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="text">
        /// The script text to evaluate.
        /// </param>
        /// <param name="loggingCallback">
        /// The callback used to log progress, if any.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode EvaluateForLoad(
            Interpreter interpreter,         /* in */
            string text,                     /* in */
            LoggingCallback loggingCallback, /* in */
            ref Result result                /* out */
            )
        {
            ReturnCode code;

            using (Interpreter childInterpreter = Interpreter.Create(null,
                    CreateFlags.NestedUse | CreateFlags.SafeAndHideUnsafe,
                    HostCreateFlags.NestedUse, ref result))
            {
                if (childInterpreter != null)
                {
                    LogOps.MaybeLogInterpreter(
                        childInterpreter, loggingCallback, "CREATE");

                    code = interpreter.AddChildInterpreter(
                        ChildInterpreterName, childInterpreter, null,
                        ref result);

                    if (code == ReturnCode.Ok)
                    {
                        try
                        {
#if LICENSING
                            if (code == ReturnCode.Ok)
                            {
                                code = childInterpreter.EvaluateTrustedScript(
                                    TrustedLicensingScript, TrustFlags.Default,
                                    ref result);
                            }
#endif

                            if (code == ReturnCode.Ok)
                            {
                                //
                                // NOTE: Evaluate the fully trusted safe child
                                //       interpreter script so it can make the
                                //       [hotkey add] sub-command available to
                                //       the untrusted hot-key script.
                                //
                                code = childInterpreter.EvaluateTrustedScript(
                                    TrustedPreLoadScript, TrustFlags.Default,
                                    ref result);
                            }

                            if (code == ReturnCode.Ok)
                            {
                                //
                                // NOTE: Evaluate the untrusted load script
                                //       in the safe child interpreter to
                                //       add the configured hot-keys.
                                //
                                code = childInterpreter.EvaluateScript(
                                    text, ref result);
                            }
                        }
                        finally
                        {
                            ReturnCode removeCode;
                            Result removeError = null;

                            removeCode = interpreter.RemoveChildInterpreter(
                                ChildInterpreterName, null, ref removeError);

                            if (removeCode == ReturnCode.Ok)
                            {
                                LogOps.MaybeLogInterpreter(
                                    childInterpreter, loggingCallback,
                                    "REMOVE");
                            }
                            else
                            {
                                LogOps.Complain(
                                    interpreter, removeCode, removeError);
                            }
                        }
                    }
                }
                else
                {
                    code = ReturnCode.Error;
                }
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the script cancellation flag for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose cancellation flag is reset.
        /// </param>
        public static void ResetCancel(
            Interpreter interpreter /* in */
            )
        {
            ResetCancel(interpreter, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the script cancellation flag for the specified interpreter,
        /// optionally ignoring a pending cancellation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose cancellation flag is reset.
        /// </param>
        /// <param name="ignorePending">
        /// Non-zero to ignore a pending cancellation.
        /// </param>
        public static void ResetCancel(
            Interpreter interpreter, /* in */
            bool ignorePending       /* in */
            )
        {
            CancelFlags cancelFlags = CancelFlags.Default;

            if (ignorePending)
                cancelFlags |= CancelFlags.IgnorePending;

            cancelFlags |= CancelFlags.UseGlobalAndLocal;
            cancelFlags |= CancelFlags.ResetGlobalAndLocal;

            ResetCancel(interpreter, cancelFlags);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the script cancellation flag for the specified interpreter
        /// using the supplied cancel flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose cancellation flag is reset.
        /// </param>
        /// <param name="cancelFlags">
        /// The flags controlling how the cancellation is reset.
        /// </param>
        private static void ResetCancel(
            Interpreter interpreter, /* in */
            CancelFlags cancelFlags  /* in */
            )
        {
            ReturnCode resetCode;
            Result resetError = null;

#if NOTIFY
            cancelFlags |= CancelFlags.Notify | CancelFlags.ForExternal;
#else
            cancelFlags |= CancelFlags.ForExternal;
#endif

            cancelFlags |= CancelFlags.TryLock; /* AVOID DEADLOCKS */

            resetCode = Engine.ResetCancel(
                interpreter, cancelFlags, ref resetError);

            if (resetCode != ReturnCode.Ok)
                LogOps.Complain(interpreter, resetCode, resetError);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cancels script evaluation in the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose evaluation is cancelled.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode CancelEvaluate(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            return interpreter.CancelAnyEvaluate(
                null, CancelFlags.UnwindAndNotify, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates a script (or script file) in the specified interpreter,
        /// optionally in an isolated interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="text">
        /// The script text or file name to evaluate.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to evaluate in an isolated interpreter.
        /// </param>
        /// <param name="needCommand">
        /// Non-zero when the hot-key command must be available.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        /// <param name="isFileName">
        /// Non-zero when text is a file name rather than a script.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode Evaluate(
            Interpreter interpreter, /* in */
            string text,             /* in */
            bool isolated,           /* in */
            bool needCommand,        /* in */
            bool resetCancel,        /* in */
            bool isFileName,         /* in */
            ref Result result        /* out */
            )
        {
            return Evaluate(
                interpreter, text, null, null, isolated, needCommand,
                resetCancel, isFileName, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates a script (or script file) in the specified interpreter,
        /// optionally exposing a named opaque object to it and optionally
        /// using an isolated interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="text">
        /// The script text or file name to evaluate.
        /// </param>
        /// <param name="objectName">
        /// The name of the opaque object to expose, if any.
        /// </param>
        /// <param name="objectValue">
        /// The value of the opaque object to expose, if any.
        /// </param>
        /// <param name="isolated">
        /// Non-zero to evaluate in an isolated interpreter.
        /// </param>
        /// <param name="needCommand">
        /// Non-zero when the hot-key command must be available.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        /// <param name="isFileName">
        /// Non-zero when text is a file name rather than a script.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode Evaluate(
            Interpreter interpreter, /* in */
            string text,             /* in */
            string objectName,       /* in */
            object objectValue,      /* in */
            bool isolated,           /* in */
            bool needCommand,        /* in */
            bool resetCancel,        /* in */
            bool isFileName,         /* in */
            ref Result result        /* out */
            )
        {
            if (isolated)
            {
                Result localResult = null; /* REUSED */

                using (Interpreter newInterpreter = Interpreter.Create(null,
                        CreateFlags.SingleUse, HostCreateFlags.SingleUse,
                        ref localResult))
                {
                    if (newInterpreter != null)
                    {
                        if ((interpreter != null) && needCommand)
                        {
                            long token = 0; /* REUSED */
                            ICommand command = null;

                            localResult = null;

                            if (interpreter.GetCommand(
                                    commandName, LookupFlags.NoWrapper,
                                    ref token, ref command,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;
                                return ReturnCode.Error;
                            }

                            token = 0;
                            localResult = null;

                            if (newInterpreter.AddCommand(
                                    command, null, ref token,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;
                                return ReturnCode.Error;
                            }
                        }
                        else
                        {
                            localResult = null;

                            if (newInterpreter.SetVariableValue(
                                    VariableFlags.None, "isolated",
                                    isolated.ToString(), null,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;
                                return ReturnCode.Error;
                            }
                        }

                        bool added = false;

                        try
                        {
                            if (objectValue != null)
                            {
                                localResult = null;

                                if (FixupReturnValue(
                                        newInterpreter, objectName,
                                        objectValue, isolated,
                                        ref localResult) == ReturnCode.Ok)
                                {
                                    added = true;
                                    objectName = localResult;
                                }
                                else
                                {
                                    result = localResult;
                                    return ReturnCode.Error;
                                }
                            }

                            if (resetCancel)
                                ResetCancel(newInterpreter);

                            ReturnCode code;

                            localResult = null;

                            if (isFileName)
                            {
                                code = newInterpreter.EvaluateFile(
                                    text, ref localResult);
                            }
                            else
                            {
                                code = newInterpreter.EvaluateScript(
                                    text, ref localResult);
                            }

                            result = localResult;
                            return code;
                        }
                        finally
                        {
                            if (added)
                            {
                                bool dispose = true;
                                ReturnCode removeCode;
                                Result removeResult = null;

                                removeCode = newInterpreter.RemoveObject(
                                    objectName, null, ref dispose,
                                    ref removeResult);

                                if (removeCode != ReturnCode.Ok)
                                {
                                    LogOps.Complain(
                                        newInterpreter, removeCode,
                                        removeResult);
                                }

                                added = false;
                            }
                        }
                    }
                    else
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }
                }
            }
            else
            {
                if (interpreter == null)
                {
                    result = "invalid interpreter";
                    return ReturnCode.Error;
                }

                bool added = false;

                try
                {
                    Result localResult = null; /* REUSED */

                    if (objectValue != null)
                    {
                        if (FixupReturnValue(
                                interpreter, objectName,
                                objectValue, isolated,
                                ref localResult) == ReturnCode.Ok)
                        {
                            added = true;
                            objectName = localResult;
                        }
                        else
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }

                    if (resetCancel)
                        ResetCancel(interpreter);

                    ReturnCode code;

                    localResult = null;

                    if (isFileName)
                    {
                        code = interpreter.EvaluateFile(
                            text, ref localResult);
                    }
                    else
                    {
                        code = interpreter.EvaluateScript(
                            text, ref localResult);
                    }

                    result = localResult;
                    return code;
                }
                finally
                {
                    if (added)
                    {
                        bool dispose = true;
                        ReturnCode removeCode;
                        Result removeResult = null;

                        removeCode = interpreter.RemoveObject(
                            objectName, null, ref dispose,
                            ref removeResult);

                        if (removeCode != ReturnCode.Ok)
                        {
                            LogOps.Complain(
                                interpreter, removeCode, removeResult);
                        }

                        added = false;
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the contents of a script file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="fileName">
        /// The file to read.
        /// </param>
        /// <param name="text">
        /// On success, receives the file contents.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ReadFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref string text,         /* out */
            ref Result error         /* out */
            )
        {
            return Engine.ReadScriptFile(
                interpreter, fileName, ref text, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied script flags contain the given
        /// flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        public static bool HasFlags(
            HotKeyScriptFlags flags,    /* in */
            HotKeyScriptFlags hasFlags, /* in */
            bool all                    /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != HotKeyScriptFlags.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Normalizes the line endings of the supplied text to those used by
        /// the engine.
        /// </summary>
        /// <param name="result">
        /// On input, the text to normalize; on output, the normalized text.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static void ForceEngineLineEndings(
            StringBuilder result /* in */
            )
        {
            if (result == null)
                return;

            //
            // NOTE: Replace all carriage-return / line-feed pairs with a
            //       single line-feed.
            //
            result.Replace(
                Environment.NewLine, Characters.LineFeed.ToString());

            //
            // NOTE: Replace all carriage-returns with line-feeds.
            //
            result.Replace(Characters.CarriageReturn, Characters.LineFeed);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the portion of the supplied text that should actually be
        /// evaluated (stripping any surrounding markers).
        /// </summary>
        /// <param name="text">
        /// The raw text.
        /// </param>
        /// <returns>
        /// The text to evaluate.
        /// </returns>
        public static string GetTextToEvaluate(
            string text /* in */
            )
        {
            if (!String.IsNullOrEmpty(text) &&
                (text.IndexOfAny(LineTerminatorChars) != Index.Invalid))
            {
                text = text.Trim();

                StringBuilder result = new StringBuilder(text);

                //
                // BUGFIX: The script engine cannot handle carriage-returns
                //         in the text to evaluate.
                //
                if (text.IndexOf(Characters.CarriageReturn) != Index.Invalid)
                    ForceEngineLineEndings(result);

                //
                // NOTE: Return the hot-key script text, modified.
                //
                return result.ToString();
            }

            return text;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied value consists only of letters.
        /// </summary>
        /// <param name="value">
        /// The value to test.
        /// </param>
        /// <returns>
        /// Non-zero when the value is letters only; otherwise, zero.
        /// </returns>
        private static bool IsLettersOnly(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return true;

            for (int index = 0; index < value.Length; index++)
                if (!char.IsLetter(value[index]))
                    return false;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats an embedded tag marker for the supplied tag name.
        /// </summary>
        /// <param name="tagName">
        /// The tag name to format.
        /// </param>
        /// <returns>
        /// The formatted tag marker.
        /// </returns>
        private static string FormatTag(
            string tagName /* in */
            )
        {
            return String.Format(EmbeddedTagFormat, (tagName != null) ?
                tagName.ToLowerInvariant() : DefaultTagName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the text associated with an embedded tag from the supplied
        /// text, starting at the given index.
        /// </summary>
        /// <param name="text">
        /// The text to search.
        /// </param>
        /// <param name="tagName">
        /// The tag name to extract.
        /// </param>
        /// <param name="startIndex">
        /// The index at which to begin searching.
        /// </param>
        /// <returns>
        /// The extracted tag text, or null when not found.
        /// </returns>
        public static string ExtractTag(
            string text,    /* in */
            string tagName, /* in */
            int startIndex  /* in */
            )
        {
            //
            // NOTE: If the input hot-key script text is null or empty,
            //       there can be no tags.  Therefore, return null.
            //
            if (String.IsNullOrEmpty(text))
                return null;

            //
            // NOTE: The hot-key tag name must consist solely of letters;
            //       otherwise, return null.
            //
            if (!IsLettersOnly(tagName))
                return null;

            //
            // NOTE: Format the hot-key tag name into the final tag text
            //       to search for.
            //
            string tagText = FormatTag(tagName);

            //
            // NOTE: If the tag text could not be formatted properly,
            //       return null.
            //
            if (String.IsNullOrEmpty(tagText))
                return null;

            //
            // NOTE: Grab the length of the hot-key script text now.
            //
            int length = text.Length;

            //
            // NOTE: If the starting index is negative, that really means
            //       start at the end minus that many characters.
            //
            if (startIndex < 0)
                startIndex = (length - 1) - Math.Abs(startIndex);

            //
            // NOTE: Verify that the starting index is within the string
            //       bounds; otherwise, return null.
            //
            if ((startIndex < 0) || (startIndex >= length))
                return null;

            //
            // HACK: Extract a portion of the hot-key script text itself
            //       to use as the display name, starting at the magic
            //       prefix constant and ending with a line-terminator.
            //
            int tagStartIndex = text.IndexOf(
                tagText, startIndex, Utility.GetSystemComparisonType(
                true));

            //
            // NOTE: Make sure that we found the magic prefix constant;
            //       otherwise, return null.
            //
            if (tagStartIndex == Index.Invalid)
                return null;

            //
            // NOTE: Advance to just past the tag text itself.
            //
            tagStartIndex += tagText.Length;

            //
            // NOTE: Make sure there is at least one character after it.
            //
            if (tagStartIndex >= length)
                return null;

            //
            // NOTE: Next, search for the next line-terminator character,
            //       starting after the magic prefix constant.
            //
            int tagStopIndex = text.IndexOfAny(LineTerminatorChars,
                tagStartIndex);

            //
            // NOTE: Make sure that we actually found a line-terminator
            //       character.
            //
            if (tagStopIndex == Index.Invalid)
                return null;

            //
            // NOTE: Extract and return the entire character range,
            //       trimmed of all external white-space.
            //
            return text.Substring(
                tagStartIndex, tagStopIndex - tagStartIndex).Trim();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the text that should be loaded as hot-key definitions from
        /// the supplied text.
        /// </summary>
        /// <param name="text">
        /// The raw text.
        /// </param>
        /// <returns>
        /// The text to load.
        /// </returns>
        public static string GetTextToLoad(
            string text /* in */
            )
        {
            if (!String.IsNullOrEmpty(text) &&
                (text.IndexOfAny(LineTerminatorChars) != Index.Invalid))
            {
                text = text.Trim();

                StringBuilder result = new StringBuilder(text);

                if (text.IndexOf(Characters.CarriageReturn) == Index.Invalid)
                {
                    result.Replace(
                        Characters.LineFeed.ToString(), Environment.NewLine);
                }

                //
                // NOTE: Replace a newline followed by two spaces with just
                //       a newline.  Effectively, this will remove one level
                //       of indentation from the hot-key script text.
                //
                result.Replace(NewLineWithIndent, Environment.NewLine);

                //
                // NOTE: Return the hot-key script text, modified.
                //
                return result.ToString();
            }

            return text;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the supplied text for saving as hot-key definitions.
        /// </summary>
        /// <param name="text">
        /// The text to save.
        /// </param>
        /// <param name="nested">
        /// Non-zero when the text is nested within another structure.
        /// </param>
        /// <returns>
        /// The text to save.
        /// </returns>
        public static string GetTextToSave(
            string text, /* in */
            bool nested  /* in */
            )
        {
            if (!String.IsNullOrEmpty(text) &&
                (text.IndexOfAny(LineTerminatorChars) != Index.Invalid))
            {
                text = text.Trim();

                StringBuilder result = new StringBuilder(text);

                if (result.Length > 0)
                {
                    if ((result[0] != Characters.CarriageReturn) &&
                        (result[0] != Characters.LineFeed))
                    {
                        result.Insert(0, Environment.NewLine);
                    }

                    //
                    // NOTE: When the nested parameter is non-zero, that means
                    //       the hot-key text is part of a larger script text;
                    //       therefore, it should be indented.
                    //
                    if (nested)
                        result.Replace(Environment.NewLine, NewLineWithIndent);

                    int length = result.Length;

                    if ((result[length - 1] != Characters.CarriageReturn) &&
                        (result[length - 1] != Characters.LineFeed))
                    {
                        result.Append(Environment.NewLine);
                    }
                }

                return result.ToString();
            }

            return text;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the candidate file-name part names searched when resolving
        /// files.
        /// </summary>
        /// <returns>
        /// The file-name part names.
        /// </returns>
        public static string[] GetFileNamePartNames()
        {
            return FileNamePartNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Collects the candidate auto-load file names for the specified
        /// sub-command, optionally merging demo and non-demo variants.
        /// </summary>
        /// <param name="subCommandName">
        /// The sub-command whose auto-load files are collected.
        /// </param>
        /// <param name="merge">
        /// Non-zero to merge variants into a single set.
        /// </param>
        /// <param name="fileNames">
        /// On output, receives the auto-load file names mapped to their demo
        /// flag.
        /// </param>
        public static void GetAutoLoadFileNames(
            string subCommandName,                 /* in */
            bool merge,                            /* in */
            ref Dictionary<string, bool> fileNames /* in, out */
            )
        {
            Dictionary<string, bool> localFileNames =
                new Dictionary<string, bool>();

            foreach (bool demo in new bool[] { true, false })
            {
                foreach (bool user in new bool[] { true, false })
                {
                    string[] partNames = GetFileNamePartNames();

                    if (partNames == null)
                        continue;

                    foreach (string partName in partNames)
                    {
                        string fileName = Path.Combine(
                            user ?
                                ManagerOps.GetUserDirectory() :
                                GetDirectory(),
                            String.Format(
                                AutoLoadFileNameFormat,
                                commandName, demo ?
                                    "demo" : subCommandName,
                            !String.IsNullOrEmpty(partName) ?
                                Characters.MinusSign + partName :
                                String.Empty, FileExtension.Script));

                        if (String.IsNullOrEmpty(fileName))
                            continue;

                        if (localFileNames.ContainsKey(fileName))
                            continue;

                        localFileNames.Add(
                            fileName, File.Exists(fileName));
                    }
                }
            }

            if (fileNames != null)
            {
                foreach (KeyValuePair<string, bool> pair in localFileNames)
                {
                    string key = pair.Key;

                    if (merge || !fileNames.ContainsKey(key))
                        fileNames[key] = pair.Value;
                }
            }
            else
            {
                fileNames = new Dictionary<string, bool>(localFileNames);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Refreshes all open hot-key viewer forms.
        /// </summary>
        public static void NotifyViewForms()
        {
            NotifyViewForms(Shell.Form.GetHotKeyManager());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Refreshes the hot-key viewer forms associated with the specified
        /// manager.
        /// </summary>
        /// <param name="hotKeyManager">
        /// The manager whose viewer forms are refreshed.
        /// </param>
        private static void NotifyViewForms(
            IHotKeyManager hotKeyManager /* in */
            )
        {
            //
            // HACK: Make sure the active hot-key viewer forms are notified
            //       about any changes to the currently configured list of
            //       hot-keys.  This will not be done if the hot-key manager
            //       has disabled its notifications -OR- there is no active
            //       hot-key manager.
            //
            if ((hotKeyManager != null) && hotKeyManager.Notify)
            {
                foreach (Form form in BaseForm.CopyOpenForms())
                {
                    IHotKeyViewer hotKeyViewer = form as IHotKeyViewer;

                    if (hotKeyViewer != null)
                        hotKeyViewer.Refresh(false);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to load each of the supplied hot-key files into the
        /// manager, accumulating per-file results and errors.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script is evaluated.
        /// </param>
        /// <param name="hotKeyManager">
        /// The manager to load the hot-keys into.
        /// </param>
        /// <param name="fileNames">
        /// The files to load, mapped to their demo flag.
        /// </param>
        /// <param name="strictCount">
        /// Non-zero to require the expected hot-key count per file.
        /// </param>
        /// <param name="strictRegister">
        /// Non-zero to require each hot-key to register.
        /// </param>
        /// <param name="results">
        /// On output, receives the per-file results.
        /// </param>
        /// <param name="errors">
        /// On output, receives the accumulated errors.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode TryAutoLoadFiles(
            Interpreter interpreter,                /* in */
            IHotKeyManager hotKeyManager,           /* in */
            Dictionary<string, bool> fileNames,     /* in */
            bool strictCount,                       /* in */
            bool strictRegister,                    /* in */
            ref Dictionary<string, Result> results, /* out */
            ref ResultList errors                   /* out */
            )
        {
            if (hotKeyManager == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid hot-key manager");
                return ReturnCode.Error;
            }

            if (fileNames == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid file names");
                return ReturnCode.Error;
            }

            bool savedNotify = hotKeyManager.Notify;
            hotKeyManager.Notify = false;

            try
            {
                int errorCount = 0;

                Dictionary<string, Result> localResults =
                    new Dictionary<string, Result>();

                foreach (KeyValuePair<string, bool> pair in fileNames)
                {
                    if (!pair.Value) /* NOTE: Does not exist? Skip. */
                        continue;

                    string fileName = pair.Key;
                    string text = null;
                    Result error = null; /* REUSED */

                    if (ReadFile(
                            interpreter, fileName, ref text,
                            ref error) != ReturnCode.Ok)
                    {
                        if (error != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(error);
                        }

                        localResults.Add(fileName, error);
                        errorCount++;

                        continue;
                    }

                    error = null;

                    if (hotKeyManager.LoadHotKeys(
                            text, strictCount, strictRegister,
                            ref error) != ReturnCode.Ok)
                    {
                        if (error != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(error);
                        }

                        localResults.Add(fileName, error);
                        errorCount++;

                        continue;
                    }

                    localResults.Add(fileName, null); /* SUCCESS */
                }

                results = localResults;

                return errorCount == 0 ?
                    ReturnCode.Ok : ReturnCode.Error;
            }
            finally
            {
                hotKeyManager.Notify = savedNotify;

                /* NO RESULT */
                NotifyViewForms();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Captures the error code and error information from the interpreter
        /// into the supplied hot-key.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key that receives the error state.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter to read the error state from.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ErrorCodeAndInfoFromInterpreter(
            IHotKey hotKey,          /* in */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (hotKey == null)
            {
                error = "invalid hot-key";
                return ReturnCode.Error;
            }

            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ReturnCode code;
            Result errorCode = null;
            Result errorInfo = null;
            ResultList errors = null;

            code = interpreter.CopyErrorInformation(
                VariableFlags.None, true, ref errorCode, ref errorInfo,
                ref errors);

            if (code == ReturnCode.Ok)
                hotKey.SetErrorCodeAndInfo(errorCode, errorInfo);
            else
                error = errors;

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the error code and error information from the supplied
        /// hot-key into the interpreter.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key whose error state is restored.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter to write the error state to.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ErrorCodeAndInfoToInterpreter(
            IHotKey hotKey,          /* in */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (hotKey == null)
            {
                error = "invalid hot-key";
                return ReturnCode.Error;
            }

            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            Result errorCode;
            Result errorInfo;

            lock (hotKey.SyncRoot)
            {
                errorCode = hotKey.ErrorCode;
                errorInfo = hotKey.ErrorInfo;
            }

            if (interpreter.SetVariableValue(
                    VariableFlags.GlobalOnly, "errorCode", errorCode,
                    null, ref error) != ReturnCode.Ok)
            {
                LogOps.Complain(
                    hotKey, interpreter, ReturnCode.Error, error);
            }

            if (interpreter.SetVariableValue(
                    VariableFlags.GlobalOnly, "errorInfo", errorInfo,
                    null, ref error) != ReturnCode.Ok)
            {
                LogOps.Complain(
                    hotKey, interpreter, ReturnCode.Error, error);
            }

            return ReturnCode.Ok;
        }
        #endregion
    }
}
