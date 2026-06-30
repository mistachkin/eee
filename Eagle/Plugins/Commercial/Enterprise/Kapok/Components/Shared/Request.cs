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
    /// This instanced class represents a logical request object that is used
    /// to help handle querying of (cached?) input parameters.
    /// </summary>
#if KAPOK
    [ObjectId("8996cd01-e798-469c-b37d-8ac819055024")]
#else
    [Guid("8996cd01-e798-469c-b37d-8ac819055024")]
#endif
    internal sealed class Request : IRequest
    {
        #region Private Data
        /// <summary>
        /// The logical page context associated with this request, if any.
        /// </summary>
        private HttpContext context; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The <see cref="HttpRequest" /> instance associated with this
        /// logical request object, if any.
        /// </summary>
        private HttpRequest request; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This IP address where the current request originated.
        /// </summary>
        private string address;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The HTTP request method (e.g. <c>GET</c>, <c>POST</c>) of the
        /// current request.
        /// </summary>
        private string method;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The virtual path associated with the current request, if any.
        /// </summary>
        private string path;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the form data associated with the
        /// current request, if any.
        /// </summary>
        private IRequestDictionary form; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the query string associated with
        /// the current request, if any.
        /// </summary>
        private IRequestDictionary query; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The files uploaded as part of the current
        /// <c>multipart/form-data</c> request body, if any.  Never
        /// null after Initialize completes; the list itself owns the
        /// IUploadedFile instances and disposes them when this
        /// request is disposed.
        /// </summary>
        private IList<IUploadedFile> files;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The <see cref="Stream" /> instance containing the body of the
        /// current request, if any.
        /// </summary>
        private Stream stream; /* NOT OWNED */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// This constructor is used to create and initialize an instance of
        /// this logical request object class.  There should not be any other
        /// constructors for this class.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may not be null.
        /// </param>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        private Request(
            HttpContext context, /* in */
            HttpRequest request, /* in */
            bool copyData        /* in */
            )
        {
            this.context = context;
            this.request = request;

            ///////////////////////////////////////////////////////////////////

            Initialize(context, request, copyData);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
#if !NET_STANDARD_20
        /// <summary>
        /// Attempts to create a new logical request object instance.
        /// </summary>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        /// <returns>
        /// The newly created logical request object instance -OR- null if it
        /// cannot be created.
        /// </returns>
        public static IRequest Create(
            bool copyData /* in */
            )
        {
            return Create(AspNetOps.GetHttpContext(), copyData);
        }
#endif

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to create a new logical request object instance.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may not be null.
        /// </param>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        /// <returns>
        /// The newly created logical request object instance -OR- null if it
        /// cannot be created.
        /// </returns>
        public static IRequest Create(
            HttpContext context, /* in */
            bool copyData        /* in */
            )
        {
            if (context == null)
                return null;

            return new Request(
                context, AspNetOps.GetHttpRequest(context), copyData);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Initializes private fields of a new instance of this class using
        /// the specified <see cref="HttpContext" /> and
        /// <see cref="HttpRequest" /> instances.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request, if any.  This parameter may not be null.
        /// </param>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        private void Initialize(
            HttpContext context, /* in */
            HttpRequest request, /* in */
            bool copyData        /* in */
            )
        {
            if (request == null)
                return;

            this.address = AspNetOps.GetAddress(
                context, request);

            this.method = AspNetOps.GetMethod(request);

            this.path = AspNetOps.GetPath(request);

            this.form = new RequestDictionary(
                AspNetOps.GetForm(request), copyData);

            this.query = new RequestDictionary(
                AspNetOps.GetQuery(request), copyData);

            this.files = AspNetOps.GetFiles(request);

#if !NET_STANDARD_20
            this.stream = request.InputStream;
#else
            this.stream = request.Body;
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRequest Members
        /// <summary>
        /// This IP address where the current request originated.
        /// </summary>
        public string Address
        {
            get { CheckDisposed(); return address; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The HTTP request method (e.g. <c>GET</c>, <c>POST</c>) of the
        /// current request.  Always upper-case; never null.
        /// </summary>
        public string Method
        {
            get { CheckDisposed(); return method; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The virtual path associated with the current request, if any.
        /// </summary>
        public string Path
        {
            get { CheckDisposed(); return path; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the form data associated with the
        /// current request, if any.
        /// </summary>
        public IRequestDictionary Form
        {
            get { CheckDisposed(); return form; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The cached input parameters from the query string associated with
        /// the current request, if any.
        /// </summary>
        public IRequestDictionary Query
        {
            get { CheckDisposed(); return query; }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The files uploaded as part of the current
        /// <c>multipart/form-data</c> request body.  Never null;
        /// returns an empty enumerable when no files were uploaded.
        /// </summary>
        public IEnumerable<IUploadedFile> Files
        {
            get
            {
                CheckDisposed();

                if (files == null)
                    return new List<IUploadedFile>();

                return files;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the <see cref="Stream" /> instance containing
        /// the body of the current request, if any.
        /// </summary>
        /// <returns>
        /// The <see cref="Stream" /> instance containing the body of the
        /// current request -OR- null if it cannot be determined.
        /// </returns>
        public Stream GetInputStream()
        {
            CheckDisposed();

            return stream;
        }

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
        public bool TryGetValue(
            string name,     /* in */
            out string value /* out */
            )
        {
            CheckDisposed();

            if ((query != null) && query.TryGetValue(name, out value))
                return true;

            if ((form != null) && form.TryGetValue(name, out value))
                return true;

            value = null;
            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// This method is used to dispose of this logical request object.
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
        /// This field is used to determine if this logical request object has
        /// been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is used to verify that this logical request object has
        /// not been disposed, i.e. from within public members.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// This exception will be thrown if this instance has been disposed.
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
                        typeof(Request).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is called to perform cleanup of any resources that are
        /// in use by this logical request object when it is being disposed via
        /// the destructor -OR- via explicit calls to the
        /// <see cref="Dispose()" /> method.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this logical request object is being explicitly
        /// disposed via the <see cref="Dispose()" /> method.
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

                    context = null; /* NOT OWNED */
                    request = null; /* NOT OWNED */
                    path = null;
                    form = null; /* NOT OWNED */
                    query = null; /* NOT OWNED */

                    if (files != null)
                    {
                        for (int index = 0; index < files.Count; index++)
                        {
                            IUploadedFile file = files[index];

                            if (file != null)
                                file.Dispose();
                        }

                        files = null;
                    }
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
        /// This destructor should be called by the CLR runtime when this
        /// logical request object is being finalized by the garbage
        /// collector.
        /// </summary>
        ~Request()
        {
            Dispose(false);
        }
        #endregion
    }
}
