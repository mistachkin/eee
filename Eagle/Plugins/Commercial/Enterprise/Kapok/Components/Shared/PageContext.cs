/*
 * PageContext.cs --
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
using Eagle._Components.Public;
#else
using System.Runtime.InteropServices;
#endif

using Kapok.Interfaces.Shared;

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This instanced class represents a logical page context that acts as a
    /// parent to the logical request and response objects.
    /// </summary>
#if KAPOK
    [ObjectId("01510e14-5cac-4ec4-a9c5-10baafda7b2e")]
#else
    [Guid("01510e14-5cac-4ec4-a9c5-10baafda7b2e")]
#endif
    internal sealed class PageContext : IPageContext
    {
        #region Private Data
        /// <summary>
        /// Abstract request object (i.e. not an <c>HttpRequest</c>),
        /// used to query the request data sent by the user.
        /// </summary>
        private IRequest request;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Abstract response object (i.e. not an <c>HttpResponse</c>),
        /// used to send the response data to the user.
        /// </summary>
        private IResponse response;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Abstract identity object (i.e. not an
        /// <see cref="System.Security.Principal.IPrincipal" />), used to
        /// inspect the authenticated user associated with this request.
        /// </summary>
        private IIdentityContext identity;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// This constructor is used to create and initialize an instance of
        /// this logical page context class.  There should not be any other
        /// constructors for this class.
        /// </summary>
        /// <param name="request">
        /// The logical request for the logical page context to be created.
        /// This parameter may be null; however, that should almost never be
        /// the case as the logical request object is generally required in
        /// order to be able to perform useful work in page implementations.
        /// </param>
        /// <param name="response">
        /// The logical response for the logical page context to be created.
        /// This parameter may be null; however, that should almost never be
        /// the case as the logical response object is generally required in
        /// order to be able to perform useful work in page implementations.
        /// </param>
        /// <param name="identity">
        /// The logical identity context for the logical page context to be
        /// created.  This parameter may be null; callers that pass null
        /// will see an anonymous identity from
        /// <see cref="GetIdentity" />.
        /// </param>
        public PageContext(
            IRequest request,         /* in: OPTIONAL */
            IResponse response,       /* in: OPTIONAL */
            IIdentityContext identity /* in: OPTIONAL */
            )
        {
            this.request = request;
            this.response = response;
            this.identity = identity;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPageContext Members
        /// <summary>
        /// Returns the logical request object for this logical page context.
        /// </summary>
        /// <returns>
        /// The logical request object -OR- null if it cannot be determined.
        /// </returns>
        public IRequest GetRequest()
        {
            CheckDisposed();

            return request;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the logical response object for this logical page context.
        /// </summary>
        /// <returns>
        /// The logical response object -OR- null if it cannot be determined.
        /// </returns>
        public IResponse GetResponse()
        {
            CheckDisposed();

            return response;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the logical identity context for this logical page
        /// context.
        /// </summary>
        /// <returns>
        /// The logical identity context -OR- null if it cannot be
        /// determined.  When the request passed through authentication
        /// middleware but no principal was attached, an
        /// <see cref="IIdentityContext" /> wrapping a null principal is
        /// returned (so callers may always rely on a non-null result).
        /// </returns>
        public IIdentityContext GetIdentity()
        {
            CheckDisposed();

            return identity;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// This method is used to dispose of this logical page context.
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
        /// This field is used to determine if this logical page context has
        /// been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is used to verify that this logical page context has
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
                        typeof(PageContext).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is called to perform cleanup of any resources that are
        /// in use by this logical page context when it is being disposed via
        /// the destructor -OR- via explicit calls to the
        /// <see cref="Dispose()" /> method.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this logical page context is being explicitly disposed
        /// via the <see cref="Dispose()" /> method.
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

                    if (request != null)
                    {
                        request.Dispose();
                        request = null;
                    }

                    if (response != null)
                    {
                        response.Dispose();
                        response = null;
                    }

                    if (identity != null)
                    {
                        identity.Dispose();
                        identity = null;
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
        /// logical page context is being finalized by the garbage collector.
        /// </summary>
        ~PageContext()
        {
            Dispose(false);
        }
        #endregion
    }
}
