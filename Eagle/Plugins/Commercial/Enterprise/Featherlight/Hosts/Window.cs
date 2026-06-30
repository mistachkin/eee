/*
 * Window.cs --
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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;
using _Hosts = Eagle._Hosts;

#if !CONSOLE
using ConsoleColor = Eagle._Components.Public.ConsoleColor;
#endif

namespace Featherlight.Hosts
{
    /// <summary>
    /// Implements a WPF-backed windowed interpreter host that routes input,
    /// output, prompting, sizing, and colors to Featherlight windows.
    /// </summary>
    [ObjectId("9372a55b-ebc4-4745-a4e0-ce73fdc1fe39")]
    public class Window : _Hosts.Core, IHostWindowManager, IDisposable
    {
        #region Private Constants
        /// <summary>
        /// The maximum number of bytes written to a window buffer at one time.
        /// </summary>
        private const int BufferWriteSize = 1048576; /* 1MB */
        /// <summary>
        /// The buffer size, in bytes, at which window output is cleared.
        /// </summary>
        private const int BufferClearSize = 2097152; /* 2MB */
        /// <summary>
        /// The maximum number of milliseconds to wait when writing output to a
        /// window.
        /// </summary>
        private const int WriteMilliseconds = 10000; /* 10 seconds */
        /// <summary>
        /// The maximum number of milliseconds to wait for a window to be
        /// created.
        /// </summary>
        private const int CreateWindowTimeout = 20000;
        /// <summary>
        /// The format string used to build the interactive window status text.
        /// </summary>
        private const string StatusFormat = "busy: current {0}, previous {1}";
        /// <summary>
        /// The format string used to build the window title.
        /// </summary>
        private const string TitleFormat = "{0} {1}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Constants
        /// <summary>
        /// The name of the box window used for debug output.
        /// </summary>
        protected static readonly string DebugBoxName = "Debug";
        /// <summary>
        /// The name of the box window used for complaint output.
        /// </summary>
        protected static readonly string ComplainBoxName = "Complain";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The unique identifier of this windowed host.
        /// </summary>
        private long windowId;
        /// <summary>
        /// The type of window used for interactive input.
        /// </summary>
        private WindowType inputWindowType;
        /// <summary>
        /// The type of window used for interactive output.
        /// </summary>
        private WindowType outputWindowType;
        /// <summary>
        /// The event handler invoked when a window is opened.
        /// </summary>
        private EventHandler openedHandler;
        /// <summary>
        /// The event handler invoked when a window is closed.
        /// </summary>
        private EventHandler closedHandler;
        /// <summary>
        /// The line terminator string used when writing output.
        /// </summary>
        private string newLine;
        /// <summary>
        /// Non-zero to create output windows separate from the interactive
        /// window.
        /// </summary>
        private bool createOutput;
        /// <summary>
        /// Non-zero to route trace listener output to this host.
        /// </summary>
        private bool traceToHost;
        /// <summary>
        /// The exit code to be used when this host is shut down.
        /// </summary>
        private ExitCode exitCode;
        /// <summary>
        /// The collection of managed windows, keyed by name.
        /// </summary>
        private IDictionary<string, IHostWindow> windows;
        /// <summary>
        /// The primary interactive window associated with this host.
        /// </summary>
        private IHostInteractiveWindow interactiveWindow;
        /// <summary>
        /// The output window currently being used for box output.
        /// </summary>
        private IHostOutputWindow boxWindow;
        /// <summary>
        /// The trace listener used to route trace output to a window.
        /// </summary>
        private TraceListener traceListener;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Featherlight Members
        #region Private Constructors
        /// <summary>
        /// Constructs an instance of this class.
        /// </summary>
        /// <param name="hostData">
        /// The data used to create and configure this host.
        /// </param>
        private Window(
            IHostData hostData /* in */
            )
            : base(hostData)
        {
            this.windowId = SafeGetInterpreterId();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs an instance of this class.
        /// </summary>
        /// <param name="hostData">
        /// The data used to create and configure this host.
        /// </param>
        /// <param name="interactiveWindow">
        /// The primary interactive window for this host.
        /// </param>
        /// <param name="openedHandler">
        /// The event handler invoked when a window is opened.
        /// </param>
        /// <param name="closedHandler">
        /// The event handler invoked when a window is closed.
        /// </param>
        /// <param name="newLine">
        /// The line terminator string used when writing output.
        /// </param>
        /// <param name="inputWindowType">
        /// The type of window used for interactive input.
        /// </param>
        /// <param name="outputWindowType">
        /// The type of window used for interactive output.
        /// </param>
        /// <param name="createOutput">
        /// Non-zero to create output windows separate from the interactive
        /// window.
        /// </param>
        /// <param name="traceToHost">
        /// Non-zero to route trace listener output to this host.
        /// </param>
        public Window(
            IHostData hostData,                       /* in */
            IHostInteractiveWindow interactiveWindow, /* in */
            EventHandler openedHandler,               /* in */
            EventHandler closedHandler,               /* in */
            string newLine,                           /* in */
            WindowType inputWindowType,               /* in */
            WindowType outputWindowType,              /* in */
            bool createOutput,                        /* in */
            bool traceToHost                          /* in */
            )
            : this(hostData)
        {
            this.interactiveWindow = interactiveWindow;
            this.openedHandler = openedHandler;
            this.closedHandler = closedHandler;
            this.newLine = newLine;
            this.inputWindowType = inputWindowType;
            this.outputWindowType = outputWindowType;
            this.createOutput = createOutput;
            this.traceToHost = traceToHost;

            ///////////////////////////////////////////////////////////////////

            Initialize();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        #region Host Flags Support
        /// <summary>
        /// Resets the cached host flags to their default state.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool PrivateResetHostFlags()
        {
            hostFlags = HostFlags.Invalid;

            return base.ResetHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the cached host flags if they have not yet been
        /// initialized.
        /// </summary>
        /// <returns>
        /// The host flags for this windowed host.
        /// </returns>
        protected override HostFlags MaybeInitializeHostFlags()
        {
            if (hostFlags == HostFlags.Invalid)
            {
                //
                // HACK: Force the prompt to be displayed because
                //       otherwise commands that produce no output
                //       can be confusing (i.e. sometimes, nothing
                //       appears to happen).
                //
                // HACK: Auto-flush the output and error host
                //       channels because the Flush method of the
                //       WpfStream class uses that opportunity to
                //       scroll the text box to its end.
                //
                // HACK: Color support is faked.  This host does
                //       not actually support colors (yet).
                //       Eventually, this host may be able to use
                //       the WPF TextRange classes and styles in
                //       order to support colors.
                //
                hostFlags = HostFlags.ForcePrompt |
                            HostFlags.Graphical |
                            HostFlags.UnlimitedSize |
                            HostFlags.MultipleLineInput |
                            HostFlags.AutoFlushHost |
                            HostFlags.AutoFlushOutput |
                            HostFlags.AutoFlushError |
                            base.MaybeInitializeHostFlags();
            }

            return hostFlags;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method assumes the lock is NOT held; otherwise, it
        //       will cause a deadlock.
        //
        /// <summary>
        /// Creates the input, output, error, and trace windows used by this
        /// host.
        /// </summary>
        private void Initialize()
        {
            //
            // NOTE: If an exception (e.g. OutOfMemoryException) gets
            //       caught while creating any of the windows below,
            //       the failed flag will be set to true and these
            //       windows will be disposed in the finally block.
            //
            bool failed = false;
            IHostInputWindow inputWindow = null;
            IHostOutputWindow outputWindow = null;
            IHostOutputWindow errorWindow = null;
            IHostOutputWindow traceWindow = null;

            ///////////////////////////////////////////////////////////////////

            try
            {
                inputWindow = GetWindow(
                    WindowType.Input, true) as IHostInputWindow;

                if (inputWindow != null)
                {
                    input = new HostStream(inputWindow, true, false);
                }
                else
                {
                    //
                    // NOTE: Maybe out-of-memory?  Signal failure so
                    //       that all partially created state will be
                    //       cleaned up.
                    //
                    failed = true;
                    return;
                }

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: Create new output windows that are distinct from
                //       the default (interactive) window?
                //
                if (createOutput)
                {
                    outputWindow = GetWindow(
                        WindowType.Output, true) as IHostOutputWindow;

                    if (outputWindow != null)
                    {
                        output = new HostStream(outputWindow, false, true);
                    }
                    else
                    {
                        //
                        // NOTE: Maybe out-of-memory?  Signal failure so
                        //       that all partially created state will be
                        //       cleaned up.
                        //
                        failed = true;
                        return;
                    }

                    errorWindow = GetWindow(
                        WindowType.Error, true) as IHostOutputWindow;

                    if (errorWindow != null)
                    {
                        error = new HostStream(errorWindow, false, true);
                    }
                    else
                    {
                        //
                        // NOTE: Maybe out-of-memory?  Signal failure so
                        //       that all partially created state will be
                        //       cleaned up.
                        //
                        failed = true;
                        return;
                    }

                    //
                    // NOTE: Is all trace listener output being sent
                    //       to the interpreter host?  If so, create a
                    //       special window to support this.
                    //
                    if (traceToHost)
                    {
                        traceWindow = GetWindow(
                            WindowType.Trace, true) as IHostOutputWindow;

                        if (traceWindow != null)
                        {
                            traceListener = new WindowTraceListener(
                                CommonOps.GetWindowName(traceWindow),
                                traceWindow, newLine, BufferWriteSize,
                                BufferClearSize, WriteMilliseconds);

                            Trace.Listeners.Add(traceListener);
                        }
                        else
                        {
                            //
                            // NOTE: Maybe out-of-memory?  Signal failure
                            //       so that all partially created state
                            //       will be cleaned up.
                            //
                            failed = true;
                            return;
                        }
                    }
                }
                else if (interactiveWindow != null)
                {
                    output = new HostStream(interactiveWindow, false, true);
                    error = new HostStream(interactiveWindow, false, true);
                }
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                if (failed)
                {
                    MaybeDisposeStreams();

                    MaybeDisposeWindows(
                        ref inputWindow, ref outputWindow, ref errorWindow,
                        ref traceWindow);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a special (box or interactive) window should be
        /// used for the request and, if so, returns it.
        /// </summary>
        /// <param name="id">
        /// The identifier associated with the window.
        /// </param>
        /// <param name="name">
        /// The name of the window.
        /// </param>
        /// <param name="windowType">
        /// The type of the window being requested.
        /// </param>
        /// <param name="create">
        /// Non-zero if the window may be created.
        /// </param>
        /// <param name="window">
        /// Upon success, receives the special window to use.
        /// </param>
        /// <returns>
        /// Non-zero if a special window was found; otherwise, zero.
        /// </returns>
        private bool MaybeUseSpecialWindow(
            long id,               /* in */
            string name,           /* in */
            WindowType windowType, /* in */
            bool create,           /* in */
            ref IHostWindow window /* out */
            )
        {
            if (CommonOps.HasWindowType(windowType, WindowType.Box))
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (boxWindow != null)
                    {
                        window = boxWindow;
                        return true;
                    }

                    if (!create &&
                        (outputWindowType != WindowType.Interactive))
                    {
                        if (windows != null)
                        {
                            string outputName = CommonOps.GetWindowName(
                                CommonOps.WindowTypeToName(outputWindowType),
                                id);

                            if ((outputName != null) &&
                                windows.TryGetValue(outputName, out window))
                            {
                                return true;
                            }
                        }
                    }

                    if (CommonOps.IsInteractiveWindowName(name) &&
                        (interactiveWindow != null))
                    {
                        window = interactiveWindow;
                        return true;
                    }
                }
            }

            if (CommonOps.HasWindowType(windowType, WindowType.Interactive))
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (CommonOps.IsInteractiveWindowName(name) &&
                        (interactiveWindow != null))
                    {
                        window = interactiveWindow;
                        return true;
                    }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified named window or replaces the existing window
        /// with the same name.
        /// </summary>
        /// <param name="name">
        /// The name of the window.
        /// </param>
        /// <param name="window">
        /// The window to add or update.
        /// </param>
        /// <param name="initialize">
        /// Non-zero to create the window collection if necessary.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool AddOrUpdateWindow(
            string name,        /* in */
            IHostWindow window, /* in */
            bool initialize     /* in */
            )
        {
            //
            // NOTE: If the specified (named) window is already present,
            //       close it and replace it with the new one.  We could
            //       prevent the named window from being replaced here;
            //       however, this method does not make policy decisions.
            //
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (name == null)
                    return false;

                if (windows != null)
                {
                    IHostWindow oldWindow;

                    if (windows.TryGetValue(name, out oldWindow))
                    {
                        if (!Object.ReferenceEquals(window, oldWindow))
                        {
                            if (oldWindow != null)
                                oldWindow.Close();

                            windows[name] = window;
                        }
                    }
                    else
                    {
                        windows.Add(name, window);
                    }

                    return true;
                }
                else if (initialize)
                {
                    windows = new Dictionary<string, IHostWindow>();
                    windows.Add(name, window);

                    return true;
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the specified named window, optionally closing its
        /// interpreter channels.
        /// </summary>
        /// <param name="name">
        /// The name of the window to remove.
        /// </param>
        /// <param name="windowType">
        /// The type of the window to remove.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter whose channels may be removed.
        /// </param>
        /// <param name="channels">
        /// Non-zero to remove the associated interpreter channels.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool RemoveWindow(
            string name,             /* in */
            WindowType windowType,   /* in */
            Interpreter interpreter, /* in */
            bool channels            /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (name == null)
                    return false;

                if ((windows == null) || (windows.Count == 0))
                    return false;

                if (channels &&
                    (interpreter != null) && !interpreter.Disposing &&
                    !MaybeRemoveChannels(interpreter, windowType))
                {
                    return false;
                }

                return windows.Remove(name);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and displays a new window on a dedicated thread.
        /// </summary>
        /// <param name="obj">
        /// The window creation request, as a CreateWindowTriplet.
        /// </param>
        private void CreateWindow(
            object obj /* in */
            ) // ParameterizedThreadStart
        {
            try
            {
                IHostWindowFactory windowFactory;

                lock (syncRoot)
                {
                    windowFactory = this.interactiveWindow;
                }

                if (windowFactory == null)
                    return;

                CreateWindowTriplet windowTriplet = obj as CreateWindowTriplet;

                if (windowTriplet == null)
                    return;

                WindowNameTriplet nameTriplet = windowTriplet.X;

                if (nameTriplet == null)
                    return;

                long id = nameTriplet.X;
                string name = nameTriplet.Y;

                if (name == null)
                    return;

                WindowType windowType = nameTriplet.Z;

                //
                // HACK: Enforce minimum size constraints on the input and
                //       output channel windows.
                //
                bool minimumSize = false;
                bool autoSize = false;
                bool autoFlush = false;

                if (CommonOps.HasWindowType(windowType, WindowType.Input) ||
                    CommonOps.HasWindowType(windowType, WindowType.Output) ||
                    CommonOps.HasWindowType(windowType, WindowType.Error))
                {
                    minimumSize = true;
                }
                else if (CommonOps.HasWindowType(windowType, WindowType.Box))
                {
                    autoSize = true;
                    autoFlush = true;
                }

                IHostInteractiveWindow interactiveWindow =
                    windowFactory as IHostInteractiveWindow;

                IHostWindowRegistrar windowRegistrar =
                    (interactiveWindow != null) ?
                        interactiveWindow.WindowRegistrar : null;

                windowTriplet.Y = windowFactory.CreateWindow(
                    this, windowRegistrar, id, name, windowType,
                    this.InputWindowType, this.OutputWindowType,
                    this.OpenedHandler, this.ClosedHandler,
                    WindowPositionInfo.Automatic(), minimumSize,
                    autoSize, false, autoFlush);

                IHostWindow window = windowTriplet.Y;

                if (window == null)
                    return;

                window.SetTitle(name);
                window.Refresh();

                AddOrUpdateWindow(name, window, true);

                EventWaitHandle @event = windowTriplet.Z;

                if (@event != null)
                    @event.Set();

                window.ShowDialog();

                CommonOps.Shutdown();
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method assumes the lock is held.
        //
        /// <summary>
        /// Removes the interpreter channels associated with the specified
        /// window type, if any.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose channels may be removed.
        /// </param>
        /// <param name="windowType">
        /// The type of window whose channels should be removed.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool MaybeRemoveChannels(
            Interpreter interpreter, /* in */
            WindowType windowType    /* in */
            )
        {
            if (CommonOps.HasWindowType(windowType, WindowType.Input) &&
                (input != null))
            {
                if ((interpreter != null) &&
                    interpreter.IsStreamForChannel(
                        null, ChannelType.Input, input))
                {
                    ReturnCode closeCode;
                    Result closeError = null;

                    closeCode = interpreter.RemoveChannel(
                        null, ChannelType.Input, false, true, true,
                        ref closeError);

                    if (closeCode != ReturnCode.Ok)
                    {
                        Complain(closeCode, closeError);
                        return false;
                    }

                    input = null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (CommonOps.HasWindowType(windowType, WindowType.Output) &&
                (output != null))
            {
                if ((interpreter != null) &&
                    interpreter.IsStreamForChannel(
                        null, ChannelType.Output, output))
                {
                    ReturnCode closeCode;
                    Result closeError = null;

                    closeCode = interpreter.RemoveChannel(
                        null, ChannelType.Output, false, true, true,
                        ref closeError);

                    if (closeCode != ReturnCode.Ok)
                    {
                        Complain(closeCode, closeError);
                        return false;
                    }

                    output = null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (CommonOps.HasWindowType(windowType, WindowType.Error) &&
                (error != null))
            {
                if ((interpreter != null) &&
                    interpreter.IsStreamForChannel(
                        null, ChannelType.Error, error))
                {
                    ReturnCode closeCode;
                    Result closeError = null;

                    closeCode = interpreter.RemoveChannel(
                        null, ChannelType.Error, false, true, true,
                        ref closeError);

                    if (closeCode != ReturnCode.Ok)
                    {
                        Complain(closeCode, closeError);
                        return false;
                    }

                    error = null;
                }
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method assumes the lock is held.
        //
        /// <summary>
        /// Disposes the input, output, and error streams, if any.
        /// </summary>
        private void MaybeDisposeStreams()
        {
            try
            {
                if (error != null)
                {
                    error.Dispose();
                    error = null;
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }

            ///////////////////////////////////////////////////////////////////

            try
            {
                if (output != null)
                {
                    output.Dispose();
                    output = null;
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }

            ///////////////////////////////////////////////////////////////////

            try
            {
                if (input != null)
                {
                    input.Dispose();
                    input = null;
                }
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This method assumes the lock is held.
        //
        /// <summary>
        /// Disposes the specified input, output, error, and trace windows, if
        /// any.
        /// </summary>
        /// <param name="inputWindow">
        /// The input window to dispose; set to null upon return.
        /// </param>
        /// <param name="outputWindow">
        /// The output window to dispose; set to null upon return.
        /// </param>
        /// <param name="errorWindow">
        /// The error window to dispose; set to null upon return.
        /// </param>
        /// <param name="traceWindow">
        /// The trace window to dispose; set to null upon return.
        /// </param>
        private void MaybeDisposeWindows(
            ref IHostInputWindow inputWindow,   /* in, out */
            ref IHostOutputWindow outputWindow, /* in, out */
            ref IHostOutputWindow errorWindow,  /* in, out */
            ref IHostOutputWindow traceWindow   /* in, out */
            )
        {
            ReturnCode disposeCode;
            Result disposeError = null;

            ///////////////////////////////////////////////////////////////////

            if (traceWindow != null)
            {
                disposeCode = Utility.TryDisposeObject<IHostOutputWindow>(
                    ref traceWindow, ref disposeError);

                traceWindow = null;

                if (disposeCode != ReturnCode.Ok)
                    Complain(disposeCode, disposeError);
            }

            ///////////////////////////////////////////////////////////////////

            if (errorWindow != null)
            {
                disposeCode = Utility.TryDisposeObject<IHostOutputWindow>(
                    ref errorWindow, ref disposeError);

                errorWindow = null;

                if (disposeCode != ReturnCode.Ok)
                    Complain(disposeCode, disposeError);
            }

            ///////////////////////////////////////////////////////////////////

            if (outputWindow != null)
            {
                disposeCode = Utility.TryDisposeObject<IHostOutputWindow>(
                    ref outputWindow, ref disposeError);

                outputWindow = null;

                if (disposeCode != ReturnCode.Ok)
                    Complain(disposeCode, disposeError);
            }

            ///////////////////////////////////////////////////////////////////

            if (inputWindow != null)
            {
                disposeCode = Utility.TryDisposeObject<IHostInputWindow>(
                    ref inputWindow, ref disposeError);

                inputWindow = null;

                if (disposeCode != ReturnCode.Ok)
                    Complain(disposeCode, disposeError);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports an error condition associated with this host.
        /// </summary>
        /// <param name="code">
        /// The return code associated with the error.
        /// </param>
        /// <param name="result">
        /// The result containing information about the error.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Complain(
            ReturnCode code, /* in */
            Result result    /* in */
            )
        {
            Interpreter interpreter = SafeGetInterpreter();

            if (interpreter != null)
            {
                if (interpreter.Disposed)
                {
                    DebugTrace(String.Format(
                        "Complain: window host {0} interpreter disposed",
                        windowId), TracePriority.Highest);
                }
            }
            else
            {
                DebugTrace(String.Format(
                    "Complain: window host {0} missing interpreter",
                    windowId), TracePriority.Highest);
            }

            Utility.Complain(interpreter, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a diagnostic trace message associated with this host.
        /// </summary>
        /// <param name="message">
        /// The trace message to write.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DebugTrace(
            string message /* in */
            )
        {
            try
            {
                Utility.DebugTrace(
                    SafeGetInterpreter(), message, typeof(Window).Name,
                    TracePriority.MediumLow |
                        TracePriority.ViaWrapperFromPlugin, 1);
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a diagnostic trace message with the specified priority.
        /// </summary>
        /// <param name="message">
        /// The trace message to write.
        /// </param>
        /// <param name="priority">
        /// The priority of the trace message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DebugTrace(
            string message,        /* in */
            TracePriority priority /* in */
            )
        {
            //
            // HACK: Avoid calling into Complain here because we may be
            //       called from within an existing Complain call.
            //
            try
            {
                Utility.DebugTrace(
                    SafeGetInterpreter(), message, typeof(Window).Name,
                    priority | TracePriority.ViaWrapperFromPlugin, 1);
            }
            catch
            {
                // do nothing.
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the specified character followed by the configured line
        /// terminator.
        /// </summary>
        /// <param name="value">
        /// The character to append the line terminator to.
        /// </param>
        /// <returns>
        /// The character followed by the line terminator.
        /// </returns>
        private string MaybeAppendNewLine(
            char value /* in */
            )
        {
            lock (syncRoot)
            {
                return String.Format("{0}{1}", value, newLine);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the specified string followed by the configured line
        /// terminator.
        /// </summary>
        /// <param name="value">
        /// The string to append the line terminator to.
        /// </param>
        /// <returns>
        /// The string followed by the line terminator.
        /// </returns>
        private string MaybeAppendNewLine(
            string value /* in */
            )
        {
            lock (syncRoot)
            {
                return String.Format("{0}{1}", value, newLine);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Gets the identifier of the interpreter associated with this host,
        /// or zero if unavailable.
        /// </summary>
        /// <returns>
        /// The interpreter identifier, or zero if unavailable.
        /// </returns>
        protected virtual long SafeGetInterpreterId()
        {
            Interpreter interpreter = SafeGetInterpreter();

            if (interpreter != null)
                return interpreter.IdNoThrow;

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the title of the interactive window.
        /// </summary>
        /// <param name="setup">
        /// Non-zero to set up the title; otherwise, zero.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected virtual bool SetupTitle(
            bool setup /* in */
            )
        {
            IHostWindow window = GetWindow(WindowType.Interactive, false);

            if (window != null)
            {
                //
                // NOTE: Make sure we can easily differentiate between the
                //       different interactive windows (i.e. currently,
                //       there is one per interpreter Id and our window Id
                //       is the same as that interpreter Id).
                //
                string title = CommonOps.FormatWindowName(String.Format(
                    TitleFormat, DefaultTitle, base.Title).Trim(),
                    this.WindowId);

                return window.SetTitle(title);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the named box window.
        /// </summary>
        /// <param name="name">
        /// The name of the box window.
        /// </param>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected virtual bool WriteBox(
            string name, /* in */
            string value /* in */
            )
        {
            int left = _Position.Invalid;
            int top = _Position.Invalid;

            return WriteBox(
                name, value, null, false, false, ref left, ref top);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the named box window, if any.
        /// </summary>
        /// <param name="name">
        /// The name of the box window to close.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        protected virtual bool CloseBox(
            string name /* in */
            )
        {
            IHostOutputWindow outputWindow = GetWindow(
                name, WindowType.Box, false) as IHostOutputWindow;

            if (outputWindow != null)
                return outputWindow.Close();

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Featherlight Interface Members
        #region IHostWindowIdentifier Members
        /// <summary>
        /// Gets or sets the unique identifier of this windowed host.
        /// </summary>
        public virtual long WindowId
        {
            get { CheckDisposed(); lock (syncRoot) { return windowId; } }
            set { CheckDisposed(); lock (syncRoot) { windowId = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the name of this window.
        /// </summary>
        public virtual string WindowName
        {
            get { CheckDisposed(); return null; }
            set { CheckDisposed(); throw new NotSupportedException(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of this window.
        /// </summary>
        public virtual WindowType WindowType
        {
            get { CheckDisposed(); return WindowType.None; }
            set { CheckDisposed(); throw new NotSupportedException(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of window used for interactive input.
        /// </summary>
        public virtual WindowType InputWindowType
        {
            get { CheckDisposed(); return inputWindowType; }
            set { CheckDisposed(); inputWindowType = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of window used for interactive output.
        /// </summary>
        public virtual WindowType OutputWindowType
        {
            get { CheckDisposed(); return outputWindowType; }
            set { CheckDisposed(); outputWindowType = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostEventManager Members
        /// <summary>
        /// Gets or sets the event handler invoked when a window is opened.
        /// </summary>
        public virtual EventHandler OpenedHandler
        {
            get { CheckDisposed(); lock (syncRoot) { return openedHandler; } }
            set { CheckDisposed(); lock (syncRoot) { openedHandler = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the event handler invoked when a window is closed.
        /// </summary>
        public virtual EventHandler ClosedHandler
        {
            get { CheckDisposed(); lock (syncRoot) { return closedHandler; } }
            set { CheckDisposed(); lock (syncRoot) { closedHandler = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowRegistrar Members
        /// <summary>
        /// Gets a value indicating whether this instance is currently locked.
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
        /// Gets or sets the exit code to be used when this host is shut down.
        /// </summary>
        public ExitCode ExitCode
        {
            get { CheckDisposed(); lock (syncRoot) { return exitCode; } }
            set { CheckDisposed(); lock (syncRoot) { exitCode = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of windows currently managed by this host.
        /// </summary>
        public int WindowCount
        {
            get
            {
                CheckDisposed();

                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (windows == null)
                        return 0;

                    return windows.Count;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds a managed window with the specified name and type.
        /// </summary>
        /// <param name="name">
        /// The name of the window to find, or null to match any name.
        /// </param>
        /// <param name="windowType">
        /// The type of the window to find.
        /// </param>
        /// <returns>
        /// The matching window, or null if none was found.
        /// </returns>
        public IHostWindow FindWindow(
            string name,          /* in */
            WindowType windowType /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (windows == null)
                    return null;

                foreach (KeyValuePair<string, IHostWindow> pair in windows)
                {
                    IHostWindow window = pair.Value;

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
        /// Registers the specified named window with this host.
        /// </summary>
        /// <param name="name">
        /// The name of the window to register.
        /// </param>
        /// <param name="window">
        /// The window to register.
        /// </param>
        /// <param name="owned">
        /// Non-zero if the window is owned by this host.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool RegisterWindow(
            string name,        /* in */
            IHostWindow window, /* in */
            bool owned          /* in: IGNORED */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "RegisterWindow: name = {0}, window = {1}, owned = {2}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(window), owned));

            return AddOrUpdateWindow(name, window, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unregisters the named window from this host.
        /// </summary>
        /// <param name="name">
        /// The name of the window to unregister.
        /// </param>
        /// <param name="windowType">
        /// The type of the window to unregister.
        /// </param>
        /// <param name="close">
        /// Non-zero to close the window when unregistering.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool UnregisterWindow(
            string name,           /* in */
            WindowType windowType, /* in */
            bool close             /* in: IGNORED */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "UnregisterWindow: name = {0}, windowType = {1}, close = {2}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(windowType), close));

            return RemoveWindow(
                name, windowType, SafeGetInterpreter(), true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the specified window, optionally shutting down the host.
        /// </summary>
        /// <param name="window">
        /// The window to close.
        /// </param>
        /// <param name="shutdown">
        /// Non-zero to shut down the host after closing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Close(
            IHostWindow window, /* in */
            bool shutdown       /* in */
            )
        {
            CheckDisposed();

            return CommonOps.CloseWindow(window, false, shutdown);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes all windows managed by this host.
        /// </summary>
        /// <param name="application">
        /// Non-zero if the entire application is shutting down.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Shutdown(
            bool application /* in: NOT USED */
            )
        {
            CheckDisposed();

            DebugTrace(String.Format(
                "Shutdown: application = {0}", application));

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (windows == null)
                    return false;

                IDictionary<string, IHostWindow> localWindows =
                    new Dictionary<string, IHostWindow>(windows);

                foreach (KeyValuePair<string, IHostWindow> pair
                        in localWindows)
                {
                    IHostWindow window = pair.Value;

                    if (window != null)
                    {
                        /* IGNORED */
                        UnregisterWindow(
                            pair.Key, window.WindowType, false);

                        /* IGNORED */
                        window.CloseAsync(); /* throw */
                    }
                }

                windows.Clear();

                return true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowManager Members
        /// <summary>
        /// Determines whether this instance is in the process of being
        /// disposed.
        /// </summary>
        /// <returns>
        /// Non-zero if this instance is being disposed; otherwise, zero.
        /// </returns>
        public virtual bool IsDisposing()
        {
            //
            // NOTE: This would defeat the purpose of this method (i.e.
            //       do not uncomment it).
            //
            // CheckDisposed();

            return (disposeCount > 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Injects the specified value into the interactive window as input.
        /// </summary>
        /// <param name="value">
        /// The input value to inject.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool InjectInput(
            string value /* in */
            )
        {
            CheckDisposed();

            if (value != null)
            {
                value = value.Trim();

                string boxCharacterSet = GetBoxCharacterSet();

                if (!String.IsNullOrEmpty(boxCharacterSet))
                    value = value.Trim(boxCharacterSet.ToCharArray());

                IHostInteractiveWindow interactiveWindow;

                lock (syncRoot)
                {
                    interactiveWindow = this.interactiveWindow;
                }

                if (interactiveWindow != null)
                    return interactiveWindow.InsertInput(value);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window of the specified type, optionally creating it.
        /// </summary>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <returns>
        /// The window, or null if none was found or created.
        /// </returns>
        public virtual IHostWindow GetWindow(
            WindowType windowType, /* in */
            bool create            /* in */
            )
        {
            CheckDisposed();

            return GetWindow(this.WindowId, windowType, create);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window of the specified type for the specified identifier,
        /// optionally creating it.
        /// </summary>
        /// <param name="id">
        /// The identifier associated with the window.
        /// </param>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <returns>
        /// The window, or null if none was found or created.
        /// </returns>
        public virtual IHostWindow GetWindow(
            long id,               /* in */
            WindowType windowType, /* in */
            bool create            /* in */
            )
        {
            CheckDisposed();

            return GetWindow(
                id, CommonOps.WindowTypeToName(windowType), windowType,
                create);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the named window of the specified type, optionally creating
        /// it.
        /// </summary>
        /// <param name="name">
        /// The name of the window.
        /// </param>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <returns>
        /// The window, or null if none was found or created.
        /// </returns>
        public virtual IHostWindow GetWindow(
            string name,           /* in */
            WindowType windowType, /* in */
            bool create            /* in */
            )
        {
            CheckDisposed();

            return GetWindow(this.WindowId, name, windowType, create);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the named window of the specified type for the specified
        /// identifier, optionally creating it.
        /// </summary>
        /// <param name="id">
        /// The identifier associated with the window.
        /// </param>
        /// <param name="name">
        /// The name of the window.
        /// </param>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <returns>
        /// The window, or null if none was found or created.
        /// </returns>
        public virtual IHostWindow GetWindow(
            long id,               /* in */
            string name,           /* in */
            WindowType windowType, /* in */
            bool create            /* in */
            )
        {
            CheckDisposed();

            //
            // NOTE: The window name cannot be null.
            //
            if (name == null)
                return null;

            //
            // NOTE: Ok, the window name is valid; however, the title
            //       (which is currently always identical to the name)
            //       needs to include the window Id as well.
            //
            name = CommonOps.GetWindowName(name, id);

            //
            // NOTE: Check if we need to use the current "box window"
            //       or the interactive window.
            //
            IHostWindow window = null;

            if (MaybeUseSpecialWindow(
                    id, name, windowType, create, ref window))
            {
                return window;
            }

            bool found;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (windows != null)
                {
                    found = windows.TryGetValue(name, out window);
                }
                else
                {
                    window = null;
                    found = false;
                }
            }

            if (!found && create)
            {
                //
                // BUGFIX: Disallow all host window creation when the
                //         host has been marked as exiting; otherwise,
                //         there may be deadlocks due to locks being
                //         held while our plugin is terminated.
                //
                if (IsExiting())
                    return null;

                EventWaitHandle @event = new ManualResetEvent(false);

                CreateWindowTriplet triplet = new CreateWindowTriplet(
                   true, new WindowNameTriplet(id, name, windowType),
                   null, @event);

                //
                // HACK: Avoid using the active interpreter stack due
                //       to test "interp-1.12".  Otherwise, we cannot
                //       clear all the references to the interpreter
                //       that is our parent.
                //
                Thread thread = Engine.CreateThread(
                    SafeGetInterpreter(), CreateWindow, 0, true, false,
                    false);

                if (thread != null)
                {
                    try
                    {
                        thread.Name = name;
                        thread.Start(triplet); /* throw */

                        if (@event.WaitOne(CreateWindowTimeout))
                            window = triplet.Y;
                    }
                    catch (Exception e)
                    {
                        Complain(ReturnCode.Error, e);
                    }
                }
            }

            return window;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Activates the named window of the specified type, optionally
        /// creating it.
        /// </summary>
        /// <param name="name">
        /// The name of the window to activate.
        /// </param>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool ActivateWindow(
            string name,           /* in */
            WindowType windowType, /* in */
            bool create            /* in */
            )
        {
            CheckDisposed();

            if (name == null)
                return false;

            IHostWindow window = GetWindow(name, windowType, create);

            if (window != null)
                return window.Activate();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Positions the named window of the specified type, optionally
        /// creating it.
        /// </summary>
        /// <param name="name">
        /// The name of the window to position.
        /// </param>
        /// <param name="windowType">
        /// The type of the window.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The positioning information to apply.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not exist.
        /// </param>
        /// <param name="always">
        /// Non-zero to position the window even if it already exists.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool PositionWindow(
            string name,                           /* in */
            WindowType windowType,                 /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool create,                           /* in */
            bool always                            /* in */
            )
        {
            CheckDisposed();

            if (name == null)
                return false;

            IHostWindow window = GetWindow(name, windowType, false);
            bool exists = (window != null);

            if (create && (window == null))
                window = GetWindow(name, windowType, true);

            if ((always || !exists) && (window != null))
                return window.Position(windowPositionInfo);

            return false;
        }
        #endregion
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Eagle Core Library Members
        #region IGetInterpreter / ISetInterpreter Members
        /// <summary>
        /// Gets or sets the interpreter associated with this windowed host.
        /// </summary>
        public override Interpreter Interpreter
        {
            set
            {
                CheckDisposed();

                //
                // HACK: If the interpreter is purposely being reset, e.g.
                //       in order to facilitate a test like "interp-1.12",
                //       then make sure to null out all references to it
                //       that we know about; however, do not simply reset
                //       our interpreter to whatever value is passed in.
                //
                if (value == null)
                {
                    Interpreter localInterpreter = base.Interpreter;

                    /* IGNORED */
                    Shell.Window.MaybeResetPluginInterpreter(
                        localInterpreter);

                    if (interactiveWindow != null)
                    {
                        /* IGNORED */
                        interactiveWindow.MaybeResetInteractiveInterpreter(
                            localInterpreter);
                    }
                }

                base.Interpreter = value;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IInteractiveHost Members
        #region TODO: Derived Classes (Should Customize)
        /// <summary>
        /// Updates the interactive window status as interactive processing
        /// begins.
        /// </summary>
        /// <param name="levels">
        /// The current number of interactive nesting levels.
        /// </param>
        /// <param name="text">
        /// Receives the text to be processed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode BeginProcessing(
            int levels,      /* in */
            ref string text, /* out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
            }

            //
            // BUGFIX: Only modify the status if the interpreter for this
            //         host is the primary one for the interactive window.
            //
            Interpreter interpreter = SafeGetInterpreter();

            if ((interactiveWindow != null) &&
                interactiveWindow.MatchInteractiveInterpreter(interpreter))
            {
                levels++;

                string value = (levels > 0) ?
                    String.Format(StatusFormat, levels, levels - 1) : null;

                if (!interactiveWindow.SetStatus(value))
                {
                    error = "failed to set interactive window status";
                    return ReturnCode.Error;
                }
            }

            return base.BeginProcessing(levels, ref text, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the interactive window status as interactive processing
        /// ends.
        /// </summary>
        /// <param name="levels">
        /// The current number of interactive nesting levels.
        /// </param>
        /// <param name="text">
        /// Receives the text to be processed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode EndProcessing(
            int levels,      /* in */
            ref string text, /* out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
            }

            //
            // BUGFIX: Only modify the status if the interpreter for this
            //         host is the primary one for the interactive window.
            //
            Interpreter interpreter = SafeGetInterpreter();

            if ((interactiveWindow != null) &&
                interactiveWindow.MatchInteractiveInterpreter(interpreter))
            {
                levels--;

                string value = (levels > 0) ?
                    String.Format(StatusFormat, levels, levels + 1) : null;

                if (!interactiveWindow.SetStatus(value))
                {
                    error = "failed to set interactive window status";
                    return ReturnCode.Error;
                }
            }

            return base.EndProcessing(levels, ref text, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the interactive window status when interactive processing
        /// is done.
        /// </summary>
        /// <param name="levels">
        /// The current number of interactive nesting levels.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode DoneProcessing(
            int levels,      /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
            }

            //
            // BUGFIX: Only modify the status if the interpreter for this
            //         host is the primary one for the interactive window.
            //
            Interpreter interpreter = SafeGetInterpreter();

            if ((interactiveWindow != null) &&
                interactiveWindow.MatchInteractiveInterpreter(interpreter))
            {
                levels--;

                string value = (levels > 0) ?
                    String.Format(StatusFormat, levels, levels + 1) : null;

                if (!interactiveWindow.SetStatus(value))
                {
                    error = "failed to set interactive window status";
                    return ReturnCode.Error;
                }
            }

            return base.DoneProcessing(levels, ref error);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Maybe Customize)
        /// <summary>
        /// Gets or sets the title of the interactive window.
        /// </summary>
        public override string Title
        {
            set
            {
                CheckDisposed();

                base.Title = value;
                SetupTitle(true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Refreshes the title of the interactive window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool RefreshTitle()
        {
            CheckDisposed();

            return SetupTitle(true);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Should Customize)
        /// <summary>
        /// Determines whether input for this host is redirected.
        /// </summary>
        /// <returns>
        /// Non-zero, because input always comes from a window.
        /// </returns>
        public override bool IsInputRedirected()
        {
            CheckDisposed();

            //
            // NOTE: We have no input stream; therefore, the input
            //       must come from somewhere else (i.e. a window).
            //
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this host is open.
        /// </summary>
        /// <returns>
        /// Non-zero, because this host is always open.
        /// </returns>
        public override bool IsOpen()
        {
            CheckDisposed();

            /* ALWAYS OPEN */
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Pauses this host.
        /// </summary>
        /// <returns>
        /// Always returns zero, because pausing is not implemented.
        /// </returns>
        public override bool Pause()
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Flushes any pending output to the box window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Flush()
        {
            CheckDisposed();
            EnterWriteLevel();

            try
            {
                IHostOutputWindow outputWindow = GetWindow(
                    WindowType.Box, false) as IHostOutputWindow;

                if (outputWindow != null)
                    return outputWindow.Flush();
            }
            catch
            {
                // do nothing.
            }
            finally
            {
                ExitWriteLevel();
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// The cached host flags for this windowed host.
        /// </summary>
        private HostFlags hostFlags = HostFlags.Invalid;
        /// <summary>
        /// Gets the host flags for this windowed host.
        /// </summary>
        /// <returns>
        /// The host flags for this host.
        /// </returns>
        public override HostFlags GetHostFlags()
        {
            CheckDisposed();

            return MaybeInitializeHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a line of input from the input window.
        /// </summary>
        /// <param name="value">
        /// Upon success, receives the line of input that was read.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool ReadLine(
            ref string value /* out */
            )
        {
            CheckDisposed();
            EnterReadLevel();

            try
            {
                IHostInputWindow inputWindow = GetWindow(
                    this.InputWindowType, false) as IHostInputWindow;

                if (inputWindow != null)
                {
                    value = inputWindow.ReadLine();
                    return true;
                }
            }
            finally
            {
                ExitReadLevel();
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a line terminator to the output window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool WriteLine()
        {
            CheckDisposed();
            EnterWriteLevel();

            try
            {
                return Write(MaybeAppendNewLine(null));
            }
            catch
            {
                // do nothing.
            }
            finally
            {
                ExitWriteLevel();
            }

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStreamHost Members
        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Non-zero if the default input stream has been requested.
        /// </summary>
        private bool useDefaultIn;
        /// <summary>
        /// Gets the default input stream for this host.
        /// </summary>
        public override Stream DefaultIn
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    useDefaultIn = true;

                    return input;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if the default output stream has been requested.
        /// </summary>
        private bool useDefaultOut;
        /// <summary>
        /// Gets the default output stream for this host.
        /// </summary>
        public override Stream DefaultOut
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    useDefaultOut = true;

                    return output;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if the default error stream has been requested.
        /// </summary>
        private bool useDefaultError;
        /// <summary>
        /// Gets the default error stream for this host.
        /// </summary>
        public override Stream DefaultError
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    useDefaultError = true;

                    return error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The stream used for interactive input.
        /// </summary>
        private Stream input;
        /// <summary>
        /// Gets or sets the input stream for this host.
        /// </summary>
        public override Stream In
        {
            get { CheckDisposed(); lock (syncRoot) { return input; } }
            set { CheckDisposed(); lock (syncRoot) { input = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The stream used for interactive output.
        /// </summary>
        private Stream output;
        /// <summary>
        /// Gets or sets the output stream for this host.
        /// </summary>
        public override Stream Out
        {
            get { CheckDisposed(); lock (syncRoot) { return output; } }
            set { CheckDisposed(); lock (syncRoot) { output = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The stream used for error output.
        /// </summary>
        private Stream error;
        /// <summary>
        /// Gets or sets the error stream for this host.
        /// </summary>
        public override Stream Error
        {
            get { CheckDisposed(); lock (syncRoot) { return error; } }
            set { CheckDisposed(); lock (syncRoot) { error = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the channel translations for the default standard channels.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SetupChannels()
        {
            //
            // NOTE: If this host is marked as exiting, do not modify
            //       standard channels as that could lead to deadlock
            //       if the interpreter is being disposed.  In theory,
            //       getting to this method when the host is exiting
            //       should be impossible.
            //
            if (IsExiting())
                return false;

            Interpreter interpreter = SafeGetInterpreter();

            if (interpreter == null)
                return false;

            bool result = true;
            Result error = null; /* REUSED */

            if (useDefaultIn && interpreter.SetChannelTranslation(
                    null, ChannelType.Input, StreamTranslation.auto,
                    StreamTranslation.auto, ref error) != ReturnCode.Ok)
            {
                result = false;
            }

            error = null; /* REUSED */

            if (useDefaultOut && interpreter.SetChannelTranslation(
                    null, ChannelType.Output, StreamTranslation.binary,
                    StreamTranslation.binary, ref error) != ReturnCode.Ok)
            {
                result = false;
            }

            error = null; /* REUSED */

            if (useDefaultError && interpreter.SetChannelTranslation(
                    null, ChannelType.Error, StreamTranslation.binary,
                    StreamTranslation.binary, ref error) != ReturnCode.Ok)
            {
                result = false;
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Gets or sets the encoding used for input.
        /// </summary>
        public override Encoding InputEncoding
        {
            get { CheckDisposed(); return null; }
            set { CheckDisposed(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the encoding used for output.
        /// </summary>
        public override Encoding OutputEncoding
        {
            get { CheckDisposed(); return null; }
            set { CheckDisposed(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the encoding used for error output.
        /// </summary>
        public override Encoding ErrorEncoding
        {
            get { CheckDisposed(); return null; }
            set { CheckDisposed(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the input stream to its default state.
        /// </summary>
        /// <returns>
        /// Always returns zero, because resetting is not supported.
        /// </returns>
        public override bool ResetIn()
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the output stream to its default state.
        /// </summary>
        /// <returns>
        /// Always returns zero, because resetting is not supported.
        /// </returns>
        public override bool ResetOut()
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the error stream to its default state.
        /// </summary>
        /// <returns>
        /// Always returns zero, because resetting is not supported.
        /// </returns>
        public override bool ResetError()
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether output for this host is redirected.
        /// </summary>
        /// <returns>
        /// Always returns zero, because output is not redirected.
        /// </returns>
        public override bool IsOutputRedirected()
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether error output for this host is redirected.
        /// </summary>
        /// <returns>
        /// Always returns zero, because error output is not redirected.
        /// </returns>
        public override bool IsErrorRedirected()
        {
            CheckDisposed();

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDebugHost Members
        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Creates a new windowed host that is a copy of this instance for the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to associate with the new host.
        /// </param>
        /// <returns>
        /// The newly created host.
        /// </returns>
        public override IHost Clone(
            Interpreter interpreter /* in */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;
            EventHandler openedHandler;
            EventHandler closedHandler;
            string newLine;
            WindowType inputWindowType;
            WindowType outputWindowType;
            bool createOutput;
            bool traceToHost;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
                openedHandler = this.openedHandler;
                closedHandler = this.closedHandler;
                newLine = this.newLine;
                inputWindowType = this.inputWindowType;
                outputWindowType = this.outputWindowType;
                createOutput = this.createOutput;
                traceToHost = this.traceToHost;
            }

            return new Window(new HostData(
                Name, Group, Description, ClientData,
                typeof(Window).Name, interpreter, ResourceManager,
                Profile, Utility.GetHostCreateFlags(HostCreateFlags,
                UseAttach, UseForce, NoColor, NoTitle, NoIcon, NoProfile,
                NoCancel)), interactiveWindow, openedHandler, closedHandler,
                newLine, inputWindowType, outputWindowType, createOutput,
                traceToHost);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Gets the host test flags for this windowed host.
        /// </summary>
        /// <returns>
        /// The host test flags for this host.
        /// </returns>
        public override HostTestFlags GetTestFlags()
        {
            CheckDisposed();

            return HostTestFlags.None;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cancels the current interactive input and signals cancellation.
        /// </summary>
        /// <param name="force">
        /// Non-zero to forcibly cancel.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Cancel(
            bool force,      /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
            }

            if (interactiveWindow != null)
            {
                if (!interactiveWindow.SetInput(null))
                {
                    error = "failed to reset input";
                    return ReturnCode.Error;
                }

                if (!interactiveWindow.SignalCanceled())
                {
                    error = "failed to signal canceled";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            else
            {
                error = "interactive window unavailable";
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the interactive window in order to exit the host.
        /// </summary>
        /// <param name="force">
        /// Non-zero to forcibly exit.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Exit(
            bool force,      /* in: NOT USED */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostWindow window;

            lock (syncRoot)
            {
                window = this.interactiveWindow;
            }

            if (CommonOps.CloseWindow(window, true, true))
                return ReturnCode.Ok;

            error = "failed to close interactive window";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a debug line terminator.
        /// </summary>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteDebugLine()
        {
            CheckDisposed();

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Writes the specified debug value to the debug box window.
        /// </summary>
        /// <param name="value">
        /// The debug value to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool WriteDebugLine( /* NOTE: For DebugOps.Write. */
            string value
            )
        {
            CheckDisposed();

            /* IGNORED */
            CloseBox(DebugBoxName);

            return WriteBox(DebugBoxName, value);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Writes the specified debug character.
        /// </summary>
        /// <param name="value">
        /// The character to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteDebug(
            char value,  /* in */
            bool newLine /* in */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified debug string.
        /// </summary>
        /// <param name="value">
        /// The string to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteDebug(
            string value, /* in */
            bool newLine  /* in */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes an error line terminator.
        /// </summary>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteErrorLine()
        {
            CheckDisposed();

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Writes the specified error value to the complaint box window.
        /// </summary>
        /// <param name="value">
        /// The error value to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool WriteErrorLine( /* NOTE: For DebugOps.Complain. */
            string value
            )
        {
            CheckDisposed();

            /* IGNORED */
            CloseBox(ComplainBoxName);

            return WriteBox(ComplainBoxName, value);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Writes the specified error character.
        /// </summary>
        /// <param name="value">
        /// The character to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteError(
            char value,  /* in */
            bool newLine /* in */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified error string.
        /// </summary>
        /// <param name="value">
        /// The string to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool WriteError(
            string value, /* in */
            bool newLine  /* in */
            )
        {
            CheckDisposed();

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IInformationHost Members
        #region TODO: Derived Classes (Maybe Customize)
        /// <summary>
        /// Writes interpreter announcement information; overridden to do
        /// nothing.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the announcement.
        /// </param>
        /// <param name="breakpointType">
        /// The type of breakpoint, if any.
        /// </param>
        /// <param name="value">
        /// The announcement text.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <param name="foregroundColor">
        /// The foreground color to use.
        /// </param>
        /// <param name="backgroundColor">
        /// The background color to use.
        /// </param>
        /// <returns>
        /// Non-zero, as this method intentionally does nothing.
        /// </returns>
        public override bool WriteAnnouncementInfo(
            Interpreter interpreter,       /* in */
            BreakpointType breakpointType, /* in */
            string value,                  /* in */
            bool newLine,                  /* in */
            ConsoleColor foregroundColor,  /* in */
            ConsoleColor backgroundColor   /* in */
            )
        {
            CheckDisposed();

            //
            // HACK: This method is not required; however, having just
            //       the announcement visible in the interactive output
            //       window when the header information is shown looks
            //       quite strange; therefore, we override the default
            //       behavior and do nothing.
            //
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes custom interpreter information; this host provides none.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the information.
        /// </param>
        /// <param name="detailFlags">
        /// The flags controlling which details are written.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <param name="foregroundColor">
        /// The foreground color to use.
        /// </param>
        /// <param name="backgroundColor">
        /// The background color to use.
        /// </param>
        /// <returns>
        /// Non-zero, as this host provides no custom information.
        /// </returns>
        public override bool WriteCustomInfo(
            Interpreter interpreter,      /* in */
            DetailFlags detailFlags,      /* in */
            bool newLine,                 /* in */
            ConsoleColor foregroundColor, /* in */
            ConsoleColor backgroundColor  /* in */
            )
        {
            CheckDisposed();

            //
            // NOTE: This host implementation currently provides no
            //       custom information.
            //
            return true;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IBoxHost Members
        #region TODO: Derived Classes (Maybe Customize)
        /// <summary>
        /// Begins a box, creating and clearing the box window.
        /// </summary>
        /// <param name="name">
        /// The name of the box.
        /// </param>
        /// <param name="list">
        /// The list of name/value pairs for the box.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the box.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool BeginBox(
            string name,           /* in */
            StringPairList list,   /* in */
            IClientData clientData /* in */
            )
        {
            CheckDisposed();

            //
            // NOTE: The default host *CAN* legitimately call
            //       BeginBox with a null name.
            //
            if (name == null)
                name = CommonOps.BoxWindowName;

            IHostOutputWindow outputWindow = GetWindow(
                name, WindowType.Box, true) as IHostOutputWindow;

            if (outputWindow != null)
            {
                try
                {
                    outputWindow.Clear();
                    ResetPosition();
                }
                finally
                {
                    lock (syncRoot)
                    {
                        boxWindow = outputWindow;
                    }
                }

                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ends the current box, refreshing the box window.
        /// </summary>
        /// <param name="name">
        /// The name of the box.
        /// </param>
        /// <param name="list">
        /// The list of name/value pairs for the box.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the box.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool EndBox(
            string name,           /* in */
            StringPairList list,   /* in */
            IClientData clientData /* in */
            )
        {
            CheckDisposed();

            IHostOutputWindow outputWindow;

            lock (syncRoot)
            {
                outputWindow = boxWindow;
            }

            try
            {
                if (outputWindow != null)
                {
                    WriteLine();

                    return outputWindow.Refresh();
                }
            }
            finally
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (boxWindow != null)
                        boxWindow = null;
                }
            }

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IColorHost Members
        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Resets the foreground and background colors.
        /// </summary>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool ResetColors()
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current foreground and background colors.
        /// </summary>
        /// <param name="foregroundColor">
        /// Upon success, receives the foreground color.
        /// </param>
        /// <param name="backgroundColor">
        /// Upon success, receives the background color.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool GetColors(
            ref ConsoleColor foregroundColor, /* in, out */
            ref ConsoleColor backgroundColor  /* in, out */
            )
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the specified foreground and background colors.
        /// </summary>
        /// <param name="foregroundColor">
        /// The foreground color to adjust.
        /// </param>
        /// <param name="backgroundColor">
        /// The background color to adjust.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool AdjustColors(
            ref ConsoleColor foregroundColor, /* in, out */
            ref ConsoleColor backgroundColor  /* in, out */
            )
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the foreground color.
        /// </summary>
        /// <param name="foregroundColor">
        /// The foreground color to set.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool SetForegroundColor(
            ConsoleColor foregroundColor /* in */
            )
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the background color.
        /// </summary>
        /// <param name="backgroundColor">
        /// The background color to set.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool SetBackgroundColor(
            ConsoleColor backgroundColor /* in */
            )
        {
            CheckDisposed();

            /* NOT IMPLEMENTED */
            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPositionHost Members
        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Gets the current cursor position.
        /// </summary>
        /// <param name="left">
        /// Upon success, receives the column position.
        /// </param>
        /// <param name="top">
        /// Upon success, receives the row position.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool GetPosition(
            ref int left, /* in, out */
            ref int top   /* in, out */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cursor position.
        /// </summary>
        /// <param name="left">
        /// The new column position.
        /// </param>
        /// <param name="top">
        /// The new row position.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool SetPosition(
            int left, /* in */
            int top   /* in */
            )
        {
            CheckDisposed();

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ISizeHost Members
        #region TODO: Derived Classes (Maybe Customize)
        /// <summary>
        /// Resets the size of the specified host element.
        /// </summary>
        /// <param name="hostSizeType">
        /// The host element whose size should be reset.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool ResetSize(
            HostSizeType hostSizeType /* in */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the size, in characters, of the output window.
        /// </summary>
        /// <param name="hostSizeType">
        /// The host element whose size is being queried.
        /// </param>
        /// <param name="width">
        /// Upon success, receives the width, in characters.
        /// </param>
        /// <param name="height">
        /// Upon success, receives the height, in characters.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool GetSize(
            HostSizeType hostSizeType, /* in */
            ref int width,             /* out */
            ref int height             /* out */
            )
        {
            CheckDisposed();

            IHostOutputWindow outputWindow = GetWindow(
                this.OutputWindowType, false) as IHostOutputWindow;

            if (outputWindow != null)
            {
                double windowWidth = 0.0;
                double windowHeight = 0.0;
                double characterWidth = 0.0;
                double characterHeight = 0.0;

                if (outputWindow.GetSize(hostSizeType,
                        ref windowWidth, ref windowHeight) &&
                    outputWindow.GetCharacterSize(
                        ref characterWidth, ref characterHeight))
                {
                    width = (int)(windowWidth / characterWidth);
                    height = (int)(windowHeight / characterHeight);

                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the size of the output window.
        /// </summary>
        /// <param name="hostSizeType">
        /// The host element whose size is being set.
        /// </param>
        /// <param name="width">
        /// The new width.
        /// </param>
        /// <param name="height">
        /// The new height.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SetSize(
            HostSizeType hostSizeType, /* in */
            int width,                 /* in */
            int height                 /* in */
            )
        {
            CheckDisposed();

            IHostOutputWindow outputWindow = GetWindow(
                this.OutputWindowType, false) as IHostOutputWindow;

            if ((outputWindow != null) &&
                outputWindow.SetSize(hostSizeType, width, height))
            {
                return true;
            }

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IReadHost Members
        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Reads a single key of input from the input window.
        /// </summary>
        /// <param name="value">
        /// Upon success, receives the value of the key that was read.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Read(
            ref int value /* out */
            )
        {
            CheckDisposed();
            EnterReadLevel();

            try
            {
                IHostInputWindow inputWindow = GetWindow(
                    this.InputWindowType, false) as IHostInputWindow;

                if (inputWindow != null)
                {
                    return CommonOps.ReadKey(
                        inputWindow.ReadKey(), ref value);
                }
            }
            finally
            {
                ExitReadLevel();
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a single key of input from the input window.
        /// </summary>
        /// <param name="intercept">
        /// Non-zero to intercept the key so it is not displayed.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the key information that was read.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool ReadKey( /* OPTIONAL (?) */
            bool intercept,
            ref IClientData value
            )
        {
            CheckDisposed();
            EnterReadLevel();

            try
            {
                IHostInputWindow inputWindow = GetWindow(
                    this.InputWindowType, false) as IHostInputWindow;

                if (inputWindow != null)
                {
                    value = new ClientData(inputWindow.ReadKey());
                    return true;
                }
            }
            finally
            {
                ExitReadLevel();
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

#if CONSOLE
        /// <summary>
        /// Reads a single key of input; not supported by this host.
        /// </summary>
        /// <param name="intercept">
        /// Non-zero to intercept the key so it is not displayed.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the key information that was read.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not supported.
        /// </returns>
        [Obsolete()]
        public override bool ReadKey(
            bool intercept,          /* in */
            ref ConsoleKeyInfo value /* in, out */
            )
        {
            CheckDisposed();

            return false;
        }
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IWriteHost Members
        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Writes the specified character to the box window.
        /// </summary>
        /// <param name="value">
        /// The character to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Write(
            char value,  /* in */
            bool newLine /* in */
            )
        {
            CheckDisposed();
            EnterWriteLevel();

            try
            {
                IHostOutputWindow outputWindow = GetWindow(
                    WindowType.Box, false) as IHostOutputWindow;

                if (outputWindow != null)
                {
                    return newLine ?
                        outputWindow.Write(
                            MaybeAppendNewLine(value), BufferClearSize) :
                        outputWindow.Write(
                            value.ToString(), BufferClearSize);
                }
            }
            catch
            {
                // do nothing.
            }
            finally
            {
                ExitWriteLevel();
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified string to the box window.
        /// </summary>
        /// <param name="value">
        /// The string to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Write(
            string value, /* in */
            bool newLine  /* in */
            )
        {
            CheckDisposed();
            EnterWriteLevel();

            try
            {
                IHostOutputWindow outputWindow = GetWindow(
                    WindowType.Box, false) as IHostOutputWindow;

                if (outputWindow != null)
                {
                    return newLine ?
                        outputWindow.Write(
                            MaybeAppendNewLine(value), BufferClearSize) :
                        outputWindow.Write(
                            value, BufferClearSize);
                }
            }
            catch
            {
                // do nothing.
            }
            finally
            {
                ExitWriteLevel();
            }

            return false;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHost Members
        #region TODO: Derived Classes (Maybe Customize)
        /// <summary>
        /// Builds a list describing the current state of this host.
        /// </summary>
        /// <param name="detailFlags">
        /// The flags controlling which details are included.
        /// </param>
        /// <returns>
        /// A list of name/value pairs describing the host state.
        /// </returns>
        public override StringList QueryState(
            DetailFlags detailFlags /* in */
            )
        {
            CheckDisposed();

            StringList result = new StringList();

            result.Add("HeaderFlags", GetHeaderFlags().ToString());
            result.Add("DetailFlags", GetDetailFlags().ToString());
            result.Add("HostFlags", GetHostFlags().ToString());
            result.Add("ReadLevels", ReadLevels.ToString());
            result.Add("WriteLevels", WriteLevels.ToString());
            result.Add("WindowId", WindowId.ToString());
            result.Add("WindowName", WindowName);
            result.Add("WindowType", WindowType.ToString());
            result.Add("ExitCode", ExitCode.ToString());
            result.Add("WindowCount", WindowCount.ToString());

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Emits a beep.
        /// </summary>
        /// <param name="frequency">
        /// The frequency of the beep, in hertz.
        /// </param>
        /// <param name="duration">
        /// The duration of the beep, in milliseconds.
        /// </param>
        /// <returns>
        /// Always returns zero, because this operation is not implemented.
        /// </returns>
        public override bool Beep(
            int frequency, /* in */
            int duration   /* in */
            )
        {
            CheckDisposed();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this host is idle.
        /// </summary>
        /// <returns>
        /// Non-zero, because this host has no better idle detection.
        /// </returns>
        public override bool IsIdle()
        {
            CheckDisposed();

            //
            // STUB: We have no better idle detection.
            //
            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Must Customize)
        /// <summary>
        /// Clears the output window and resets the cursor position.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool Clear()
        {
            CheckDisposed();
            EnterWriteLevel();

            try
            {
                IHostOutputWindow outputWindow = GetWindow(
                    this.OutputWindowType, false) as IHostOutputWindow;

                if (outputWindow != null)
                {
                    outputWindow.Clear();
                    ResetPosition();

                    return true;
                }
            }
            finally
            {
                ExitWriteLevel();
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TODO: Derived Classes (Mostly Verbatim)
        /// <summary>
        /// Resets the cached host flags to their default state.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool ResetHostFlags()
        {
            CheckDisposed();

            return PrivateResetHostFlags();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the interactive command history.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode ResetHistory(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            IHostInteractiveWindow interactiveWindow;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interactiveWindow = this.interactiveWindow;
            }

            if (interactiveWindow != null)
            {
                if (!interactiveWindow.ResetHistory())
                {
                    error = "failed to reset history";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            else
            {
                error = "interactive window unavailable";
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the mode for the specified channel; not implemented.
        /// </summary>
        /// <param name="channelType">
        /// The type of channel whose mode is requested.
        /// </param>
        /// <param name="mode">
        /// Upon success, receives the mode for the specified channel.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode GetMode(
            ChannelType channelType, /* in */
            ref uint mode,           /* in, out */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            error = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the mode for the specified channel; not implemented.
        /// </summary>
        /// <param name="channelType">
        /// The type of channel whose mode is being set.
        /// </param>
        /// <param name="mode">
        /// The mode to set for the specified channel.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode SetMode(
            ChannelType channelType, /* in */
            uint mode,               /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            error = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens this host; not implemented.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Open(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            error = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes this host; not implemented.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Close(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            error = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Discards any pending host state; not implemented.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Discard(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            error = "not implemented";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets this host to its default state.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Reset(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            if (base.Reset(ref error) == ReturnCode.Ok)
            {
                if (!PrivateResetHostFlags()) /* NON-VIRTUAL */
                {
                    error = "failed to reset flags";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Begins an output section.
        /// </summary>
        /// <param name="name">
        /// The name of the section.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the section.
        /// </param>
        /// <returns>
        /// Always returns non-zero; this operation is ignored.
        /// </returns>
        public override bool BeginSection(
            string name,           /* in */
            IClientData clientData /* in */
            )
        {
            CheckDisposed();

            /* IGNORED */
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ends an output section.
        /// </summary>
        /// <param name="name">
        /// The name of the section.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the section.
        /// </param>
        /// <returns>
        /// Always returns non-zero; this operation is ignored.
        /// </returns>
        public override bool EndSection(
            string name,           /* in */
            IClientData clientData /* in */
            )
        {
            CheckDisposed();

            /* IGNORED */
            return true;
        }
        #endregion
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IMaybeDisposed Members
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        public override bool Disposed
        {
            get { return disposed; }
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
            if (disposed &&
                Engine.IsThrowOnDisposed(SafeGetInterpreter(), null))
            {
                throw new ObjectDisposedException(typeof(Window).Name);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of times disposal of this instance has been initiated.
        /// </summary>
        private int disposeCount;
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
                if (Interlocked.Increment(ref disposeCount) == 1)
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

                                Shutdown(false);

                                //
                                // NOTE: Remove our custom trace listener
                                //       and then dispose it.
                                //
                                if (traceListener != null)
                                {
                                    Trace.Listeners.Remove(traceListener);

                                    traceListener.Dispose(); /* throw */
                                    traceListener = null;
                                }
                            }

                            //
                            // release unmanaged resources here...
                            //
                        }
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
    }
}
