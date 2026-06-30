/*
 * TemplateOps.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using HotKey.Interfaces.Private;
using Eagle._Interfaces.Public;

namespace HotKey.Components.Private
{
    #region Hot-Key Template Types Enumeration
    /// <summary>
    /// Identifies the kind of template used to generate a hot-key script, for
    /// example a custom script, a script file, or one that launches an
    /// executable, application, program, file, folder, URI, or bookmark.
    /// </summary>
    [Flags()]
    [ObjectId("dc19d14b-74eb-4100-9e7c-e06233cc3859")]
    internal enum HotKeyTemplateType
    {
        /// <summary>
        /// The hot-key script type is not set.
        /// </summary>
        None = 0x0,         /* The hot-key script type is not set. */

        /// <summary>
        /// Invalid hot-key template type; do not use.
        /// </summary>
        Invalid = 0x1,      /* Invalid hot-key template type, do not use. */

        /// <summary>
        /// The script type is not known.
        /// </summary>
        Unknown = 0x2,      /* The script type is not known. */

        /// <summary>
        /// The script is a custom script.
        /// </summary>
        Script = 0x4,       /* The script is a custom script. */

        /// <summary>
        /// The script should evaluate a script file.
        /// </summary>
        ScriptFile = 0x8,   /* The script should evaluate a script file. */

        /// <summary>
        /// Inserts standard metadata for the hot-key.
        /// </summary>
        Metadata = 0x10,    /* Inserts standard metadata for the hot-key. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script should launch an executable file.
        /// </summary>
        Executable = 0x20,  /* The script should launch an executable file. */

        /// <summary>
        /// The script should launch an application using the Explorer shell.
        /// </summary>
        Application = 0x40, /* The script should launch an application using
                             * the Explorer shell. */

        /// <summary>
        /// The script should launch a program file.
        /// </summary>
        Program = 0x80,     /* The script should launch a program file. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script should launch a file or document using the Explorer
        /// shell.
        /// </summary>
        File = 0x100,       /* The script should launch a file or document
                             * using the Explorer shell. */

        /// <summary>
        /// The script should launch a folder using the Explorer shell.
        /// </summary>
        Folder = 0x200,     /* The script should launch a folder using the
                             * Explorer shell. */

        /// <summary>
        /// The script should launch a URI in the default web browser.
        /// </summary>
        URI = 0x400,        /* The script should launch a URI in the default
                             * web browser. */

        /// <summary>
        /// The script should launch a bookmark from the default web browser.
        /// </summary>
        Bookmark = 0x800,   /* The script should launch a bookmark from the
                             * default web browser. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined0 = 0x1000,   /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined1 = 0x2000,   /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined2 = 0x4000,   /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined3 = 0x8000,   /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined4 = 0x10000,  /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined5 = 0x20000,  /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined6 = 0x40000,  /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined7 = 0x80000,  /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined8 = 0x100000, /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        UserDefined9 = 0x200000, /* Reserved for application/user use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The mask of all user-defined template types.
        /// </summary>
        UserDefinedMask = UserDefined0 | UserDefined1 | UserDefined2 |
                          UserDefined3 | UserDefined4 | UserDefined5 |
                          UserDefined6 | UserDefined7 | UserDefined8 |
                          UserDefined9,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The mask of the standard launch template types (executable,
        /// application, and program).
        /// </summary>
        StandardMask = Executable | Application | Program,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        Reserved = unchecked((int)0x80000000), /* Reserved for future use. */
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides helpers for the hot-key template scripts: locating the
    /// template directory and per-type template files, registering the
    /// template packages, cancelling running template scripts, locking the
    /// manager window, and evaluating a template script (synchronously or
    /// asynchronously).
    /// </summary>
    [ObjectId("624a9e98-20cf-4ce0-b717-32cc3095eaff")]
    internal static class TemplateOps
    {
        #region Private Constants
        /// <summary>
        /// The format string used to build a template file name from the
        /// command name, template type, optional part name, and extension.
        /// </summary>
        private const string FileNameFormat = "{0}-template-{1}{2}{3}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the script to be evaluated in order to provide the
        //       hot-key template packages to the interpreter.
        //
        /// <summary>
        /// The script evaluated to make the hot-key template packages
        /// available to the interpreter.
        /// </summary>
        private static readonly string PackageScript = GetPackageScanCommand();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the script to be evaluated in order to cancel all
        //       running hot-key template scripts.
        //
        /// <summary>
        /// The script evaluated to cancel all running hot-key template
        /// scripts.
        /// </summary>
        private static readonly string CancelScript =
            "package require HotKey.Template.Common; cancelTemplateThreads";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the script to be evaluated in order to prevent the
        //       hot-key manager window from being closed.
        //
        /// <summary>
        /// The script (format string) evaluated to prevent the hot-key
        /// manager window from being closed.
        /// </summary>
        private static readonly string NoCloseScript =
            "package require HotKey.Template.Common; preventWindowClose {0}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Builds the package-scan command used to locate and register the
        /// hot-key template packages in the template directory.
        /// </summary>
        /// <returns>
        /// The package-scan command script.
        /// </returns>
        private static string GetPackageScanCommand()
        {
            Result error = null; /* NOT USED */

            return Utility.GetPackageScanCommand(
                null, null, new string[] { GetDirectory() }, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied template-type flags contain the
        /// given flags.
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
        private static bool HasFlags(
            HotKeyTemplateType flags,    /* in */
            HotKeyTemplateType hasFlags, /* in */
            bool all                     /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != HotKeyTemplateType.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the lowercase name used in template file names for the
        /// specified template type (mapping the standard mask to "standard").
        /// </summary>
        /// <param name="templateType">
        /// The template type whose name is requested.
        /// </param>
        /// <returns>
        /// The template type name.
        /// </returns>
        private static string GetName(
            HotKeyTemplateType templateType /* in */
            )
        {
            switch (templateType)
            {
                case HotKeyTemplateType.StandardMask:
                    {
                        //
                        // HACK: Remove the trailing "Mask" portion of
                        //       this "well-known" template type value,
                        //       which is a mask of other values -AND-
                        //       will be handled by the target template
                        //       script file itself.
                        //
                        return "standard";
                    }
                default:
                    {
                        return templateType.ToString().ToLowerInvariant();
                    }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Gets the hot-key template directory (the "Templates" subdirectory
        /// of the manager directory).
        /// </summary>
        /// <returns>
        /// The template directory path.
        /// </returns>
        public static string GetDirectory()
        {
            return Path.Combine(
                ManagerOps.GetDirectory(), "Templates");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Registers the hot-key template packages with the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to register the packages with.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode AddPackages(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            return ScriptOps.Evaluate(
                interpreter, PackageScript, false, false, true,
                false, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cancels all running hot-key template scripts in the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose template scripts are cancelled.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Cancel(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            return ScriptOps.Evaluate(
                interpreter, CancelScript, false, true, true,
                false, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Locks the hot-key manager window (identified by its handle) so it
        /// cannot be closed, temporarily wrapping the handle as an opaque
        /// object for the locking script and removing it afterward.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the lock script.
        /// </param>
        /// <param name="hWnd">
        /// The window handle of the manager window to lock.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode LockWindow(
            Interpreter interpreter, /* in */
            IntPtr hWnd,             /* in */
            ref Result result        /* out */
            )
        {
            bool added = false;
            Result localResult = null; /* opaque object handle name */

            try
            {
                if (ScriptOps.FixupReturnValue(
                        interpreter, null, hWnd, false,
                        ref localResult) == ReturnCode.Ok)
                {
                    added = true;

                    return ScriptOps.Evaluate(
                        interpreter, String.Format(
                        NoCloseScript, localResult),
                        false, false, true, false,
                        ref result);
                }
                else
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            finally
            {
                if (added)
                {
                    bool dispose = true;
                    ReturnCode removeCode;
                    Result removeResult = null;

                    removeCode = interpreter.RemoveObject(
                        localResult, null, ref dispose,
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

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the script file name for a template type.  When user
        /// templates apply, the user, template, script, and manager
        /// directories are searched (across the configured file-name parts);
        /// otherwise the template-directory file name is returned.  In strict
        /// mode, a missing file yields null.
        /// </summary>
        /// <param name="templateType">
        /// The template type whose file name is requested.
        /// </param>
        /// <param name="user">
        /// Non-zero to include the user template locations in the search.
        /// </param>
        /// <param name="strict">
        /// Non-zero to require the file to exist (returning null otherwise).
        /// </param>
        /// <returns>
        /// The template file name, or null when none applies or (in strict
        /// mode) the file does not exist.
        /// </returns>
        public static string GetFileName(
            HotKeyTemplateType templateType, /* in */
            bool user,                       /* in */
            bool strict                      /* in */
            )
        {
            if (templateType != HotKeyTemplateType.None)
            {
                //
                // TODO: Move this file search / loading logic into its own
                //       method and parameterize it.  Perhaps take advantage
                //       of the Utility.SearchForPath method, which is used
                //       by the core library to search for script files?
                //
                string templateDirectory = GetDirectory();
                string commandName = ScriptOps.commandName;

                if (user || HasFlags(templateType,
                        HotKeyTemplateType.UserDefinedMask, false))
                {
                    foreach (string directory in new string[] {
                            ManagerOps.GetUserDirectory(),
                            templateDirectory,
                            ScriptOps.GetDirectory(),
                            ManagerOps.GetDirectory()
                        })
                    {
                        if (directory == null)
                            continue;

                        string[] partNames =
                            ScriptOps.GetFileNamePartNames();

                        if (partNames == null)
                            continue;

                        foreach (string partName in partNames)
                        {
                            string partFileName = Path.Combine(
                                directory, String.Format(
                                FileNameFormat, commandName,
                                GetName(templateType),
                                !String.IsNullOrEmpty(partName) ?
                                    Characters.MinusSign + partName :
                                    String.Empty, FileExtension.Script));

                            if (String.IsNullOrEmpty(partFileName)) continue;
                            if (!File.Exists(partFileName)) continue;

                            return partFileName;
                        }
                    }

                    //
                    // NOTE: In practice, strict mode should not be used by
                    //       callers that interact with the user, directly
                    //       or indirectly (i.e. it should be used for purely
                    //       automated scripts only).
                    //
                    if (strict)
                        return null;
                }

                //
                // NOTE: In relaxed mode, just return the file name as if it
                //       were present in the hot-key template directory;
                //       otherwise, return null if the required template file
                //       does not exist in the template directory.
                //
                string fileName = Path.Combine(
                    templateDirectory, String.Format(FileNameFormat,
                    commandName, GetName(templateType), String.Empty,
                    FileExtension.Script));

                if (!strict || File.Exists(fileName))
                    return fileName;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the template script for the specified template type,
        /// resolving its file name first.  Evaluation may be synchronous or
        /// asynchronous (with the result delivered to the supplied editor
        /// result via a completion callback, bridged across application
        /// domains when the manager is isolated).  In relaxed (non-strict)
        /// mode a missing template is a no-op.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the template script.
        /// </param>
        /// <param name="hotKeyEditorResult">
        /// The editor result that receives the asynchronous completion.
        /// </param>
        /// <param name="templateType">
        /// The template type to evaluate.
        /// </param>
        /// <param name="user">
        /// Non-zero to include the user template locations when resolving the
        /// file.
        /// </param>
        /// <param name="interactive">
        /// Non-zero when invoked interactively (relaxing strict file
        /// resolution).
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to evaluate the template asynchronously.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag before evaluation.
        /// </param>
        /// <param name="append">
        /// Non-zero to append the result to the editor; zero to replace it.
        /// </param>
        /// <param name="strict">
        /// Non-zero to treat a missing or non-existent template file as an
        /// error.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Evaluate(
            Interpreter interpreter,                /* in */
            IHotKeyEditorResult hotKeyEditorResult, /* in */
            HotKeyTemplateType templateType,        /* in */
            bool user,                              /* in */
            bool interactive,                       /* in */
            bool asynchronous,                      /* in */
            bool resetCancel,                       /* in */
            bool append,                            /* in */
            bool strict,                            /* in */
            ref Result result                       /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (hotKeyEditorResult == null)
            {
                result = "invalid hot-key editor result";
                return ReturnCode.Error;
            }

            if (templateType == HotKeyTemplateType.Invalid)
            {
                result = "invalid hot-key template type";
                return ReturnCode.Error;
            }

            if (templateType != HotKeyTemplateType.None)
            {
                string fileName = GetFileName(
                    templateType, user, !interactive);

                if (String.IsNullOrEmpty(fileName))
                {
                    if (strict)
                    {
                        result = String.Format(
                            "invalid hot-key template type {0} script file name",
                            Utility.FormatWrapOrNull(templateType));

                        return ReturnCode.Error;
                    }

                    return ReturnCode.Ok;
                }

                if (!File.Exists(fileName))
                {
                    if (strict)
                    {
                        result = String.Format(
                            "cannot evaluate hot-key template type {0} " +
                            "script file {1}, it does not exist",
                            Utility.FormatWrapOrNull(templateType),
                            Utility.FormatWrapOrNull(fileName));

                        return ReturnCode.Error;
                    }

                    return ReturnCode.Ok;
                }

                if (asynchronous)
                {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                    //
                    // NOTE: Yes, this is somewhat more complex than the normal
                    //       case of receiving an asynchronous script completion
                    //       callback.  However, it does work.
                    //
                    if (Shell.Form.IsHotKeyIsolated(interpreter))
                    {
                        IAsynchronousCallback hotKeyEditor = new HotKeyEditor(
                            hotKeyEditorResult);

                        AsynchronousCallbackBridge asynchronousCallbackBridge =
                            AsynchronousCallbackBridge.Create(hotKeyEditor, ref result);

                        if (asynchronousCallbackBridge == null)
                            return ReturnCode.Error;

                        if (resetCancel)
                            ScriptOps.ResetCancel(interpreter);

                        return interpreter.EvaluateFile(
                            fileName, asynchronousCallbackBridge.AsynchronousCallback,
                            new ClientData(append), ref result);
                    }
                    else
#endif
                    {
                        if (resetCancel)
                            ScriptOps.ResetCancel(interpreter);

                        return interpreter.EvaluateFile(fileName,
                            hotKeyEditorResult.TemplateAsynchronousCallback,
                            new ClientData(append), ref result);
                    }
                }
                else
                {
                    if (resetCancel)
                        ScriptOps.ResetCancel(interpreter);

                    return interpreter.EvaluateFile(fileName, ref result);
                }
            }

            return ReturnCode.Ok;
        }
        #endregion
    }
}
