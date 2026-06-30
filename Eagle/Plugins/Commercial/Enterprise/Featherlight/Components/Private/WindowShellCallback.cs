/*
 * WindowShellCallback.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Collections.Generic;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// A shell callback for the windowed environment.  The argument-preview
    /// and script-evaluation entry points are intentionally not implemented
    /// (they return an error); only the unknown-argument handler is wired up,
    /// delegating to the shared pop-style handler.
    /// </summary>
    [ObjectId("03f93d42-fe16-49aa-8d1a-53b7f557d7d3")]
    internal sealed class WindowShellCallback :
        ScriptMarshalByRefObject, IShellCallback
    {
        /// <summary>
        /// Previews a command-line argument before it is processed.  Not
        /// implemented.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter processing the argument.
        /// </param>
        /// <param name="interactiveHost">
        /// The interactive host, if any.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="whatIf">
        /// Non-zero to evaluate without taking effect.
        /// </param>
        /// <param name="index">
        /// On input and output, the index of the current argument.
        /// </param>
        /// <param name="arg">
        /// On input and output, the current argument.
        /// </param>
        /// <param name="argv">
        /// On input and output, the remaining argument list.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode PreviewArgument(
            Interpreter interpreter,          /* in */
            IInteractiveHost interactiveHost, /* in */
            IClientData clientData,           /* in */
            bool whatIf,                      /* in */
            ref int index,                    /* in, out */
            ref string arg,                   /* in, out */
            ref IList<string> argv,           /* in, out */
            ref Result result                 /* out */
            )
        {
            result = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles a command-line argument that was not recognized.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter processing the argument.
        /// </param>
        /// <param name="interactiveHost">
        /// The interactive host, if any.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="switchCount">
        /// The number of switch arguments seen so far.
        /// </param>
        /// <param name="arg">
        /// The unrecognized argument.
        /// </param>
        /// <param name="whatIf">
        /// Non-zero to evaluate without taking effect.
        /// </param>
        /// <param name="argv">
        /// On input and output, the remaining argument list.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode UnknownArgument(
            Interpreter interpreter,          /* in */
            IInteractiveHost interactiveHost, /* in */
            IClientData clientData,           /* in */
            int switchCount,                  /* in */
            string arg,                       /* in */
            bool whatIf,                      /* in */
            ref IList<string> argv,           /* in, out */
            ref Result result                 /* out */
            )
        {
            return CommonOps.PopUnknownArgumentCallback(
                interpreter, interactiveHost, clientData, switchCount, arg,
                whatIf, ref argv, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates a script supplied on the command line.  Not implemented.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the script.
        /// </param>
        /// <param name="text">
        /// The script text to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the script result or an error message.
        /// </param>
        /// <param name="errorLine">
        /// Upon failure, receives the line number of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode EvaluateScript(
            Interpreter interpreter, /* in */
            string text,             /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            result = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates a script file supplied on the command line.  Not
        /// implemented.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the file.
        /// </param>
        /// <param name="fileName">
        /// The file to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the script result or an error message.
        /// </param>
        /// <param name="errorLine">
        /// Upon failure, receives the line number of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode EvaluateFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            result = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates an encoded script file supplied on the command line.  Not
        /// implemented.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the file.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The file to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the script result or an error message.
        /// </param>
        /// <param name="errorLine">
        /// Upon failure, receives the line number of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode EvaluateEncodedFile(
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            result = "not implemented";
            return ReturnCode.Error;
        }
    }
}
