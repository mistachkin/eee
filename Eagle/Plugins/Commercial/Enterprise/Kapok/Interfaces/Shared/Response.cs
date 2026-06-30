/*
 * Response.cs --
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

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

using Kapok.Components.Shared;

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents a logical response object for use during web
    /// server request processing.
    /// </summary>
#if KAPOK
    [ObjectId("478d3e15-8aed-4ac4-ae76-67db21ca0e3c")]
#else
    [Guid("478d3e15-8aed-4ac4-ae76-67db21ca0e3c")]
#endif
    public interface IResponse : IDisposable
    {
        /// <summary>
        /// Sets the content type for the current request, which may have been
        /// explicitly specified during the creation of this instance -OR- it
        /// may be the default content type.
        /// </summary>
        /// <param name="contentType">
        /// The optional content type for the response.  If this content type
        /// is null then the exact content type used is officially undefined.
        /// </param>
        void Start(string contentType);

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Appends a string value to the response body that will be emitted
        /// for the current request.
        /// </summary>
        /// <param name="value">
        /// The string value to append to the response body.
        /// </param>
        void Write(string value);

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Appends a string value to the response body that will be emitted
        /// for the current request -AND- sets the HTTP response (status?) code
        /// to the specified value.
        /// </summary>
        /// <param name="value">
        /// The string value to append to the response body.
        /// </param>
        /// <param name="statusCode">
        /// The <see cref="HttpStatusCode" /> value to use, which will first
        /// be converted to an <see cref="Int32" />.
        /// </param>
        void Write(string value, HttpStatusCode statusCode);

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Writes the contents of the specified local file to the response
        /// body for the current request.
        /// </summary>
        /// <param name="fileName">
        /// The name of the local file to include in the response body.
        /// </param>
        void WriteFile(string fileName);

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Marks the current response body as complete and prevents further
        /// changes to it.
        /// </summary>
        void End();
    }
}
