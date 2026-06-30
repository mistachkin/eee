/*
 * check.cgi.cs --
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

namespace Kapok.support
{
    /// <summary>
    /// Implements the support check script page.
    /// </summary>
    [ObjectId("59c354f0-844d-4c32-9715-fbaabf3fe387")]
    public partial class check : ScriptPage
    {
        #region Private Constants
        #region Script Page Name
        /// <summary>
        /// The base name used to look up the settings for this page.
        /// </summary>
        private static readonly string ScriptPageName = "Support";
        #endregion
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
        /// Validates the support check request and populates the argument list
        /// passed to the evaluated script.
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

            string id = PageOps.GetParameter(request, "id");

            if (String.IsNullOrEmpty(id))
            {
                result = "invalid support id";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            SettingDataType dataType = SettingDataType.DefaultAndExpand;

            if (arguments == null)
                arguments = new StringList();

            arguments.Add(id);

            arguments.Add(GetScriptPageSetting("Database",
                dataType | SettingDataType.FileName));
        }
        #endregion
    }
}
