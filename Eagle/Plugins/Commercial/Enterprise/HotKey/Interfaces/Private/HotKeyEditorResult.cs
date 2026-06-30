/*
 * HotKeyEditorResult.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace HotKey.Interfaces.Private
{
    //
    // NOTE: This interface is currently private; however, it may be "promoted"
    //       to public at some point.
    //
    /// <summary>
    /// Represents an editor that can receive the outcome of an asynchronous
    /// operation and reflect a script result in its displayed text.
    /// </summary>
    [ObjectId("b1a607d1-9316-4a43-9eac-2128294a8733")]
    internal interface IHotKeyEditorResult
    {
        /// <summary>
        /// The asynchronous callback invoked when a template (or other
        /// background) operation completes, carrying its context.
        /// </summary>
        /// <param name="context">
        /// The context describing the completed asynchronous operation.
        /// </param>
        void TemplateAsynchronousCallback(
            IAsynchronousContext context
        ); /* AsynchronousCallback */

        /// <summary>
        /// Updates the editor's displayed text from a script result, its
        /// return code, and error line.
        /// </summary>
        /// <param name="returnCode">
        /// The return code of the operation whose result is shown.
        /// </param>
        /// <param name="result">
        /// The result text to show.
        /// </param>
        /// <param name="errorLine">
        /// The line number associated with an error, or zero when none.
        /// </param>
        /// <param name="append">
        /// Non-zero to append to the existing text; zero to replace it.
        /// </param>
        void ModifyTextFromResult(
            ReturnCode returnCode,
            Result result,
            int errorLine,
            bool append
        );
    }
}
