/*
 * Delegates.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

namespace Licensing.Components.Private.Delegates
{
#if NETWORK
    /// <summary>
    /// Represents a callback used to obtain a formatted time string of the
    /// <see cref="TimeStringType" /> specified by <paramref name="type" />,
    /// optionally incorporating the supplied <paramref name="args" /> when
    /// composing the result.  This callback is used by the networking code to
    /// abstract the generation of time stamps used during license-related
    /// communication.
    /// </summary>
    /// <param name="type">
    /// The <see cref="TimeStringType" /> indicating which kind of time string
    /// to produce.
    /// </param>
    /// <param name="args">
    /// Zero or more additional arguments used when producing the time string;
    /// the interpretation of these arguments depends on the requested
    /// <paramref name="type" />.
    /// </param>
    /// <returns>
    /// The formatted time string corresponding to the requested
    /// <paramref name="type" />.
    /// </returns>
    internal delegate string GetTimeStringCallback(
        TimeStringType type, /* in */
        params object[] args /* in */
    );
#endif
}
