/*
 * revoked.cgi.cs --
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
using Eagle._Containers.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

namespace Kapok.certificate
{
    /// <summary>
    /// Implements the certificate revocation list script page.
    /// </summary>
    [ObjectId("6916fffe-adaf-4622-b8be-ef3e58888c2c")]
    public partial class revoked : ScriptPage
    {
        #region Private Constants
        #region Script Page Name
        /// <summary>
        /// The base name used to look up the settings for this page.
        /// </summary>
        private static readonly string ScriptPageName = "Revocation";
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
        /// Validates the revocation request and writes the requested
        /// revocation list to the response.
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

            IResponse response = pageContext.GetResponse();

            if (response == null)
            {
                result = "invalid response object";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            string type = PageOps.GetParameter(request, "type");

            ///////////////////////////////////////////////////////////////////

            SettingDataType dataType = SettingDataType.DefaultAndExpand;

            string fileName = null;

            if (Utility.SystemStringEquals(type, "key", true))
            {
                fileName = GetScriptPageSetting("TokensListFile",
                    dataType | SettingDataType.FileName);
            }
            else if (String.IsNullOrEmpty(type) ||
                Utility.SystemStringEquals(type, "certificate", true))
            {
                fileName = GetScriptPageSetting("IdsListFile",
                    dataType | SettingDataType.FileName);
            }
            else
            {
                result = "revocation client misconfiguration";
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            try
            {
                if (!String.IsNullOrEmpty(fileName))
                {
                    if (File.Exists(fileName))
                    {
                        response.WriteFile(fileName); /* throw */
                        returnCode = ReturnCode.Return;
                    }
                    else
                    {
                        result = "revocation server misconfiguration";
                        returnCode = ReturnCode.Error;
                    }
                }
                else
                {
                    response.Write(StringList.MakeList(
                        String.Empty, String.Empty));

                    returnCode = ReturnCode.Return;
                }
            }
            catch (Exception e)
            {
                result = e;
                returnCode = ReturnCode.Error;
            }
        }
        #endregion
    }
}
