/*
 * HostWindowRegistrar.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Featherlight.Components.Public;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Tracks the set of open host windows and coordinates their orderly
    /// shutdown.  Because Featherlight freely creates and destroys WPF
    /// windows, none of which is inherently the primary one, a single
    /// registrar owns the master list, holds the process exit code, and drives
    /// application shutdown once every window has closed; this is what lets
    /// the windowed host behave correctly under the explicit-shutdown model
    /// WPF requires here.
    /// </summary>
    [ObjectId("a5cefd8d-59b2-4b79-862b-a59f3d6537c0")]
    public interface IHostWindowRegistrar
    {
        /// <summary>
        /// Gets a value indicating whether the registrar is locked against
        /// further changes.
        /// </summary>
        bool IsLocked { get; }
        /// <summary>
        /// Gets or sets the exit code reported when the registrar shuts down.
        /// </summary>
        ExitCode ExitCode { get; set; }

        /// <summary>
        /// Gets the number of registered windows.
        /// </summary>
        int WindowCount { get; }

        /// <summary>
        /// Finds the registered window with the specified name and type.
        /// </summary>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        IHostWindow FindWindow(string name, WindowType windowType);
        /// <summary>
        /// Registers a host window under the specified name.
        /// </summary>
        /// <param name="name">
        /// The name to register the window under.
        /// </param>
        /// <param name="window">
        /// The host window to register.
        /// </param>
        /// <param name="owned">
        /// Non-zero if the registrar owns (and may dispose) the window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool RegisterWindow(string name, IHostWindow window, bool owned);
        /// <summary>
        /// Unregisters the named window of the specified type.
        /// </summary>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window.
        /// </param>
        /// <param name="close">
        /// Non-zero to close the window when unregistering.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool UnregisterWindow(string name, WindowType windowType, bool close);

        /// <summary>
        /// Closes the specified window, optionally shutting down the
        /// registrar.
        /// </summary>
        /// <param name="window">
        /// The host window to close.
        /// </param>
        /// <param name="shutdown">
        /// Non-zero to shut down the registrar after closing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Close(IHostWindow window, bool shutdown);
        /// <summary>
        /// Shuts down all registered windows.
        /// </summary>
        /// <param name="application">
        /// Non-zero to also shut down the application.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Shutdown(bool application);
    }
}
