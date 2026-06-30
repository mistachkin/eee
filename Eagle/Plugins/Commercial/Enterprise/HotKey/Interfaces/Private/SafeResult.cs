/*
 * SafeResult.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace HotKey.Interfaces.Private
{
    //
    // NOTE: This interface is currently private; however, it may be "promoted"
    //       to public at some point.
    //
    /// <summary>
    /// Represents an object (typically a form) that exposes thread-safe
    /// operations over a result/log display.
    /// </summary>
    [ObjectId("66e7af3f-d542-49b6-b35c-c5ae8581b722")]
    internal interface ISafeResult
    {
        /// <summary>
        /// Clears the displayed result safely from any thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SafeClearResult();

        /// <summary>
        /// Appends the supplied text as a log entry safely from any thread.
        /// </summary>
        /// <param name="text">
        /// The log entry text to append.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SafeAppendLogEntry(string text);

        /// <summary>
        /// Copies the displayed result to the clipboard safely from any
        /// thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SafeCopyResultToClipboard();
    }
}
