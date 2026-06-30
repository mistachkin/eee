/*
 * Enumerations.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;

namespace Featherlight.Components.Public
{
    /// <summary>
    /// Specifies the on-screen position of a host window.
    /// </summary>
    [ObjectId("6e906cda-195e-4128-b09e-c649a552f4ac")]
    public enum WindowPosition
    {
        /// <summary>
        /// No position.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid position; do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Choose a position automatically.
        /// </summary>
        Automatic = 0x2,
        /// <summary>
        /// The top-left position.
        /// </summary>
        TopLeft = 0x4,
        /// <summary>
        /// The top-center position.
        /// </summary>
        TopCenter = 0x8,
        /// <summary>
        /// The top-right position.
        /// </summary>
        TopRight = 0x10,
        /// <summary>
        /// The middle-left position.
        /// </summary>
        MiddleLeft = 0x20,
        /// <summary>
        /// The middle-center position.
        /// </summary>
        MiddleCenter = 0x40,
        /// <summary>
        /// The middle-right position.
        /// </summary>
        MiddleRight = 0x80,
        /// <summary>
        /// The bottom-left position.
        /// </summary>
        BottomLeft = 0x100,
        /// <summary>
        /// The bottom-center position.
        /// </summary>
        BottomCenter = 0x200,
        /// <summary>
        /// The bottom-right position.
        /// </summary>
        BottomRight = 0x400,

        /// <summary>
        /// The first position in the range.
        /// </summary>
        First = TopLeft,
        /// <summary>
        /// The last position in the range.
        /// </summary>
        Last = BottomRight,

        //
        // NOTE: Only these positions are available for automatic selection.
        //
        /// <summary>
        /// The set of positions available for automatic selection.
        /// </summary>
        AutomaticMask = TopLeft | TopCenter | TopRight |
                        MiddleLeft | MiddleRight | BottomLeft |
                        BottomCenter | BottomRight
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Specifies the kind of host window.
    /// </summary>
    [Flags()]
    [ObjectId("6d2f2ffa-309b-4bdb-a213-94c9cb8ff3dd")]
    public enum WindowType
    {
        /// <summary>
        /// No window type.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An input window.
        /// </summary>
        Input = 0x1,
        /// <summary>
        /// An output window.
        /// </summary>
        Output = 0x2,
        /// <summary>
        /// An error window.
        /// </summary>
        Error = 0x4,
        /// <summary>
        /// A trace window.
        /// </summary>
        Trace = 0x8,
        /// <summary>
        /// A simple text-box window.
        /// </summary>
        Box = 0x10,
        /// <summary>
        /// An interactive window.
        /// </summary>
        Interactive = 0x20,
        /// <summary>
        /// The set of all window types.
        /// </summary>
        Mask = Input | Output | Error | Trace | Box | Interactive,
        /// <summary>
        /// The default window type.
        /// </summary>
        Default = Interactive
    }
}
