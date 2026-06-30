/*
 * HostWindowFactory.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Featherlight.Components.Public;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Creates host windows and the interpreter hosts that drive them.  Hiding
    /// window construction behind a factory keeps the concrete WPF window
    /// types out of the rest of the system: the shell and the interpreter host
    /// subsystem ask the factory for a new host (through the interpreter's
    /// new-host callback) or for a new window of a given type, and receive
    /// back the abstract interfaces rather than WPF classes.
    /// </summary>
    [ObjectId("60888994-1629-4fd2-9934-37a65e730703")]
    public interface IHostWindowFactory
    {
        /// <summary>
        /// Creates a new windowed host.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the host is created for.
        /// </param>
        /// <param name="hostData">
        /// The data used to create and configure the host.
        /// </param>
        /// <param name="primary">
        /// Non-zero if this is the primary host.
        /// </param>
        /// <returns>
        /// The new host.
        /// </returns>
        IHost NewHost(
            Interpreter interpreter, IHostData hostData, bool primary);

        /// <summary>
        /// Disposes all hosts created by this factory.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool DisposeHosts();

        /// <summary>
        /// Creates a host window of the specified type.
        /// </summary>
        /// <param name="windowType">
        /// The type of window to create.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type used for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type used for output.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The initial position information.
        /// </param>
        /// <param name="minimumSize">
        /// Non-zero to constrain the window to its minimum size.
        /// </param>
        /// <param name="autoSize">
        /// Non-zero to size the window automatically.
        /// </param>
        /// <param name="autoClose">
        /// Non-zero to close the window automatically.
        /// </param>
        /// <param name="autoFlush">
        /// Non-zero to flush output automatically.
        /// </param>
        /// <returns>
        /// The new host window.
        /// </returns>
        IHostWindow CreateWindow(
            WindowType windowType, WindowType inputWindowType,
            WindowType outputWindowType, WindowPositionInfo windowPositionInfo,
            bool minimumSize, bool autoSize, bool autoClose, bool autoFlush);

        /// <summary>
        /// Creates a host window with full control over its manager,
        /// registrar, identity, and event handlers.
        /// </summary>
        /// <param name="windowManager">
        /// The window manager that will own the window.
        /// </param>
        /// <param name="windowRegistrar">
        /// The registrar that will track the window.
        /// </param>
        /// <param name="id">
        /// The window id.
        /// </param>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window to create.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type used for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type used for output.
        /// </param>
        /// <param name="openedHandler">
        /// The handler invoked when the window is opened.
        /// </param>
        /// <param name="closedHandler">
        /// The handler invoked when the window is closed.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The initial position information.
        /// </param>
        /// <param name="minimumSize">
        /// Non-zero to constrain the window to its minimum size.
        /// </param>
        /// <param name="autoSize">
        /// Non-zero to size the window automatically.
        /// </param>
        /// <param name="autoClose">
        /// Non-zero to close the window automatically.
        /// </param>
        /// <param name="autoFlush">
        /// Non-zero to flush output automatically.
        /// </param>
        /// <returns>
        /// The new host window.
        /// </returns>
        IHostWindow CreateWindow(
            IHostWindowManager windowManager,
            IHostWindowRegistrar windowRegistrar, long id, string name,
            WindowType windowType, WindowType inputWindowType,
            WindowType outputWindowType, EventHandler openedHandler,
            EventHandler closedHandler, WindowPositionInfo windowPositionInfo,
            bool minimumSize, bool autoSize, bool autoClose, bool autoFlush);
    }
}
