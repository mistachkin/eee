/*
 * provision.cgi.cs --
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
using Eagle._Interfaces.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;
using IOP = Kapok.Components.Private.InterpreterOps;

namespace Kapok.service
{
    /// <summary>
    /// Implements the service provisioning script page.
    /// </summary>
    [ObjectId("9e8f63e9-6f9d-4c4d-816e-98ae3e857476")]
    public partial class provision : ScriptPage
    {
        #region Private Constants
        #region Script Page Name
        /// <summary>
        /// The base name used to look up the settings for this page.
        /// </summary>
        private static readonly string ScriptPageName = "Provision";
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// Holds the saved page environment state, restored when the request
        /// is finalized.
        /// </summary>
        private IClientData environmentClientData = null;
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
        /// Configures the script request, loading the settings for this page
        /// It also saves and sets up the page environment.
        /// </summary>
        public override void ConfigureScriptRequest()
        {
            base.ConfigureScriptRequest();

            ///////////////////////////////////////////////////////////////////

            WebSettingsOps.LoadPage(
                GetPageData(), GetScriptPageName(null),
                SettingDataType.DefaultAndExpand);

            ///////////////////////////////////////////////////////////////////

            if (!WebEnvironmentOps.SavePage(
                    GetPageData(), ref environmentClientData))
            {
                Utility.DebugTrace(
                    "ConfigureScriptRequest: could not save " +
                    "page environment", typeof(provision).Name,
                    TracePriority.Highest | TracePriority.FromPlugin);
            }

            ///////////////////////////////////////////////////////////////////

            WebEnvironmentOps.SetupPage(
                GetPageData(), GetScriptPageName(null),
                SettingDataType.DefaultAndExpand);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates the provisioning request and populates the argument list
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

            IResponse response = pageContext.GetResponse();

            if (response == null)
            {
                result = "invalid response object";
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

            ///////////////////////////////////////////////////////////////////

            Interpreter interpreter = IOP.GetOrCreate(
                ArgsOps.DoUseAutomatic(), GetPageData(),
                InterpreterPhase.Validate, true, null,
                ref result);

            if (interpreter == null)
            {
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            SettingDataType dataType = SettingDataType.DefaultAndExpand;

            string fileName = GetScriptPageSetting(
                "Database", dataType | SettingDataType.FileName);

            if (!StorageOps.InitializeAndCheckAccess(
                    interpreter, fileName, (Guid)apiKey, ref result))
            {
                returnCode = ReturnCode.Error;
                return;
            }

            ///////////////////////////////////////////////////////////////////

            object enumValue = Utility.TryParseEnum(
                typeof(ProvisionType), PageOps.GetParameter(
                request, "provisionType"), false, true,
                ref result);

            if (!(enumValue is ProvisionType))
            {
                result = "invalid provision type";
                returnCode = ReturnCode.Error;
                return;
            }

            ProvisionType provisionType = (ProvisionType)enumValue;

            switch (provisionType)
            {
                case ProvisionType.License:
                    {
                        TimeSpan? duration = ValueOps.TryParseTimeSpan(
                            PageOps.GetParameter(request, "duration"));

                        if (duration == null)
                        {
                            result = "invalid duration";
                            returnCode = ReturnCode.Error;
                            return;
                        }

                        ///////////////////////////////////////////////////////

                        //
                        // NOTE: This parameter is optional; however, the
                        //       corresponding argument to the script is
                        //       required.  This value should be true when
                        //       it is not present.
                        //
                        bool? encrypted = null;

                        if (Value.GetNullableBoolean2(
                                PageOps.GetParameter(request, "encrypted"),
                                ValueFlags.AnyBoolean, null, ref encrypted,
                                ref result) != ReturnCode.Ok)
                        {
                            returnCode = ReturnCode.Error;
                            return;
                        }

                        ///////////////////////////////////////////////////////

                        string entityName = PageOps.GetParameter(
                            request, "entityName");

                        if (String.IsNullOrEmpty(entityName))
                        {
                            result = "invalid entity name";
                            returnCode = ReturnCode.Error;
                            return;
                        }

                        ///////////////////////////////////////////////////////

                        //
                        // NOTE: This parameter is optional.
                        //
                        string templateType = PageOps.GetParameter(
                            request, "templateType");

                        ///////////////////////////////////////////////////////

                        if (arguments == null)
                            arguments = new StringList();

                        arguments.Add("provisionType");
                        arguments.Add(provisionType.ToString());
                        arguments.Add("entityName");
                        arguments.Add(entityName);
                        arguments.Add("duration");
                        arguments.Add(duration.ToString());
                        arguments.Add("encrypted");

                        if (encrypted != null)
                        {
                            arguments.Add(((bool)encrypted).ToString());
                        }
                        else
                        {
                            arguments.Add(
                                Constants.DefaultEncrypted.ToString());
                        }

                        if (!String.IsNullOrEmpty(templateType))
                        {
                            arguments.Add("templateType");
                            arguments.Add(templateType);
                        }

                        break;
                    }
                default:
                    {
                        result = "unsupported provision type";
                        returnCode = ReturnCode.Error;
                        break;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the saved page environment and cleans up the state used
        /// while processing the request.
        /// </summary>
        public override void FinalizeScriptRequest()
        {
            if (!WebEnvironmentOps.RestorePage(
                    GetPageData(), environmentClientData))
            {
                Utility.DebugTrace(
                    "FinalizeScriptRequest: could not restore " +
                    "page environment", typeof(provision).Name,
                    TracePriority.Highest | TracePriority.FromPlugin);
            }

            ///////////////////////////////////////////////////////////////////

            base.FinalizeScriptRequest();
        }
        #endregion
    }
}
