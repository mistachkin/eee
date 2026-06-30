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

#if !NET_STANDARD_20
using System.Web;
#endif

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.AspNetCore.Http;
#endif

#if KAPOK
using Eagle._Attributes;
using Eagle._Components.Public;
#else
using System.Runtime.InteropServices;
#endif

using Kapok.Interfaces.Shared;

namespace Kapok.Components.Shared
{
    /// <summary>
    /// Default implementation of <see cref="IUploadedFile" />.  On
    /// .NET Framework wraps an <c>HttpPostedFile</c>; on ASP.NET Core
    /// wraps an <c>IFormFile</c>.
    /// </summary>
#if KAPOK
    [ObjectId("f2a3b4c5-d6e7-4809-1a2b-3c4d5e6f7081")]
#else
    [Guid("f2a3b4c5-d6e7-4809-1a2b-3c4d5e6f7081")]
#endif
    internal sealed class UploadedFile : IUploadedFile
    {
        #region Private Data
        /// <summary>
        /// The form field name that carried the upload.
        /// </summary>
        private string fieldName;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The original file name supplied by the client.
        /// </summary>
        private string fileName;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The content-type header supplied with the upload.
        /// </summary>
        private string contentType;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Length in bytes of the uploaded file body.
        /// </summary>
        private long length;

#if !NET_STANDARD_20
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The .NET Framework underlying file representation.
        /// </summary>
        private HttpPostedFile postedFile;
#else
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The ASP.NET Core underlying file representation.
        /// </summary>
        private IFormFile formFile;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
#if !NET_STANDARD_20
        /// <summary>
        /// Constructs a wrapper around an <see cref="HttpPostedFile" />
        /// from the .NET Framework collection.
        /// </summary>
        /// <param name="fieldName">
        /// The form field name carrying the upload.  MUST NOT be
        /// null.
        /// </param>
        /// <param name="postedFile">
        /// The underlying posted file.  MUST NOT be null.
        /// </param>
        public UploadedFile(
            string fieldName,        /* in */
            HttpPostedFile postedFile /* in */
            )
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName");

            if (postedFile == null)
                throw new ArgumentNullException("postedFile");

            this.fieldName = fieldName;
            this.postedFile = postedFile;

            this.fileName = postedFile.FileName != null
                ? postedFile.FileName : String.Empty;

            this.contentType = postedFile.ContentType != null
                ? postedFile.ContentType : String.Empty;

            this.length = postedFile.ContentLength;
        }
#else
        /// <summary>
        /// Constructs a wrapper around an <see cref="IFormFile" />
        /// from the ASP.NET Core collection.
        /// </summary>
        /// <param name="formFile">
        /// The underlying form file.  MUST NOT be null.
        /// </param>
        public UploadedFile(
            IFormFile formFile /* in */
            )
        {
            if (formFile == null)
                throw new ArgumentNullException("formFile");

            this.formFile = formFile;

            this.fieldName = formFile.Name != null
                ? formFile.Name : String.Empty;

            this.fileName = formFile.FileName != null
                ? formFile.FileName : String.Empty;

            this.contentType = formFile.ContentType != null
                ? formFile.ContentType : String.Empty;

            this.length = formFile.Length;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IUploadedFile Members
        /// <summary>
        /// The form field name that carried the upload.
        /// </summary>
        public string FieldName
        {
            get { CheckDisposed(); return fieldName; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The original file name supplied by the client.
        /// </summary>
        public string FileName
        {
            get { CheckDisposed(); return fileName; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The content-type header supplied with the upload.
        /// </summary>
        public string ContentType
        {
            get { CheckDisposed(); return contentType; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Length in bytes of the uploaded file body.
        /// </summary>
        public long Length
        {
            get { CheckDisposed(); return length; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Opens a fresh stream for reading the uploaded file body.
        /// </summary>
        /// <returns>
        /// A readable stream over the file body.
        /// </returns>
        public Stream OpenReadStream()
        {
            CheckDisposed();

#if !NET_STANDARD_20
            return postedFile.InputStream;
#else
            return formFile.OpenReadStream();
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Disposes this uploaded file.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Tracks whether this uploaded file has been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Verifies that this uploaded file has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed)
            {
#if KAPOK
                if (Engine.IsThrowOnDisposed(null, false))
#endif
                {
                    throw new ObjectDisposedException(
                        typeof(UploadedFile).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Performs cleanup of any resources held by this uploaded
        /// file.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero when this uploaded file is being explicitly
        /// disposed via <see cref="Dispose()" />.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            if (!disposed)
            {
                if (disposing)
                {
                    ////////////////////////////////////
                    // dispose managed resources here...
                    ////////////////////////////////////

                    //
                    // NOTE: The underlying posted-file / form-file
                    //       objects are owned by the host runtime;
                    //       we only drop our references.
                    //
#if !NET_STANDARD_20
                    postedFile = null;
#else
                    formFile = null;
#endif
                }

                //////////////////////////////////////
                // release unmanaged resources here...
                //////////////////////////////////////

                //
                // NOTE: This object is now disposed.
                //
                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizer.
        /// </summary>
        ~UploadedFile()
        {
            Dispose(false);
        }
        #endregion
    }
}
