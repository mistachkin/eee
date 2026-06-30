/*
 * EntryPoint.cs --
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

using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// Provides the assembly entry point and web hosting configuration for
    /// the standalone Kapok web server host.
    /// </summary>
#if KAPOK
    [ObjectId("5820236e-ca92-49d5-af8f-32aaaa923757")]
#else
    [Guid("5820236e-ca92-49d5-af8f-32aaaa923757")]
#endif
    internal class EntryPoint
    {
        #region Assembly Entry Point Method
        /// <summary>
        /// Attempts to configure start the ASP.NET Core web server in the
        /// current process.
        /// </summary>
        /// <param name="args">
        /// The command line arguments.
        /// </param>
        /// <returns>
        /// Zero upon success; otherwise, non-zero.
        /// </returns>
        [STAThread()] /* WinForms */
#if NO_MAIN
        public static int WebHostMain<T>(
#else
        private static int Main(
#endif
            string[] args /* in */
            )
#if NO_MAIN
            where T : class
#endif
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder(args);

#if NO_MAIN
            builder.UseStartup<T>();
#else
            builder.UseStartup<EntryPoint>();
#endif
            builder.Build().Run();

            return 0;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to configure the web hosting environment for the ASP.NET
        /// Core runtime.
        /// </summary>
        /// <param name="app">
        /// This provides mechanisms to configure the web application request
        /// pipeline.
        /// </param>
        /// <param name="env">
        /// This provides information about the web hosting environment that
        /// a web application is running in.
        /// </param>
        public virtual void Configure(
            IApplicationBuilder app, /* in */
            IHostingEnvironment env  /* in */
            )
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.Run((context) => {
                TestPage testPage = new TestPage();
                return testPage.RequestDelegate(context);
            });
        }
    }
}
