/*
 * storage.cgi.cs --
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
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

namespace Kapok.var
{
    /// <summary>
    /// Implements the variable storage server script page, which is backed by
    /// managed code rather than a script file.
    /// </summary>
    [ObjectId("99a3909d-1cb0-4bf8-9f5e-e5fc8fd78b35")]
    public partial class storage : ScriptPage
    {
        #region Private Constants
        #region Script Page Name
        /// <summary>
        /// The base name used to look up the settings for this page.
        /// </summary>
        private static readonly string ScriptPageName = "Variable";
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The database file name for variable storage.
        /// </summary>
        private string fileName;
        /// <summary>
        /// The API key authorizing the request.
        /// </summary>
        private Guid apiKey;
        /// <summary>
        /// The variable storage method to perform.
        /// </summary>
        private VariableMethod method;
        /// <summary>
        /// The variable name, when applicable.
        /// </summary>
        private string varName;
        /// <summary>
        /// The variable value, when applicable.
        /// </summary>
        private string varValue;
        /// <summary>
        /// The name-matching pattern, when applicable.
        /// </summary>
        private string pattern;
        /// <summary>
        /// Non-zero for case-insensitive pattern matching.
        /// </summary>
        private bool noCase;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Saves the parsed request parameters for use by the request handler.
        /// </summary>
        /// <param name="fileName">
        /// The database file name for variable storage.
        /// </param>
        /// <param name="apiKey">
        /// The API key authorizing the request.
        /// </param>
        /// <param name="method">
        /// The variable storage method to perform.
        /// </param>
        /// <param name="varName">
        /// The variable name, when applicable.
        /// </param>
        /// <param name="varValue">
        /// The variable value, when applicable.
        /// </param>
        /// <param name="pattern">
        /// The name-matching pattern, when applicable.
        /// </param>
        /// <param name="noCase">
        /// Non-zero for case-insensitive pattern matching.
        /// </param>
        private void SaveParameters(
            string fileName,       /* in */
            Guid apiKey,           /* in */
            VariableMethod method, /* in */
            string varName,        /* in */
            string varValue,       /* in */
            string pattern,        /* in */
            bool noCase            /* in */
            )
        {
            this.fileName = fileName;
            this.apiKey = apiKey;
            this.method = method;
            this.varName = varName;
            this.varValue = varValue;
            this.pattern = pattern;
            this.noCase = noCase;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Kapok.ScriptPage Overrides
        /// <summary>
        /// Gets the base name of this script page.
        /// </summary>
        /// <param name="default">
        /// The default page name to use when one cannot be derived.
        /// </param>
        /// <returns>
        /// The base page name.
        /// </returns>
        public override string GetScriptPageName(
            string @default /* in */
            )
        {
            return ScriptPageName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the script request, loading the settings for this page.
        /// </summary>
        public override void ConfigureScriptRequest()
        {
            base.ConfigureScriptRequest();

            ///////////////////////////////////////////////////////////////////

            WebSettingsOps.LoadPage(
                GetPageData(), GetScriptPageName(null),
                SettingDataType.DefaultAndExpand);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates the variable storage request and saves the parsed
        /// parameters for later handling.
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
        public override void ValidateScriptRequest(
            ref StringList arguments,  /* in, out */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
            base.ValidateScriptRequest(
                ref arguments, ref returnCode, ref result);

            ///////////////////////////////////////////////////////////////////

            IPageContext pageContext = GetPageContext();

            if (pageContext == null)
            {
                result = "invalid page context object";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            IRequest request = pageContext.GetRequest();

            if (request == null)
            {
                result = "invalid request object";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            Guid? apiKey = ValueOps.TryParseGuid(PageOps.GetParameter(
                request, "apiKey"));

            if (apiKey == null)
            {
                result = "invalid API key";
                returnCode = ReturnCode.Error;
                return;
            }

            VariableMethod? method = Utility.TryParseEnum(
                typeof(VariableMethod), PageOps.GetParameter(
                request, "method"), false, true,
                ref result) as VariableMethod?;

            if (method == null)
            {
                result = "invalid variable method";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: This parameter is optional; however, the corresponding
            //       argument to the script is required.  This value should
            //       be false when it is not present.
            //
            bool? noCase = null;

            if (Value.GetNullableBoolean2(PageOps.GetParameter(
                    request, "noCase"), ValueFlags.AnyBoolean, null,
                    ref noCase, ref result) != ReturnCode.Ok)
            {
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: These parameters may be optional, depending on the method
            //       being requested.  The variable storage server script will
            //       determine if an error response is necessary.
            //
            string pattern = PageOps.GetParameter(request, "pattern");
            string varName = PageOps.GetParameter(request, "varName");
            string varValue = PageOps.GetParameter(request, "varValue");

            ///////////////////////////////////////////////////////////////////

            SettingDataType dataType = SettingDataType.DefaultAndExpand;

            SaveParameters(GetScriptPageSetting("Database",
                dataType | SettingDataType.FileName), (Guid)apiKey,
                (VariableMethod)method, varName, varValue, pattern,
                (noCase != null) ? (bool)noCase : false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the variable storage request using managed code, returning
        /// the result of the storage operation.
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
        public override void HandleScriptRequest(
            Interpreter interpreter,   /* in */
            ref bool quiet,            /* out */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
            returnCode = StorageOps.Process(
                interpreter, fileName, apiKey, method, varName,
                varValue, pattern, noCase, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the response body as a status word followed by the result.
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
        public override string FormatScriptResponse(
            ReturnCode returnCode, /* in */
            Result result,         /* in */
            int errorLine          /* in */
            )
        {
            StringList list = new StringList();

            list.Add(
                returnCode.ToString().ToUpperInvariant()); // "OK" / "ERROR"

            if (result != null)
                list.Add(result);

            return list.ToString();
        }
        #endregion
    }
}
