/*
 * script.cgi.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

namespace Kapok.wrapper
{
    /// <summary>
    /// Implements the generic script wrapper page, which evaluates a wrapped
    /// script using the request form and query values.
    /// </summary>
    [ObjectId("4238d578-0ece-4268-bff6-2eb89e6637fe")]
    public partial class script : ScriptPage
    {
        #region Kapok.ScriptPage Overrides
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
        /// Validates the script request and populates the argument list with
        /// the request address, form, and query values.
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

            PageOps.AddArgument("address",
                request.Address, ref arguments);

            PageOps.AddForm(request, ref arguments);
            PageOps.AddQuery(request, ref arguments);
        }

        ///////////////////////////////////////////////////////////////////////

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
        public override void ReadScriptBlocksFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref string blockText     /* out */
            )
        {
            Result result = null;

            if (WebScriptOps.ReadBlocksFile(
                    interpreter, fileName, ref result) == ReturnCode.Ok)
            {
                blockText = result;
            }
            else
            {
                blockText = null;
            }
        }
        #endregion
    }
}
