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

#if NET_STANDARD_20
using System.IO;
#endif

#if !NET_STANDARD_20
using System.Threading;
#endif

#if NET_STANDARD_20
using System.Threading.Tasks;
#endif

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
    /// This instanced class represents a logical response object that is used
    /// to help render the result of the page being rendered by the current
    /// request.
    /// </summary>
#if KAPOK
    [ObjectId("db154118-dcb4-40ba-a186-1edd5a9dd17a")]
#else
    [Guid("db154118-dcb4-40ba-a186-1edd5a9dd17a")]
#endif
    internal sealed class Response : IResponse
    {
        #region Private Constants
        /// <summary>
        /// This is the default content type for use when generating an HTTP
        /// server response.
        /// </summary>
        private static readonly string ContentType = "text/plain";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The <see cref="HttpContext" /> associated with this logical
        /// response object.
        /// </summary>
        private HttpContext context; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The <see cref="HttpResponse" /> associated with this logical
        /// response object.
        /// </summary>
        private HttpResponse response; /* NOT OWNED */

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The content type that should be used when generating the response.
        /// If this field is null the default content type will be used.
        /// </summary>
        private string contentType;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// This constructor is used to create and initialize an instance of
        /// this logical response object class.  There should not be any other
        /// constructors for this class.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current request,
        /// if any.  This parameter may not be null.
        /// </param>
        /// <param name="response">
        /// The <see cref="HttpResponse" /> associated with the current
        /// request, if any.  This parameter may not be null.
        /// </param>
        /// <param name="contentType">
        /// The content type that should be used when generating the response.
        /// This parameter may be null.  If this parameter is null the default
        /// content type will be used.
        /// </param>
        private Response(
            HttpContext context,   /* in */
            HttpResponse response, /* in */
            string contentType     /* in: OPTIONAL */
            )
        {
            this.context = context;
            this.response = response;
            this.contentType = contentType;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
#if !NET_STANDARD_20
        /// <summary>
        /// Creates an initializes a new instance of this class using the
        /// specified content type.
        /// </summary>
        /// <param name="contentType">
        /// The content type that should be used when generating the response.
        /// This parameter may be null.  If this parameter is null the default
        /// content type will be used.
        /// </param>
        /// <returns>
        /// The newly created instance -OR- null if it could not be created.
        /// </returns>
        public static IResponse Create(
            string contentType /* in: OPTIONAL */
            )
        {
            return Create(AspNetOps.GetHttpContext(), contentType);
        }
#endif

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Creates an initializes a new instance of this class using the
        /// specified <see cref="HttpContext" /> and content type.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current request,
        /// if any.  This parameter may not be null.
        /// </param>
        /// <param name="contentType">
        /// The content type that should be used when generating the response.
        /// This parameter may be null.  If this parameter is null the default
        /// content type will be used.
        /// </param>
        /// <returns>
        /// The newly created instance -OR- null if it could not be created.
        /// </returns>
        public static IResponse Create(
            HttpContext context, /* in */
            string contentType   /* in: OPTIONAL */
            )
        {
            if (context == null)
                return null;

            return new Response(
                context, AspNetOps.GetHttpResponse(context), contentType);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Sets the HTTP response (status?) code for the current request.
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="HttpStatusCode" /> value to use, which will first
        /// be converted to an <see cref="Int32" />.
        /// </param>
        private void SetStatusCode(
            HttpStatusCode statusCode /* in */
            )
        {
            if (response == null)
                return;

            response.StatusCode = (int)statusCode;
        }

        ///////////////////////////////////////////////////////////////////////

#if NET_STANDARD_20
        /// <summary>
        /// Attempts to synchronously wait for a <see cref="Task"/> instance
        /// to be completed.  Hopefully, this task crap actually works.
        /// </summary>
        /// <param name="task">
        /// The task to synchronously wait for.  If this parameter is null,
        /// nothing will be done.
        /// </param>
        private void JustWaitForTheTask(
            Task task /* in */
            )
        {
            if (task == null)
                return;

            task.GetAwaiter().GetResult();
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IResponse Members
        /// <summary>
        /// Sets the content type for the current request, which may have been
        /// explicitly specified during the creation of this instance -OR- it
        /// may be the default content type.
        /// </summary>
        /// <param name="contentType">
        /// The optional content type for the response.  If this content type
        /// is null then the exact content type used is officially undefined.
        /// </param>
        public void Start(
            string contentType /* in: OPTIONAL */
            )
        {
            CheckDisposed();

            if (response == null)
                return;

            string localContentType;

            if (contentType != null)
                localContentType = contentType;
            else
                localContentType = this.contentType;

            if (localContentType == null)
                localContentType = ContentType;

            response.Clear();
            response.ContentType = localContentType;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Appends a string value to the response body that will be emitted
        /// for the current request.
        /// </summary>
        /// <param name="value">
        /// The string value to append to the response body.
        /// </param>
        public void Write(
            string value /* in */
            )
        {
            CheckDisposed();

            if (response == null)
                return;

#if !NET_STANDARD_20
            response.Write(value);
#else
            JustWaitForTheTask(response.WriteAsync(value));
#endif
        }

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
        public void Write(
            string value,             /* in */
            HttpStatusCode statusCode /* in */
            )
        {
            CheckDisposed();

            SetStatusCode(statusCode);
            Write(value);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Writes the contents of the specified local file to the response
        /// body for the current request.
        /// </summary>
        /// <param name="fileName">
        /// The name of the local file to include in the response body.
        /// </param>
        public void WriteFile(
            string fileName /* in */
            )
        {
            CheckDisposed();

            if (response == null)
                return;

#if !NET_STANDARD_20
            response.WriteFile(fileName);
#else
            JustWaitForTheTask(response.SendFileAsync(fileName));
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Marks the current response body as complete and prevents further
        /// changes to it.  In general, this method should never throw any
        /// exceptions.
        /// </summary>
        public void End()
        {
            CheckDisposed();

            if (response == null)
                return;

            ///////////////////////////////////////////////////////////////////
            //                    Legacy ASP.NET Support                     //
            ///////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
            //
            // HACK: Apparently, the .NET Framework (2.0?) always (?) ends
            //       up calling the Thread.Abort method during processing.
            //       We must block this because our caller may not be 100%
            //       done servicing the (original) associated request.
            //
            try
            {
                response.End(); /* throw */
            }
#if false
            catch (ThreadAbortException e)
#else
            catch (ThreadAbortException)
#endif
            {
                Thread.ResetAbort();

#if false
                Utility.DebugTrace(
                    e, typeof(Response).Name,
                    TracePriority.Medium |
                        TracePriority.FromPlugin);
#endif
            }
#endif

            ///////////////////////////////////////////////////////////////////
            //                     ASP.NET Core Support                      //
            ///////////////////////////////////////////////////////////////////

#if NET_STANDARD_20
            try
            {
#if NET_STANDARD_21
                //
                // HACK: Attempt to synchronously mark the response
                //       as "complete".  This is not available until
                //       ASP.NET Core 3.0.
                //
                JustWaitForTheTask(response.CompleteAsync());
#else
                //
                // HACK: Attempt to flush the underlying body stream,
                //       if any.  This should work (and be "allowed")
                //       on the ASP.NET Core 2.0 runtime.
                //
                Stream stream = response.Body;

                if (stream != null)
                    JustWaitForTheTask(stream.FlushAsync());
#endif
            }
#if KAPOK
            catch (Exception e)
#else
            catch
#endif
            {
#if KAPOK
                Utility.DebugTrace(
                    e, typeof(Response).Name,
                    TracePriority.Highest);
#else
                // do nothing.
#endif
            }
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// This method is used to dispose of this logical response object.
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
        /// This field is used to determine if this logical response object has
        /// been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is used to verify that this logical response object has
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
                        typeof(Response).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is called to perform cleanup of any resources that are
        /// in use by this logical response object when it is being disposed
        /// via the destructor -OR- via explicit calls to the
        /// <see cref="Dispose()" /> method.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this logical response object is being explicitly
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
                    response = null; /* NOT OWNED */
                    contentType = null;
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
        /// logical response object is being finalized by the garbage
        /// collector.
        /// </summary>
        ~Response()
        {
            Dispose(false);
        }
        #endregion
    }
}
