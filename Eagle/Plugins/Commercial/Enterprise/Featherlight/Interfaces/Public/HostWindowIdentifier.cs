/*
 * HostWindowIdentifier.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Featherlight.Components.Public;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Provides the identity of a host window: its numeric id, its name, and
    /// the window types describing its role.  This is the most basic facet of
    /// the Featherlight window abstraction; the window manager, the registrar,
    /// and the windows themselves all share it so that a window can be looked
    /// up and addressed (and its input role distinguished from its output
    /// role) independently of its other capabilities.
    /// </summary>
    [ObjectId("39d80f3d-3455-44f2-bafd-c4f4fa1ba71b")]
    public interface IHostWindowIdentifier
    {
        /// <summary>
        /// Gets or sets the window id.
        /// </summary>
        long WindowId { get; set; }
        /// <summary>
        /// Gets or sets the window name.
        /// </summary>
        string WindowName { get; set; }
        /// <summary>
        /// Gets or sets the type of this window.
        /// </summary>
        WindowType WindowType { get; set; }
        /// <summary>
        /// Gets or sets the window type used for input.
        /// </summary>
        WindowType InputWindowType { get; set; }
        /// <summary>
        /// Gets or sets the window type used for output.
        /// </summary>
        WindowType OutputWindowType { get; set; }
    }
}
