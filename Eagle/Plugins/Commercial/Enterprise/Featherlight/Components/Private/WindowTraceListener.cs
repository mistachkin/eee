/*
 * WindowTraceListener.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// A trace listener that buffers trace output and flushes it
    /// asynchronously to a host stream manager (the trace window), so tracing
    /// never blocks the thread that emits it.
    /// </summary>
    [ObjectId("862426ac-1456-4044-a487-bcea2b64fef4")]
    internal sealed class WindowTraceListener : TraceListener
    {
        #region Private Data
        /// <summary>
        /// Used to synchronize access to the trace buffer and timer.
        /// </summary>
        private readonly object syncRoot = new object();
        /// <summary>
        /// The timer that periodically flushes the trace buffer to the stream.
        /// </summary>
        private Timer writeTimer;
        /// <summary>
        /// The buffer accumulating trace text before it is written.
        /// </summary>
        private StringBuilder traceBuffer;
        /// <summary>
        /// The stream manager that receives the buffered trace output.
        /// </summary>
        private IHostStreamManager streamManager;
        /// <summary>
        /// The line terminator appended by WriteLine.
        /// </summary>
        private string newLine;
        /// <summary>
        /// The buffer length, in characters, at which the buffer is flushed.
        /// </summary>
        private int bufferWriteSize;
        /// <summary>
        /// The length argument passed to the stream manager when the buffer is
        /// flushed.
        /// </summary>
        private int bufferClearSize;
        /// <summary>
        /// The interval, in milliseconds, between automatic buffer flushes.
        /// </summary>
        private int writeMilliseconds;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="WindowTraceListener" />
        /// class and starts the flush timer.
        /// </summary>
        /// <param name="name">
        /// The name of the trace listener.
        /// </param>
        private WindowTraceListener(
            string name /* in */
            )
            : base(name)
        {
            traceBuffer = new StringBuilder(bufferWriteSize);

            writeTimer = new Timer(
                new TimerCallback(WriteBufferToStream), traceBuffer,
                writeMilliseconds, writeMilliseconds);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="WindowTraceListener" />
        /// class that writes buffered trace output to the specified stream
        /// manager.
        /// </summary>
        /// <param name="name">
        /// The name of the trace listener.
        /// </param>
        /// <param name="streamManager">
        /// The stream manager that receives the trace output.
        /// </param>
        /// <param name="newLine">
        /// The line terminator appended by WriteLine.
        /// </param>
        /// <param name="bufferWriteSize">
        /// The buffer length, in characters, at which the buffer is flushed.
        /// </param>
        /// <param name="bufferClearSize">
        /// The length argument passed to the stream manager when the buffer is
        /// flushed.
        /// </param>
        /// <param name="writeMilliseconds">
        /// The interval, in milliseconds, between automatic flushes.
        /// </param>
        public WindowTraceListener(
            string name,                      /* in */
            IHostStreamManager streamManager, /* in */
            string newLine,                   /* in */
            int bufferWriteSize,              /* in */
            int bufferClearSize,              /* in */
            int writeMilliseconds             /* in */
            )
            : this(name)
        {
            this.streamManager = streamManager;
            this.newLine = newLine;
            this.bufferWriteSize = bufferWriteSize;
            this.bufferClearSize = bufferClearSize;
            this.writeMilliseconds = writeMilliseconds;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Writes the accumulated trace buffer to the stream manager and
        /// clears it.
        /// </summary>
        /// <param name="state">
        /// The timer state; when a boolean, indicates whether to flush.
        /// </param>
        private void WriteBufferToStream(
            object state /* in */
            ) /* System.Threading.TimerCallback */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((traceBuffer != null) && (traceBuffer.Length > 0))
                {
                    string message = traceBuffer.ToString();

                    if (streamManager != null)
                    {
                        bool flush = (state is bool) ?
                            (bool)state : false;

                        streamManager.WriteAsync(
                            message, bufferClearSize, flush);
                    }

                    traceBuffer.Length = 0;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Diagnostics.TraceListener Overrides
        /// <summary>
        /// Buffers a trace message, flushing asynchronously when the buffer is
        /// full.
        /// </summary>
        /// <param name="message">
        /// The trace message to write.
        /// </param>
        public override void Write(
            string message /* in */
            )
        {
            CheckDisposed();

            //
            // HACK: It is very important that the method used to send
            //       the message text to the destination window cannot
            //       block, regardless of which thread this method is
            //       called on.  Therefore, we must communicate with
            //       the actual window used to hold the trace output
            //       asynchronously.
            //
            lock (syncRoot) /* TRANSACTIONAL */
            {
                traceBuffer.Append(message);

                if (traceBuffer.Length >= bufferWriteSize)
                    WriteBufferToStream(false);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Buffers a trace message followed by the line terminator.
        /// </summary>
        /// <param name="message">
        /// The trace message to write.
        /// </param>
        public override void WriteLine(
            string message /* in */
            )
        {
            CheckDisposed();

            Write(String.Format("{0}{1}", message, newLine));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Flushes the buffered trace output to the stream manager.
        /// </summary>
        public override void Flush()
        {
            CheckDisposed();

            WriteBufferToStream(true);
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
            {
                throw new ObjectDisposedException(
                    typeof(WindowTraceListener).Name);
            }
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
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            try
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (!disposed)
                    {
                        if (disposing)
                        {
                            //
                            // dispose managed resources here...
                            //

                            if (writeTimer != null)
                            {
                                writeTimer.Dispose();
                                writeTimer = null;
                            }

                            if (traceBuffer != null)
                            {
                                traceBuffer.Length = 0;
                                traceBuffer = null;
                            }

                            streamManager = null; /* NOT OWNED */
                        }

                        //
                        // release unmanaged resources here...
                        //
                    }
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
        /// Finalizes this trace listener, releasing any resources that were
        /// not by an explicit call to <see cref="TraceListener.Dispose()" />.
        /// </summary>
        ~WindowTraceListener()
        {
            Dispose(false);
        }
        #endregion
    }
}
