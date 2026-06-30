/*
 * WebSettingsOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Kapok.Components.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Public;
using WSO = Kapok.Components.Private.WebScriptOps;
using WTO = Kapok.Components.Private.WebTokenOps;
using WVO = Kapok.Components.Private.WebVerifyOps;
using EnvOps = Kapok.Components.Shared.EnvironmentOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides the multi-source setting resolution used by the server: it
    /// searches indexed names across script variables, .NET application
    /// settings, and environment variables, applies token expansion, and
    /// verifies values against their data type.
    /// </summary>
    [ObjectId("b046d1ce-f53b-47f8-ac27-c48a33f5dfe2")]
    internal static class WebSettingsOps
    {
        #region Private Constants
        //
        // NOTE: These are the minimum and maximum search indexes that will
        //       be appended to the certificate setting prefixes (above).
        //
        /// <summary>
        /// The default minimum indexed-search index.
        /// </summary>
        private static readonly int DefaultMinimumIndex = 1;
        /// <summary>
        /// The default maximum indexed-search index.
        /// </summary>
        private static readonly int DefaultMaximumIndex = 9;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Configuration Support Methods
        /// <summary>
        /// Determines whether the supplied setting flags contain the given
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
            SettingDataType flags,    /* in */
            SettingDataType hasFlags, /* in */
            bool all                  /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != SettingDataType.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the indexed-search bounds to their defaults.
        /// </summary>
        /// <param name="foundIndex">
        /// On output, receives the initial found index.
        /// </param>
        /// <param name="noSearch">
        /// Non-zero to disable indexed search.
        /// </param>
        /// <param name="minimumIndex">
        /// On output, receives the minimum index.
        /// </param>
        /// <param name="maximumIndex">
        /// On output, receives the maximum index.
        /// </param>
        public static void InitializeIndexes(
            int foundIndex,       /* in */
            bool noSearch,        /* in */
            out int minimumIndex, /* out */
            out int maximumIndex  /* out */
            )
        {
            minimumIndex = (foundIndex != Index.Invalid) ?
                foundIndex : DefaultMinimumIndex - 1;

            maximumIndex = noSearch ?
                minimumIndex : DefaultMaximumIndex;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the indexed-search bounds for a setting, honoring any
        /// per-setting minimum/maximum index overrides.
        /// </summary>
        /// <param name="settingName">
        /// The setting name being resolved.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing override bounds.
        /// </param>
        /// <param name="foundIndex">
        /// On output, receives the initial found index.
        /// </param>
        /// <param name="dataType">
        /// The data type and flags for the setting.
        /// </param>
        /// <param name="minimumIndex">
        /// On output, receives the minimum index.
        /// </param>
        /// <param name="maximumIndex">
        /// On output, receives the maximum index.
        /// </param>
        private static void InitializeIndexes(
            string settingName,       /* in */
            CultureInfo cultureInfo,  /* in */
            int foundIndex,           /* in */
            SettingDataType dataType, /* in */
            out int minimumIndex,     /* out */
            out int maximumIndex      /* out */
            )
        {
            bool noSearch = HasFlags(
                    dataType, SettingDataType.NoSearch, true);

            bool noVariableValue = HasFlags(
                    dataType, SettingDataType.NoVariableValue, true);

            ///////////////////////////////////////////////////////////////////

            InitializeIndexes(foundIndex,
                noSearch, out minimumIndex, out maximumIndex);

            ///////////////////////////////////////////////////////////////////

            if (noVariableValue)
                return;

            if (foundIndex != Index.Invalid)
                goto skipMinimum;

            ///////////////////////////////////////////////////////////////////

            int localMinimumIndex = Index.Invalid;

            if ((Value.GetInteger2(WSO.GetVariableValue(
                    String.Format("{0}_SettingMinimumIndex", settingName),
                    dataType), ValueFlags.AnyInteger, cultureInfo,
                    ref localMinimumIndex) == ReturnCode.Ok) ||
                (Value.GetInteger2(WSO.GetVariableValue(
                    String.Format("SettingMinimumIndex", settingName),
                    dataType), ValueFlags.AnyInteger, cultureInfo,
                    ref localMinimumIndex) == ReturnCode.Ok))
            {
                minimumIndex = localMinimumIndex;
            }

            ///////////////////////////////////////////////////////////////////

        skipMinimum:

            int localMaximumIndex = Index.Invalid;

            if ((Value.GetInteger2(WSO.GetVariableValue(
                    String.Format("{0}_SettingMaximumIndex", settingName),
                    dataType), ValueFlags.AnyInteger, cultureInfo,
                    ref localMaximumIndex) == ReturnCode.Ok) ||
                (Value.GetInteger2(WSO.GetVariableValue(
                    String.Format("SettingMaximumIndex", settingName),
                    dataType), ValueFlags.AnyInteger, cultureInfo,
                    ref localMaximumIndex) == ReturnCode.Ok))
            {
                maximumIndex = localMaximumIndex;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a .NET application setting value by name.
        /// </summary>
        /// <param name="name">
        /// The application setting name.
        /// </param>
        /// <returns>
        /// The setting value, or null when not found.
        /// </returns>
        private static string GetAppSetting(
            string name /* in */
            )
        {
            return Utility.GetAppSetting(name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats an indexed, configuration-prefixed setting name.
        /// </summary>
        /// <param name="settingName">
        /// The base setting name.
        /// </param>
        /// <param name="configuration">
        /// The configuration prefix, if any.
        /// </param>
        /// <param name="index">
        /// The search index.
        /// </param>
        /// <returns>
        /// The formatted setting name.
        /// </returns>
        private static string FormatGlobalIndexedName(
            string settingName,   /* in */
            string configuration, /* in */
            int index             /* in */
            )
        {
            if (configuration != null)
            {
                if (index > 0)
                {
                    return String.Format(
                        "{0}.{1}{2}", configuration,
                        settingName, index);
                }
                else
                {
                    return String.Format(
                        "{0}.{1}", configuration,
                        settingName);
                }
            }
            else
            {
                if (index > 0)
                {
                    return String.Format(
                        "{0}{1}", settingName, index);
                }
                else
                {
                    return settingName;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a setting resolution should be traced, given the
        /// data-type flags and the resolved value.
        /// </summary>
        /// <param name="dataType">
        /// The data type and flags for the setting.
        /// </param>
        /// <param name="value">
        /// The resolved value.
        /// </param>
        /// <param name="priority">
        /// On output, receives the trace priority to use.
        /// </param>
        /// <returns>
        /// Non-zero when the resolution should be traced; otherwise, zero.
        /// </returns>
        public static bool ShouldTrace(
            SettingDataType dataType,  /* in */
            string value,              /* in */
            out TracePriority priority /* out */
            )
        {
            if (!String.IsNullOrEmpty(value))
            {
                priority = TracePriority.MediumLow; /* EXEMPT */

                return HasFlags(
                    dataType, SettingDataType.TraceOk, true);
            }
            else
            {
                priority = TracePriority.MediumHigh; /* EXEMPT */

                return HasFlags(
                    dataType, SettingDataType.TraceError, true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a global setting value by name (backs the <c>kapok setting</c>
        /// sub-command), searching all sources and indexed names.
        /// </summary>
        /// <param name="settingName">
        /// The setting name to resolve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <returns>
        /// The resolved setting value, or null when not found.
        /// </returns>
        public static string GetGlobal(
            string settingName,      /* in */
            SettingDataType dataType /* in */
            )
        {
            int foundIndex = Index.Invalid;

            return GetGlobal(
                settingName, dataType, ref foundIndex);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a global setting value by name, reporting the indexed-search
        /// position at which it was found.
        /// </summary>
        /// <param name="settingName">
        /// The setting name to resolve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <param name="foundIndex">
        /// On output, receives the index at which the value was found.
        /// </param>
        /// <returns>
        /// The resolved setting value, or null when not found.
        /// </returns>
        private static string GetGlobal( /* PRIMARY */
            string settingName,       /* in */
            SettingDataType dataType, /* in */
            ref int foundIndex        /* in, out */
            )
        {
            string indexedName = null;
            string result = null;
            string variableValue = null;
            string appSetting = null;
            string environment = null;

            try
            {
                if (!String.IsNullOrEmpty(settingName))
                {
                    bool expandTokens = HasFlags(
                        dataType, SettingDataType.ExpandTokens,
                        true);

                    bool noSearch = HasFlags(
                        dataType, SettingDataType.NoSearch,
                        true);

                    bool mustVerify = HasFlags(
                        dataType, SettingDataType.MustVerify,
                        true);

                    bool noVariableValue = HasFlags(
                        dataType, SettingDataType.NoVariableValue,
                        true);

                    bool noAppSetting = HasFlags(
                        dataType, SettingDataType.NoAppSetting,
                        true);

                    bool noEnvironment = HasFlags(
                        dataType, SettingDataType.NoEnvironment,
                        true);

                    int minimumIndex;
                    int maximumIndex;

                    InitializeIndexes(
                        settingName, null, foundIndex, dataType,
                        out minimumIndex, out maximumIndex);

                    bool found = false;

                    foreach (string configuration in new string[] {
                            Utility.GetAssemblyConfiguration(
                                WebGlobalState.GetAssembly()),
                            null })
                    {
                        for (int index = minimumIndex;
                                index <= maximumIndex; index++)
                        {
                            indexedName = FormatGlobalIndexedName(
                                settingName, configuration, index);

                            if (!noVariableValue)
                            {
                                variableValue = WSO.GetVariableValue(
                                    indexedName, dataType);

                                result = variableValue;
                            }

                            if (!noAppSetting)
                            {
                                appSetting = GetAppSetting(
                                    indexedName);

                                if (noVariableValue ||
                                    String.IsNullOrEmpty(result))
                                {
                                    result = appSetting;
                                }
                            }

                            if (!noEnvironment)
                            {
                                environment = EnvOps.GetVariableValue(
                                    indexedName);

                                if ((noVariableValue && noAppSetting) ||
                                    String.IsNullOrEmpty(result))
                                {
                                    result = environment;
                                }
                            }

                            if (expandTokens &&
                                !String.IsNullOrEmpty(result))
                            {
                                result = WTO.Expand(result, dataType);
                            }

                            if (noSearch ||
                                WVO.AnyValue(ref result, dataType))
                            {
                                foundIndex = index;
                                found = true;

                                break;
                            }
                        }

                        if (found)
                            break;
                    }

                    if (!found)
                    {
                        //
                        // NOTE: Mutate the indexed name to make
                        //       it more generic and informative.
                        //       In the trace message, this will
                        //       indicate how many indexes were
                        //       actually checked.
                        //
                        indexedName = String.Format(
                            "{0}[{1}-{2}]", settingName,
                            minimumIndex, maximumIndex);

                        if (mustVerify)
                        {
                            //
                            // NOTE: Since we know the value was
                            //       not verified, force return
                            //       null.
                            //
                            result = null;

                            //
                            // HACK: For consistency in the trace
                            //       message, null both originally
                            //       read values as well.
                            //
                            variableValue = null;
                            appSetting = null;
                            environment = null;
                        }
                    }
                }
            }
            finally
            {
                TracePriority priority;

                if (ShouldTrace(dataType, result, out priority))
                {
                    priority |= TracePriority.FromPlugin;

                    Utility.DebugTrace(String.Format(
                        "GetGlobal: settingName = {0}, " +
                        "dataType = {1}, indexedName = {2}, " +
                        "variableValue = {3}, appSetting = {4}, " +
                        "environment = {5}, foundIndex = {6}, " +
                        "result = {7}",
                        Utility.FormatWrapOrNull(settingName),
                        Utility.FormatWrapOrNull(dataType),
                        Utility.FormatWrapOrNull(indexedName),
                        Utility.FormatWrapOrNull(variableValue),
                        Utility.FormatWrapOrNull(appSetting),
                        Utility.FormatWrapOrNull(environment),
                        Utility.FormatWrapOrNull(foundIndex),
                        Utility.FormatWrapOrNull(result)),
                        typeof(WebSettingsOps).Name, priority);
                }
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a page-scoped setting value by name.
        /// </summary>
        /// <param name="pageName">
        /// The page name used to scope the setting.
        /// </param>
        /// <param name="settingName">
        /// The setting name to resolve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <returns>
        /// The resolved setting value, or null when not found.
        /// </returns>
        public static string GetPage(
            string pageName,         /* in */
            string settingName,      /* in */
            SettingDataType dataType /* in */
            )
        {
            int foundIndex = Index.Invalid;

            return GetPage(
                pageName, settingName, dataType, ref foundIndex);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a page-scoped setting value by name, reporting the
        /// indexed-search position at which it was found.
        /// </summary>
        /// <param name="pageName">
        /// The page name used to scope the setting.
        /// </param>
        /// <param name="settingName">
        /// The setting name to resolve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <param name="foundIndex">
        /// On output, receives the index at which the value was found.
        /// </param>
        /// <returns>
        /// The resolved setting value, or null when not found.
        /// </returns>
        public static string GetPage(
            string pageName,          /* in */
            string settingName,       /* in */
            SettingDataType dataType, /* in */
            ref int foundIndex        /* in, out */
            )
        {
            if (pageName != null)
            {
                return GetGlobal(String.Format(
                    "{0}{1}", pageName, settingName), dataType,
                    ref foundIndex);
            }
            else
            {
                return GetGlobal(settingName, dataType,
                    ref foundIndex);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the page configuration settings into the supplied page data.
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
        public static void LoadPage(
            IScriptPageData pageData, /* in */
            string pageName,          /* in */
            SettingDataType dataType  /* in */
            )
        {
            if (pageData == null)
                return;

            //
            // HACK: Only retain the "special handling" flags from the
            //       data type flags specified by the caller.
            //
            SettingDataType localDataType =
                dataType & SettingDataType.FlagsMask;

            ///////////////////////////////////////////////////////////////////

            pageData.Setup = GetPage(pageName,
                "SetupScript", localDataType | SettingDataType.Script);

            pageData.Blocks = ValueOps.TryParseBoolean(GetPage(
                pageName, "ScriptBlocks", localDataType |
                SettingDataType.Boolean), false);

            ///////////////////////////////////////////////////////////////////

            object enumValue = ValueOps.TryParseFlagsEnum(
                pageData.BlockFlags.ToString(), GetPage(
                pageName, "ScriptBlockFlags", localDataType |
                SettingDataType.Enumeration), typeof(ScriptBlockFlags),
                true, true);

            if (enumValue is ScriptBlockFlags)
                pageData.BlockFlags = (ScriptBlockFlags)enumValue;

            ///////////////////////////////////////////////////////////////////

            pageData.FileName = GetPage(
                pageName, "ScriptFile", localDataType |
                SettingDataType.FileName);

            pageData.Enabled = ValueOps.TryParseBoolean(GetPage(
                pageName, "ServerEnabled", localDataType |
                SettingDataType.Boolean), false);

            pageData.LicensingEnabled = ValueOps.TryParseBoolean(GetPage(
                pageName, "ServerLicensingEnabled", localDataType |
                SettingDataType.Boolean), false);

            pageData.CreateInterpreter = ValueOps.TryParseBoolean(GetPage(
                pageName, "ServerCreateInterpreter", localDataType |
                SettingDataType.Boolean), false);

            pageData.CacheInterpreter = ValueOps.TryParseBoolean(GetPage(
                pageName, "ServerCacheInterpreter", localDataType |
                SettingDataType.Boolean), false);

            pageData.CacheSeconds = ValueOps.TryParseWideInteger(GetPage(
                pageName, "ServerCacheSeconds", localDataType |
                SettingDataType.WideInteger), 0);

            pageData.SecurityLevel = ValueOps.TryParseInteger(GetPage(
                pageName, "ServerSecurityLevel", localDataType |
                SettingDataType.Integer), 0);

            ///////////////////////////////////////////////////////////////////

            enumValue = ValueOps.TryParseFlagsEnum(
                pageData.SecurityFlags.ToString(), GetPage(
                pageName, "ServerSecurityFlags", localDataType |
                SettingDataType.Enumeration), typeof(SecurityFlags),
                true, true);

            if (enumValue is SecurityFlags)
                pageData.SecurityFlags = (SecurityFlags)enumValue;

            ///////////////////////////////////////////////////////////////////

            pageData.Environment = ValueOps.TryParseEnvironment(
                GetPage(pageName, "ServerEnvironment",
                localDataType | SettingDataType.StringListMask),
                localDataType);
        }
        #endregion
    }
}
