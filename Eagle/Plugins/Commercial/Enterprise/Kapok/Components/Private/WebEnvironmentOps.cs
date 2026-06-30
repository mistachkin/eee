/*
 * WebEnvironmentOps.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Public;

using EnvironmentPair = Eagle._Interfaces.Public.IAnyPair<
    string, Kapok.Components.Shared.SettingDataType>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides helpers for saving, setting up, and restoring the per-page
    /// environment variables around request processing.
    /// </summary>
    [ObjectId("acc6ff4d-6a74-49f4-913c-522447e5f9db")]
    internal static class WebEnvironmentOps
    {
        /// <summary>
        /// Saves the current values of the page's environment variables into
        /// the supplied caller data.
        /// </summary>
        /// <param name="pageData">
        /// The page data describing the environment variables.
        /// </param>
        /// <param name="environmentClientData">
        /// The caller data that receives the saved values.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool SavePage(
            IScriptPageData pageData,             /* in */
            ref IClientData environmentClientData /* out */
            )
        {
            if (pageData == null)
                return false;

            IEnumerable<string> names = EnvironmentOps.GetVariableNames(
                pageData.Environment);

            if (names == null)
                return false;

            return Utility.SaveEnvironmentVariables(
                names, ref environmentClientData);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the page's environment variables from its configuration
        /// settings.
        /// </summary>
        /// <param name="pageData">
        /// The page data to populate.
        /// </param>
        /// <param name="pageName">
        /// The page name used to look up settings.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the settings.
        /// </param>
        public static void SetupPage(
            IScriptPageData pageData, /* in */
            string pageName,          /* in */
            SettingDataType dataType  /* in */
            )
        {
            if (pageData == null)
                return;

            IEnumerable<EnvironmentPair> environment = pageData.Environment;

            if (environment == null)
                return;

            foreach (EnvironmentPair anyPair in environment)
            {
                if (anyPair == null)
                    continue;

                string envVarName = anyPair.X;

                if (String.IsNullOrEmpty(envVarName))
                    continue;

                string settingName = EnvironmentOps.FormatName(
                    envVarName);

                if (String.IsNullOrEmpty(settingName))
                    continue;

                string envVarValue = WebSettingsOps.GetPage(
                    pageName, settingName, dataType | anyPair.Y);

                if (String.IsNullOrEmpty(envVarValue))
                {
                    envVarValue = WebSettingsOps.GetGlobal(
                        settingName, dataType | anyPair.Y);

                    if (String.IsNullOrEmpty(envVarValue))
                        continue;
                }

                /* IGNORED */
                Utility.SetEnvironmentVariable(envVarName, envVarValue);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the page's environment variables from the previously saved
        /// caller data.
        /// </summary>
        /// <param name="pageData">
        /// The page data describing the environment variables.
        /// </param>
        /// <param name="environmentClientData">
        /// The caller data holding the saved values.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool RestorePage(
            IScriptPageData pageData,         /* in */
            IClientData environmentClientData /* in, out */
            )
        {
            if (pageData == null)
                return false;

            IEnumerable<string> names = EnvironmentOps.GetVariableNames(
                pageData.Environment);

            if (names == null)
                return false;

            return Utility.RestoreEnvironmentVariables(
                names, environmentClientData);
        }
    }
}
