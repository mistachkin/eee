/*
 * EntryPoint.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if CONSOLE
using System;
#endif

using System.Diagnostics;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Eagle._Attributes;
using Eagle._Components.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Public;
using CA = Kapok.Components.Private.ConfigurationAction;
using CAS = Kapok.Components.Private.ConfigurationActions;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides the process entry point and ASP.NET Core startup configuration
    /// for hosting the Kapok web server as a standalone application.
    /// </summary>
    [ObjectId("3e588ced-a5ed-4e97-9247-03eca0751a29")]
    internal sealed class EntryPoint
    {
        #region Private Data
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
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The application entry point.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments.
        /// </param>
        private static void Main(
            string[] args /* in */
            )
        {
            /* IGNORED */
            MaybeSetupLoggingAndTracing(WebTraceOps.ShouldUseConsole());

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder(args);

            builder.UseStartup<EntryPoint>();
            builder.Build().Run();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up logging and tracing, optionally directing output to the
        /// console.
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
                            typeof(EntryPoint).FullName);
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
                            typeof(EntryPoint).FullName);
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
                            typeof(EntryPoint).FullName);
                    }
#endif
                }
            }

            return (count > 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the ASP.NET Core application request pipeline.
        /// </summary>
        /// <param name="app">
        /// The application builder to configure.
        /// </param>
        /// <param name="env">
        /// The hosting environment.
        /// </param>
        public void Configure(
            IApplicationBuilder app, /* in */
            IHostingEnvironment env  /* in */
            )
        {
            /* IGNORED */
            Utility.MaybeSetBinaryPath(env.ContentRootPath, false);

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.Run(async (context) => {
                IScriptPage scriptPage = null;
                Result error = null;

                if (WebPageOps.CreateScript(AspNetOps.GetPath(
                        AspNetOps.GetHttpRequest(context)),
                        ref scriptPage, ref error) != ReturnCode.Ok)
                {
                    Utility.Complain(null, ReturnCode.Error, error);
                    scriptPage = new ScriptPage();
                }

                await scriptPage.RequestDelegate(context);
            });
        }
    }
}
