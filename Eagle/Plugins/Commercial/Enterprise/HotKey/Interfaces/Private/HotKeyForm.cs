/*
 * HotKeyForm.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Globalization;
using Eagle._Attributes;

namespace HotKey.Interfaces.Private
{
    //
    // NOTE: This interface is currently private; however, it may be "promoted"
    //       to public at some point.
    //
    /// <summary>
    /// Represents a plugin-owned form that exposes thread-safe identity
    /// accessors and pattern matching, and that can be closed safely.
    /// </summary>
    [ObjectId("94b6f8fd-00a9-46da-8fcb-49ca54b659a4")]
    internal interface IHotKeyForm : ISafeClose
    {
        /// <summary>
        /// Gets the form's id in a thread-safe manner.
        /// </summary>
        int SafeId { get; } /* THREAD-SAFE */

        /// <summary>
        /// Gets the form's name in a thread-safe manner.
        /// </summary>
        string SafeName { get; } /* THREAD-SAFE */

        /// <summary>
        /// Gets the form's text (title) in a thread-safe manner.
        /// </summary>
        string SafeText { get; } /* THREAD-SAFE */

        /// <summary>
        /// Determines whether the form's name or text matches the supplied
        /// pattern.  Thread-safe.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to match against the form's name or text.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when matching.
        /// </param>
        /// <param name="exactOnly">
        /// Non-zero to require an exact match rather than a pattern match.
        /// </param>
        /// <returns>
        /// Non-zero when the form matches; otherwise, zero.
        /// </returns>
        bool DoesMatch(string pattern, CultureInfo cultureInfo,
            bool exactOnly); /* THREAD-SAFE */
    }
}
