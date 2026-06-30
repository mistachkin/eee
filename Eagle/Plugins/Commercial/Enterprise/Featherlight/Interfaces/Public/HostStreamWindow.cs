/*
 * HostStreamWindow.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Marks a host window that provides both input and output streams.
    /// Combining the input and output capabilities in a single interface lets
    /// one window serve as a host's complete console replacement.
    /// </summary>
    [ObjectId("686f39ab-4971-4328-b0fb-843702014fc6")]
    public interface IHostStreamWindow :
            IHostInputWindow, IHostOutputWindow
    {
        // nothing.
    }
}
