/*
 * StatusPage.cs --
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
using System.Web;
#endif

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.AspNetCore.Http;
#endif

using Eagle._Attributes;
using Kapok.Components.Shared;
using Kapok.Interfaces.Shared;

namespace Kapok
{
    /// <summary>
    /// Implements a script page that produces a fixed HTTP status code and
    /// message response, used for error and status pages.
    /// </summary>
    [ObjectId("601f25b8-bbb1-4482-9342-595cf263988b")]
    public class StatusPage : ScriptPage
    {
        #region Private Data
        /// <summary>
        /// The HTTP status code returned by this page.
        /// </summary>
        private HttpStatusCode statusCode;
        /// <summary>
        /// The status message returned by this page.
        /// </summary>
        private string message;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="StatusPage" /> with the specified
        /// status code and message.
        /// </summary>
        /// <param name="statusCode">
        /// The HTTP status code to return.
        /// </param>
        /// <param name="message">
        /// The status message to return.
        /// </param>
        public StatusPage(
            HttpStatusCode statusCode, /* in */
            string message             /* in */
            )
            : base()
        {
            this.statusCode = statusCode;
            this.message = message;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Platform Abstraction Methods
        /// <summary>
        /// Runs the server request handler for this status page, writing the
        /// configured status code and message.
        /// </summary>
        /// <param name="context">
        /// The request context to process.
        /// </param>
        protected override void ExecuteServerHandler(
            HttpContext context /* in: OPTIONAL */
            )
        {
            using (IPageContext pageContext = CreatePageContext(context))
            {
                if (pageContext != null)
                {
                    IResponse response = pageContext.GetResponse();

                    if (response != null)
                    {
                        response.Start(null);
                        response.Write(message, statusCode);
                        response.End();
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "invalid page context object");
                }
            }
        }
        #endregion
    }
}
