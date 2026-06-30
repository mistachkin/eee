/*
 * WebVerifyOps.cs --
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
using Eagle._Containers.Public;
using Kapok.Components.Shared;
using IOP = Kapok.Components.Private.InterpreterOps;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides verification helpers that validate a candidate setting value
    /// against the requested data type (script, path, list, or scalar).
    /// </summary>
    [ObjectId("11152b57-9178-4e29-b210-fb7e20742c4a")]
    internal static class WebVerifyOps
    {
        /// <summary>
        /// Verifies that the value is acceptable as a script for the data
        /// type.
        /// </summary>
        /// <param name="value">
        /// The candidate value.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags to verify against.
        /// </param>
        /// <returns>
        /// Non-zero when the value is valid; otherwise, zero.
        /// </returns>
        private static bool ScriptValue(
            ref string value,        /* in, out */
            SettingDataType dataType /* in */
            )
        {
            if (!NonListValue(ref value, dataType))
                return false;

            Interpreter interpreter;
            Result error; /* REUSED */
            bool notReady = false;

            error = null;

            interpreter = IOP.GetOrCreate(
                ArgsOps.DoUseAutomatic(),
                InterpreterPhase.Configuration,
                true, false, null, ref error);

            if (interpreter == null)
                goto error;

            error = null;

            if (Parser.IsComplete(
                    interpreter, null, Parser.StartLine,
                    value, 0, Length.Invalid, ref notReady,
                    ref error))
            {
                return true;
            }

        error:

            TracePriority priority;

            if (WebSettingsOps.ShouldTrace(
                    dataType, value, out priority))
            {
                priority |= TracePriority.FromPlugin;

                /* NO RESULT */
                Utility.ChangeBaseTracePriority(
                    ref priority, TracePriority.High);

                Utility.DebugTrace(String.Format(
                    "ScriptValue: notReady = {0}, error = {1}",
                    notReady, Utility.FormatWrapOrNull(error)),
                    typeof(WebVerifyOps).Name, priority);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the value is acceptable as a non-path scalar.
        /// </summary>
        /// <param name="value">
        /// The candidate value.
        /// </param>
        /// <param name="allowEmpty">
        /// Non-zero to allow a null or empty value.
        /// </param>
        /// <returns>
        /// Non-zero when the value is valid; otherwise, zero.
        /// </returns>
        private static bool NonPathValue(
            string value,   /* in */
            bool allowEmpty /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return allowEmpty;

            if (WebTokenOps.DoesContain(value))
            {
                //
                // NOTE: This is a non-path value; however, it still
                //       appears to contain tokens -OR- it is totally
                //       missing.  That is taken to mean that we need
                //       to keep searching.
                //
                return false;
            }

            //
            // NOTE: This is a non-path value and it contains no (more)
            //       tokens to be replaced.  Therefore, succeed now.
            //
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the value is acceptable as a file or directory path.
        /// </summary>
        /// <param name="value">
        /// The candidate path.
        /// </param>
        /// <param name="allowEmpty">
        /// Non-zero to allow a null or empty value.
        /// </param>
        /// <param name="noExists">
        /// Non-zero to allow a path that does not exist.
        /// </param>
        /// <param name="isFile">
        /// Non-zero when the path must be a file.
        /// </param>
        /// <param name="isDirectory">
        /// Non-zero when the path must be a directory.
        /// </param>
        /// <param name="createPath">
        /// Non-zero to create the path when it does not exist.
        /// </param>
        /// <returns>
        /// Non-zero when the value is valid; otherwise, zero.
        /// </returns>
        private static bool PathValue(
            ref string value, /* in, out */
            bool allowEmpty,  /* in */
            bool noExists,    /* in */
            bool isFile,      /* in */
            bool isDirectory, /* in */
            bool createPath   /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return allowEmpty;

            if (WebTokenOps.DoesContain(value))
            {
                //
                // NOTE: This is a non-path value; however, it still
                //       appears to contain tokens -OR- it is totally
                //       missing.  That is taken to mean that we need
                //       to keep searching.
                //
                return false;
            }

            //
            // HACK: Mutate the original (path) value to use native
            //       directory separators.
            //
            value = Utility.TranslatePath(
                value, PathTranslationType.Native);

            if (String.IsNullOrEmpty(value))
                return allowEmpty;

            if (noExists)
            {
                //
                // HACK: Allow files and directories to be created
                //       dynamically if a special flag is set.
                //
                if (createPath)
                {
                    if (isFile && !File.Exists(value))
                    {
                        /* IGNORED */
                        File.CreateText(value); /* throw */
                    }

                    if (isDirectory && !Directory.Exists(value))
                    {
                        /* IGNORED */
                        Directory.CreateDirectory(value); /* throw */
                    }
                }

                return true;
            }
            else if (isDirectory)
            {
                return Directory.Exists(value);
            }
            else
            {
                return File.Exists(value);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the value is acceptable as a non-list value for the
        /// data type.
        /// </summary>
        /// <param name="value">
        /// The candidate value.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags to verify against.
        /// </param>
        /// <returns>
        /// Non-zero when the value is valid; otherwise, zero.
        /// </returns>
        private static bool NonListValue(
            ref string value,        /* in, out */
            SettingDataType dataType /* in */
            )
        {
            dataType &= ~SettingDataType.List;

            bool isPath = WebSettingsOps.HasFlags(
                dataType, SettingDataType.PathMask, false);

            bool allowEmpty = WebSettingsOps.HasFlags(
                dataType, SettingDataType.AllowEmpty, true);

            if (isPath)
            {
                bool noExists = WebSettingsOps.HasFlags(
                    dataType, SettingDataType.NoExists, true);

                bool isFile = WebSettingsOps.HasFlags(
                    dataType, SettingDataType.FileName, false);

                bool isDirectory = WebSettingsOps.HasFlags(
                    dataType, SettingDataType.DirectoryName, false);

                bool createPath = WebSettingsOps.HasFlags(
                    dataType, SettingDataType.CreatePath, false);

                return PathValue(
                    ref value, allowEmpty, noExists, isFile,
                    isDirectory, createPath);
            }
            else
            {
                return NonPathValue(value, allowEmpty);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the value is acceptable for the data type,
        /// dispatching to the appropriate type-specific check.
        /// </summary>
        /// <param name="value">
        /// The candidate value.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags to verify against.
        /// </param>
        /// <returns>
        /// Non-zero when the value is valid; otherwise, zero.
        /// </returns>
        public static bool AnyValue(
            ref string value,        /* in, out */
            SettingDataType dataType /* in */
            )
        {
            bool isList = WebSettingsOps.HasFlags(
                dataType, SettingDataType.List, true);

            bool isScript = WebSettingsOps.HasFlags(
                dataType, SettingDataType.Script, true);

            if (isList)
            {
                if (!String.IsNullOrEmpty(value))
                {
                    StringList list = null;
                    Result error = null;

                    if (Parser.SplitList(
                            null, value, 0, Length.Invalid, false,
                            ref list, ref error) == ReturnCode.Ok)
                    {
                        int count = list.Count;

                        for (int index = 0; index < count; index++)
                        {
                            string element = list[index];

                            if (isScript)
                            {
                                if (!ScriptValue(
                                        ref element, dataType))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                if (!NonListValue(
                                        ref element, dataType))
                                {
                                    return false;
                                }
                            }

                            list[index] = element;
                        }

                        value = list.ToString();
                        return true;
                    }

                    //
                    // HACK: Technically, this method should not really
                    //       emit anything; however, this is a fairly
                    //       serious unexpected error (i.e. a malformed
                    //       Tcl list value in the server configuration
                    //       file).  This call may be removed at some
                    //       point in the future.
                    //
                    Utility.Complain(null, ReturnCode.Error, error);
                }

                return false;
            }
            else if (isScript)
            {
                return ScriptValue(ref value, dataType);
            }
            else
            {
                return NonListValue(ref value, dataType);
            }
        }
    }
}
