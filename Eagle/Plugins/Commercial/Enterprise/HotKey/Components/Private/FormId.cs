/*
 * FormId.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Threading;
using Eagle._Attributes;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Provides the thread-safe allocator of monotonically increasing form
    /// ids used with the <see cref="Interfaces.Private.IHotKeyForm" />
    /// interface.
    /// </summary>
    [ObjectId("a8908a57-f8d9-43df-9045-04f6d5786143")]
    internal static class FormId
    {
        #region Private Data
        //
        // NOTE: The last form Id assigned for use with the IHotKeyForm
        //       interface.
        //
        /// <summary>
        /// The most recently assigned form id.
        /// </summary>
        private static int id;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Form Helper Methods
        /// <summary>
        /// Gets the most recently assigned form id without allocating a new
        /// one.
        /// </summary>
        /// <returns>
        /// The previously assigned form id.
        /// </returns>
        public static int GetPrevious()
        {
            return Interlocked.CompareExchange(ref id, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically allocates and returns the next form id.
        /// </summary>
        /// <returns>
        /// The newly allocated form id.
        /// </returns>
        public static int GetNext()
        {
            return Interlocked.Increment(ref id);
        }
        #endregion
    }
}
