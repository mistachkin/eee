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

using System;

#if !NET_STANDARD_20
using System.Diagnostics;
#endif

#if !NET_STANDARD_20
using System.Web;
#endif

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.AspNetCore.Http;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using Kapok.Components.Public;
using Kapok.Interfaces.Public;
using Kapok.Interfaces.Shared;
using CA = Kapok.Components.Private.ConfigurationAction;
using CAS = Kapok.Components.Private.ConfigurationActions;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok
{
    //
    // HACK: For the web pages to work properly when running on ASP.NET,
    //       they must (eventually) derive from the System.Web.UI.Page
    //       class.
    //
    /// <summary>
    /// Provides the base implementation of a Kapok script page (<see
    /// cref="Kapok.Interfaces.Public.IScriptPage" />).  It manages the page
    /// context and configuration data, sets up logging and tracing, and
    /// implements the standard request lifecycle (configure, pre-validate,
    /// validate, read/handle, and format response).
    /// </summary>
    [ObjectId("27cf84e0-851d-4b35-a204-48e11855f8e1")]
    public class ScriptPage : BasePage, IScriptPage
    {
        #region Private Static Data
#if !NET_STANDARD_20
        /// <summary>
        /// The trace listener installed for diagnostic output, if any.
        /// </summary>
        private static TraceListener listener;

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// The log-file trace listener installed for diagnostic output, if
        /// any.
        /// </summary>
        private static TraceListener logListener;
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The page context for the current request.
        /// </summary>
        private IPageContext pageContext;
        /// <summary>
        /// The configuration data for the current page.
        /// </summary>
        private IScriptPageData pageData;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
#if !NET_STANDARD_20
        /// <summary>
        /// Sets up logging and tracing for the page, optionally directing
        /// output to the console.
        /// </summary>
        /// <param name="useConsole">
        /// Non-zero to also direct output to the console.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool MaybeSetupLoggingAndTracing(
            bool useConsole /* in */
            )
        {
            int count = 0;

            lock (WebTraceOps.GetSyncRoot()) /* TRANSACTIONAL */
            {
                if (!CAS.IsDone(CA.MaybeConfigureSettings) &&
                    WebTraceOps.MaybeConfigureSettings())
                {
                    /* IGNORED */
                    CAS.TryMarkDone(CA.MaybeConfigureSettings);

                    count++;

#if CONSOLE
                    if (useConsole)
                    {
                        Console.WriteLine(
                            "{0}: The trace settings were configured.",
                            typeof(ScriptPage).FullName);
                    }
#endif
                }
            }

#if TEST
            lock (WebTraceOps.GetSyncRoot()) /* TRANSACTIONAL */
            {
                if (!CAS.IsDone(CA.MaybeSetupLogFile) &&
                    WebTraceOps.MaybeSetupLogFile(ref logListener))
                {
                    /* IGNORED */
                    CAS.TryMarkDone(CA.MaybeSetupLogFile);

                    count++;

#if CONSOLE
                    if (useConsole && (logListener != null))
                    {
                        Console.WriteLine(
                            "{0}: The trace log file was setup.",
                            typeof(ScriptPage).FullName);
                    }
#endif
                }
            }
#endif

            lock (WebTraceOps.GetSyncRoot()) /* TRANSACTIONAL */
            {
                if (!CAS.IsDone(CA.MaybeSetupListeners) &&
                    WebTraceOps.MaybeSetupListeners(ref listener))
                {
                    /* IGNORED */
                    CAS.TryMarkDone(CA.MaybeSetupListeners);

                    count++;

#if CONSOLE
                    if (useConsole && (listener != null))
                    {
                        Console.WriteLine(
                            "{0}: The trace listeners were setup.",
                            typeof(ScriptPage).FullName);
                    }
#endif
                }
            }

            return (count > 0);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Sets the page context and configuration data for the current
        /// request.
        /// </summary>
        /// <param name="pageContext">
        /// The page context to use.
        /// </param>
        /// <param name="pageData">
        /// The page configuration data to use.
        /// </param>
        private void SetContextAndPageData(
            IPageContext pageContext, /* in */
            IScriptPageData pageData        /* in */
            )
        {
            this.pageContext = pageContext;
            this.pageData = pageData;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IScriptPage Members
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
        public virtual IPageContext GetPageContext()
        {
            return pageContext;
        }

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
        public virtual IScriptPageData GetPageData()
        {
            return pageData;
        }

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
        /// </summary>
        /// <param name="default">
        /// The default page name to use when one cannot be derived.
        /// </param>
        /// <returns>
        /// The base page name.
        /// </returns>
        public virtual string GetScriptPageName(
            string @default /* in */
            )
        {
            return PageOps.GetScriptName(GetType(), @default);
        }

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
        public virtual string GetScriptPageSetting(
            string settingName,      /* in */
            SettingDataType dataType /* in */
            )
        {
            int foundIndex = Index.Invalid;

            return GetScriptPageSetting(
                settingName, dataType, ref foundIndex);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called during page load in order to obtain the
        //       value of a configuration setting for the page.
        //
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
        public virtual string GetScriptPageSetting(
            string settingName,       /* in */
            SettingDataType dataType, /* in */
            ref int foundIndex        /* in, out */
            )
        {
            return WebSettingsOps.GetPage(
                GetScriptPageName(null), settingName, dataType,
                ref foundIndex);
        }

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
        public virtual void ConfigureScriptRequest()
        {
            // do nothing.
        }

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
        public virtual void PreValidateScriptRequest(
            ref bool? refresh,         /* out */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
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

            if (Value.GetNullableBoolean2(PageOps.GetParameter(
                    request, "refresh"), ValueFlags.AnyBoolean, null,
                    ref refresh, ref result) != ReturnCode.Ok)
            {
                returnCode = ReturnCode.Error;
                return;
            }
        }

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
        public virtual void ValidateScriptRequest(
            ref StringList arguments,  /* in, out */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
            // do nothing.
        }

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
        public virtual void ReadScriptBlocksFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref string blockText     /* out */
            )
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called by the page event handler in order to
        //       handle a non-script page (i.e. one with hard-wired managed
        //       code backing the page, e.g. the variable storage server).
        //
        /// <summary>
        /// Handles a non-script page (one backed by managed code).
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
        public virtual void HandleScriptRequest(
            Interpreter interpreter,   /* in */
            ref bool quiet,            /* in, out */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
            returnCode = ReturnCode.Error;
            result = "script page not configured";
        }

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
        public virtual string FormatScriptResponse(
            ReturnCode returnCode, /* in */
            Result result,         /* in */
            int errorLine          /* in */
            )
        {
            return Utility.FormatResult(returnCode, result, errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method is called to cleanup any (temporary) state used
        //       while processing the request.
        //
        /// <summary>
        /// Cleans up any temporary state used while processing the request.
        /// </summary>
        public virtual void FinalizeScriptRequest()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Platform Abstraction Methods
        /// <summary>
        /// Runs the server request handler for this page within the supplied
        /// context.
        /// </summary>
        /// <param name="context">
        /// The request context to process.
        /// </param>
        protected override void ExecuteServerHandler(
            HttpContext context /* in: OPTIONAL */
            ) /* ENTRY-POINT */
        {
#if !NET_STANDARD_20
            /* IGNORED */
            MaybeSetupLoggingAndTracing(WebTraceOps.ShouldUseConsole());
#endif

            using (IPageContext pageContext = CreatePageContext(context))
            {
                ServerPhase phase;
                bool fatalError;
                ReturnCode code;
                Result error = null;

                if (pageContext != null)
                {
                    using (IScriptPageData pageData = new ScriptPageData())
                    {
                        using (IServer server = new Server())
                        {
                            try
                            {
                                SetContextAndPageData(
                                    pageContext, pageData);

                                code = server.Handler(
                                    pageContext.GetResponse(),
                                    this, pageData, out phase,
                                    out fatalError, ref error);
                            }
                            finally
                            {
                                SetContextAndPageData(null, null);
                            }
                        }
                    }
                }
                else
                {
                    phase = ServerPhase.Skipped; /* nothing done */
                    fatalError = true; /* no output emitted */

                    error = "invalid page context object";
                    code = ReturnCode.Error;
                }

                //
                // HACK: Upon failure -AND- only if some output was
                //       not already emitted by the handler, emit a
                //       complaint.
                //
                if ((code != ReturnCode.Ok) && fatalError)
                {
                    Utility.Complain(null, code, error);

                    Utility.DebugTrace(
                        "ExecuteServerHandler", "FATAL ERROR",
                        typeof(ScriptPage).Name, TracePriority.Highest |
                        TracePriority.FromPlugin, false, "phase", phase,
                        "code", code, "error", error);
                }
            }
        }
        #endregion
    }
}
