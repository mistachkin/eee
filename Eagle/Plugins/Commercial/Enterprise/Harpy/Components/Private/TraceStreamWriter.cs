/*
 * TraceStreamWriter.cs --
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
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides a <see cref="StreamWriter" /> that records the parameters
    /// used to create it (file name, append mode, sharing mode, and encoding)
    /// so they can be queried later for tracing purposes.
    /// </summary>
    [ObjectId("98cc2323-2258-4df2-884f-fe68324a93cf")]
    internal sealed class TraceStreamWriter : StreamWriter
    {
        #region Private Data
        /// <summary>
        /// The name of the file associated with this stream writer.
        /// </summary>
        private string fileName;
        /// <summary>
        /// Non-zero if output is appended to the file instead of overwriting
        /// its existing contents.
        /// </summary>
        private bool append;
        /// <summary>
        /// Non-zero if the underlying file was opened in a shared mode.
        /// </summary>
        private bool shared;
        /// <summary>
        /// The text encoding used when writing to the underlying stream.
        /// </summary>
        private Encoding encoding;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of this class that writes to the
        /// supplied <paramref name="stream" />, recording the associated file
        /// name, append mode, sharing mode, and encoding for later querying.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the stream.
        /// </param>
        /// <param name="append">
        /// Non-zero if output is appended to the file instead of overwriting
        /// its existing contents.
        /// </param>
        /// <param name="shared">
        /// Non-zero if the underlying file was opened in a shared mode.
        /// </param>
        /// <param name="encoding">
        /// The text encoding used when writing to the stream.
        /// </param>
        /// <param name="stream">
        /// The underlying stream to which output is written.
        /// </param>
        public TraceStreamWriter(
            string fileName,   /* in */
            bool append,       /* in */
            bool shared,       /* in */
            Encoding encoding, /* in */
            Stream stream      /* in */
            )
            : base(stream)
        {
            SaveData(fileName, append, shared, encoding);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance of this class that opens the file named
        /// by <paramref name="fileName" /> for writing, recording the
        /// associated file name, append mode, sharing mode, and encoding for
        /// later querying.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to open for writing.
        /// </param>
        /// <param name="append">
        /// Non-zero if output is appended to the file instead of overwriting
        /// its existing contents.
        /// </param>
        /// <param name="shared">
        /// Non-zero if the underlying file was opened in a shared mode.
        /// </param>
        /// <param name="encoding">
        /// The text encoding used when writing to the file.
        /// </param>
        public TraceStreamWriter(
            string fileName,  /* in */
            bool append,      /* in */
            bool shared,      /* in */
            Encoding encoding /* in */
            )
            : base(fileName, append, encoding)
        {
            SaveData(fileName, append, shared, encoding);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Resets the recorded file name, append mode, sharing mode, and
        /// encoding to their default values.
        /// </summary>
        private void ResetData()
        {
            this.fileName = null;
            this.append = false;
            this.shared = false;
            this.encoding = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the file name, append mode, sharing mode, and encoding
        /// used to create this stream writer.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the stream.
        /// </param>
        /// <param name="append">
        /// Non-zero if output is appended to the file instead of overwriting
        /// its existing contents.
        /// </param>
        /// <param name="shared">
        /// Non-zero if the underlying file was opened in a shared mode.
        /// </param>
        /// <param name="encoding">
        /// The text encoding used when writing to the stream.
        /// </param>
        private void SaveData(
            string fileName,  /* in */
            bool append,      /* in */
            bool shared,      /* in */
            Encoding encoding /* in */
            )
        {
            this.fileName = fileName;
            this.append = append;
            this.shared = shared;
            this.encoding = encoding;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// Gets the name of the file associated with this stream writer.
        /// </summary>
        public string FileName
        {
            get { CheckDisposed(); return fileName; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether output is appended to the file
        /// instead of overwriting its existing contents.
        /// </summary>
        public bool Append
        {
            get { CheckDisposed(); return append; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether the underlying file was opened in
        /// a shared mode.
        /// </summary>
        public bool Shared
        {
            get { CheckDisposed(); return shared; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the text encoding used when writing to the underlying stream.
        /// </summary>
        public override Encoding Encoding
        {
            get { CheckDisposed(); return encoding; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this object instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an <see cref="ObjectDisposedException" /> if this object
        /// instance has been disposed and the engine is configured to throw
        /// on disposed objects.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object instance has been disposed.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
            {
                throw new ObjectDisposedException(
                    typeof(TraceStreamWriter).Name);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this object instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from the
        /// <see cref="IDisposable.Dispose" /> method; otherwise, it is being
        /// called from the finalizer.
        /// </param>
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    //if (disposing)
                    //{
                    //    ////////////////////////////////////
                    //    // dispose managed resources here...
                    //    ////////////////////////////////////
                    //}

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////

                    ResetData();
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
