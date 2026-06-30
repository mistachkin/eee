/*
 * RequestDictionary.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents a logical collection of read-only input
    /// parameters for use during web server request processing.
    /// </summary>
#if KAPOK
    [ObjectId("d2972e8e-0076-499d-94c6-e6a1d85ac9c2")]
#else
    [Guid("d2972e8e-0076-499d-94c6-e6a1d85ac9c2")]
#endif
    public interface IRequestDictionary : IDisposable
    {
        /// <summary>
        /// Attempts to determine and return the full list of keys available
        /// in the underlying collection.
        /// </summary>
        IEnumerable<string> AllKeys { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the value of the specified input parameter,
        /// which may have originated from the query string or form data.
        /// </summary>
        /// <param name="name">
        /// The name of the input parameter.
        /// </param>
        /// <returns>
        /// The value of the input parameter -OR- null if it cannot be
        /// determined.
        /// </returns>
        string this[string name] { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the value of the specified input parameter,
        /// which may have originated from the query string or form data.
        /// </summary>
        /// <param name="name">
        /// The name of the input parameter.
        /// </param>
        /// <param name="value">
        /// The value of the input parameter -OR- null if it cannot be
        /// determined.
        /// </param>
        /// <returns>
        /// Non-zero if the specified named value is found; otherwise, zero.
        /// </returns>
        bool TryGetValue(string name, out string value);
    }
}
