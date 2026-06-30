/*
 * Server.cs --
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
using System.Runtime.CompilerServices;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Public;
using Kapok.Components.Shared;
using Kapok.Interfaces.Public;
using Kapok.Interfaces.Shared;
using _Public = Eagle._Components.Public;
using CA = Kapok.Components.Private.ConfigurationAction;
using CAS = Kapok.Components.Private.ConfigurationActions;
using IOP = Kapok.Components.Private.InterpreterOps;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Implements the Kapok web server request pipeline (<see
    /// cref="Kapok.Interfaces.Public.IServer" />), coordinating interpreter
    /// creation and caching, license verification, script evaluation, and
    /// response generation across the server phases.
    /// </summary>
    [ObjectId("c15e0626-71f3-4d0b-bd8f-2439c85f4032")]
    internal sealed class Server : IServer
    {
        #region Private Constants
        /// <summary>
        /// The number of seconds an API-key status result is cached.
        /// </summary>
        private const int CacheStatusSeconds = 10; // TODO: Good default?
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// The object used to synchronize access to the server's shared state.
        /// </summary>
        private static readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="Server" /> instance.
        /// </summary>
        public Server()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IServer Members
        /// <summary>
        /// Traces the result of a server phase.
        /// </summary>
        /// <param name="pageData">
        /// The page data associated with the request.
        /// </param>
        /// <param name="phase">
        /// The server phase that produced the result.
        /// </param>
        /// <param name="code">
        /// The return code of the phase.
        /// </param>
        /// <param name="result">
        /// The result of the phase.
        /// </param>
        /// <param name="errorLine">
        /// The error line number, or zero when none.
        /// </param>
        /// <param name="errorCode">
        /// The error code, if any.
        /// </param>
        /// <param name="errorInfo">
        /// The error information, if any.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TraceResult(
            IScriptPageData pageData, /* in */
            ServerPhase phase,        /* in */
            ReturnCode code,          /* in */
            Result result,            /* in */
            int errorLine,            /* in */
            string errorCode,         /* in */
            string errorInfo          /* in */
            )
        {
            if ((pageData != null) && WebLicenseOps.HasFlags(
                    pageData.SecurityFlags, SecurityFlags.TraceResult,
                    true))
            {
                Utility.DebugTrace(String.Format("TraceResult: " +
                    "pageData = {0}, phase = {1}, code = {2}, " +
                    "result = {3}, errorLine = {4}, errorCode = {5}, " +
                    "errorInfo = {6}", Utility.FormatWrapOrNull(pageData),
                    Utility.FormatWrapOrNull(phase), code,
                    Utility.FormatWrapOrNull(result), errorLine,
                    Utility.FormatWrapOrNull(errorCode),
                    Utility.FormatWrapOrNull(errorInfo)),
                    typeof(Server).Name, TracePriority.Medium |
                        TracePriority.ViaWrapperFromPlugin);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Traces an error from a server phase and returns it.
        /// </summary>
        /// <param name="phase">
        /// The server phase that produced the error.
        /// </param>
        /// <param name="code">
        /// The return code of the phase.
        /// </param>
        /// <param name="result">
        /// The error result.
        /// </param>
        /// <returns>
        /// The traced error result.
        /// </returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Result TraceError(
            ServerPhase phase,        /* in */
            ReturnCode code,          /* in */
            Result result             /* in */
            )
        {
            Utility.DebugTrace(String.Format("TraceError: " +
                "phase = {0}, code = {1}, result = {2}",
                Utility.FormatWrapOrNull(phase), code,
                Utility.FormatWrapOrNull(result)),
                typeof(Server).Name, TracePriority.Highest |
                    TracePriority.ViaWrapperFromPlugin);

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Processes a script-page request through the full server pipeline,
        /// advancing through each <c>ServerPhase</c> from reading parameters
        /// to ending the response.
        /// </summary>
        /// <param name="response">
        /// The response to write output to.
        /// </param>
        /// <param name="page">
        /// The script page being processed.
        /// </param>
        /// <param name="pageData">
        /// The configuration data for the page.
        /// </param>
        /// <param name="phase">
        /// On output, receives the server phase reached.
        /// </param>
        /// <param name="fatalError">
        /// On output, non-zero when processing did not complete.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode Handler(
            IResponse response,       /* in */
            IScriptPage page,         /* in */
            IScriptPageData pageData, /* in */
            out ServerPhase phase,    /* out */
            out bool fatalError,      /* out */
            ref Result error          /* out */
            )
        {
            //
            // NOTE: This local variable is used to keep track of the phase
            //       within this method that is currently executing.
            //
            phase = ServerPhase.Parameters;

            //
            // HACK: Initially, set the fatal error flag to true; this is a
            //       signal to the caller that this method did not actually
            //       produce any output, successful or otherwise.  For that
            //       case, the caller will produce its own error output.
            //
            // NOTE: The "contract" for the fatalError parameter is that it
            //       will only be set to false if the method has completed
            //       all the way to the end.  This does not (necessarily)
            //       imply anything else, including whether or not output
            //       was actually emitted.
            //
            fatalError = true;

            if (response == null)
            {
                error = TraceError(phase,
                    ReturnCode.Error, "invalid response object");

                return ReturnCode.Error;
            }

            if (page == null)
            {
                error = TraceError(phase,
                    ReturnCode.Error, "invalid page");

                return ReturnCode.Error;
            }

            if (pageData == null)
            {
                error = TraceError(phase,
                    ReturnCode.Error, "invalid page data");

                return ReturnCode.Error;
            }

            bool quiet = false;
            Interpreter interpreter = null;
            ReturnCode code = ReturnCode.Ok;
            Result result = null;
            int errorLine = 0;
            Result errorCode = null;
            Result errorInfo = null;

            phase = ServerPhase.StartResponse;

            response.Start(null);

            try
            {
                phase = ServerPhase.Configure;

                page.ConfigureScriptRequest(); /* throw */

                if (!pageData.Enabled)
                {
                    result = "page disabled via configuration";
                    code = ReturnCode.Error;

                    return code;
                }

                string certificateFileName = null;
                IClientData certificateClientData = null;

                if (pageData.LicensingEnabled)
                {
                    //
                    // NOTE: The method called here is guaranteed
                    //       to be 100% thread-safe and idempotent.
                    //
                    phase = ServerPhase.Certificates;

                    /* IGNORED */
                    WebLicenseOps.ConfigureCertificates(
                        GetType(), pageData.SecurityFlags,
                        ref certificateFileName,
                        ref certificateClientData);
                }

                phase = ServerPhase.PreValidate;

                bool? refresh = null;

                page.PreValidateScriptRequest(
                    ref refresh, ref code, ref result);

                if (code != ReturnCode.Ok)
                {
                    if (code == ReturnCode.Return)
                    {
                        phase = ServerPhase.PreValidateResponse;
                        code = ReturnCode.Ok;
                    }

                    return code;
                }

                bool useThreadId = true; // TODO: Shared pool instead?

                phase = ServerPhase.Freshness;

                if (pageData.CreateInterpreter && IOP.WillBeCreatedIfRequested(
                        pageData, InterpreterPhase.Server, useThreadId, refresh))
                {
                    //
                    // HACK: Use of "ConfigurationActions" within this method
                    //       indicates that the wrapped method call(s) perform
                    //       "one-time" configuration actions that are either
                    //       per-AppDomain -OR- per-process, and do not need
                    //       to be repeated for each request.
                    //
                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        phase = ServerPhase.PackagePaths;

                        if (!CAS.IsDone(CA.DisablePackageRootPath) &&
                            WebScriptOps.DisablePackageRootPath()) /* throw */
                        {
                            /* IGNORED */
                            CAS.TryMarkDone(CA.DisablePackageRootPath);
                        }

                        phase = ServerPhase.ScriptLibrary;

                        if (!CAS.IsDone(CA.ConfigureLibrary) &&
                            WebScriptOps.ConfigureLibrary(false)) /* throw */
                        {
                            /* IGNORED */
                            CAS.TryMarkDone(CA.ConfigureLibrary);
                        }

                        phase = ServerPhase.AutoPath;

                        if (!CAS.IsDone(CA.ConfigureAutoPath) &&
                            WebScriptOps.ConfigureAutoPath(false)) /* throw */
                        {
                            /* IGNORED */
                            CAS.TryMarkDone(CA.ConfigureAutoPath);
                        }
                    }
                }

                lock (syncRoot) /* TRANSACTIONAL */
                {
                    phase = ServerPhase.SQLite;

                    if (!CAS.IsDone(CA.ConfigureSQLiteBaseDirectory) &&
                        WebStorageOps.ConfigureSQLiteBaseDirectory()) /* throw */
                    {
                        /* IGNORED */
                        CAS.TryMarkDone(CA.ConfigureSQLiteBaseDirectory);
                    }
                }

                if (WebLicenseOps.HasFlags(
                        pageData.SecurityFlags, SecurityFlags.DumpEnvironment,
                        true))
                {
                    phase = ServerPhase.DumpEnvironment;

                    /* NO RESULT */
                    WebTraceOps.DumpEnvironment();
                }

                phase = ServerPhase.Validate;

                StringList arguments = null;

                page.ValidateScriptRequest(
                    ref arguments, ref code, ref result);

                if (code != ReturnCode.Ok)
                {
                    if (code == ReturnCode.Return)
                    {
                        phase = ServerPhase.ValidateResponse;
                        code = ReturnCode.Ok;
                    }

                    return code;
                }

                phase = ServerPhase.Interpreter;

#if !DEBUG
            retry:
#endif

                bool created;

                using (interpreter = IOP.GetOrCreate(ArgsOps.DoUseAutomatic(),
                        pageData, InterpreterPhase.Server, useThreadId, refresh,
                        out created, ref result))
                {
                    TracePriority priority = IOP.ShouldTraceCacheStatus(
                        CacheStatusSeconds, created, useThreadId) ?
                            TracePriority.MediumHigh : TracePriority.MediumLow;

                    Utility.DebugTrace(String.Format(
                        "Handler: Cache status: {0}", Utility.FormatWrapOrNull(
                        IOP.GetCacheStatus())), typeof(Server).Name, priority |
                        TracePriority.FromPlugin);

                    if (pageData.CreateInterpreter && (interpreter == null))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

#if !DEBUG
                    //
                    // HACK: This is a huge "hack".  It prevents a disposed (and
                    //       wrongly still cached?) interpreter from being used;
                    //       instead, it will force interpreter creation.
                    //
                    // WARNING: This block of code is officially considered to be
                    //          a temporary production fail-safe.  At some point,
                    //          it will be removed (i.e. once the cache is 100%
                    //          reliable and fully battle tested).  Please do not
                    //          rely on this behavior.
                    //
                    if ((refresh == null) &&
                        pageData.CreateInterpreter && interpreter.Disposed)
                    {
                        Utility.DebugTrace(String.Format(
                            "Handler: REFRESH, INTERPRETER {0} IS DISPOSED?",
                            interpreter.IdNoThrow), typeof(Server).Name,
                            TracePriority.Highest | TracePriority.FromPlugin);

                        refresh = true;
                        goto retry;
                    }
#endif

                    if (created && pageData.LicensingEnabled)
                    {
                        int securityLevel = pageData.SecurityLevel;
                        bool isolated = false;

                        if (securityLevel > 0)
                        {
                            phase = ServerPhase.SdkSecurity;

                            code = WebLicenseOps.ConfigureSecurity(
                                interpreter, certificateFileName,
                                pageData.SecurityFlags,
                                (securityLevel > 1), ref isolated,
                                ref result); /* throw */

                            if (code != ReturnCode.Ok)
                                return code;
                        }

                        phase = ServerPhase.Licensed;

                        code = WebLicenseOps.VerifyCertificate(
                            interpreter, certificateClientData,
                            isolated, ref result); /* throw */

                        if (code != ReturnCode.Ok)
                            return code;
                    }

                    if ((interpreter != null) &&
                        (arguments != null))
                    {
                        phase = ServerPhase.SetArguments;

                        Utility.DebugTrace(String.Format(
                            "Handler: Setting arguments: {0}",
                            arguments), typeof(Server).Name,
                            TracePriority.MediumHigh |
                                TracePriority.FromPlugin);

                        code = interpreter.SetArguments(
                            arguments, ref result);

                        if (code != ReturnCode.Ok)
                            return code;
                    }

                    string text = pageData.Setup;

                    if ((interpreter != null) &&
                        !String.IsNullOrEmpty(text))
                    {
                        phase = ServerPhase.EvaluateSetup;

                        code = interpreter.EvaluateScript(
                            text, ref result, ref errorLine);

#if DEBUG
                        WebScriptOps.CopyErrorInformation(
                            interpreter, code, phase, false,
                            ref errorCode, ref errorInfo);
#endif

                        if (code != ReturnCode.Ok)
                            return code;
                    }

                    string fileName = pageData.FileName;

                    if (String.IsNullOrEmpty(fileName))
                    {
                        //
                        // HACK: This is not actually a scripted
                        //       page?  Instead, it handles all
                        //       requests directly.  Generally,
                        //       it also emits the response and
                        //       a default response is disabled
                        //       using the quiet flag.  However,
                        //       since the called page may still
                        //       require access to the (active)
                        //       interpreter stack, make sure to
                        //       maintain it via push / pop.
                        //
                        if (interpreter != null)
                        {
                            //
                            // NOTE: This method is guaranteed to
                            //       have transactional semantics;
                            //       if the method does not throw
                            //       an exception, the interpreter
                            //       will be added to the active
                            //       interpreter stack.
                            //
                            /* IGNORED */
                            interpreter.PushActive(null);
                        }

                        try
                        {
                            phase = ServerPhase.Handle;

                            page.HandleScriptRequest(
                                interpreter, ref quiet,
                                ref code, ref result);
                        }
                        finally
                        {
                            if (interpreter != null)
                            {
                                /* IGNORED */
                                interpreter.PopActive();
                            }
                        }

                        return code;
                    }

                    phase = ServerPhase.CheckFile;

                    if (!File.Exists(fileName))
                    {
                        result = "script file not found";
                        code = ReturnCode.Error;

                        return code;
                    }

                    phase = ServerPhase.BuildResponse;

                    if (pageData.Blocks)
                    {
                        //
                        // NOTE: Technically, the ReadScriptBlocksFile
                        //       method does not require an interpreter;
                        //       however, the Process method does.  So,
                        //       it seems better to fail early here.
                        //
                        phase = ServerPhase.ValidateBlocks;

                        if (interpreter == null)
                        {
                            result = "no blocks read: invalid interpreter";
                            code = ReturnCode.Error;

                            return code;
                        }

                        phase = ServerPhase.ReadBlocks;

                        string blockText = null;

                        page.ReadScriptBlocksFile(
                            interpreter, fileName, ref blockText);

                        if (blockText == null)
                        {
                            result = "script block text is null";
                            code = ReturnCode.Error;

                            return code;
                        }

                        phase = ServerPhase.ProcessBlocks;

                        StringBuilder output = null;
                        ResultList errors = null;

                        code = _Public.ScriptBlocks.Process(
                            interpreter, blockText,
                            pageData.BlockFlags,
                            ref output, ref errors);

#if DEBUG
                        WebScriptOps.CopyErrorInformation(
                            interpreter, code, phase, false,
                            ref errorCode, ref errorInfo);
#endif

                        if (code == ReturnCode.Ok)
                            result = output;
                        else
                            result = errors;
                    }
                    else if (interpreter != null)
                    {
                        phase = ServerPhase.EvaluateFile;

                        code = interpreter.EvaluateFile(
                            fileName, ref result, ref errorLine);

                        WebScriptOps.CopyErrorInformation(
                            interpreter, code, phase, false,
                            ref errorCode, ref errorInfo);
                    }
                    else
                    {
                        result = "no script evaluated: invalid interpreter";
                        code = ReturnCode.Error;
                    }

                    if (code == ReturnCode.Ok)
                        phase = ServerPhase.EndResponse;
                }
            }
            catch (Exception e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            finally
            {
                //
                // HACK: First, if there was any kind of error, perform
                //       extra error handling configured for this server
                //       instance.
                //
                if (code != ReturnCode.Ok)
                {
                    /* IGNORED */
                    TraceError(phase, code, result);
                }

                //
                // HACK: Always trace the page data and its result so it
                //       can end up in the log file, if any.
                //
                TraceResult(
                    pageData, phase, code, result, errorLine, errorCode,
                    errorInfo);

                //
                // HACK: In quiet mode, which is generally only used for
                //       non-scripted pages, skip emitting the response,
                //       as the managed request handler is responsible.
                //
                if (!quiet)
                {
                    response.Write(page.FormatScriptResponse(
                        code, result, errorLine));

#if DEBUG
                    PageOps.CheckWriteErrorInformation(
                        response, code, errorCode, errorInfo);
#endif
                }

                //
                // NOTE: Finally, cleanup any resources that were created
                //       within the ConfigureScriptRequest method, et al.
                //       In theory, this method call can throw exceptions;
                //       however, that is fine since the caller will (still)
                //       see the fatal error flag is set.
                //
                page.FinalizeScriptRequest(); /* throw */

                //
                // NOTE: At this point, we should be done sending output to
                //       the client; therefore, flush all buffered output,
                //       if any, and complete the request.  In theory, this
                //       method call can throw exceptions; however, that is
                //       fine since the caller will (still) see the fatal
                //       error flag is set.
                //
                response.End(); /* throw */

                //
                // HACK: Do we want to dispose of thread-specific contexts
                //       for the interpreter?  This will prevent them from
                //       being (randomly?) garbage collected later, when a
                //       thread is transient (?) and never seen again.
                //
                if ((interpreter != null) && WebLicenseOps.HasFlags(
                        pageData.SecurityFlags, SecurityFlags.DisposeThread,
                        true))
                {
                    try
                    {
                        if (!interpreter.DisposeThread()) /* throw */
                        {
                            Utility.DebugTrace(String.Format(
                                "Handler: Could not dispose thread for " +
                                "interpreter {0}", interpreter.IdNoThrow),
                                typeof(Server).Name, TracePriority.Higher |
                                TracePriority.FromPlugin);
                        }
                    }
                    catch (Exception e)
                    {
                        Utility.DebugTrace(
                            e, typeof(Server).Name,
                            TracePriority.Highest |
                                TracePriority.FromPlugin);
                    }
                }

                //
                // HACK: At this point, we know some output was produced by
                //       this method.  Set the fatal error flag to false in
                //       order to prevent our caller from emitting its own
                //       "fatal error" output.
                //
                fatalError = false;

                //
                // NOTE: Possibly cleanup stale interpreters that may be in
                //       the interpreter cache at this point, even when not
                //       owned by the by this thread.  This action will not
                //       be performed if the current "page request" did not
                //       require an interpreter to be created or used.
                //
                if (pageData.CreateInterpreter)
                {
                    //
                    // HACK: Hard-coded queue timeout of about three seconds.
                    //
                    IOP.MaybeCleanupStale(
                        pageData.CacheSeconds, pageData.SecurityFlags);
                }
            }

            return code;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(Server).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            if (!disposed)
            {
                //if (disposing)
                //{
                //    ////////////////////////////////////
                //    // dispose managed resources here...
                //    ////////////////////////////////////
                //}

                //////////////////////////////////////
                // release unmanaged resources here...
                //////////////////////////////////////

                //
                // NOTE: This object is now disposed.
                //
                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        ~Server()
        {
            Dispose(false);
        }
        #endregion
    }
}
