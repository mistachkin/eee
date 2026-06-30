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
using System.Collections.Specialized;

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
    /// This class represents a logical collection of read-only input
    /// parameters for use during web server request processing.
    /// </summary>
#if KAPOK
    [ObjectId("5ef69dbb-c66e-42e0-975b-2e5c5e075e1d")]
#else
    [Guid("5ef69dbb-c66e-42e0-975b-2e5c5e075e1d")]
#endif
    internal sealed class RequestDictionary : IRequestDictionary
    {
        #region Private Data
        /// <summary>
        /// This is the collection of name / value input parameter pairs that
        /// are being managed by this instance.
        /// </summary>
        private NameValueCollection collection;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Non-zero if deep copies of the input parameter collections will be
        /// made.
        /// </summary>
        private bool copyData;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// This constructor is used to create and initialize an instance of
        /// this logical request object class.  There should not be any other
        /// constructors for this class.
        /// </summary>
        /// <param name="collection">
        /// The collection of name / value input parameters to be managed by
        /// this instance.
        /// </param>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        public RequestDictionary(
            NameValueCollection collection, /* in */
            bool copyData                   /* in */
            )
        {
            Initialize(collection, copyData);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Initializes private fields of a new instance of this class using
        /// the specified <see cref="NameValueCollection" />  instance.
        /// </summary>
        /// <param name="collection">
        /// The collection of name / value input parameters to be managed by
        /// this instance.
        /// </param>
        /// <param name="copyData">
        /// Non-zero if deep copies of the input parameter collections should
        /// be made.
        /// </param>
        private void Initialize(
            NameValueCollection collection, /* in */
            bool copyData                   /* in */
            )
        {
            this.collection = copyData ?
                AspNetOps.CopyNamesAndValues(collection) : collection;

            this.copyData = copyData;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRequestDictionary Members
        /// <summary>
        /// Attempts to determine and return the full list of keys available
        /// in the underlying collection.
        /// </summary>
        public IEnumerable<string> AllKeys
        {
            get
            {
                if (collection == null)
                    return null;

                return collection.AllKeys;
            }
        }

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
        public string this[string name]
        {
            get
            {
                if (collection == null)
                    return null;

                return collection[name];
            }
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
            value = this[name];
            return (value != null);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// This method is primarily intended for diagnostics and test use.
        /// It will return a string that should be suitable for checking if
        /// this instance contains the expected name / value pairs.
        /// </summary>
        /// <returns>
        /// Either a string suitable for diagnostic and testing use -OR- null
        /// if one cannot be determined.
        /// </returns>
        public override string ToString()
        {
            if (collection == null)
                return null;

            return collection.ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// This method is used to dispose of this object.
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
        /// This field is used to determine if this object has been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is used to verify that this object has not been
        /// disposed, i.e. from within public members.
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
                        typeof(RequestDictionary).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method is called to perform cleanup of any resources that are
        /// in use by this object when it is being disposed via the destructor
        /// -OR- via explicit calls to the <see cref="Dispose()" /> method.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this object is being explicitly disposed via the
        /// <see cref="Dispose()" /> method.
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

                    if (collection != null)
                    {
                        if (copyData)
                            collection.Clear();

                        collection = null;
                    }

                    copyData = false;
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
        /// object is being finalized by the garbage collector.
        /// </summary>
        ~RequestDictionary()
        {
            Dispose(false);
        }
        #endregion
    }
}
