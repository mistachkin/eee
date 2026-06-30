/*
 * renew.cgi.cs --
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
using Eagle._Constants;
using Eagle._Containers.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.certificate
{
    /// <summary>
    /// Implements the certificate renewal script page.
    /// </summary>
    [ObjectId("8daed1db-10fc-4f1c-b3c6-74bf23b8e784")]
    public partial class renew : ScriptPage
    {
        #region Private Constants
        #region Script Page Name
        /// <summary>
        /// The base name used to look up the settings for this page.
        /// </summary>
        private static readonly string ScriptPageName = "Renewal";
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
        /// Validates the certificate renewal request and populates the
        /// argument list passed to the evaluated script.
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

            Guid? requestId = ValueOps.TryParseGuid(PageOps.GetParameter(
                request, "requestId"));

            if (requestId == null)
            {
                result = "invalid requestId";
                returnCode = ReturnCode.Error;
                return;
            }

            DateTime? requestTimeStamp = ValueOps.TryParseUtcDateTime(
                PageOps.GetParameter(request, "requestTimeStamp"),
                Constants.TimeStampFormat);

            if (requestTimeStamp == null)
            {
                result = "invalid requestTimeStamp";
                returnCode = ReturnCode.Error;
                return;
            }

            Guid? certificateId = ValueOps.TryParseGuid(PageOps.GetParameter(
                request, "certificateId"));

            if (certificateId == null)
            {
                result = "invalid certificateId";
                returnCode = ReturnCode.Error;
                return;
            }

            string requestHash = PageOps.GetParameter(request, "requestHash");

            if (String.IsNullOrEmpty(requestHash))
            {
                result = "invalid requestHash";
                returnCode = ReturnCode.Error;
                return;
            }

            //
            // NOTE: These parameters are optional; however, the corresponding
            //       arguments to the script are required.  These values will
            //       be false when they are not present.
            //
            bool scriptMode = false;

            if (Value.GetBoolean2(PageOps.GetParameter(
                    request, "scriptMode"), ValueFlags.AnyBoolean, null,
                    ref scriptMode, ref result) != ReturnCode.Ok)
            {
                returnCode = ReturnCode.Error;
                return;
            }

            bool embedded = false;

            if (Value.GetBoolean2(PageOps.GetParameter(
                    request, "embedded"), ValueFlags.AnyBoolean, null,
                    ref embedded, ref result) != ReturnCode.Ok)
            {
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: These parameters are optional.  Also, they will only
            //       actually be used when "pass-through mode" is enabled.
            //
            string entityName = PageOps.GetParameter(request, "entityName");
            string entityValue = PageOps.GetParameter(request, "entityValue");

            ///////////////////////////////////////////////////////////////////

            SettingDataType dataType = SettingDataType.DefaultAndExpand;

            if (arguments == null)
                arguments = new StringList();

            arguments.Add(((Guid)requestId).ToString(
                Constants.IdFormat));

            arguments.Add(((DateTime)requestTimeStamp).ToString(
                Constants.TimeStampFormat));

            arguments.Add(((Guid)certificateId).ToString(
                Constants.IdFormat));

            arguments.Add(requestHash);

            arguments.Add(GetScriptPageSetting("PassThroughMode",
                dataType | SettingDataType.Boolean));

            arguments.Add(scriptMode.ToString());
            arguments.Add(embedded.ToString());
            arguments.Add(GetScriptPageSetting("CertificateDatabase",
                dataType | SettingDataType.FileName));

            int foundIndex = Index.Invalid;

            if (scriptMode)
            {
                arguments.Add(GetScriptPageSetting(
                    "ScriptKeyRingDatabase",
                    dataType | SettingDataType.FileName,
                    ref foundIndex));

                arguments.Add(GetScriptPageSetting("ScriptKeyId",
                    dataType | SettingDataType.Integer,
                    ref foundIndex));
            }
            else
            {
                arguments.Add(GetScriptPageSetting(
                    "LicenseKeyRingDatabase",
                    dataType | SettingDataType.FileName,
                    ref foundIndex));

                arguments.Add(GetScriptPageSetting("LicenseKeyId",
                    dataType | SettingDataType.Integer,
                    ref foundIndex));
            }

            arguments.Add(GetScriptPageSetting("Timeout",
                dataType | SettingDataType.Integer));

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: These parameters are optional.  Also, they will only
            //       actually be used when "pass-through mode" is enabled.
            //
            if (entityName != null)
                arguments.Add(entityName);

            if (entityValue != null)
                arguments.Add(entityValue);

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Do we want to prevent [otherwise valid] requests from
            //       ever timing out?  The code to handle this setting is
            //       always compiled into the code; however, it should only
            //       be set when debugging.
            //
            bool noRequestTimeout = ValueOps.TryParseBoolean(
                GetScriptPageSetting("NoRequestTimeout",
                dataType | SettingDataType.Boolean), false);

            if (noRequestTimeout)
            {
                /* NO RESULT */
                Utility.SetEnvironmentVariable(
                    "NoRequestTimeout", noRequestTimeout.ToString(),
                    false);
            }
        }
        #endregion
    }
}
