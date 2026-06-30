/*
 * InteractiveWindow.xaml.cs --
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
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;
using _Interfaces = Eagle._Interfaces.Public;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Featherlight.Windows
{
    /// <summary>
    /// The top-level WPF window that hosts an interactive Eagle session.  It
    /// is a stream window that owns an Eagle interpreter, runs its interactive
    /// loop, and can manufacture additional hosts and windows.
    /// </summary>
    [ObjectId("085dd28f-16be-4ee8-bc45-45f9e0288979")]
    public sealed partial class InteractiveWindow
            : BaseWindow, IHostInteractiveWindow
    {
        #region Private Constants
        /// <summary>
        /// The name used for the box window that displays tab-completion
        /// results.
        /// </summary>
        private const string CompletionWindowName = "completions";
        /// <summary>
        /// The number of milliseconds to wait for the interactive loop thread
        /// to exit before forcibly aborting it.
        /// </summary>
        private const int ThreadJoinTimeout = 2000;

        ///////////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The help text shown when the user presses F1, describing the
        /// supported key bindings.
        /// </summary>
        private static readonly string KeyDownHelpText = String.Format(
            "F1: Shows this help message.{0}" +
            "F2: Toggles single-line entry mode.{0}" +
            "Ctrl-Up: Sets the input to the previous input from the history.{0}" +
            "Ctrl-Down: Sets the input to the next input from the history.{0}" +
            "Ctrl-N: Starts a new interactive Eagle session.{0}",
            NewLine);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The command line arguments used to start the interactive session.
        /// </summary>
        private IEnumerable<string> args;
        /// <summary>
        /// The thread that runs the interactive loop for this window.
        /// </summary>
        private Thread thread;
        /// <summary>
        /// The interpreter that backs this interactive window.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// The host associated with the interpreter for this interactive
        /// window.
        /// </summary>
        private IHost host;
        /// <summary>
        /// The number of interactive loops currently active for this window.
        /// </summary>
        private int activeInteractiveLoops;
        /// <summary>
        /// The nesting level counter used to guard against re-entrant
        /// interactive loop shutdown.
        /// </summary>
        private int shutdownLevels;
        /// <summary>
        /// Non-zero if name matching for tab-completion should be
        /// case-insensitive.
        /// </summary>
        private bool noCase;
        /// <summary>
        /// The dispatcher frame pushed while waiting for the interactive loop
        /// thread to exit.
        /// </summary>
        private DispatcherFrame dispatcherFrame;
        /// <summary>
        /// Non-zero if the input box is in single-line entry mode.
        /// </summary>
        private bool singleLine;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The collection of hosts manufactured by this window, keyed by
        /// identifier.
        /// </summary>
        private Dictionary<long, IHost> hosts;
        /// <summary>
        /// The list of input lines entered by the interactive user.
        /// </summary>
        private StringList historyList;
        /// <summary>
        /// The current position within the command history list.
        /// </summary>
        private int historyIndex;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Initializes a new instance of the InteractiveWindow class.
        /// </summary>
        /// <param name="windowRegistrar">
        /// The registrar used to track windows created by this instance.
        /// </param>
        /// <param name="windowId">
        /// The unique identifier for this window.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type used for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type used for output.
        /// </param>
        /// <param name="openedHandler">
        /// The event handler invoked when the window is opened.
        /// </param>
        /// <param name="closedHandler">
        /// The event handler invoked when the window is closed.
        /// </param>
        /// <param name="args">
        /// The command line arguments used to start the interactive session.
        /// </param>
        public InteractiveWindow(
            IHostWindowRegistrar windowRegistrar, /* in */
            long windowId,                        /* in */
            WindowType inputWindowType,           /* in */
            WindowType outputWindowType,          /* in */
            EventHandler openedHandler,           /* in */
            EventHandler closedHandler,           /* in */
            IEnumerable<string> args              /* in */
            )
            : base(null, null, windowRegistrar, windowId,
                   WindowType.Interactive, inputWindowType,
                   outputWindowType, openedHandler, closedHandler,
                   null, null, WindowPositionInfo.None(), false,
                   false, true, false)
        {
            InitializeComponent();

            ///////////////////////////////////////////////////////////////////

            this.args = args;

            ///////////////////////////////////////////////////////////////////

            hosts = new Dictionary<long, IHost>();
            historyList = new StringList();
            historyIndex = Index.Invalid;
            noCase = true;
            dispatcherFrame = null;
            singleLine = false;

            ///////////////////////////////////////////////////////////////////

            this.InputBox = txtInput;
            this.OutputBox = txtOutput;

            ///////////////////////////////////////////////////////////////////

            this.Loaded += new RoutedEventHandler(Window_Loaded);
            this.LocationChanged += new EventHandler(Window_LocationChanged);
            this.SizeChanged += new SizeChangedEventHandler(Window_SizeChanged);
            this.Activated += new EventHandler(Window_Activated);
            this.PreviewKeyDown += new KeyEventHandler(Window_PreviewKeyDown);
            this.KeyDown += new KeyEventHandler(Window_KeyDown);
            this.Closing += new CancelEventHandler(Window_Closing);

            ///////////////////////////////////////////////////////////////////

            txtInput.KeyDown += new KeyEventHandler(txtInput_KeyDown);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Returns the interpreter for this window in a thread-safe manner.
        /// </summary>
        /// <returns>
        /// The interpreter for this window, or null if there is none.
        /// </returns>
        private Interpreter SafeGetInterpreter()
        {
            Interpreter localInterpreter;

            lock (syncRoot)
            {
                localInterpreter = interpreter;
            }

            return localInterpreter;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the host for this window in a thread-safe manner.
        /// </summary>
        /// <returns>
        /// The host for this window, or null if there is none.
        /// </returns>
        private IHost SafeGetHost()
        {
            IHost localHost;

            lock (syncRoot)
            {
                localHost = host;
            }

            return localHost;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the window manager associated with this window's host, in a
        /// thread-safe manner.
        /// </summary>
        /// <returns>
        /// The host window manager, or null if one cannot be obtained.
        /// </returns>
        private IHostWindowManager SafeGetHostWindowManager()
        {
            IHost localHost = SafeGetHost();

            if (localHost is IHostWindowManager)
                return (IHostWindowManager)localHost;

            if (localHost == null)
                return null;

            IClientData clientData = localHost.ClientData;

            if (clientData == null)
                return null;

            return clientData.Data as IHostWindowManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the stream manager for the window of the specified type.
        /// </summary>
        /// <param name="windowType">
        /// The type of window whose stream manager is being requested.
        /// </param>
        /// <returns>
        /// The stream manager for the specified window type, or null if one
        /// cannot be obtained.
        /// </returns>
        private IHostStreamManager GetStreamManager(
            WindowType windowType /* in */
            )
        {
            IHostWindowManager windowManager = SafeGetHostWindowManager();

            if (windowManager == null)
                return null;

            return windowManager.GetWindow(
                windowType, false) as IHostStreamManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Positions the window of the specified type using the host window
        /// manager.
        /// </summary>
        /// <param name="name">
        /// The name of the window to position.
        /// </param>
        /// <param name="windowType">
        /// The type of the window to position.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The position information to apply to the window.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the window if it does not already exist.
        /// </param>
        /// <param name="always">
        /// Non-zero to always apply the position, even if the window already
        /// exists.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool PositionWindow(
            string name,                           /* in */
            WindowType windowType,                 /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool create,                           /* in */
            bool always                            /* in */
            )
        {
            IHostWindowManager windowManager = SafeGetHostWindowManager();

            if (windowManager == null)
                return false;

            return windowManager.PositionWindow(
                name, windowType, windowPositionInfo, create, always);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Simulates the interactive user entering and submitting the
        /// specified command text.
        /// </summary>
        /// <param name="text">
        /// The command text to enter and submit.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool DoCommand(
            string text /* in */
            )
        {
            //
            // HACK: Simulate a command being entered by the interactive
            //       user.
            //
            if (SetInput(text))
            {
                btnEnter_Click(null, null);
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interactive window, shows it modally, and shuts down
        /// its dispatcher when it closes.
        /// </summary>
        /// <param name="obj">
        /// The thread start parameter; not used.
        /// </param>
        private void InteractiveWindowStart(
            object obj /* in */
            ) // ParameterizedThreadStart
        {
            IHostWindow window = CreateWindow(
                null, this.WindowRegistrar, Utility.NextId(), null,
                WindowType.Interactive, this.InputWindowType,
                this.OutputWindowType, this.OpenedHandler,
                this.ClosedHandler, WindowPositionInfo.None(), false,
                false, false, false);

            if (window != null)
            {
                window.ShowDialog();

                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Interactive Thread Helper Methods
        /// <summary>
        /// Configures this window's identifier and window manager from the
        /// specified interpreter and host.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose identifier is adopted by this window.
        /// </param>
        /// <param name="host">
        /// The host used as the window manager for this window.
        /// </param>
        private void SetupWindowIdAndManager(
            Interpreter interpreter, /* in */
            IHost host               /* in */
            )
        {
            this.WindowId = interpreter.IdNoThrow;
            this.WindowManager = host as IHostWindowManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Wraps the core shell main entry point, supplying the appropriate
        /// command line argument callback and tracking the active interactive
        /// loop count.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to run the shell for.
        /// </param>
        /// <param name="clientData">
        /// The client data to pass to the shell.
        /// </param>
        /// <param name="host">
        /// The host associated with the interpreter.
        /// </param>
        /// <param name="initialize">
        /// Non-zero to initialize the interpreter.
        /// </param>
        /// <param name="loop">
        /// Non-zero to enter the interactive loop.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error message.
        /// </param>
        /// <returns>
        /// The exit code produced by the shell.
        /// </returns>
        private ExitCode ShellMainCoreWrapper(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            IHost host,              /* in */
            bool initialize,         /* in */
            bool loop,               /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return Utility.FailureExitCode();
            }

            Interlocked.Increment(ref activeInteractiveLoops);

            try
            {
#if SHELL
                //
                // HACK: If the interpreter is a transparent proxy,
                //       then the plugin was loaded into an isolated
                //       application domain -AND- it did not create
                //       its own interpreter.  That means we should
                //       not need our custom command line argument
                //       callback.  If we did need it, it would be
                //       necessary to use ShellCallbackBridge -AND-
                //       a custom IShellCallback implementation.
                //
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                ShellCallbackBridge callbackBridge = null;
#endif

                if (Utility.IsTransparentProxy(interpreter))
                {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                    callbackBridge = ShellCallbackBridge.Create(
                        new WindowShellCallback(), ref result);

                    if (callbackBridge == null)
                        return Utility.FailureExitCode();
#else
                    result = "cannot set delegates via interpreter proxy";
                    return Utility.FailureExitCode();
#endif
                }

                IShellCallbackData callbackData = ShellCallbackData.Create();

#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                if (callbackBridge != null)
                {
                    callbackData.UnknownArgumentCallback =
                        new UnknownArgumentCallback(
                            callbackBridge.UnknownArgumentCallback);
                }
                else
#endif
                {
                    callbackData.UnknownArgumentCallback =
                        new UnknownArgumentCallback(
                            CommonOps.PopUnknownArgumentCallback);
                }

                return Interpreter.ShellMainCore(
                    interpreter, callbackData, clientData, args, initialize,
                    loop, ref result);
#else
                result = "not implemented";
                return Utility.FailureExitCode();
#endif
            }
            finally
            {
                Interlocked.Decrement(ref activeInteractiveLoops);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the common shutdown logic after an interactive thread
        /// completes, recording the exit code and closing or releasing the
        /// window as appropriate.
        /// </summary>
        /// <param name="host">
        /// The host associated with the interactive thread, used to notify the
        /// user.
        /// </param>
        /// <param name="exitCode">
        /// The exit code produced by the interactive thread.
        /// </param>
        private void InteractiveThreadStartEpilogue(
            IHost host,       /* in */
            ExitCode exitCode /* in */
            )
        {
            //
            // NOTE: If the window registrar is available, set its
            //       exit code to the exit code for this thread now.
            //       We will not bother checking the window count
            //       here because the semantics will basically be
            //       "last one to set the exit code wins" anyhow.
            //       This must be done prior to closing this window
            //       to avoid a race condition with our "parent"
            //       thread.
            //
            /* IGNORED */
            SetExitCode(exitCode);

            //
            // NOTE: If we are being closed by the Window_Closing
            //       event, we must exit that dispatcher frame now.
            //
            if (this.IsClosing)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (dispatcherFrame != null)
                        dispatcherFrame.Continue = false;
                }
            }
            //
            // NOTE: Otherwise, if we are set to auto-close, invoke
            //       the Close method now.  The Window_Closing
            //       event will detect that no interactive loops
            //       are active and simply wait for this thread to
            //       exit (which it will very shortly).
            //
            else if (this.AutoClose)
            {
                Close();
            }
            //
            // NOTE: Otherwise, make sure the user knows there is
            //       no more interactive thread (or loop for that
            //       matter).
            //
            else if (host != null)
            {
                host.WriteLine("exited interactive thread.");
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Threading.ParameterizedThreadStart
        /// <summary>
        /// Runs the interactive loop on a newly created interpreter, handling
        /// startup option processing, host creation, and shutdown.
        /// </summary>
        /// <param name="obj">
        /// The thread start parameter, expected to be the command line
        /// arguments.
        /// </param>
        private void InteractiveThreadStartWithCreate(
            object obj /* in */
            ) // System.Threading.ParameterizedThreadStart
        {
            ExitCode exitCode = Utility.SuccessExitCode();
            IEnumerable<string> args = obj as IEnumerable<string>;

            CreateFlags createFlags = CreateFlags.ShellUse |
                CreateFlags.SetArguments;

#if NOTIFY || NOTIFY_OBJECT
            //
            // BUGFIX: Prevent the interpreter from ever attempting to
            //         notify any other interpreters [about anything]
            //         by setting the "GlobalNotify" property of the
            //         interpreter to false in the pre-init script.
            //
            createFlags |= CreateFlags.NoGlobalNotify;
#endif

            createFlags = Interpreter.GetStartupCreateFlags(
                args, createFlags, OptionOriginFlags.Plugin, true, true);

            HostCreateFlags hostCreateFlags = HostCreateFlags.Disable;

            hostCreateFlags = Interpreter.GetStartupHostCreateFlags(
                args, hostCreateFlags, OptionOriginFlags.Plugin, true, true);

            ReturnCode code;
            Result result = null;
            string text = null;

            code = Interpreter.GetStartupPreInitializeText(
                args, createFlags, OptionOriginFlags.Plugin, true, true,
                ref text, ref result);

            if (code != ReturnCode.Ok)
            {
                Complain(null, code, result);
                exitCode = Utility.ReturnCodeToExitCode(code, true);

                /* IGNORED */
                SetExitCode(exitCode);
                return;
            }

            string libraryPath = null;

            code = Interpreter.GetStartupLibraryPath(
                args, createFlags, OptionOriginFlags.Plugin, true, true,
                ref libraryPath, ref result);

            if (code != ReturnCode.Ok)
            {
                Complain(null, code, result);
                exitCode = Utility.ReturnCodeToExitCode(code, true);

                /* IGNORED */
                SetExitCode(exitCode);
                return;
            }

            using (Interpreter localInterpreter = CommonOps.CreateInterpreter(
                    args, createFlags, hostCreateFlags, text, libraryPath,
                    ref result))
            {
                if (localInterpreter == null)
                {
                    Complain(null, ReturnCode.Error, result);
                    exitCode = Utility.FailureExitCode();

                    /* IGNORED */
                    SetExitCode(exitCode);
                    return;
                }

                bool initialize = true;
                bool loop = true;

                code = Interpreter.ProcessStartupOptions(
                    localInterpreter, args, createFlags,
                    OptionOriginFlags.Plugin, true, true,
                    ref initialize, ref loop, ref result);

                IHost localHost = null;

                if (code == ReturnCode.Ok)
                {
                    lock (syncRoot)
                    {
                        interpreter = localInterpreter;
                    }

                    localHost = NewHost(localInterpreter, null, true);
                    SetupWindowIdAndManager(localInterpreter, localHost);

                    localHost.Title = null; // NOTE: Force refresh.
                    localInterpreter.Host = localHost;

                    lock (syncRoot)
                    {
                        host = localHost;
                    }

                    exitCode = ShellMainCoreWrapper(
                        localInterpreter, null, localHost, initialize,
                        loop, ref result);

                    code = Utility.ExitCodeToReturnCode(exitCode);

                    if (code != ReturnCode.Ok)
                    {
                        if (String.IsNullOrEmpty(result))
                        {
                            result = String.Format(
                                "shell core returned bad exit code: {0}",
                                exitCode);
                        }

                        Complain(localInterpreter, code, result);
                    }
                }
                else
                {
                    Complain(localInterpreter, code, result);
                    exitCode = Utility.ReturnCodeToExitCode(code, true);
                }

                //
                // NOTE: Invoke the common thread shutdown logic; this
                //       will handle closing out the WPF window, etc.
                //
                InteractiveThreadStartEpilogue(localHost, exitCode);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Runs the interactive loop on the existing plugin interpreter,
        /// temporarily replacing and later restoring its host.
        /// </summary>
        /// <param name="obj">
        /// The thread start parameter, expected to be the command line
        /// arguments.
        /// </param>
        private void InteractiveThreadStartWithExisting(
            object obj /* in */
            ) // System.Threading.ParameterizedThreadStart
        {
            Interpreter localInterpreter;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                localInterpreter = Shell.Window.GetPluginInterpreter();
            }

            ExitCode exitCode = Utility.SuccessExitCode();
            IEnumerable<string> args = obj as IEnumerable<string>;

            if (localInterpreter == null)
            {
                Complain(null, ReturnCode.Error, "invalid interpreter");
                exitCode = Utility.FailureExitCode();

                /* IGNORED */
                SetExitCode(exitCode);
                return;
            }

            IHost localHost = NewHost(localInterpreter, null, false);
            SetupWindowIdAndManager(localInterpreter, localHost);

            IHost savedHost = null;
            bool locked = false;

            try
            {
                localInterpreter.TryLockWithWait(
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    savedHost = localInterpreter.Host;
                    localInterpreter.Host = localHost;
                }
                else
                {
                    Complain(localInterpreter,
                        ReturnCode.Error, "interpreter is locked");

                    exitCode = Utility.FailureExitCode();

                    /* IGNORED */
                    SetExitCode(exitCode);
                    return;
                }
            }
            finally
            {
                localInterpreter.ExitLock(
                    ref locked); /* TRANSACTIONAL */
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                interpreter = localInterpreter;
                host = localHost;
            }

            ReturnCode code;
            Result result = null;

            exitCode = ShellMainCoreWrapper(
                localInterpreter, null, localHost, false, true,
                ref result);

            code = Utility.ExitCodeToReturnCode(exitCode);

            if (code != ReturnCode.Ok)
            {
                if (String.IsNullOrEmpty(result))
                {
                    result = String.Format(
                        "shell core returned bad exit code: {0}",
                        exitCode);
                }

                Complain(localInterpreter, code, result);
            }

            //
            // NOTE: This interpreter is not owned by us; therefore,
            //       we do not want to really mark it as "exited".
            //
            localInterpreter.ExitNoThrow = false;

            //
            // HACK: Restore the interpreter host that was saved prior
            //       to entering the interactive loop.  Ideally, this
            //       would be a per-thread datum; however, that is not
            //       how the core library works.  Also, it may change
            //       from within the interactive loop, we must verify
            //       its current value before restoring the previously
            //       saved value.
            //
            locked = false;

            try
            {
                localInterpreter.TryLockWithWait(
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    IHost newHost = localInterpreter.Host;

                    if (Object.ReferenceEquals(newHost, localHost))
                    {
                        localInterpreter.Host = savedHost;
                    }
                    else
                    {
                        Complain(localInterpreter,
                            ReturnCode.Error, "interpreter host changed");
                    }
                }
                else
                {
                    Complain(localInterpreter,
                        ReturnCode.Error, "interpreter is locked");
                }
            }
            finally
            {
                localInterpreter.ExitLock(
                    ref locked); /* TRANSACTIONAL */
            }

            //
            // NOTE: Invoke the common thread shutdown logic; this
            //       will handle closing out the WPF window, etc.
            //
            InteractiveThreadStartEpilogue(savedHost, exitCode);
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Event Handlers
        /// <summary>
        /// Handles the window Activated event by giving keyboard focus to the
        /// input box.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_Activated(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            Invoke(txtInput, new DelegateWithNoArgs(delegate()
            {
                if (txtInput.Focusable)
                    txtInput.Focus();
            }));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window Loaded event by recording the window position
        /// and starting the interactive loop.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_Loaded(
            object sender,    /* in */
            RoutedEventArgs e /* in */
            )
        {
            this.WindowPositionInfo = PositionInfoFromWindow(
                WindowPosition.None, this);

            if (!StartupInteractiveLoop())
                MessageBox("Failed to startup interactive loop.");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window LocationChanged event by recording the new
        /// window position.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_LocationChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            this.WindowPositionInfo = PositionInfoFromWindow(
                WindowPosition.None, this);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window SizeChanged event by recording the new window
        /// position.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_SizeChanged(
            object sender,         /* in */
            SizeChangedEventArgs e /* in */
            )
        {
            this.WindowPositionInfo = PositionInfoFromWindow(
                WindowPosition.None, this);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window Closing event by disposing manufactured hosts
        /// and shutting down the interactive loop.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_Closing(
            object sender,    /* in */
            CancelEventArgs e /* in */
            )
        {
            if (!DisposeHosts())
                MessageBox("Failed to dispose hosts.");

            if (!ShutdownInteractiveLoop())
                MessageBox("Failed to shutdown interactive loop.");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window KeyDown event by recording the key press and
        /// signaling it as ready to be read.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_KeyDown(
            object sender, /* in */
            KeyEventArgs e /* in */
            )
        {
            if (e == null)
                return;

            if (!e.Handled)
            {
                if (SetKey(e) && SignalReadKey())
                    return;

                MessageBox("Failed to set or signal key press as ready.");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window PreviewKeyDown event by processing Enter,
        /// function keys, history navigation, and the new-session shortcut.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_PreviewKeyDown(
            object sender, /* in */
            KeyEventArgs e /* in */
            )
        {
            if (e == null)
                return;

            if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.None) &&
                (e.Key == Key.Enter))
            {
                if (singleLine)
                {
                    btnEnter_Click(sender, e);
                    e.Handled = true;
                }
            }
            else if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.None) &&
                (e.Key == Key.F1))
            {
                MessageBox(KeyDownHelpText);
            }
            else if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.None) &&
                (e.Key == Key.F2))
            {
                singleLine = !singleLine;

                SetStatus(String.Format(
                    "single-line mode: {0}", singleLine ?
                    "enabled" : "disabled"));

                e.Handled = true;
            }
            else if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.Control) &&
                (e.Key == Key.Up))
            {
                string text = null;

                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (historyList != null)
                    {
                        if (historyIndex != Index.Invalid)
                        {
                            historyIndex--;

                            if (historyIndex < 0)
                                historyIndex = 0;
                        }
                        else
                        {
                            historyIndex = historyList.Count - 1;
                        }

                        if ((historyIndex >= 0) &&
                            (historyIndex < historyList.Count))
                        {
                            text = historyList[historyIndex];
                        }
                    }
                }

                if (text != null)
                    SetInput(text);

                e.Handled = true;
            }
            else if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.Control) &&
                (e.Key == Key.Down))
            {
                string text = null;

                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (historyList != null)
                    {
                        if (historyIndex != Index.Invalid)
                        {
                            historyIndex++;

                            if (historyIndex >= historyList.Count)
                                historyIndex = historyList.Count - 1;
                        }
                        else
                        {
                            historyIndex = 0;
                        }

                        if ((historyIndex >= 0) &&
                            (historyIndex < historyList.Count))
                        {
                            text = historyList[historyIndex];
                        }
                    }
                }

                if (text != null)
                    SetInput(text);

                e.Handled = true;
            }
            else if (!e.Handled &&
                (Keyboard.Modifiers == ModifierKeys.Control) &&
                (e.Key == Key.N))
            {
                //
                // HACK: Spawn a new copy of this window complete
                //       with its own interpreter and sub-windows.
                //
                Thread thread = Engine.CreateThread(
                    SafeGetInterpreter(), InteractiveWindowStart, 0,
                    true, false, true);

                if (thread != null)
                {
                    thread.Name = String.Format(
                        "InteractiveWindowStart: {0}: {1}",
                        typeof(InteractiveWindow).FullName,
                        SafeGetInterpreter());

                    thread.Start();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the Enter button Click event by adding the input to the
        /// history and signaling the line as ready to be read.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnEnter_Click(
            object sender,    /* in */
            RoutedEventArgs e /* in */
            )
        {
            if (historyList != null)
            {
                string text = GetInput();

                if (!String.IsNullOrEmpty(text))
                    historyList.Add(text);
            }

            if (SignalReadLine())
                return;

            MessageBox("Failed to signal line input as ready.");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the Cancel button Click event by canceling any script
        /// evaluation in progress.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnCancel_Click(
            object sender,    /* in */
            RoutedEventArgs e /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            Interpreter localInterpreter = SafeGetInterpreter();

            if (localInterpreter != null)
            {
                code = localInterpreter.CancelAnyEvaluate(
                    "cancel button invoked", CancelFlags.UnwindAndNotify |
                    CancelFlags.AllInterpreters, ref error);
            }
            else
            {
                error = "invalid interpreter";
                code = ReturnCode.Error;
            }

            if (code != ReturnCode.Ok)
            {
                MessageBox(String.Format(
                    "Script cancellation error: {0}",
                    Utility.FormatResult(code, error)));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the input box KeyDown event, implementing tab-completion
        /// for Eagle commands, sub-commands, functions, procedures, types, and
        /// object members.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void txtInput_KeyDown(
            object sender, /* in */
            KeyEventArgs e /* in */
            )
        {
            if (e == null)
                return;

            if (e.Handled ||
                (Keyboard.Modifiers != ModifierKeys.None) ||
                (e.Key != Key.Tab))
            {
                return;
            }

            Interpreter localInterpreter;
            IHost localHost;
            bool localNoCase;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                localInterpreter = interpreter;
                localHost = host;
                localNoCase = noCase;
            }

            if ((localInterpreter == null) || (localHost == null))
                goto done;

            {
                bool help = false;
                StringList list = null;
                Result error = null;

                /* IGNORED */
                CompletionOps.Complete(
                    localInterpreter, localNoCase, GetInput(),
                    ref help, ref list, ref error);

                if (help)
                {
                    DoCommand("#help");
                }
                else if ((list != null) && (list.Count > 0))
                {
                    int hostLeft = 0; /* NOT USED */
                    int hostTop = 0; /* NOT USED */

                    if (localHost.WriteBox(CompletionWindowName,
                            new StringPairList(list), null,
                            false, false, ref hostLeft, ref hostTop))
                    {
                        IHostWindowManager windowManager =
                            SafeGetHostWindowManager();

                        if (windowManager != null)
                        {
                            windowManager.ActivateWindow(
                                CompletionWindowName, WindowType.Box,
                                true);
                        }

                        Activate();
                    }
                }

                if (error != null)
                    CommonOps.WriteError(localHost, error, true);
            }

        done:

            e.Handled = true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindow Members
        /// <summary>
        /// Gets the size of the specified host element, delegating window
        /// sizes to the base class and computing buffer sizes from the output
        /// box viewport.
        /// </summary>
        /// <param name="hostSizeType">
        /// The type of size being requested.
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
        public override bool GetSize(
            HostSizeType hostSizeType, /* in */
            ref double width,          /* out */
            ref double height          /* out */
            )
        {
            if ((hostSizeType == HostSizeType.WindowCurrent) ||
                (hostSizeType == HostSizeType.WindowMaximum))
            {
                return base.GetSize(hostSizeType, ref width, ref height);
            }

            if ((hostSizeType == HostSizeType.Any) ||
                (hostSizeType == HostSizeType.BufferCurrent) ||
                (hostSizeType == HostSizeType.BufferMaximum))
            {
                double localWidth = 0.0;
                double localHeight = 0.0;

                Invoke(txtOutput, new DelegateWithNoArgs(delegate()
                {
                    localWidth = txtOutput.ViewportWidth;
                    localHeight = txtOutput.ViewportHeight;
                }));

                width = localWidth;
                height = localHeight;
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the size of the specified host element, delegating window
        /// sizes to the base class and applying buffer sizes to the output
        /// box.
        /// </summary>
        /// <param name="hostSizeType">
        /// The type of size being set.
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
        public override bool SetSize(
            HostSizeType hostSizeType, /* in */
            double width,              /* in */
            double height              /* in */
            )
        {
            if ((hostSizeType == HostSizeType.WindowCurrent) ||
                (hostSizeType == HostSizeType.WindowMaximum))
            {
                return base.SetSize(hostSizeType, width, height);
            }

            if ((hostSizeType == HostSizeType.Any) ||
                (hostSizeType == HostSizeType.BufferCurrent) ||
                (hostSizeType == HostSizeType.BufferMaximum))
            {
                Invoke(txtOutput, new DelegateWithNoArgs(delegate()
                {
                    txtOutput.Width = width;
                    txtOutput.Height = height;
                }));

                return true;
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostStreamManager Members
        /// <summary>
        /// Signals that a key press is ready to be read, delegating to the
        /// input window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SignalReadKey()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.SignalReadKey();

            if (streamManager != null)
                return streamManager.SignalReadKey();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals that a line of input is ready to be read, delegating to the
        /// input window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SignalReadLine()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.SignalReadLine();

            if (streamManager != null)
                return streamManager.SignalReadLine();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals that input has been canceled, delegating to the input
        /// window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SignalCanceled()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.SignalCanceled();

            if (streamManager != null)
                return streamManager.SignalCanceled();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits for a key press to become available, delegating to the input
        /// window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool WaitReadKey()
        {
            if (this.IsClosing)
                return false;

            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.WaitReadKey();

            if (streamManager != null)
                return streamManager.WaitReadKey();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits for a line of input to become available, delegating to the
        /// input window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool WaitReadLine()
        {
            if (this.IsClosing)
                return false;

            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.WaitReadLine();

            if (streamManager != null)
                return streamManager.WaitReadLine();

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the next available key press, delegating to the input
        /// window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// The key press event data, or null if none is available.
        /// </returns>
        public override EventArgs ReadKey()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.ReadKey();

            if (streamManager != null)
                return streamManager.ReadKey();

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the next available line of input, delegating to the input
        /// window's stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// The line of input, or null if none is available.
        /// </returns>
        public override string ReadLine()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.ReadLine();

            if (streamManager != null)
                return streamManager.ReadLine();

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the most recently read key press without consuming it,
        /// delegating to the input window's stream manager when it differs
        /// from this instance.
        /// </summary>
        /// <returns>
        /// The key press event data, or null if none is available.
        /// </returns>
        public override EventArgs GetKey()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.GetKey();

            if (streamManager != null)
                return streamManager.GetKey();

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the pending key press, delegating to the input window's stream
        /// manager when it differs from this instance.
        /// </summary>
        /// <param name="value">
        /// The key press event data to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SetKey(
            EventArgs value /* in */
            )
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.SetKey(value);

            if (streamManager != null)
                return streamManager.SetKey(value);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the current input text, delegating to the input window's
        /// stream manager when it differs from this instance.
        /// </summary>
        /// <returns>
        /// The current input text, or null if none is available.
        /// </returns>
        public override string GetInput()
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.GetInput();

            if (streamManager != null)
                return streamManager.GetInput();

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified text to the current input, delegating to the
        /// input window's stream manager when it differs from this instance.
        /// </summary>
        /// <param name="value">
        /// The text to append to the input.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool AddInput(
            string value /* in */
            )
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.AddInput(value);

            if (streamManager != null)
                return streamManager.AddInput(value);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Inserts the specified text into the current input, delegating to
        /// the input window's stream manager when it differs from this
        /// instance.
        /// </summary>
        /// <param name="value">
        /// The text to insert into the input.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool InsertInput(
            string value /* in */
            )
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.InsertInput(value);

            if (streamManager != null)
                return streamManager.InsertInput(value);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Replaces the current input with the specified text, delegating to
        /// the input window's stream manager when it differs from this
        /// instance.
        /// </summary>
        /// <param name="value">
        /// The text to set as the input.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool SetInput(
            string value /* in */
            )
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.InputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.SetInput(value);

            if (streamManager != null)
                return streamManager.SetInput(value);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified line to the output, delegating to the output
        /// window's stream manager when it differs from this instance.
        /// </summary>
        /// <param name="value">
        /// The line of text to append to the output.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public override bool AddOutputLine(
            string value /* in */
            )
        {
            IHostStreamManager streamManager = GetStreamManager(
                this.OutputWindowType);

            if (Object.ReferenceEquals(streamManager, this))
                return base.AddOutputLine(value);

            if (streamManager != null)
                return streamManager.AddOutputLine(value);

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostOutputWindow Members
        /// <summary>
        /// Returns the size of a single character in the output box, assuming
        /// a fixed-width font.
        /// </summary>
        /// <param name="width">
        /// Upon success, receives the character width.
        /// </param>
        /// <param name="height">
        /// Upon success, receives the character height.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool GetCharacterSize(
            ref double width, /* in, out */
            ref double height /* out */
            )
        {
            double localWidth = 0.0;
            double localHeight = 0.0;

            Invoke(txtOutput, new DelegateWithNoArgs(delegate()
            {
                //
                // HACK: This basically assumes that all characters are
                //       the same size (i.e. using a fixed-width font).
                //
                localWidth = CommonOps.MeasureTextWidth(
                    txtOutput, CommonOps.MeasureTextWidthString) /
                    CommonOps.MeasureTextWidthDivisor;

                localHeight = CommonOps.MeasureTextHeight(
                    txtOutput, CommonOps.MeasureTextHeightString) /
                    CommonOps.MeasureTextHeightDivisor;
            }));

            width = localWidth;
            height = localHeight;
            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowFactory Members
        /// <summary>
        /// Creates a new host bound to this window and, unless it is the
        /// primary host, tracks it for later disposal.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the new host is associated with.
        /// </param>
        /// <param name="hostData">
        /// The host data to use, or null to create new host data.
        /// </param>
        /// <param name="primary">
        /// Non-zero if the new host is the primary host owned by the
        /// interpreter.
        /// </param>
        /// <returns>
        /// The newly created host.
        /// </returns>
        public IHost NewHost(
            Interpreter interpreter, /* in */
            IHostData hostData,      /* in */
            bool primary             /* in */
            )
        {
            IHost localHost = new Hosts.Window(
                (hostData != null) ? hostData :
                    CommonOps.NewHostData(interpreter), this,
                this.OpenedHandler, this.ClosedHandler, NewLine,
                this.InputWindowType, this.OutputWindowType, true,
                Utility.ShouldTraceToHost(interpreter));

            //
            // NOTE: The primary host will be kept track of [and disposed] by
            //       the interpreter belonging to this interactive window;
            //       therefore, skip adding it here.
            //
            if (!primary)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (hosts != null)
                        hosts.Add(Utility.NextId(), localHost);
                }
            }

            return localHost;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes all hosts manufactured by this window.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool DisposeHosts()
        {
            Interpreter localInterpreter;
            Dictionary<long, IHost> localHosts;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                localInterpreter = interpreter;
                localHosts = new Dictionary<long, IHost>(hosts);
            }

            if (localHosts != null)
            {
                bool result = true;

                foreach (KeyValuePair<long, IHost> pair in localHosts)
                {
                    IHost localHost = pair.Value;

                    if (localHost == null)
                        continue;

                    IDisposable disposable = localHost as IDisposable;

                    if (disposable == null)
                        continue;

                    try
                    {
                        disposable.Dispose(); /* throw */
                    }
                    catch (Exception e)
                    {
                        Complain(localInterpreter, ReturnCode.Error, e);

                        //
                        // NOTE: We failed at least once.
                        //
                        result = false;
                    }
                }

                return result;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new window of the specified type using the current window
        /// manager, registrar, and identifier of this instance.
        /// </summary>
        /// <param name="windowType">
        /// The type of window to create.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type to use for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type to use for output.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The position information to apply to the new window.
        /// </param>
        /// <param name="minimumSize">
        /// Non-zero to constrain the window to a minimum size.
        /// </param>
        /// <param name="autoSize">
        /// Non-zero to size the window automatically.
        /// </param>
        /// <param name="autoClose">
        /// Non-zero to close the window automatically when its loop exits.
        /// </param>
        /// <param name="autoFlush">
        /// Non-zero to flush the window output automatically.
        /// </param>
        /// <returns>
        /// The newly created window, or null if the window type is
        /// unsupported.
        /// </returns>
        public IHostWindow CreateWindow(
            WindowType windowType,                 /* in */
            WindowType inputWindowType,            /* in */
            WindowType outputWindowType,           /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool minimumSize,                      /* in */
            bool autoSize,                         /* in */
            bool autoClose,                        /* in */
            bool autoFlush                         /* in */
            )
        {
            return CreateWindow(
                this.WindowManager, this.WindowRegistrar, this.WindowId,
                null, windowType, inputWindowType, outputWindowType,
                this.OpenedHandler, this.ClosedHandler, windowPositionInfo,
                minimumSize, autoSize, autoClose, autoFlush);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new window of the specified type and, if a registrar is
        /// supplied, registers it.
        /// </summary>
        /// <param name="windowManager">
        /// The window manager to associate with the new window.
        /// </param>
        /// <param name="windowRegistrar">
        /// The registrar used to track the new window.
        /// </param>
        /// <param name="id">
        /// The unique identifier for the new window.
        /// </param>
        /// <param name="name">
        /// The name of the new window.
        /// </param>
        /// <param name="windowType">
        /// The type of window to create.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type to use for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type to use for output.
        /// </param>
        /// <param name="openedHandler">
        /// The event handler invoked when the new window is opened.
        /// </param>
        /// <param name="closedHandler">
        /// The event handler invoked when the new window is closed.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The position information to apply to the new window.
        /// </param>
        /// <param name="minimumSize">
        /// Non-zero to constrain the window to a minimum size.
        /// </param>
        /// <param name="autoSize">
        /// Non-zero to size the window automatically.
        /// </param>
        /// <param name="autoClose">
        /// Non-zero to close the window automatically when its loop exits.
        /// </param>
        /// <param name="autoFlush">
        /// Non-zero to flush the window output automatically.
        /// </param>
        /// <returns>
        /// The newly created window, or null if the window type is
        /// unsupported.
        /// </returns>
        public IHostWindow CreateWindow(
            IHostWindowManager windowManager,      /* in */
            IHostWindowRegistrar windowRegistrar,  /* in */
            long id,                               /* in */
            string name,                           /* in */
            WindowType windowType,                 /* in */
            WindowType inputWindowType,            /* in */
            WindowType outputWindowType,           /* in */
            EventHandler openedHandler,            /* in */
            EventHandler closedHandler,            /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool minimumSize,                      /* in */
            bool autoSize,                         /* in */
            bool autoClose,                        /* in */
            bool autoFlush                         /* in */
            )
        {
            IHostWindow window = null;

            switch (windowType & WindowType.Mask)
            {
                case WindowType.Input:
                    {
                        window = new InputWindow(
                            windowManager, this, windowRegistrar, id,
                            windowType, openedHandler, closedHandler,
                            windowPositionInfo, minimumSize, autoSize,
                            autoClose, autoFlush);

                        break;
                    }
                case WindowType.Output:
                case WindowType.Error:
                case WindowType.Trace:
                case WindowType.Box:
                    {
                        window = new OutputWindow(
                            windowManager, this, windowRegistrar, id,
                            windowType, openedHandler, closedHandler,
                            windowPositionInfo, minimumSize, autoSize,
                            autoClose, autoFlush);

                        break;
                    }
                case WindowType.Interactive:
                    {
                        window = new InteractiveWindow(
                            windowRegistrar, id, inputWindowType,
                            outputWindowType, openedHandler,
                            closedHandler, args);

                        break;
                    }
            }

            //
            // NOTE: If possible, keep track of this newly created window
            //       using the provided window registrar.
            //
            if ((windowRegistrar != null) && (window != null))
            {
                /* IGNORED */
                windowRegistrar.RegisterWindow(
                    CommonOps.FormatWindowName(name, id), window, true);
            }

            return window;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostInteractiveWindow Members
        /// <summary>
        /// Determines whether this window currently has an interactive
        /// interpreter.
        /// </summary>
        /// <returns>
        /// Non-zero if an interactive interpreter is present; otherwise, zero.
        /// </returns>
        public bool HaveInteractiveInterpreter()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return (interpreter != null);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified interpreter is the interactive
        /// interpreter for this window.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to compare against this window's interpreter.
        /// </param>
        /// <returns>
        /// Non-zero if the specified interpreter matches; otherwise, zero.
        /// </returns>
        public bool MatchInteractiveInterpreter(
            Interpreter interpreter /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return Object.ReferenceEquals(
                    interpreter, this.interpreter);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the reference to the interactive interpreter for this
        /// window.
        /// </summary>
        /// <returns>
        /// Non-zero if an interpreter reference was present and cleared;
        /// otherwise, zero.
        /// </returns>
        public bool ResetInteractiveInterpreter()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                bool result = (interpreter != null);

                interpreter = null;

                return result;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the reference to the interactive interpreter only if it
        /// matches the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that must match before the reference is cleared.
        /// </param>
        /// <returns>
        /// Non-zero if the interpreter reference was cleared; otherwise, zero.
        /// </returns>
        public bool MaybeResetInteractiveInterpreter(
            Interpreter interpreter /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!HaveInteractiveInterpreter())
                    return false;

                if (!MatchInteractiveInterpreter(interpreter))
                    return false;

                return ResetInteractiveInterpreter();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Starts the interactive loop on a background thread, creating a new
        /// interpreter or using the existing plugin interpreter as
        /// appropriate.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool StartupInteractiveLoop()
        {
            DebugTraceMe("StartupInteractiveLoop: entered");

            Interpreter localInterpreter;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                localInterpreter = interpreter;

                if (localInterpreter == null)
                    localInterpreter = Shell.Window.GetPluginInterpreter();
            }

            string threadNamePrefix;
            Thread localThread;

            if (Utility.DoesEnvironmentVariableExist(
                    CommonOps.NoCreateInterpreterEnvVarName))
            {
                threadNamePrefix = "InteractiveThreadStartWithExisting";

                localThread = Engine.CreateThread(localInterpreter,
                    InteractiveThreadStartWithExisting, 0, true, false,
                    true);
            }
            else
            {
                threadNamePrefix = "InteractiveThreadStartWithCreate";

                localThread = Engine.CreateThread(localInterpreter,
                    InteractiveThreadStartWithCreate, 0, true, false,
                    true);
            }

            if (localThread != null)
            {
                localThread.Name = String.Format(
                    "{0}: {1}: {2}", threadNamePrefix,
                    typeof(InteractiveWindow).FullName,
                    localInterpreter);

                localThread.Start(args);

                lock (syncRoot)
                {
                    thread = localThread;
                }

                DebugTraceMe("StartupInteractiveLoop: exited (true)");

                return true;
            }

            DebugTraceMe("StartupInteractiveLoop: exited (false)");

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shuts down the interactive loop, signaling the interpreter to exit
        /// and waiting for the loop thread to terminate or aborting it if
        /// necessary.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool ShutdownInteractiveLoop()
        {
            DebugTraceMe("ShutdownInteractiveLoop: entered");

            int levels = Interlocked.Increment(ref shutdownLevels);

            try
            {
                if (levels == 1)
                {
                    Interpreter localInterpreter;
                    Thread localThread;

                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        //
                        // NOTE: Transfer interpreter reference to local
                        //       variable.
                        //
                        localInterpreter = interpreter;
                        interpreter = null;

                        //
                        // NOTE: Transfer thread reference to local
                        //       variable.
                        //
                        localThread = thread;
                        thread = null;
                    }

                    if (localThread != null)
                    {
                        //
                        // NOTE: Is the interactive loop for our interpreter
                        //       still running?  If so, try to shut it down
                        //       gracefully.
                        //
                        if (localThread.IsAlive &&
                            (localInterpreter != null) &&
                            (Interlocked.CompareExchange(
                                    ref activeInteractiveLoops, 0, 0) > 0))
                        {
                            //
                            // NOTE: Mark window as "pending close" now.  This
                            //       is (maybe) necessary because we need to get
                            //       the interactive loop thread to exit the
                            //       dispatcher frame we plan on pushing later.
                            //       This assignment would be redundant in the
                            //       event we are being called from our Closing
                            //       event because our base window class also
                            //       sets this property in its Closing event;
                            //       however, it would NOT be redundant if
                            //       somebody else calls this method.
                            //
                            if (!this.IsClosing)
                                this.IsClosing = true;

                            ///////////////////////////////////////////////////

                            //
                            // NOTE: Force interactive loop to exit the very
                            //       next time it checks its "Exit" property.
                            //       This works better than using script
                            //       cancellation because this property is
                            //       "sticky" once set and it is checked from
                            //       all the same places.
                            //
                            localInterpreter.ExitNoThrow = true;

                            ///////////////////////////////////////////////////

                            //
                            // NOTE: Attempt to cause current call to ReadLine
                            //       inside the interactive loop to return null;
                            //       this should cause the interactive loop to
                            //       now exit, as long as the Exit property of
                            //       the interpreter has been set to true.
                            //
                            SetInput(String.Empty); SignalReadLine();

                            ///////////////////////////////////////////////////

                            //
                            // BUGFIX: This method will not work correctly (i.e.
                            //         will deadlock) if this thread happens to
                            //         be the interactive loop thread.
                            //
                            if ((localThread != null) && !Object.ReferenceEquals(
                                    localThread, Thread.CurrentThread))
                            {
                                //
                                // NOTE: Create new dispatcher frame so that
                                //       the pending Windows message to fetch
                                //       input text (from inside the ReadLine
                                //       method) can succeed and exit.
                                //
                                DispatcherFrame localDispatcherFrame =
                                    new DispatcherFrame();

                                lock (syncRoot)
                                {
                                    dispatcherFrame = localDispatcherFrame;
                                }

                                Dispatcher.PushFrame(localDispatcherFrame);

                                //
                                // NOTE: Wait for the thread to exit cleanly;
                                //       failing that, kill it by force.
                                //
                                if (!localThread.Join(ThreadJoinTimeout) &&
                                    localThread.IsAlive &&
                                    !localInterpreter.NoThreadAbort)
                                {
                                    localThread.Abort(); /* BUGBUG: Leaks? */
                                }
                            }
                        }

                        localThread = null;
                    }
                }
                else
                {
                    DebugTraceMe("ShutdownInteractiveLoop: busy");
                }
            }
            finally
            {
                Interlocked.Decrement(ref shutdownLevels);
            }

            DebugTraceMe("ShutdownInteractiveLoop: exited");

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the status text displayed in the window's status label.
        /// </summary>
        /// <param name="value">
        /// The status text to display.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool SetStatus(string value)
        {
            DebugTraceMe(String.Format("SetStatus: {0}",
                !String.IsNullOrEmpty(value) ? value : "entered"));

            Invoke(lblStatus, new DelegateWithNoArgs(delegate()
            {
                lblStatus.Content = value;
            }));

            DebugTraceMe("SetStatus: exited");

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the command history and resets the history position.
        /// </summary>
        /// <returns>
        /// Non-zero if the history was cleared; otherwise, zero.
        /// </returns>
        public bool ResetHistory()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                bool result = false;

                if ((historyList != null) && (historyList.Count > 0))
                {
                    historyList.Clear();
                    historyIndex = Index.Invalid;

                    result = true;
                }

                return result;
            }
        }
        #endregion
    }
}
