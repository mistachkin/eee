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
using Eagle._Attributes;
using Eagle._Components.Public;
using Kapok.Components.Public;
using Kapok.Interfaces.Shared;

namespace Kapok.Interfaces.Public
{
    /// <summary>
    /// Represents the Kapok web server that processes a script-page request
    /// through its multi-phase pipeline and traces results and errors.
    /// </summary>
    [ObjectId("671cf1f0-e246-4c71-8627-2074610e665c")]
    public interface IServer : IDisposable
    {
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
        void TraceResult(
            IScriptPageData pageData,
            ServerPhase phase,
            ReturnCode code,
            Result result,
            int errorLine,
            string errorCode,
            string errorInfo
        );

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
        Result TraceError(
            ServerPhase phase,
            ReturnCode code,
            Result result
        );

        /// <summary>
        /// Processes a script-page request through the full server pipeline.
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
        ReturnCode Handler(
            IResponse response,
            IScriptPage page,
            IScriptPageData pageData,
            out ServerPhase phase,
            out bool fatalError,
            ref Result error
        );
    }
}
