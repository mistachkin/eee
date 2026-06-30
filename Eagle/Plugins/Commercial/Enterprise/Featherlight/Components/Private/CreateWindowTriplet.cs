/*
 * CreateWindowTriplet.cs --
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
using Eagle._Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Bundles the inputs and completion handle used when creating a host
    /// window on the dispatcher thread: the triplet identifying the window to
    /// create, the resulting host window, and the wait handle signaled when
    /// creation finishes.
    /// </summary>
    [ObjectId("0b186b55-0dde-439c-9bda-c4f870fbec5d")]
    internal sealed class CreateWindowTriplet :
        MutableAnyTriplet<WindowNameTriplet, IHostWindow, EventWaitHandle>
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="CreateWindowTriplet" />
        /// class.
        /// </summary>
        /// <param name="mutable">
        /// Non-zero if the triplet may be modified after construction.
        /// </param>
        /// <param name="x">
        /// The triplet identifying the window to create.
        /// </param>
        /// <param name="y">
        /// The created host window.
        /// </param>
        /// <param name="z">
        /// The wait handle signaled when creation completes.
        /// </param>
        public CreateWindowTriplet(
            bool mutable,        /* in */
            WindowNameTriplet x, /* in */
            IHostWindow y,       /* in */
            EventWaitHandle z    /* in */
            )
            : base(mutable, x, y, z)
        {
            // do nothing.
        }
    }
}
