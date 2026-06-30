/*
 * DemoPlugin.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Interfaces.Public;

namespace Demo.Interfaces.Public
{
    /// <summary>
    /// Describes the demo plugin, which swaps a demo host into the interpreter
    /// it is loaded into.
    /// </summary>
    [ObjectId("5e8c2e7c-96b0-4368-b25d-90f027a00147")]
    public interface IDemoPlugin : IPlugin
    {
        /// <summary>
        /// Gets or sets the original interpreter host saved before the demo
        /// host was installed.
        /// </summary>
        IHost SavedHost { get; set; }
        /// <summary>
        /// Gets or sets the demo host installed into the interpreter.
        /// </summary>
        IDemoHost DemoHost { get; set; }
    }
}
