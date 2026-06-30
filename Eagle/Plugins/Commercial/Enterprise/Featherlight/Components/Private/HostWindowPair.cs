/*
 * HostWindowPair.cs --
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
using Featherlight.Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Pairs a host window with a flag indicating whether the registrar owns
    /// it (and may therefore dispose it).
    /// </summary>
    [ObjectId("5021df5f-f849-4789-ad2e-ec4bb0d322fd")]
    internal sealed class HostWindowPair : AnyPair<IHostWindow, bool>
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="HostWindowPair" />
        /// class.
        /// </summary>
        /// <param name="x">
        /// The host window.
        /// </param>
        /// <param name="y">
        /// Non-zero if the registrar owns the window.
        /// </param>
        public HostWindowPair(
            IHostWindow x, /* in */
            bool y         /* in */
            )
            : base(x, y)
        {
            // do nothing.
        }
    }
}
