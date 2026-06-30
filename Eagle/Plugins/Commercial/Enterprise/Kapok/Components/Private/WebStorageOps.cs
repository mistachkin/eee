/*
 * WebStorageOps.cs --
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
using Kapok.Components.Shared;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides helpers for resolving the storage command and format for a
    /// variable operation and for configuring the SQLite base directory.
    /// </summary>
    [ObjectId("88bb7b7d-d6b6-40c1-9c3e-6c5fc8d20514")]
    internal static class WebStorageOps
    {
        #region Private Constants
        //
        // NOTE: The value of this environment variable, if any, will be
        //       used by the System.Data.SQLite managed assembly to help
        //       locate the native interop assembly (or core library).
        //
        /// <summary>
        /// The name of the environment variable that pre-loads the SQLite base
        /// directory.
        /// </summary>
        private static readonly string PreLoadSQLiteBaseDirectoryEnvVarName =
            "PreLoadSQLite_BaseDirectory";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Variable Storage Support Methods
        /// <summary>
        /// Gets the storage command name for the specified variable method.
        /// </summary>
        /// <param name="method">
        /// The variable method (operation).
        /// </param>
        /// <returns>
        /// The command name.
        /// </returns>
        public static string GetCommand(
            VariableMethod method /* in */
            )
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.StringListMask;

            string result = WebSettingsOps.GetGlobal(
                String.Format("{0}StorageCommand({1})",
                typeof(VariableMethod).Name, method),
                dataType);

            TracePriority priority;

            if (WebSettingsOps.ShouldTrace(
                    dataType, result, out priority))
            {
                priority |= TracePriority.FromPlugin;

                /* NO RESULT */
                Utility.ChangeBaseTracePriority(
                    ref priority, TracePriority.Medium);

                Utility.DebugTrace(String.Format(
                    "GetCommand: method = {0}, dataType = {1}, " +
                    "result = {2}", Utility.FormatWrapOrNull(method),
                    Utility.FormatWrapOrNull(dataType),
                    Utility.FormatWrapOrNull(result)),
                    typeof(WebStorageOps).Name, priority);
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the storage format name for the specified variable method.
        /// </summary>
        /// <param name="method">
        /// The variable method (operation).
        /// </param>
        /// <returns>
        /// The format name.
        /// </returns>
        public static string GetFormat(
            VariableMethod method /* in */
            )
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.StringListMask;

            string result = WebSettingsOps.GetGlobal(
                String.Format("{0}StorageFormat({1})",
                typeof(VariableMethod).Name, method),
                dataType);

            TracePriority priority;

            if (WebSettingsOps.ShouldTrace(
                    dataType, result, out priority))
            {
                priority |= TracePriority.FromPlugin;

                /* NO RESULT */
                Utility.ChangeBaseTracePriority(
                    ref priority, TracePriority.Medium);

                Utility.DebugTrace(String.Format(
                    "GetFormat: method = {0}, dataType = {1}, " +
                    "result = {2}", Utility.FormatWrapOrNull(method),
                    Utility.FormatWrapOrNull(dataType),
                    Utility.FormatWrapOrNull(result)),
                    typeof(WebStorageOps).Name, priority);
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Data.SQLite Support Methods
        /// <summary>
        /// Configures the SQLite base directory (a one-time-per-AppDomain
        /// action).
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool ConfigureSQLiteBaseDirectory()
        {
            //
            // HACK: Workaround for ASP.NET relocating our managed assembly
            //       files to its temporary directory.  This must be done
            //       because our native SQLite library is only present in our
            //       binary directory, not the temporary assembly directory
            //       that ASP.NET creates for us.  This should only be done
            //       if the environment variable in question is not already
            //       set.
            //
            bool result = false;
            AppDomain appDomain = WebGlobalState.GetAppDomain();
            string directory = null;

            if (appDomain != null)
            {
                if (!Utility.DoesEnvironmentVariableExist(
                        PreLoadSQLiteBaseDirectoryEnvVarName))
                {
                    directory = appDomain.RelativeSearchPath;

                    /* NO RESULT */
                    Utility.SetEnvironmentVariable(
                        PreLoadSQLiteBaseDirectoryEnvVarName,
                        directory, false);

                    result = true;
                }
            }

            Utility.DebugTrace(String.Format(
                "ConfigureSQLiteBaseDirectory: directory = {0}",
                Utility.FormatWrapOrNull(directory)),
                typeof(WebStorageOps).Name, TracePriority.High |
                    TracePriority.FromPlugin);

            return result;
        }
        #endregion
    }
}
