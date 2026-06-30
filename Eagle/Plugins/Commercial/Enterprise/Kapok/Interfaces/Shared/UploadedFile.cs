/*
 * UploadedFile.cs --
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
using System.IO;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents one file uploaded as part of a
    /// multipart/form-data HTTP request.  Concrete implementations
    /// wrap the .NET Framework <c>HttpPostedFile</c> and the
    /// ASP.NET Core <c>IFormFile</c>; callers MUST NOT reach past
    /// this abstraction to those types directly.
    /// </summary>
#if KAPOK
    [ObjectId("e1f2a3b4-c5d6-4708-9a0b-1c2d3e4f5061")]
#else
    [Guid("e1f2a3b4-c5d6-4708-9a0b-1c2d3e4f5061")]
#endif
    public interface IUploadedFile : IDisposable
    {
        /// <summary>
        /// The form field name that carried the upload.  MUST NOT be null.
        /// </summary>
        string FieldName { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The original file name as supplied by the client.  May be the empty
        /// string when the client supplied no name; never null.
        /// </summary>
        string FileName { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The content-type header supplied with the upload, e.g.
        /// <c>text/csv</c>.  May be the empty string when the client supplied
        /// none; never null.
        /// </summary>
        string ContentType { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Length in bytes of the uploaded file body.
        /// </summary>
        long Length { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Opens a fresh <see cref="Stream" /> for reading the
        /// uploaded file body.  Callers are responsible for
        /// disposing the returned stream.
        /// </summary>
        /// <returns>
        /// A readable, seekable-at-best-effort <see cref="Stream" /> over the
        /// file body.
        /// </returns>
        Stream OpenReadStream();
    }
}
