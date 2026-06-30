/*
 * WindowNameTriplet.cs --
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
using Featherlight.Components.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Bundles the values that identify a window to create or look up: its id,
    /// its name, and its window type.
    /// </summary>
    [ObjectId("f35aba54-9020-4dc7-81e1-e440b2408f11")]
    internal sealed class WindowNameTriplet :
        AnyTriplet<long, string, WindowType>
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="WindowNameTriplet" />
        /// class.
        /// </summary>
        /// <param name="x">
        /// The window id.
        /// </param>
        /// <param name="y">
        /// The window name.
        /// </param>
        /// <param name="z">
        /// The window type.
        /// </param>
        public WindowNameTriplet(
            long x,      /* in */
            string y,    /* in */
            WindowType z /* in */
            )
            : base(x, y, z)
        {
            // do nothing.
        }
    }
}
