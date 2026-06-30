/*
 * BasePage.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !NET_STANDARD_20
using System;
#endif

#if !NET_STANDARD_20 && OBFUSCATION
using System.Reflection;
#endif

#if !KAPOK
using System.Runtime.InteropServices;
#endif

#if NET_STANDARD_20
using System.Threading.Tasks;
#endif

#if !NET_STANDARD_20
using System.Web;
using System.Web.UI;
#endif

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.AspNetCore.Http;
#endif

#if KAPOK
using Eagle._Attributes;
#endif

using Kapok.Interfaces.Shared;

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class provides a "base" class that hides the differences
    /// between the "legacy" ASP.NET stack and the ASP.NET Core stack.
    /// For pages to work properly when running on "legacy" ASP.NET,
    /// they must (eventually) derive from the "Page" class, which
    /// this class does.  All web pages should derive from this class
    /// and implement their functionality by adding an override of the
    /// <see cref="ExecuteServerHandler" /> method.
    /// </summary>
#if KAPOK
    [ObjectId("2657284e-ea33-409c-8e4b-e8b249093edd")]
#else
    [Guid("2657284e-ea33-409c-8e4b-e8b249093edd")]
#endif
    public class BasePage
#if !NET_STANDARD_20
        : Page
#endif
    {
        #region Platform Abstraction Methods
        /// <summary>
        /// Creates a logical page context for use by the current web page
        /// instance.  This method is virtual; however, it should not be
        /// overridden unless there is a very good, specific reason.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The newly created logical page context -OR- null if it could not
        /// be created.
        /// </returns>
        protected virtual IPageContext CreatePageContext(
            HttpContext context /* in: OPTIONAL */
            )
        {
            return AspNetOps.CreatePageContext(context, null);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method performs the whatever work is needed to render the
        /// response.  This method should always be overridden in dervied
        /// classes.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may be null.
        /// </param>
        protected virtual void ExecuteServerHandler(
            HttpContext context /* in: OPTIONAL */
            ) /* ENTRY-POINT */
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region RequestDelegate Methods (ASP.NET Core)
#if NET_STANDARD_20
        /// <summary>
        /// This method is a logical trampoline that connects the asynchronous
        /// machinery used by ASP.NET Core to the synchronous callback used by
        /// the primary web page rendering method, which is
        /// <see cref="ExecuteServerHandler" />.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The <see cref="Task" /> instance, which can be awaited, and that
        /// is being used to service the current request.
        /// </returns>
        public virtual Task RequestDelegate(
            HttpContext context /* in: OPTIONAL */
            )
        {
            //
            // NOTE: Return Task directly so callers can await it without
            //       forcing an async state machine on this method.  The
            //       synchronous ExecuteServerHandler runs on a thread-pool
            //       thread inside Task.Run.
            //
            return Task.Run(() => {
                /* NO RESULT */
                ExecuteServerHandler(context);
            });
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Page Event Handlers (ASP.NET Web Forms)
#if !NET_STANDARD_20
        /// <summary>
        /// This method is used with the legacy ASP.NET stack in order to
        /// receive requests raised via the <see cref="Control.Load" />
        /// event.
        /// </summary>
        /// <param name="sender">
        /// The object instance that originated this event, if any.  This
        /// parameter is not used and may be null.
        /// </param>
        /// <param name="e">
        /// The extra event arguments associated with this event, if any.
        /// This parameter is not used and may be null.
        /// </param>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        protected virtual void Page_Load(
            object sender, /* in: NOT USED */
            EventArgs e    /* in: NOT USED */
            )
        {
            /* NO RESULT */
            ExecuteServerHandler(null);
        }
#endif
        #endregion
    }
}
