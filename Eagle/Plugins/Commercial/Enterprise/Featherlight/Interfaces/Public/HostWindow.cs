/*
 * HostWindow.cs --
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
    /// Represents a single host window and the operations common to every kind
    /// of window: sizing, positioning, titling, exit-code reporting, and the
    /// show, activate, refresh, and close lifecycle.  It builds on the
    /// identity and event facets and is the common currency passed throughout
    /// Featherlight; the manager, factory, and registrar all deal in this
    /// interface so they never need to know the concrete WPF window type.
    /// </summary>
    [ObjectId("88cf7604-a1b1-44a9-902e-6fba0b0c81fb")]
    public interface IHostWindow : IHostWindowIdentifier, IHostEventManager
    {
        /// <summary>
        /// Gets or sets the window manager that owns this window.
        /// </summary>
        IHostWindowManager WindowManager { get; set; }
        /// <summary>
        /// Gets or sets the factory used to create windows.
        /// </summary>
        IHostWindowFactory WindowFactory { get; set; }
        /// <summary>
        /// Gets or sets the registrar that tracks this window.
        /// </summary>
        IHostWindowRegistrar WindowRegistrar { get; set; }

        /// <summary>
        /// Gets or sets the position information for this window.
        /// </summary>
        WindowPositionInfo WindowPositionInfo { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window is constrained
        /// to its minimum size.
        /// </summary>
        bool MinimumSize { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the window sizes itself
        /// automatically.
        /// </summary>
        bool AutoSize { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the window closes
        /// automatically.
        /// </summary>
        bool AutoClose { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the window is in the
        /// process of closing.
        /// </summary>
        bool IsClosing { get; set; }

        /// <summary>
        /// Closes the window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Close();
        /// <summary>
        /// Closes the window asynchronously.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool CloseAsync();
        /// <summary>
        /// Activates the window, bringing it to the foreground.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Activate();
        /// <summary>
        /// Refreshes the window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Refresh();
        /// <summary>
        /// Repositions the window using the specified position information.
        /// </summary>
        /// <param name="windowPositionInfo">
        /// The position information to apply.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool Position(WindowPositionInfo windowPositionInfo);

        /// <summary>
        /// Gets the window size of the specified kind.
        /// </summary>
        /// <param name="hostSizeType">
        /// The kind of size to retrieve.
        /// </param>
        /// <param name="width">
        /// Upon success, receives the width.
        /// </param>
        /// <param name="height">
        /// Upon success, receives the height.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool GetSize(HostSizeType hostSizeType, ref double width,
            ref double height);

        /// <summary>
        /// Sets the window size of the specified kind.
        /// </summary>
        /// <param name="hostSizeType">
        /// The kind of size to set.
        /// </param>
        /// <param name="width">
        /// The width to set.
        /// </param>
        /// <param name="height">
        /// The height to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetSize(HostSizeType hostSizeType, double width, double height);

        /// <summary>
        /// Gets the window title.
        /// </summary>
        /// <param name="value">
        /// Upon success, receives the title.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool GetTitle(ref string value);
        /// <summary>
        /// Sets the window title.
        /// </summary>
        /// <param name="value">
        /// The title to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetTitle(string value);

        /// <summary>
        /// Gets the exit code associated with the window.
        /// </summary>
        /// <param name="exitCode">
        /// Upon success, receives the exit code.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool GetExitCode(ref ExitCode exitCode);
        /// <summary>
        /// Sets the exit code associated with the window.
        /// </summary>
        /// <param name="exitCode">
        /// The exit code to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetExitCode(ExitCode exitCode);

        /// <summary>
        /// Shows the window.
        /// </summary>
        void Show(); // NOTE: Stolen from "System.Windows.Window".
        /// <summary>
        /// Shows the window as a modal dialog.
        /// </summary>
        /// <returns>
        /// True if accepted, false if canceled, or null when no result is
        /// available.
        /// </returns>
        bool? ShowDialog(); // NOTE: Stolen from "System.Windows.Window".
    }
}
