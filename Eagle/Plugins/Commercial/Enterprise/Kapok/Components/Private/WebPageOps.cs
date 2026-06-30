/*
 * WebPageOps.cs --
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
using Eagle._Containers.Public;
using Kapok.Components.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Public;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides helpers for mapping request paths to script-page types and
    /// creating script-page instances.
    /// </summary>
    [ObjectId("88bb7b7d-d6b6-40c1-9c3e-6c5fc8d20514")]
    internal static class WebPageOps
    {
        #region Path Mappings Support Methods
        /// <summary>
        /// Populates the supplied dictionary with the default path-to-type
        /// mappings.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to populate.
        /// </param>
        /// <param name="reset">
        /// Non-zero to clear the dictionary first.
        /// </param>
        private static void UseDefaultTypes(
            StringDictionary dictionary, /* in */
            bool reset                   /* in */
            )
        {
            if (dictionary == null)
                return;

            //
            // TODO: Keep this list of default path mappings updated when
            //       a web page is added -OR- removed.
            //
            string[] pairs = {
                "/certificate/renew.cgi", "Kapok.certificate.renew",
                "/certificate/request.cgi", "Kapok.certificate.request",
                "/certificate/revoked.cgi", "Kapok.certificate.revoked",
                "/service/provision.cgi", "Kapok.service.provision",
                "/support/check.cgi", "Kapok.support.check",
                "/test/page.cgi", "Kapok.test.page",
                "/wrapper/script.cgi", "Kapok.wrapper.script",
                "/var/storage.cgi", "Kapok.var.storage"
            };

            if (pairs == null) /* IMPOSSIBLE */
                return;

            int length = pairs.Length;

            if ((length % 2) != 0) /* IMPOSSIBLE */
                return;

            for (int index = 0; index < length; index += 2)
            {
                string key = pairs[index + 0];

                if (String.IsNullOrEmpty(key))
                    continue;

                string value = pairs[index + 1];

                if (value == null)
                    continue;

                if (reset || !dictionary.ContainsKey(key))
                    dictionary[key] = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the script-page type name for the specified path.
        /// </summary>
        /// <param name="pathName">
        /// The request path.
        /// </param>
        /// <param name="settingName">
        /// The setting name used to look up the type.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <returns>
        /// The type name, or null when not found.
        /// </returns>
        private static string GetType(
            string pathName,         /* in */
            string settingName,      /* in */
            SettingDataType dataType /* in */
            )
        {
            string result;

            if (pathName != null)
            {
                result = WebSettingsOps.GetGlobal(String.Format(
                    "{0}Mapping({1})", settingName, pathName),
                    dataType);
            }
            else
            {
                result = null;
            }

            Utility.DebugTrace(String.Format(
                "GetType: pathName = {0}, settingName = {1}, " +
                "dataType = {2}, result = {3}",
                Utility.FormatWrapOrNull(pathName),
                Utility.FormatWrapOrNull(settingName),
                Utility.FormatWrapOrNull(dataType),
                Utility.FormatWrapOrNull(result)),
                typeof(WebPageOps).Name, TracePriority.Medium);

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the path-to-type mappings, optionally from configuration.
        /// </summary>
        /// <param name="useConfiguration">
        /// Non-zero to include configuration-defined mappings.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the settings.
        /// </param>
        /// <returns>
        /// The dictionary of path-to-type mappings.
        /// </returns>
        private static StringDictionary GetTypes(
            bool useConfiguration,   /* in */
            SettingDataType dataType /* in */
            )
        {
            StringDictionary dictionary = new StringDictionary();

            UseDefaultTypes(dictionary, false);

            if (useConfiguration)
            {
                StringList keys = new StringList(dictionary.Keys);

                foreach (string key in keys)
                {
                    if (String.IsNullOrEmpty(key))
                        continue;

                    string value = GetType(
                        key, "PathClassName", dataType);

                    if (value == null)
                        continue;

                    dictionary[key] = value;
                }
            }

            return dictionary;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a script-page instance for the specified path.
        /// </summary>
        /// <param name="path">
        /// The request path.
        /// </param>
        /// <param name="scriptPage">
        /// On success, receives the created script page.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode CreateScript(
            string path,                /* in */
            ref IScriptPage scriptPage, /* out */
            ref Result error            /* out */
            )
        {
            if (path == null)
            {
                error = "invalid request path";
                return ReturnCode.Error;
            }

            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.TypeName;

            StringDictionary types = GetTypes(true, dataType);
            string typeName;

            if ((types != null) && types.TryGetValue(path, out typeName))
            {
                try
                {
                    Type type = Type.GetType(typeName, false, false);

                    if (type != null)
                    {
                        IScriptPage newScriptPage = Activator.CreateInstance(
                            type) as IScriptPage;

                        if (newScriptPage != null)
                        {
                            scriptPage = newScriptPage;
                        }
                        else
                        {
                            scriptPage = new StatusPage(
                                HttpStatusCode.ServiceUnavailable,
                                "could not create script page");
                        }
                    }
                    else
                    {
                        scriptPage = new StatusPage(
                            HttpStatusCode.NotImplemented,
                            "could not get type for script page");
                    }
                }
                catch (Exception e)
                {
                    scriptPage = new StatusPage(
                        HttpStatusCode.InternalServerError,
                        e.ToString());
                }
            }
            else
            {
                scriptPage = new StatusPage(
                    HttpStatusCode.NotFound,
                    "script page not found");
            }

            return ReturnCode.Ok;
        }
        #endregion
    }
}
