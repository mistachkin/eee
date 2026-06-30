/*
 * WebScriptOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Kapok.Components.Public;
using Kapok.Components.Shared;
using IOP = Kapok.Components.Private.InterpreterOps;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides script-evaluation helpers for the server: reading HTML/script
    /// block files, evaluating the configuration script, resolving script
    /// variable values, and configuring the library and auto-path.
    /// </summary>
    [ObjectId("26d6d51e-f0f3-4f95-97fa-bd94c05f658e")]
    internal static class WebScriptOps
    {
        #region Script Blocks Support Methods
        /// <summary>
        /// Reads the mixed HTML/script block content from a file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The file to read.
        /// </param>
        /// <param name="result">
        /// On success, receives the block content; on failure, an error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ReadBlocksFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref Result result        /* out */
            )
        {
            string text = null;

            if (Engine.ReadScriptFile(
                    interpreter, fileName, ref text,
                    ref result) == ReturnCode.Ok)
            {
                result = text;
                return ReturnCode.Ok;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Diagnostic Support Methods
        /// <summary>
        /// Copies the error code and error information from the interpreter,
        /// tracing them for the given phase.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the error state from.
        /// </param>
        /// <param name="code">
        /// The return code of the failed operation.
        /// </param>
        /// <param name="phase">
        /// The server phase that produced the error.
        /// </param>
        /// <param name="strict">
        /// Non-zero to require the error information to be present.
        /// </param>
        /// <param name="errorCode">
        /// On output, receives the error code.
        /// </param>
        /// <param name="errorInfo">
        /// On output, receives the error information.
        /// </param>
        public static void CopyErrorInformation(
            Interpreter interpreter, /* in */
            ReturnCode code,         /* in */
            ServerPhase phase,       /* in: NOT USED */
            bool strict,             /* in */
            ref Result errorCode,    /* out */
            ref Result errorInfo     /* out */
            )
        {
            if ((code == ReturnCode.Error) &&
                (interpreter != null))
            {
                /* IGNORED */
                interpreter.CopyErrorInformation(
                    VariableFlags.None, strict,
                    ref errorCode, ref errorInfo);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Configuration Support Methods
        /// <summary>
        /// Evaluates the server configuration script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the configuration.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool EvaluateConfiguration(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return false;
            }

            //
            // NOTE: The NoVariableValue flag here is used to prevent
            //       infinite (mutual) recursion between this method,
            //       its caller (GetVariableValue), and the GetGlobal
            //       method.
            //
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.Script |
                SettingDataType.NoVariableValue;

            string text = WebSettingsOps.GetGlobal(
                "ServerConfigurationScript", dataType);

            if (text == null)
                return true;

            Result result = null;

            if (interpreter.EvaluateScript(
                    text, ref result) == ReturnCode.Ok)
            {
                return true;
            }
            else
            {
                error = result;
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of a script variable for a setting.
        /// </summary>
        /// <param name="varName">
        /// The script variable name.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the value.
        /// </param>
        /// <returns>
        /// The variable value, or null when not found.
        /// </returns>
        public static string GetVariableValue(
            string varName,          /* in */
            SettingDataType dataType /* in */
            )
        {
            if (WebSettingsOps.HasFlags(
                    dataType, SettingDataType.TraceError, true))
            {
                return GetVariableValueOrTrace(varName, dataType);
            }
            else
            {
                return GetVariableValueOrIgnore(varName, dataType);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of a script variable, ignoring any error.
        /// </summary>
        /// <param name="varName">
        /// The script variable name.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the value.
        /// </param>
        /// <returns>
        /// The variable value, or null when not found.
        /// </returns>
        private static string GetVariableValueOrIgnore(
            string varName,          /* in */
            SettingDataType dataType /* in */
            )
        {
            TracePriority priority = TracePriority.Lowest; /* EXEMPT */
            Result error = null; /* NOT USED */

            return GetVariableValue(
                varName, dataType, ref priority, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of a script variable, tracing any error.
        /// </summary>
        /// <param name="varName">
        /// The script variable name.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the value.
        /// </param>
        /// <returns>
        /// The variable value, or null when not found.
        /// </returns>
        private static string GetVariableValueOrTrace( /* NOT USED */
            string varName,          /* in */
            SettingDataType dataType /* in */
            )
        {
            string value;
            TracePriority priority = TracePriority.Lowest; /* EXEMPT */
            Result error = null;

            value = GetVariableValue(
                varName, dataType, ref priority, ref error);

            if (value != null)
                return value;

            priority |= TracePriority.FromPlugin;

            Utility.DebugTrace(String.Format(
                "GetVariableValueOrTrace: " +
                "varName = {0}, error = {1}",
                Utility.FormatWrapOrNull(varName),
                Utility.FormatWrapOrNull(error)),
                typeof(WebScriptOps).Name, priority);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of a script variable, tracing at the given priority
        /// and reporting any error.
        /// </summary>
        /// <param name="varName">
        /// The script variable name.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the value.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when tracing.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The variable value, or null when not found.
        /// </returns>
        private static string GetVariableValue(
            string varName,             /* in */
            SettingDataType dataType,   /* in */
            ref TracePriority priority, /* in, out */
            ref Result error            /* out */
            )
        {
            Result localError; /* REUSED */
            Interpreter interpreter;
            bool created;

            localError = null;

            interpreter = IOP.GetOrCreate(
                ArgsOps.DoUseAutomatic(),
                InterpreterPhase.Configuration,
                true, false, null, out created,
                ref localError);

            if (interpreter == null)
            {
                /* NO RESULT */
                Utility.ChangeBaseTracePriority(ref priority,
                    TracePriority.Highest); /* EXEMPT */

                error = localError;

                return null;
            }

            //
            // HACK: After reading the comments associated with
            //       the GetOrCreateImmutable method, you might
            //       be wondering something like "How can this
            //       code safely evaluate a script?".  Well, we
            //       can because server configuration script(s)
            //       are supposed to be 100% idempotent.  Also,
            //       it will only be evaluated once per created
            //       interpreter (hopefully).
            //
            localError = null;

            if (created && !EvaluateConfiguration(interpreter,
                    ref localError))
            {
                /* NO RESULT */
                Utility.ChangeBaseTracePriority(ref priority,
                    TracePriority.Highest); /* EXEMPT */

                error = localError;

                return null;
            }

            Result value = null;

            localError = null;

            if (interpreter.GetVariableValue(varName,
                    ref value, ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return null;
            }

            return value;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Script Environment Support Methods
        /// <summary>
        /// Disables the package root path (a one-time-per-AppDomain action).
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool DisablePackageRootPath()
        {
            //
            // HACK: Prevent Eagle from adding the wrong "lib" directory
            //       into the interpreter auto-path.  When deployed with
            //       the .NET Framework, the binary ends up in the "bin"
            //       sub-directory within the (Kapok) source directory.
            //       Since a "lib" sub-directory also resides within the
            //       (Kapok) source directory, the calculated file name
            //       for the plugin binary would be wrong.  Instead, the
            //       package index file for the (Kapok) plugin must only
            //       be picked up via the "bin" sub-directory within the
            //       (Kapok) source directory.
            //
            return Utility.SetEnvironmentVariable(
                "No_packageRootPath", 1.ToString());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the script library path (a one-time-per-AppDomain
        /// action).
        /// </summary>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when the library is not found.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool ConfigureLibrary(
            bool errorOnNotFound /* in */
            )
        {
            //
            // HACK: Needed so that Eagle can locate the external script
            //       library when running in an ASP.NET web application.
            //
            bool result = false;

            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.DirectoryName;

            string libraryPath = WebSettingsOps.GetGlobal(
                EnvVars.EagleLibrary, dataType);

            if (!String.IsNullOrEmpty(libraryPath))
            {
                /* NO RESULT */
                Utility.SetLibraryPath(libraryPath, true);

                result = true;
            }
            else
            {
                //
                // HACK: Attempt to automatically detect the
                //       location of the core script library;
                //       however, if we cannot find it, that
                //       is not necessarily an error since it
                //       should be available embedded within
                //       the core library assembly itself.
                //
                result = Utility.DetectLibraryPath(
                    WebGlobalState.GetAssembly(), null,
                    DetectFlags.Default);

                if (!result && !errorOnNotFound)
                    result = true;
            }

            Utility.DebugTrace(String.Format(
                "ConfigureLibrary: libraryPath = {0}, result = {1}",
                Utility.FormatWrapOrNull(libraryPath), result),
                typeof(WebScriptOps).Name, TracePriority.High |
                    TracePriority.FromPlugin);

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the auto-path (a one-time-per-AppDomain action).
        /// </summary>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when a path is not found.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool ConfigureAutoPath(
            bool errorOnNotFound /* in */
            )
        {
            //
            // HACK: This is needed so that the Eagle core library will be
            //       able to locate Harpy and/or other enterprise packages,
            //       especially from child processes and/or scripts.
            //
            // HACK: The returned value is NOT a path, it is a list of paths;
            //       so, it cannot set the native path flag even though that
            //       would be convenient.  Instead, the native path handling
            //       will be performed from within the RefreshAutoPathList
            //       method, called below.
            //
            // BUGBUG: Prevent the first time through from being "sticky" by
            //         forbidding use of the environment for reading, since
            //         the environment is written by this method?
            //
            bool result = false;

            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.DirectoryName |
                SettingDataType.List;

            string libPath = WebSettingsOps.GetGlobal(
                EnvVars.EagleLibPath, dataType);

            if (!String.IsNullOrEmpty(libPath))
            {
                /* NO RESULT */
                Utility.SetEnvironmentVariable(
                    EnvVars.EagleLibPath, libPath, false);

                /* NO RESULT */
                Utility.RefreshAutoPathList(true);

                result = true;
            }
            else if (!errorOnNotFound)
            {
                result = true;
            }

            Utility.DebugTrace(String.Format(
                "ConfigureAutoPath: libPath = {0}, result = {1}",
                Utility.FormatWrapOrNull(libPath), result),
                typeof(WebScriptOps).Name, TracePriority.High |
                    TracePriority.FromPlugin);

            return result;
        }
        #endregion
    }
}
