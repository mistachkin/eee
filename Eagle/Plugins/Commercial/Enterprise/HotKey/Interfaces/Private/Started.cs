/*
 * Started.cs --
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
    /// Represents an object that tracks whether it has been started.
    /// </summary>
    [ObjectId("fc4c3efb-c1a9-4892-8cf7-f9f3b5ef3418")]
    internal interface IStarted
    {
        /// <summary>
        /// Gets or sets a value indicating whether this object has been
        /// started.
        /// </summary>
        bool Started { get; set; }
    }
}
