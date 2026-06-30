/*
 * HostStreamManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Windows.Threading;
using Eagle._Attributes;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Bridges the host's text input and output onto the underlying WPF input
    /// and output controls.  This is the heart of the integration.  Eagle
    /// drives a host with synchronous reads and writes from arbitrary threads,
    /// whereas WPF requires every control to be touched only on its dispatcher
    /// thread; this interface marshals those calls onto the dispatcher
    /// (synchronously or asynchronously), signals and waits for key and line
    /// availability, and exposes the input and output buffers, so the host can
    /// satisfy Eagle's blocking read and write contract against an
    /// asynchronous graphical surface.
    /// </summary>
    [ObjectId("a87b0540-d6e5-4f88-b8e2-e8f59b6a570c")]
    public interface IHostStreamManager
    {
        /// <summary>
        /// Gets or sets the control used for input.
        /// </summary>
        object InputBox { get; set; }
        /// <summary>
        /// Gets or sets the control used for output.
        /// </summary>
        object OutputBox { get; set; }

        /// <summary>
        /// Invokes a delegate synchronously on the dispatcher thread.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Invoke(object dispatcherObject, Delegate method,
            params object[] args);
        /// <summary>
        /// Invokes a delegate synchronously on the dispatcher thread,
        /// capturing its return value.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the value returned by the delegate.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Invoke(object dispatcherObject, Delegate method,
            ref object result, params object[] args);

        /// <summary>
        /// Invokes a delegate asynchronously on the dispatcher thread.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool BeginInvoke(object dispatcherObject, Delegate method,
            params object[] args);
        /// <summary>
        /// Invokes a delegate asynchronously on the dispatcher thread,
        /// returning the pending operation.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the pending dispatcher operation.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool BeginInvoke(object dispatcherObject, Delegate method,
            ref DispatcherOperation result, params object[] args);

        /// <summary>
        /// Signals that a key is available to be read.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SignalReadKey();
        /// <summary>
        /// Signals that a line is available to be read.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SignalReadLine();
        /// <summary>
        /// Signals that the pending read has been canceled.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SignalCanceled();

        /// <summary>
        /// Waits for a key to become available.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool WaitReadKey();
        /// <summary>
        /// Waits for a line to become available.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool WaitReadLine();

        /// <summary>
        /// Reads the next available key event.
        /// </summary>
        /// <returns>
        /// The key event, or null when none is available.
        /// </returns>
        EventArgs ReadKey();
        /// <summary>
        /// Reads the next available line of input.
        /// </summary>
        /// <returns>
        /// The line of input, or null when none is available.
        /// </returns>
        string ReadLine();

        /// <summary>
        /// Gets the most recent key event.
        /// </summary>
        /// <returns>
        /// The key event, or null when none.
        /// </returns>
        EventArgs GetKey();
        /// <summary>
        /// Sets the current key event.
        /// </summary>
        /// <param name="value">
        /// The key event to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetKey(EventArgs value);

        /// <summary>
        /// Gets the current input text.
        /// </summary>
        /// <returns>
        /// The input text.
        /// </returns>
        string GetInput();
        /// <summary>
        /// Appends text to the input.
        /// </summary>
        /// <param name="value">
        /// The text to append.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool AddInput(string value);
        /// <summary>
        /// Inserts text at the current input position.
        /// </summary>
        /// <param name="value">
        /// The text to insert.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool InsertInput(string value);
        /// <summary>
        /// Replaces the input text.
        /// </summary>
        /// <param name="value">
        /// The text to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetInput(string value);
        /// <summary>
        /// Appends a line of text to the output.
        /// </summary>
        /// <param name="value">
        /// The line to append.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool AddOutputLine(string value);

        /// <summary>
        /// Gets or sets a value indicating whether output is flushed
        /// automatically.
        /// </summary>
        bool AutoFlush { get; set; }

        /// <summary>
        /// Clears the input and output.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Clear();

        /// <summary>
        /// Writes text to the output.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Write(string value);
        /// <summary>
        /// Writes a portion of text to the output.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <param name="length">
        /// The number of characters to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Write(string value, int length);
        /// <summary>
        /// Writes a portion of text to the output, optionally flushing.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <param name="length">
        /// The number of characters to write.
        /// </param>
        /// <param name="flush">
        /// Non-zero to flush after writing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Write(string value, int length, bool flush);

        /// <summary>
        /// Writes text to the output asynchronously.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool WriteAsync(string value);
        /// <summary>
        /// Writes a portion of text to the output asynchronously.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <param name="length">
        /// The number of characters to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool WriteAsync(string value, int length);
        /// <summary>
        /// Writes a portion of text to the output asynchronously, optionally
        /// flushing.
        /// </summary>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <param name="length">
        /// The number of characters to write.
        /// </param>
        /// <param name="flush">
        /// Non-zero to flush after writing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool WriteAsync(string value, int length, bool flush);

        /// <summary>
        /// Flushes any buffered output.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Flush();
    }
}
