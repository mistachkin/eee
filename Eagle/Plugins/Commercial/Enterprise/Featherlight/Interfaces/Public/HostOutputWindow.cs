/*
 * HostOutputWindow.cs --
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
    /// Marks a host window that also acts as a stream manager for output.
    /// Beyond the stream operations it reports the size of a character cell,
    /// which the host needs in order to translate Eagle's character-based
    /// sizing and cursor positioning onto the pixel-based WPF surface.
    /// </summary>
    [ObjectId("30ca542b-0770-4c99-8ff4-5ac2482224ae")]
    public interface IHostOutputWindow :
            IHostWindow, IHostStreamManager
    {
        /// <summary>
        /// Gets the size of a single character in the output.
        /// </summary>
        /// <param name="width">
        /// Upon success, receives the character width.
        /// </param>
        /// <param name="height">
        /// Upon success, receives the character height.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool GetCharacterSize(ref double width, ref double height);
    }
}
