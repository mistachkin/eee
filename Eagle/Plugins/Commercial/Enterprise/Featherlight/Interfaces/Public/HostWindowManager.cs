/*
 * HostWindowManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Featherlight.Components.Public;

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Manages the collection of host windows belonging to a single
    /// interactive host.  It composes the identity, event, and registrar
    /// facets and adds the operations used at run time to obtain, activate,
    /// and position individual windows by type or name, creating them on
    /// demand.  The Featherlight interpreter host implements this interface,
    /// so the host itself acts as the manager of its own windows.
    /// </summary>
    [ObjectId("1b28928d-e277-406c-97b3-e77e90cd8281")]
    public interface IHostWindowManager
            : IHostWindowIdentifier, IHostEventManager, IHostWindowRegistrar
    {
        /// <summary>
        /// Determines whether the window manager is being disposed.
        /// </summary>
        /// <returns>
        /// Non-zero if the manager is being disposed; otherwise, zero.
        /// </returns>
        bool IsDisposing();

        /// <summary>
        /// Injects the specified text into the active input window.
        /// </summary>
        /// <param name="value">
        /// The input text to inject.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool InjectInput(string value);

        /// <summary>
        /// Gets the window of the specified type, optionally creating it.
        /// </summary>
        /// <param name="windowType">
        /// The type of window to get.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        IHostWindow GetWindow(WindowType windowType, bool create);
        /// <summary>
        /// Gets the window with the specified id and type, optionally creating
        /// it.
        /// </summary>
        /// <param name="id">
        /// The window id.
        /// </param>
        /// <param name="windowType">
        /// The type of window to get.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        IHostWindow GetWindow(long id, WindowType windowType, bool create);

        /// <summary>
        /// Gets the window with the specified name and type, optionally
        /// creating it.
        /// </summary>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window to get.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        IHostWindow GetWindow(string name, WindowType windowType, bool create);
        /// <summary>
        /// Gets the window with the specified id, name, and type, optionally
        /// creating it.
        /// </summary>
        /// <param name="id">
        /// The window id.
        /// </param>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window to get.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        IHostWindow GetWindow(long id, string name, WindowType windowType,
            bool create);

        /// <summary>
        /// Activates the named window of the specified type, optionally
        /// creating it.
        /// </summary>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool ActivateWindow(string name, WindowType windowType, bool create);

        /// <summary>
        /// Positions the named window of the specified type.
        /// </summary>
        /// <param name="name">
        /// The window name.
        /// </param>
        /// <param name="windowType">
        /// The type of window.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The position information to apply.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window when it does not exist.
        /// </param>
        /// <param name="always">
        /// Non-zero to reposition even when already positioned.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool PositionWindow(string name, WindowType windowType,
            WindowPositionInfo windowPositionInfo, bool create, bool always);
    }
}
