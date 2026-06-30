/*
 * SafeClose.cs --
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
    /// Represents an object (typically a form) that can be closed safely from
    /// any thread.
    /// </summary>
    [ObjectId("2b7f8184-e83d-4fe6-ade9-dd21b6965169")]
    internal interface ISafeClose
    {
        /// <summary>
        /// Determines whether a safe-close operation is currently in
        /// progress.  Thread-safe.
        /// </summary>
        /// <returns>
        /// Non-zero when a safe close is in progress; otherwise, zero.
        /// </returns>
        bool InSafeClose(); /* THREAD-SAFE */

        /// <summary>
        /// Closes the object safely, marshaling to the owning thread if
        /// necessary and waiting for completion.  Thread-safe.
        /// </summary>
        void SafeClose(); /* THREAD-SAFE */

        /// <summary>
        /// Begins closing the object safely without waiting for completion.
        /// Thread-safe.
        /// </summary>
        void SafeCloseAsynchronous(); /* THREAD-SAFE */
    }
}
