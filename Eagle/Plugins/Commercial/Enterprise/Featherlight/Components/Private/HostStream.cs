/*
 * HostStream.cs --
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

#if THROW_ON_DISPOSED
using Eagle._Components.Public;
#endif

using Eagle._Constants;
using Featherlight.Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// A stream that adapts an IHostStreamManager so the host's window input
    /// and output can be consumed as a standard stream (for example, as the
    /// interpreter's standard input or output).
    /// </summary>
    [ObjectId("6db5a11d-dfdf-418a-83f7-dd33cb36c68c")]
    internal sealed class HostStream : Stream
    {
        #region Private Data
        /// <summary>
        /// The stream manager whose input and output this stream adapts.
        /// </summary>
        private IHostStreamManager streamManager;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="HostStream" /> class.
        /// </summary>
        /// <param name="streamManager">
        /// The stream manager to adapt.
        /// </param>
        /// <param name="canRead">
        /// Non-zero if the stream supports reading.
        /// </param>
        /// <param name="canWrite">
        /// Non-zero if the stream supports writing.
        /// </param>
        public HostStream(
            IHostStreamManager streamManager, /* in */
            bool canRead,                     /* in */
            bool canWrite                     /* in */
            )
        {
            this.streamManager = streamManager;
            this.canRead = canRead;
            this.canWrite = canWrite;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Stream Members
        /// <summary>
        /// Non-zero if the stream supports reading.
        /// </summary>
        private bool canRead;
        /// <summary>
        /// Gets a value indicating whether the stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { CheckDisposed(); return canRead; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether the stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get { CheckDisposed(); return canRead; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if the stream supports writing.
        /// </summary>
        private bool canWrite;
        /// <summary>
        /// Gets a value indicating whether the stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get { CheckDisposed(); return canWrite; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Flushes any buffered output to the stream manager.
        /// </summary>
        public override void Flush()
        {
            CheckDisposed();

            if (!canWrite)
                throw new IOException();

            if (streamManager == null)
                throw new InvalidOperationException();

            if (!streamManager.Flush())
                throw new IOException();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the length, in bytes, of the available input.
        /// </summary>
        public override long Length
        {
            get
            {
                CheckDisposed();

                if (!canRead)
                    throw new NotSupportedException();

                if (streamManager == null)
                    throw new InvalidOperationException();

                string text = streamManager.GetInput();

                if (text == null)
                    throw new InvalidOperationException();

                return text.Length;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The current read position within the input.
        /// </summary>
        private int position = _Position.Invalid;
        /// <summary>
        /// Gets or sets the current read position within the input.
        /// </summary>
        public override long Position
        {
            get
            {
                CheckDisposed();

                if (!canRead)
                    throw new NotSupportedException();

                return position;
            }
            set
            {
                CheckDisposed();

                if (!canRead)
                    throw new NotSupportedException();

                if ((value < int.MinValue) || (value > int.MaxValue))
                    throw new IOException();

                position = (int)value;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a sequence of bytes from the input.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to read into.
        /// </param>
        /// <param name="offset">
        /// The offset in the buffer at which to begin writing.
        /// </param>
        /// <param name="count">
        /// The maximum number of bytes to read.
        /// </param>
        /// <returns>
        /// The number of bytes read.
        /// </returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (!canRead)
                throw new NotSupportedException();

            if (buffer == null)
                throw new ArgumentNullException();

            if ((offset < 0) || (count < 0))
                throw new ArgumentOutOfRangeException();

            int length = buffer.Length;

            if ((offset + count) > length)
                throw new ArgumentException();

            if (streamManager == null)
                throw new InvalidOperationException();

            string text = streamManager.GetInput();

            if (text == null)
                throw new InvalidOperationException();

            if (position < 0)
                position = 0;

            int index;

            for (index = offset; count > 0; index++, count--)
            {
                if (position >= text.Length)
                    break;

                buffer[index] = (byte)(text[position++] & (char)byte.MaxValue);
            }

            return (index - offset);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Seeks within the input; only seeking to the start is supported.
        /// </summary>
        /// <param name="offset">
        /// The offset relative to the origin (must be zero).
        /// </param>
        /// <param name="origin">
        /// The seek origin (must be the beginning).
        /// </param>
        /// <returns>
        /// The new position.
        /// </returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            if (!canRead)
                throw new NotSupportedException();

            if (streamManager == null)
                throw new InvalidOperationException();

            string text = streamManager.GetInput();

            if (text == null)
                throw new InvalidOperationException();

            //
            // HACK: Only allow seeking to the start of the stream.
            //
            if ((offset != 0) || (origin != SeekOrigin.Begin))
                throw new IOException();

            position = _Position.Invalid;

            return position;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="value">
        /// The desired length.
        /// </param>
        public override void SetLength(long value)
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a sequence of bytes to the output.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to write from.
        /// </param>
        /// <param name="offset">
        /// The offset in the buffer at which to begin reading.
        /// </param>
        /// <param name="count">
        /// The number of bytes to write.
        /// </param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (!canWrite)
                throw new NotSupportedException();

            if (buffer == null)
                throw new ArgumentNullException();

            if ((offset < 0) || (count < 0))
                throw new ArgumentOutOfRangeException();

            int length = buffer.Length;

            if ((offset + count) > length)
                throw new ArgumentException();

            if (streamManager == null)
                throw new InvalidOperationException();

            StringBuilder builder = new StringBuilder();

            for (int index = offset; count > 0; index++, count--)
                builder.Append((char)buffer[index]);

            if (!streamManager.Write(builder.ToString()))
                throw new IOException();

            //
            // NOTE: This flush could be done here; however, it should not be
            //       necessary because the IHostStreamManager object itself
            //       should perform a flush internally when the AutoFlush
            //       property value is true.
            //
            // if (streamManager.AutoFlush && !streamManager.Flush())
            //     throw new IOException();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(HostStream).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        protected override void Dispose(bool disposing)
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

                        streamManager = null; /* NOT OWNED */
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

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizes this stream, releasing any resources that were not
        /// released by an explicit call to <see cref="Stream.Dispose()" />.
        /// </summary>
        ~HostStream()
        {
            Dispose(false);
        }
        #endregion
    }
}
