/*
 * HostInputWindow.cs --
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
    /// Marks a host window that also acts as a stream manager for input.  The
    /// capability interfaces (input, output, and combined stream) pair a
    /// window with the stream-management facet so that one window object can
    /// stand in for the console's input, output, or both, depending on which
    /// capabilities it advertises.
    /// </summary>
    [ObjectId("ce27476b-80fb-47c0-a1a7-9b57b4c5c2b1")]
    public interface IHostInputWindow :
            IHostWindow, IHostStreamManager
    {
        // nothing.
    }
}
