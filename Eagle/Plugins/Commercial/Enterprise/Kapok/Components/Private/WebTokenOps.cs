/*
 * WebTokenOps.cs --
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
using System.Reflection;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Kapok.Components.Public;
using Kapok.Components.Shared;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides expansion of runtime tokens (of the form %TOKEN%) embedded in
    /// setting values, replacing them with values such as the assembly
    /// directory, identifier, and configuration name.
    /// </summary>
    [ObjectId("36b95e35-0bcd-472a-a13b-64cd4cb68d85")]
    internal static class WebTokenOps
    {
        #region Private Constants
        //
        // NOTE: This regular expression will be used to check if a setting
        //       value contains any replacement tokens (i.e. an environment
        //       variable reference, et al).
        //
        /// <summary>
        /// The compiled regular expression that matches %TOKEN% references.
        /// </summary>
        private static readonly Regex TokenRegEx = new Regex(
            "%[A-Z_][0-9A-Z_]*%", RegexOptions.IgnoreCase |
            RegexOptions.Compiled);

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This value will be replaced with the fully qualified native
        //       name of the directory containing the Kapok managed assembly.
        //
        /// <summary>
        /// The token replaced with the native directory of the Kapok assembly.
        /// </summary>
        private static readonly string BinDirEnvVarToken =
            Characters.PercentSign + "binDir" + Characters.PercentSign;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This value will be replaced with an identifier related to the
        //       current process, thread, context, etc.
        //
        /// <summary>
        /// The token replaced with the assembly identifier.
        /// </summary>
        private static readonly string IdEnvVarToken =
            Characters.PercentSign + "id" + Characters.PercentSign;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This value will be replaced with the current fully qualified
        //       build configuration (which may end with the assembly text and
        //       a configuration suffix) within the application setting values.
        //
        /// <summary>
        /// The token replaced with the assembly configuration name.
        /// </summary>
        private static readonly string ConfigurationEnvVarToken =
            Characters.PercentSign + EnvVars.Configuration +
            Characters.PercentSign;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: The value of this environment variable, if any, will be
        //       appended to the configuration value prior to be it being
        //       replaced within the application setting values.
        //
        /// <summary>
        /// The name of the environment variable holding the configuration
        /// suffix.
        /// </summary>
        private static readonly string ConfigurationSuffixEnvVarName =
            "CONFIGURATION_SUFFIX";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Replaces the known tokens in the value using the specified string
        /// comparison.
        /// </summary>
        /// <param name="value">
        /// The value to perform replacements in.
        /// </param>
        /// <param name="comparisonType">
        /// The string comparison used to match token names.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags governing the replacement.
        /// </param>
        /// <returns>
        /// The value with tokens replaced.
        /// </returns>
        private static string Replace(
            string value,                    /* in */
            StringComparison comparisonType, /* in */
            SettingDataType dataType         /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            ///////////////////////////////////////////////////////////////////

            if (value.IndexOf(IdEnvVarToken,
                    comparisonType) != Index.Invalid)
            {
                value = value.Replace(IdEnvVarToken,
                    Utility.GetCurrentThreadId().ToString());
            }

            ///////////////////////////////////////////////////////////////////

            bool isPath = WebSettingsOps.HasFlags(
                dataType, SettingDataType.PathMask, false);

            Assembly assembly = WebGlobalState.GetAssembly();

            ///////////////////////////////////////////////////////////////////

            if (value.IndexOf(BinDirEnvVarToken,
                    comparisonType) != Index.Invalid)
            {
                string fileName = Utility.GetOriginalLocalPath(
                    assembly);

                if (!String.IsNullOrEmpty(fileName))
                {
                    string directory = Path.GetDirectoryName(
                        fileName);

                    if (!String.IsNullOrEmpty(directory))
                    {
                        value = value.Replace(
                            BinDirEnvVarToken, directory);
                    }
                    else if (isPath)
                    {
                        return null;
                    }
                }
                else if (isPath)
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (value.IndexOf(ConfigurationEnvVarToken,
                    comparisonType) != Index.Invalid)
            {
                string configuration = Utility.GetAssemblyConfiguration(
                    assembly);

                if (!String.IsNullOrEmpty(configuration))
                {
                    string text = Utility.GetAssemblyTextOrSuffix(
                        assembly);

                    if (text == null)
                        text = String.Empty;

                    string suffix = EnvironmentOps.GetVariableValue(
                        ConfigurationSuffixEnvVarName);

                    if (suffix == null)
                        suffix = String.Empty;

                    value = value.Replace(
                        ConfigurationEnvVarToken,
                        configuration + text + suffix);
                }
                else if (isPath)
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            return value;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the value contains any expandable tokens.
        /// </summary>
        /// <param name="value">
        /// The value to inspect.
        /// </param>
        /// <returns>
        /// Non-zero when the value contains a token; otherwise, zero.
        /// </returns>
        public static bool DoesContain(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return false;

            if (value.IndexOf(
                    Characters.PercentSign) == Index.Invalid)
            {
                return false;
            }

            Regex regEx = TokenRegEx;

            if (regEx != null)
            {
                Match match = regEx.Match(value);

                if ((match == null) || !match.Success)
                    return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Expands the tokens in the value when token expansion is requested.
        /// </summary>
        /// <param name="value">
        /// The value to expand.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags governing the expansion.
        /// </param>
        /// <returns>
        /// The expanded value.
        /// </returns>
        public static string Expand(
            string value,            /* in */
            SettingDataType dataType /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            return Replace(
                Utility.ExpandEnvironmentVariables(value),
                Utility.GetSystemComparisonType(true), dataType);
        }
    }
}
