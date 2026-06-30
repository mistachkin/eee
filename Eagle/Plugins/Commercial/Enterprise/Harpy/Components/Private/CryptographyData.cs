/*
 * CryptographyData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Security.Cryptography;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the symmetric cryptography data (algorithm name, cipher mode,
    /// padding mode, initialization vector, and key) used by the licensing
    /// subsystem.  This data is also derived from a password using the
    /// PBKDF2 parameters inherited from <see cref="Rfc2898Data" />.
    /// </summary>
    [ObjectId("203f42a2-6f46-40e6-adc3-739f3fbcbad0")]
    internal class CryptographyData : Rfc2898Data, ICryptographyData
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the instance data of this
        /// object.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs an instance of this class.
        /// </summary>
        public CryptographyData()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Resets all of the cryptography data managed by this object to its
        /// default state and then clears any data managed by the base class.
        /// </summary>
        public override void ClearData()
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                ResetData();
                base.ClearData();
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Resets all of the cryptography data managed by this object to its
        /// default state, clearing and releasing the initialization vector
        /// and key if they are present.
        /// </summary>
        private void ResetData()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                symmetricAlgorithmName = null;
                cipherMode = (CipherMode)0;
                paddingMode = (PaddingMode)0;

                if (iv != null)
                {
                    iv.Clear();
                    iv = null;
                }

                if (key != null)
                {
                    key.Clear();
                    key = null;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICryptographyData Members
        /// <summary>
        /// The name of the symmetric algorithm to use.
        /// </summary>
        private string symmetricAlgorithmName;

        /// <summary>
        /// Gets or sets the name of the symmetric algorithm to use.
        /// </summary>
        public string SymmetricAlgorithmName
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return symmetricAlgorithmName;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    symmetricAlgorithmName = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The cipher mode to use with the symmetric algorithm.
        /// </summary>
        private CipherMode cipherMode;

        /// <summary>
        /// Gets or sets the cipher mode to use with the symmetric algorithm.
        /// </summary>
        public CipherMode CipherMode
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return cipherMode;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    cipherMode = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The padding mode to use with the symmetric algorithm.
        /// </summary>
        private PaddingMode paddingMode;

        /// <summary>
        /// Gets or sets the padding mode to use with the symmetric algorithm.
        /// </summary>
        public PaddingMode PaddingMode
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return paddingMode;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    paddingMode = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The initialization vector to use with the symmetric algorithm.
        /// </summary>
        private ByteList iv;

        /// <summary>
        /// Gets or sets the initialization vector to use with the symmetric
        /// algorithm.
        /// </summary>
        public ByteList Iv
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return iv;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    iv = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key to use with the symmetric algorithm.
        /// </summary>
        private ByteList key;

        /// <summary>
        /// Gets or sets the key to use with the symmetric algorithm.
        /// </summary>
        public ByteList Key
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return key;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    key = value;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this object instance has been disposed of.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException" /> if this object
        /// instance has been disposed of and the engine is configured to
        /// throw in that case.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object instance has been disposed of.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, null))
            {
                throw new ObjectDisposedException(
                    typeof(CryptographyData).Name);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes of this object instance, resetting the cryptography data
        /// managed by this object and then disposing of the base class.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from the
        /// <see cref="IDisposable.Dispose" /> method.
        /// </param>
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        /* NO RESULT */
                        ResetData();
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
}
