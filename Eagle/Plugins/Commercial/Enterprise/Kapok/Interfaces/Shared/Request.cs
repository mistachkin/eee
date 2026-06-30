/*
 * Request.cs --
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
using System.IO;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents a logical request object for use during web
    /// server request processing.
    /// </summary>
#if KAPOK
    [ObjectId("9243a996-2c46-4a88-ab72-36e0a37af81a")]
#else
    [Guid("9243a996-2c46-4a88-ab72-36e0a37af81a")]
#endif
    public interface IRequest : IDisposable
    {
        /// <summary>
        /// This IP address where the current request originated.
        /// </summary>
        string Address { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The HTTP request method (e.g. <c>GET</c>, <c>POST</c>) of the
        /// current request.  Always upper-case; never null.
        /// </summary>
        string Method { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The virtual path associated with the current request, if any.
        /// </summary>
        string Path { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the form data associated with the
        /// current request, if any.
        /// </summary>
        IRequestDictionary Form { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the query string associated with
        /// the current request, if any.
        /// </summary>
        IRequestDictionary Query { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The files uploaded as part of the current
        /// <c>multipart/form-data</c> request body.  Returns an empty
        /// enumerable when no files were uploaded; never null.
        /// </summary>
        IEnumerable<IUploadedFile> Files { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the <see cref="Stream" /> instance containing
        /// the body of the current request, if any.
        /// </summary>
        /// <returns>
        /// The <see cref="Stream" /> instance containing the body of the
        /// current request -OR- null if it cannot be determined.
        /// </returns>
        Stream GetInputStream();

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
