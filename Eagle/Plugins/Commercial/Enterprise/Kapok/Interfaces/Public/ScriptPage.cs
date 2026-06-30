/*
 * ScriptPage.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if NET_STANDARD_20
using System.Threading.Tasks;
#endif

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.AspNetCore.Http;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

namespace Kapok.Interfaces.Public
{
    /// <summary>
    /// Represents a script page served by the Kapok web server.
    /// Implementations supply the page context and configuration, validate and
    /// evaluate the request, read mixed HTML/script block content, and format
    /// the response.
    /// </summary>
    [ObjectId("1e611368-42aa-4696-8477-b73bd65328f0")]
    public interface IScriptPage
    {
        //
        // NOTE: This method is called by the page event handler in order to
        //       obtain the necessary context object.
        //
        /// <summary>
        /// Gets the page context object used by the page event handler.
        /// </summary>
        /// <returns>
        /// The page context.
        /// </returns>
        IPageContext GetPageContext();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the ConfigureScriptRequest method to
        //       obtain the script page data object to load the configuration
        //       into.
        //
        /// <summary>
        /// Gets the script page data object that the configuration is loaded
        /// into.
        /// </summary>
        /// <returns>
        /// The script page data.
        /// </returns>
        IScriptPageData GetPageData();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page "constructor" to obtain the
        //       base name of the page being called.  It will be used to query
        //       the settings for the page, and possibly for other things.  By
        //       default, this will be the the last portion of the namespace
        //       containing the page class, in "title case".  In this context,
        //       "title case" means the first letter will be in upper case and
        //       the rest will be in lower case.
        //
        /// <summary>
        /// Gets the base name of the page, used to query its settings.
        /// Defaults to the title-cased last portion of the page class
        /// namespace.
        /// </summary>
        /// <param name="default">
        /// The default page name to use when one cannot be derived.
        /// </param>
        /// <returns>
        /// The base page name.
        /// </returns>
        string GetScriptPageName(string @default);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called during page load in order to obtain the
        //       value of a configuration setting for the page.
        //
        /// <summary>
        /// Gets the value of a configuration setting for the page.
        /// </summary>
        /// <param name="settingName">
        /// The name of the setting to retrieve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <returns>
        /// The setting value, or null when not found.
        /// </returns>
        string GetScriptPageSetting(string settingName,
            SettingDataType dataType);

        /// <summary>
        /// Gets the value of a configuration setting for the page, reporting
        /// the indexed-search position at which it was found.
        /// </summary>
        /// <param name="settingName">
        /// The name of the setting to retrieve.
        /// </param>
        /// <param name="dataType">
        /// The data type and search flags for the setting.
        /// </param>
        /// <param name="foundIndex">
        /// On output, receives the index at which the value was found.
        /// </param>
        /// <returns>
        /// The setting value, or null when not found.
        /// </returns>
        string GetScriptPageSetting(string settingName,
            SettingDataType dataType, ref int foundIndex);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       setup the script setup text, the script file name, and the
        //       enabled flag.  It should not do anything else.
        //
        /// <summary>
        /// Configures the script request, setting the setup text, script file
        /// name, and enabled flag.
        /// </summary>
        void ConfigureScriptRequest();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       see if the page need a manual refresh.  It may also perform
        //       other pre-validation tasks.
        //
        /// <summary>
        /// Performs pre-validation of the script request and indicates whether
        /// the page needs a manual refresh.
        /// </summary>
        /// <param name="refresh">
        /// On output, indicates whether a manual refresh is needed.
        /// </param>
        /// <param name="returnCode">
        /// On output, receives the pre-validation return code.
        /// </param>
        /// <param name="result">
        /// On output, receives the pre-validation result or error.
        /// </param>
        void PreValidateScriptRequest(ref bool? refresh,
            ref ReturnCode returnCode, ref Result result);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       populate the argument list accessible from the scripts being
        //       evaluated (i.e. both the setup script and the script file).
        //       It is also responsible for performing any validation that is
        //       required before the script file is evaluated.
        //
        /// <summary>
        /// Validates the script request and populates the argument list
        /// available to the evaluated scripts.
        /// </summary>
        /// <param name="arguments">
        /// On output, receives the argument list for the scripts.
        /// </param>
        /// <param name="returnCode">
        /// On output, receives the validation return code.
        /// </param>
        /// <param name="result">
        /// On output, receives the validation result or error.
        /// </param>
        void ValidateScriptRequest(ref StringList arguments,
            ref ReturnCode returnCode, ref Result result);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       read the mixed-mode HTML and script blocks content from a
        //       disk file.
        //
        /// <summary>
        /// Reads the mixed-mode HTML and script block content for the page
        /// from a disk file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The file to read the block content from.
        /// </param>
        /// <param name="blockText">
        /// On output, receives the block content.
        /// </param>
        void ReadScriptBlocksFile(Interpreter interpreter, string fileName,
            ref string blockText);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       handle a non-script page (i.e. one with hard-wired managed
        //       code backing the page, e.g. the variable storage server).
        //
        /// <summary>
        /// Handles a non-script page (one backed by managed code rather than a
        /// script file).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter handling the request.
        /// </param>
        /// <param name="quiet">
        /// On output, non-zero to suppress the normal response output.
        /// </param>
        /// <param name="returnCode">
        /// On output, receives the handling return code.
        /// </param>
        /// <param name="result">
        /// On output, receives the handling result or error.
        /// </param>
        void HandleScriptRequest(Interpreter interpreter, ref bool quiet,
            ref ReturnCode returnCode, ref Result result);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called to format the response body that will
        //       be sent back to the client.
        //
        /// <summary>
        /// Formats the response body sent back to the client from a script
        /// result.
        /// </summary>
        /// <param name="returnCode">
        /// The return code of the evaluated script.
        /// </param>
        /// <param name="result">
        /// The result of the evaluated script.
        /// </param>
        /// <param name="errorLine">
        /// The error line number, or zero when none.
        /// </param>
        /// <returns>
        /// The formatted response body.
        /// </returns>
        string FormatScriptResponse(
            ReturnCode returnCode, Result result, int errorLine);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called to cleanup any (temporary) state used
        //       while processing the request.
        //
        /// <summary>
        /// Cleans up any temporary state used while processing the request.
        /// </summary>
        void FinalizeScriptRequest();

        ///////////////////////////////////////////////////////////////////////

        #region RequestDelegate Methods (ASP.NET Core)
#if NET_STANDARD_20
        /// <summary>
        /// The ASP.NET Core request delegate entry point for the page.
        /// </summary>
        /// <param name="context">
        /// The HTTP context for the request.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous request handling.
        /// </returns>
        Task RequestDelegate(HttpContext context);
#endif
        #endregion
    }
}
