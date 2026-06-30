/*
 * CertificateShellCallback.cs --
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

namespace Licensing.Components.Private
{
    /// <summary>
    /// Implements the <see cref="IShellCallback" /> interface to provide the
    /// shell argument, script, and file evaluation callbacks used during
    /// certificate-related interactive shell processing.
    /// </summary>
    [ObjectId("050894a9-2dd6-41d8-b3cd-588fc3f79f32")]
    internal sealed class CertificateShellCallback :
        ScriptMarshalByRefObject, IShellCallback
    {
        #region Shell Argument / Script / File Callbacks
        /// <summary>
        /// Previews a single command-line argument prior to it being
        /// processed by the interactive shell.  This implementation is not
        /// supported and always returns <see cref="ReturnCode.Error" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the interactive shell.
        /// </param>
        /// <param name="interactiveHost">
        /// The interactive host associated with the interpreter.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data, or null if none is available.
        /// </param>
        /// <param name="whatIf">
        /// True if the argument should be examined without actually performing
        /// any associated action; otherwise, false.
        /// </param>
        /// <param name="index">
        /// The index of the argument being previewed.  May be modified to
        /// influence subsequent argument processing.
        /// </param>
        /// <param name="arg">
        /// The argument being previewed.  May be modified.
        /// </param>
        /// <param name="argv">
        /// The list of command-line arguments.  May be modified.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message produced by this
        /// method.
        /// </param>
        /// <returns>
        /// Always returns <see cref="ReturnCode.Error" /> because this method
        /// is not supported.
        /// </returns>
        /* Eagle._Components.Public.Delegates.PreviewArgumentCallback */
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
        /// Handles a command-line argument that was not otherwise recognized
        /// by the interactive shell.  This implementation is not supported and
        /// always returns <see cref="ReturnCode.Error" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the interactive shell.
        /// </param>
        /// <param name="interactiveHost">
        /// The interactive host associated with the interpreter.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data, or null if none is available.
        /// </param>
        /// <param name="switchCount">
        /// The number of command-line switches that have been processed so
        /// far.
        /// </param>
        /// <param name="arg">
        /// The unrecognized argument being handled.
        /// </param>
        /// <param name="whatIf">
        /// True if the argument should be examined without actually performing
        /// any associated action; otherwise, false.
        /// </param>
        /// <param name="argv">
        /// The list of command-line arguments.  May be modified.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message produced by this
        /// method.
        /// </param>
        /// <returns>
        /// Always returns <see cref="ReturnCode.Error" /> because this method
        /// is not supported.
        /// </returns>
        /* Eagle._Components.Public.Delegates.UnknownArgumentCallback */
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
            result = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the specified script text using the provided interpreter
        /// by delegating to
        /// <see cref="CertificateShellOps.EvaluateScriptCallback" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that will be used to evaluate the script.
        /// </param>
        /// <param name="text">
        /// The script text to be evaluated.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message produced by
        /// evaluating the script.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateScriptCallback */
        public ReturnCode EvaluateScript(
            Interpreter interpreter, /* in */
            string text,             /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return CertificateShellOps.EvaluateScriptCallback(
                interpreter, text, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file using the
        /// provided interpreter by delegating to
        /// <see cref="CertificateShellOps.EvaluateFileCallback" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that will be used to evaluate the file.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to be evaluated.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message produced by
        /// evaluating the file.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateFileCallback */
        public ReturnCode EvaluateFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return CertificateShellOps.EvaluateFileCallback(
                interpreter, fileName, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file, using the
        /// specified character encoding, with the provided interpreter by
        /// delegating to
        /// <see cref="CertificateShellOps.EvaluateEncodedFileCallback" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that will be used to evaluate the file.
        /// </param>
        /// <param name="encoding">
        /// The character encoding used to read the file.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to be evaluated.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message produced by
        /// evaluating the file.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateEncodedFileCallback */
        public ReturnCode EvaluateEncodedFile(
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return CertificateShellOps.EvaluateEncodedFileCallback(
                interpreter, encoding, fileName, ref result, ref errorLine);
        }
        #endregion
    }
}
