/*
 * Enumerations.cs --
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

namespace Kapok.Components.Public
{
    /// <summary>
    /// Identifies the phases of the Kapok web server's request-processing
    /// pipeline, used to track progress and report where a request succeeded
    /// or failed.
    /// </summary>
    [ObjectId("c7fd7a49-eabe-4edb-970b-04ee918cf061")]
    public enum ServerPhase : ulong
    {
        /// <summary>
        /// No phase.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid phase; do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The request processing was skipped.
        /// </summary>
        Skipped = 0x100,
        /// <summary>
        /// Reading the request parameters.
        /// </summary>
        Parameters = 0x1000,
        /// <summary>
        /// Starting the response.
        /// </summary>
        StartResponse = 0x2000,
        /// <summary>
        /// Loading the page configuration settings.
        /// </summary>
        Configure = 0x4000,
        /// <summary>
        /// Discovering and configuring license certificates.
        /// </summary>
        Certificates = 0x8000,
        /// <summary>
        /// Performing pre-validation (may return early).
        /// </summary>
        PreValidate = 0x10000,
        /// <summary>
        /// Building the early response produced by pre-validation.
        /// </summary>
        PreValidateResponse = 0x20000,
        /// <summary>
        /// Checking whether a fresh interpreter is needed.
        /// </summary>
        Freshness = 0x40000,
        /// <summary>
        /// Configuring the package paths.
        /// </summary>
        PackagePaths = 0x80000,
        /// <summary>
        /// Configuring the script library path.
        /// </summary>
        ScriptLibrary = 0x100000,
        /// <summary>
        /// Configuring the auto-path.
        /// </summary>
        AutoPath = 0x200000,
        /// <summary>
        /// Configuring the SQLite base directory.
        /// </summary>
        SQLite = 0x400000,
        /// <summary>
        /// Dumping the environment variables to the trace listeners.
        /// </summary>
        DumpEnvironment = 0x800000,
        /// <summary>
        /// Performing full request validation and extracting arguments.
        /// </summary>
        Validate = 0x1000000,
        /// <summary>
        /// Building the response produced by validation (may return early).
        /// </summary>
        ValidateResponse = 0x2000000,
        /// <summary>
        /// Getting or creating the cached interpreter.
        /// </summary>
        Interpreter = 0x4000000,
        /// <summary>
        /// Loading the Harpy/Badge SDK security plugins.
        /// </summary>
        SdkSecurity = 0x8000000,
        /// <summary>
        /// Verifying the license for a new interpreter.
        /// </summary>
        Licensed = 0x10000000,
        /// <summary>
        /// Setting the script arguments.
        /// </summary>
        SetArguments = 0x20000000,
        /// <summary>
        /// Evaluating the setup script.
        /// </summary>
        EvaluateSetup = 0x40000000,
        /// <summary>
        /// Handling a non-scripted page.
        /// </summary>
        Handle = 0x80000000,
        /// <summary>
        /// Checking the configured script file.
        /// </summary>
        CheckFile = 0x100000000,
        /// <summary>
        /// Building the response.
        /// </summary>
        BuildResponse = 0x200000000,
        /// <summary>
        /// Validating the HTML/script blocks.
        /// </summary>
        ValidateBlocks = 0x400000000,
        /// <summary>
        /// Reading the HTML/script blocks.
        /// </summary>
        ReadBlocks = 0x800000000,
        /// <summary>
        /// Processing the HTML/script blocks.
        /// </summary>
        ProcessBlocks = 0x1000000000,
        /// <summary>
        /// Evaluating the configured script file.
        /// </summary>
        EvaluateFile = 0x2000000000,
        /// <summary>
        /// Ending the response (flushing output).
        /// </summary>
        EndResponse = 0x4000000000
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags controlling the security and diagnostic behavior of the server
    /// request handler, such as plugin isolation, per-request thread cleanup,
    /// and various trace outputs.
    /// </summary>
    [Flags()]
    [ObjectId("c6d1b05a-72b1-4a4b-85e5-0a437ad4b13b")]
    public enum SecurityFlags
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,             /* No special handling. */
        /// <summary>
        /// Invalid; do not use.
        /// </summary>
        Invalid = 0x1,          /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Use isolation when loading the Harpy and Badge plugins.
        /// </summary>
        UseIsolation = 0x10,     /* Use isolation when loading the Harpy and
                                  * Badge plugins. */
        /// <summary>
        /// Disable isolation after loading the Harpy and Badge plugins.
        /// </summary>
        DisableIsolation = 0x20, /* Disable isolation after loading the Harpy
                                  * and Badge plugins. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Dispose of the interpreter thread context data after each request.
        /// </summary>
        DisposeThread = 0x100,     /* Dispose of the interpreter thread
                                    * context data after each request
                                    * (WARNING: may be expensive?). */
        /// <summary>
        /// Dump all environment variables to the trace listeners.
        /// </summary>
        DumpEnvironment = 0x200,   /* Dump all environment variables to trace
                                    * listeners (i.e. the log file, etc). */
        /// <summary>
        /// Trace the final license certificate file name selection.
        /// </summary>
        TraceCertificates = 0x400, /* Trace the "final" license certificate
                                    * file name selection , etc.  Without
                                    * this flag set, only the associated
                                    * errors, if any, will be traced. */
        /// <summary>
        /// Trace the final page result(s).
        /// </summary>
        TraceResult = 0x800,       /* Trace the "final" page result(s). */
        /// <summary>
        /// Trace the stale interpreter cleanup results.
        /// </summary>
        TraceCleanup = 0x1000,     /* Trace the "stale" interpreter cleanup
                                    * results. */

        ///////////////////////////////////////////////////////////////////////

#if DEBUG
        /// <summary>
        /// The environment dump, enabled in debug builds.
        /// </summary>
        MaybeDumpEnvironment = DumpEnvironment,
        /// <summary>
        /// The certificate tracing, enabled in debug builds.
        /// </summary>
        MaybeTraceCertificates = TraceCertificates,
        /// <summary>
        /// The result tracing, enabled in debug builds.
        /// </summary>
        MaybeTraceResult = TraceResult,
        /// <summary>
        /// The cleanup tracing, enabled in debug builds.
        /// </summary>
        MaybeTraceCleanup = TraceCleanup,
#else
        /// <summary>
        /// The environment dump, disabled in non-debug builds.
        /// </summary>
        MaybeDumpEnvironment = None,
        /// <summary>
        /// The certificate tracing, disabled in non-debug builds.
        /// </summary>
        MaybeTraceCertificates = None,
        /// <summary>
        /// The result tracing, disabled in non-debug builds.
        /// </summary>
        MaybeTraceResult = None,
        /// <summary>
        /// The cleanup tracing, disabled in non-debug builds.
        /// </summary>
        MaybeTraceCleanup = None,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default security flags (none).
        /// </summary>
        Default = None
    }
}
