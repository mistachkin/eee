/*
 * WindowRegistrar.cs --
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
using System.Runtime.CompilerServices;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Shell
{
    /// <summary>
    /// The default window registrar.  It owns the master dictionary of open
    /// windows (keyed by name), holds the process exit code, and drives
    /// orderly shutdown of every window when the application closes.
    /// </summary>
    [ObjectId("cb3e680e-770d-4d30-b730-3819684657ee")]
    internal sealed class WindowRegistrar : IHostWindowRegistrar, IDisposable
    {
        #region Private Data
        //
        // NOTE: Used to synchronize access to the application and collection
        //       of windows.
        //
        /// <summary>
        /// Used to synchronize access to the application and the collection of
        /// windows.
        /// </summary>
        private readonly object syncRoot = new object();

        //
        // NOTE: The exit code for this window registrar.  This *MAY* end up
        //       being the exit code for the entire process; however, there
        //       is no guarantee that will be the case.
        //
        /// <summary>
        /// The exit code for this registrar, which may become the process exit
        /// code.
        /// </summary>
        private ExitCode exitCode;

        //
        // NOTE: The collection of windows that we know about.
        //
        /// <summary>
        /// The collection of known windows, keyed by name.
        /// </summary>
        private Dictionary<string, HostWindowPair> windows;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="WindowRegistrar" />
        /// class.
        /// </summary>
        public WindowRegistrar()
        {
            exitCode = Utility.SuccessExitCode();
            windows = new Dictionary<string, HostWindowPair>();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Emits a diagnostic trace message for the registrar.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DebugTrace(
            string message /* in */
            )
        {
            try
            {
                Utility.DebugTrace(
                    message, typeof(WindowRegistrar).Name,
                    TracePriority.MediumLow |
                        TracePriority.ViaWrapperFromPlugin);
            }
            catch (Exception e)
            {
                Utility.Complain(null, ReturnCode.Error, e);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowRegistrar Members
        /// <summary>
        /// Gets a value indicating whether the registrar is currently locked
        /// (its monitor is held).
        /// </summary>
        public bool IsLocked
        {
            get
            {
                CheckDisposed();

                if (syncRoot == null)
                    return false;

                if (!Monitor.TryEnter(syncRoot))
                    return true;

                Monitor.Exit(syncRoot);
                return false;

            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the exit code reported when the registrar shuts down.
        /// </summary>
        public ExitCode ExitCode
        {
            get { CheckDisposed(); lock (syncRoot) { return exitCode; } }
            set { CheckDisposed(); lock (syncRoot) { exitCode = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of registered windows.
        /// </summary>
        public int WindowCount
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    if (windows == null)
                        return 0;

                    return windows.Count;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the registered window with the specified name and type.
        /// </summary>
        /// <param name="name">
        /// The window name, or null to match any name.
        /// </param>
        /// <param name="windowType">
        /// The type of window.
        /// </param>
        /// <returns>
        /// The host window, or null when not found.
        /// </returns>
        public IHostWindow FindWindow(
            string name,          /* in */
            WindowType windowType /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                if (windows == null)
                    return null;

                foreach (KeyValuePair<string, HostWindowPair> pair in windows)
                {
                    HostWindowPair windowPair = pair.Value;

                    if (windowPair == null)
                        continue;

                    IHostWindow window = windowPair.X;

                    if (window == null)
                        continue;

                    if ((name != null) &&
                        !Utility.SystemStringEquals(window.WindowName, name))
                    {
                        continue;
                    }

                    if (window.WindowType != windowType)
                        continue;

                    return window;
                }

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Registers a host window under the specified name, replacing and
        /// closing any previous owned window with that name.
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
        public bool RegisterWindow(
            string name,        /* in */
            IHostWindow window, /* in */
            bool owned          /* in */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "RegisterWindow: name = {0}, window = {1}, owned = {2}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(window), owned));

            if ((name == null) || (window == null))
                return false;

            lock (syncRoot)
            {
                if (windows == null)
                    return false;

                HostWindowPair pair;

                if (windows.TryGetValue(name, out pair))
                {
                    if (pair != null)
                    {
                        IHostWindow oldWindow = pair.X;

                        if ((oldWindow != null) && pair.Y &&
                            !Object.ReferenceEquals(window, oldWindow))
                        {
                            Close(oldWindow, false);
                        }
                    }

                    windows[name] = new HostWindowPair(window, owned);
                }
                else
                {
                    windows.Add(name, new HostWindowPair(window, owned));
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unregisters the named window, optionally closing it.
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
        public bool UnregisterWindow(
            string name,           /* in */
            WindowType windowType, /* in */
            bool close             /* in */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "UnregisterWindow: name = {0}, windowType = {1}, close = {2}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(windowType), close));

            if (name == null)
                return false;

            lock (syncRoot)
            {
                if ((windows == null) || (windows.Count == 0))
                    return false;

                if (close)
                {
                    HostWindowPair pair;

                    if (windows.TryGetValue(name, out pair) &&
                        (pair != null) && pair.Y)
                    {
                        IHostWindow oldWindow = pair.X;

                        if (oldWindow != null)
                            /* IGNORED */
                            Close(oldWindow, false);
                    }
                }

                return windows.Remove(name);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the specified window, optionally shutting down.
        /// </summary>
        /// <param name="window">
        /// The host window to close.
        /// </param>
        /// <param name="shutdown">
        /// Non-zero to shut down after closing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool Close(
            IHostWindow window, /* in */
            bool shutdown       /* in */
            )
        {
            CheckDisposed();

            return CommonOps.CloseWindow(window, false, shutdown);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes and unregisters every window, shutting the registrar down.
        /// </summary>
        /// <param name="application">
        /// Non-zero when the plugin created the WPF application context; when
        /// zero, interactive windows are force-closed.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool Shutdown(
            bool application /* in */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "Shutdown: application = {0}", application));

            lock (syncRoot)
            {
                if (windows == null)
                    return false;

                StringList keys = new StringList(windows.Keys);

                for (int index = keys.Count - 1; index >= 0; index--)
                {
                    HostWindowPair pair;

                    if (windows.TryGetValue(keys[index], out pair) &&
                        (pair != null))
                    {
                        IHostWindow window = pair.X;

                        if (window == null)
                            continue;

                        //
                        // BUGFIX: If we did not create this application
                        //         context, we must close this window if
                        //         it is an interactive window.
                        //
                        bool mustClose = !application &&
                            ((window as IHostInteractiveWindow) != null);

                        /* IGNORED */
                        if ((mustClose || pair.Y) &&
                            Close(window, true)) /* throw */
                        {
                            windows.Remove(keys[index]);
                        }
                    }
                }

                return true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the resources used by this instance.
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
                throw new ObjectDisposedException(typeof(WindowRegistrar).Name);
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
        private void Dispose(bool disposing)
        {
            lock (syncRoot)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        //
                        // dispose managed resources here...
                        //

                        Shutdown(Shell.Window.WasApplicationCreated());
                    }

                    //
                    // release unmanaged resources here...
                    //
                    disposed = true;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizes this object, releasing any resources that were not
        /// released by an explicit call to <see cref="Dispose()" />.
        /// </summary>
        ~WindowRegistrar()
        {
            Dispose(false);
        }
        #endregion
    }
}
