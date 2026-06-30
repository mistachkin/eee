/*
 * HostEventManager.cs --
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

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Exposes the opened and closed event handlers for a host window.
    /// Keeping the lifecycle notifications in their own facet lets the shell
    /// and the window registrar track how many windows are currently open, and
    /// shut the WPF application down when the last one closes, without
    /// depending on any particular window implementation.
    /// </summary>
    [ObjectId("cf9d1eff-e026-4e3e-bddb-0ff02935e6b7")]
    public interface IHostEventManager
    {
        /// <summary>
        /// Gets or sets the handler invoked when the window is opened.
        /// </summary>
        EventHandler OpenedHandler { get; set; }
        /// <summary>
        /// Gets or sets the handler invoked when the window is closed.
        /// </summary>
        EventHandler ClosedHandler { get; set; }
    }
}
