/*
 * Shell.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Interfaces.Public;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;
using Featherlight.Windows;

namespace Featherlight.Shell
{
    /// <summary>
    /// The static windowed shell.  It owns the window registrar and the WPF
    /// application instance, runs the application message loop on a dedicated
    /// thread, and installs the interpreter new-host callback so the
    /// interpreter host subsystem produces windowed hosts.  This is the bridge
    /// between the Featherlight plugin and the WPF windowing system.
    /// </summary>
    [ObjectId("2ae18512-cd3b-4589-a0c7-24b845cfa6e2")]
    internal static class Window
    {
        #region Shutdown Ordering
        //
        // WARNING: The teardown sequence below is load-bearing.  It was
        //          arrived at by fixing a series of plugin-unload / host-
        //          shutdown deadlocks; reordering the steps tends to bring
        //          them back.  Read this before changing Shutdown(), the
        //          WindowRegistrar, or the window Closing handlers.
        //
        // NOTE: There are two entry points into Shutdown(), and both must be
        //       safe to reach concurrently.  The shutdownCount Interlocked
        //       guard makes the body run exactly once:
        //
        //       (A) Plugin unload.  Environment.Terminate() calls Shutdown()
        //           and then joins the interactive thread (using
        //           ThreadJoinTimeout) as a final backstop.
        //
        //       (B) Last window closed.  Window_Closed() decrements
        //           windowCount and calls Shutdown() once it reaches zero.
        //
        // NOTE: The canonical order inside Shutdown() is:
        //
        //       1. SetupNewHostCallback(false) FIRST.  Detaching
        //          Interpreter.NewHostCallback prevents the interpreter from
        //          manufacturing new windowed hosts while we are draining the
        //          registrar; otherwise teardown races window creation and
        //          may never converge.
        //
        //       2. windowRegistrar.Shutdown(applicationCreated).  Close every
        //          registered window WHILE the dispatcher message loop is
        //          still pumping (see step 3).  Windows are closed in reverse
        //          registration order (children before the primary
        //          interactive window).  When we did NOT create the WPF
        //          Application (applicationCreated == false, i.e. we are a
        //          guest in a foreign message loop), interactive windows are
        //          force-closed here rather than relying on the application
        //          to tear them down.
        //
        //       3. application.Shutdown(), only when we created it, and only
        //          marshaled onto the dispatcher thread (via CommonOps.Invoke)
        //          because it is a UI-thread operation.  This stops the
        //          message loop, so it MUST come after step 2 -- once the loop
        //          stops, windows can no longer be closed cleanly.
        //
        // NOTE: Per-window teardown (driven by step 2, or by a user-initiated
        //       window close) flows as:
        //
        //           WindowRegistrar.Shutdown
        //             -> CommonOps.CloseWindow(window, noLoop: false,
        //                  shutdown: true)
        //               -> IHostInteractiveWindow.ShutdownInteractiveLoop()
        //               -> CloseAsync()
        //
        //       ShutdownInteractiveLoop() is where the thread-join /
        //       dispatcher-frame deadlock avoidance lives; see the detailed
        //       notes on that method before touching any of this.  CloseAsync
        //       (rather than Close) is used during shutdown because the
        //       interactive loop has already been drained and the window close
        //       is simply queued, which avoids a re-entrant blocking close on
        //       the teardown path.
        //
        // NOTE: In one line -- detach new-host callback, THEN close windows
        //       (loop still pumping), THEN stop the application (loop stops).
        //
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        //
        // NOTE: This is the plugin shutdown mode for the XAML application
        //       (i.e. when an interpreter context is being used).
        //
        /// <summary>
        /// The WPF application shutdown mode used when running under an
        /// interpreter (explicit shutdown).
        /// </summary>
        private static readonly ShutdownMode PluginShutdownMode =
            ShutdownMode.OnExplicitShutdown;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the environment variable names used to configure
        //       the input/output window types for the interactive window.
        //
        /// <summary>
        /// The environment variable name used to configure the interactive
        /// window's input window type.
        /// </summary>
        private static readonly string InputWindowTypeEnvVarName =
            "InputWindowType";

        /// <summary>
        /// The environment variable name used to configure the interactive
        /// window's output window type.
        /// </summary>
        private static readonly string OutputWindowTypeEnvVarName =
            "OutputWindowType";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The default input/output window types for the interactive
        //       window.
        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The default input window type for the interactive window.
        /// </summary>
        internal static WindowType DefaultInputWindowType =
            WindowType.Interactive;

        /// <summary>
        /// The default output window type for the interactive window.
        /// </summary>
        internal static WindowType DefaultOutputWindowType =
            WindowType.Interactive;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: The object we are using to keep track of all open windows
        //       (for clean shutdown purposes only).
        //
        /// <summary>
        /// The registrar tracking all open windows, used for clean shutdown.
        /// </summary>
        private static IHostWindowRegistrar windowRegistrar;

        //
        // NOTE: The interpreter instance setup by the most recent IPlugin
        //       instance to be initialized.  This may be null.  It should
        //       be checked (e.g. for disposal status, etc) before being
        //       used.
        //
        /// <summary>
        /// The interpreter set up by the most recently initialized plugin
        /// instance; may be null.
        /// </summary>
        private static Interpreter pluginInterpreter;

        //
        // NOTE: Did this plugin create the WPF application instance?
        //
        /// <summary>
        /// Non-zero if this shell created the WPF application instance.
        /// </summary>
        private static bool applicationCreated;

        //
        // NOTE: The application instance we create in Main.
        //
        /// <summary>
        /// The WPF application instance created in Main.
        /// </summary>
        private static Application application;

        //
        // NOTE: This static (i.e. global) variable is used to keep track of
        //       the total number of active windows [derived from this class].
        //       When this number reaches zero, the application is explicitly
        //       shutdown.
        //
        /// <summary>
        /// The total number of active windows; the application is shut down
        /// when this reaches zero.
        /// </summary>
        private static int windowCount;

        //
        // NOTE: This static (i.e. global) variable is used to keep track of
        //       the total number of times the Application.Run method has been
        //       called for this application domain.
        //
        /// <summary>
        /// The number of times the application run method has been called in
        /// this application domain.
        /// </summary>
        private static int startupCount;

        //
        // NOTE: This static (i.e. global) variable is used to keep track of
        //       the total number of times the Application.Shutdown method has
        //       been called for this application domain.
        //
        /// <summary>
        /// The number of times the application shutdown method has been called
        /// in this application domain.
        /// </summary>
        private static int shutdownCount;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static Field Access Methods
        /// <summary>
        /// Determines whether the specified interpreter is the current plugin
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to compare.
        /// </param>
        /// <returns>
        /// Non-zero if it matches; otherwise, zero.
        /// </returns>
        public static bool MatchPluginInterpreter(
            Interpreter interpreter /* in */
            )
        {
            return Object.ReferenceEquals(
                interpreter, pluginInterpreter);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the plugin interpreter when it matches the specified one.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to match before clearing.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin interpreter was cleared; otherwise, zero.
        /// </returns>
        public static bool MaybeResetPluginInterpreter(
            Interpreter interpreter /* in */
            )
        {
            if ((interpreter != null) &&
                MatchPluginInterpreter(interpreter))
            {
                pluginInterpreter = null;
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current plugin interpreter.
        /// </summary>
        /// <returns>
        /// The plugin interpreter, or null when none.
        /// </returns>
        public static Interpreter GetPluginInterpreter()
        {
            return pluginInterpreter;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current plugin interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to record.
        /// </param>
        public static void SetPluginInterpreter(
            Interpreter interpreter /* in */
            )
        {
            pluginInterpreter = interpreter;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this shell created the WPF application instance.
        /// </summary>
        /// <returns>
        /// Non-zero if the application was created here; otherwise, zero.
        /// </returns>
        public static bool WasApplicationCreated()
        {
            return applicationCreated;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Application Creation Methods
        /// <summary>
        /// Gets the current WPF application or creates one, configuring it for
        /// explicit shutdown.
        /// </summary>
        /// <param name="windowRegistrar">
        /// The registrar whose exit code is set on failure.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool TryCreateApplication(
            IHostWindowRegistrar windowRegistrar /* in */
            )
        {
            application = Application.Current;
            applicationCreated = false;

            if (application != null)
            {
                return true;
            }
            else
            {
                try
                {
                    application = new Application();
                    applicationCreated = true;

                    //
                    // NOTE: This plugin requires that the shutdown mode to
                    //       be explicit (i.e. because lots of XAML windows
                    //       are routinely created and destroyed without
                    //       regard for which one is primary or last).
                    //
                    application.ShutdownMode = PluginShutdownMode;

                    //
                    // NOTE: If we get to this point, we have succeeded.
                    //
                    return true;
                }
                catch (Exception e)
                {
                    //
                    // NOTE: An exception was thrown while attempting to create
                    //       the XAML application.  Complain about it.
                    //
                    Utility.Complain(null, ReturnCode.Error, e);

                    //
                    // NOTE: Failed to create the XAML application.  Be sure
                    //       to set the exit code of the window registrar, if
                    //       any.
                    //
                    if (windowRegistrar != null)
                        windowRegistrar.ExitCode = Utility.ExceptionExitCode();
                }
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Thread Start Routine
        //
        // NOTE: This method is for use by the plugin only.
        //
        /// <summary>
        /// The interactive thread routine that runs the windowed shell entry
        /// point.  For plugin use only.
        /// </summary>
        /// <param name="obj">
        /// The command-line arguments, as a string array.
        /// </param>
        public static void MainThreadStart(
            object obj /* in */
            ) /* ParameterizedThreadStart */
        {
            try
            {
                Utility.DebugTrace(
                    "MainThreadStart: entered", typeof(Window).Name,
                    TracePriority.MediumLow | TracePriority.FromPlugin);

                ///////////////////////////////////////////////////////////////

                int exitCode = Main(obj as string[]);

                ///////////////////////////////////////////////////////////////

                Utility.DebugTrace(
                    String.Format("MainThreadStart: Main returned {0}",
                    exitCode), typeof(Window).Name,
                    TracePriority.Medium | TracePriority.FromPlugin);

                Utility.DebugTrace(
                    "MainThreadStart: exited", typeof(Window).Name,
                    TracePriority.MediumLow | TracePriority.FromPlugin);
            }
            catch (Exception e)
            {
                Utility.Complain(null, ReturnCode.Error, e);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Application Configuration Methods
        /// <summary>
        /// Determines the input and output window types for the interactive
        /// window from the environment, falling back to the defaults.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used for culture-aware parsing.
        /// </param>
        /// <param name="inputWindowType">
        /// Upon return, receives the input window type.
        /// </param>
        /// <param name="outputWindowType">
        /// Upon return, receives the output window type.
        /// </param>
        private static void GetInteractiveWindowTypes(
            Interpreter interpreter,        /* in */
            out WindowType inputWindowType, /* out */
            out WindowType outputWindowType /* out */
            )
        {
            CultureInfo cultureInfo = null;

            if (interpreter != null)
                cultureInfo = interpreter.CultureInfo;

            string value;
            object enumValue;
            Result error = null;

            value = Utility.GetEnvironmentVariable(InputWindowTypeEnvVarName);

            if (value != null)
            {
                enumValue = Utility.TryParseFlagsEnum(interpreter,
                    typeof(WindowType), DefaultInputWindowType.ToString(),
                    value, cultureInfo, true, true, true, ref error);

                if (enumValue is WindowType)
                    inputWindowType = (WindowType)enumValue;
                else
                    inputWindowType = DefaultInputWindowType;
            }
            else
            {
                inputWindowType = DefaultInputWindowType;
            }

            value = Utility.GetEnvironmentVariable(OutputWindowTypeEnvVarName);

            if (value != null)
            {
                enumValue = Utility.TryParseFlagsEnum(interpreter,
                    typeof(WindowType), DefaultOutputWindowType.ToString(),
                    value, cultureInfo, true, true, true, ref error);

                if (enumValue is WindowType)
                    outputWindowType = (WindowType)enumValue;
                else
                    outputWindowType = DefaultOutputWindowType;
            }
            else
            {
                outputWindowType = DefaultOutputWindowType;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Application Entry Point
        /// <summary>
        /// The windowed application entry point.  Creates the application and
        /// the primary interactive window, registers it, and runs the message
        /// loop.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments.
        /// </param>
        /// <returns>
        /// The process exit code.
        /// </returns>
        [STAThread()]
        public static int Main(
            string[] args /* in */
            )
        {
            if (Interlocked.Increment(ref startupCount) == 1)
            {
                //
                // NOTE: Make sure that this class handles all calls to create
                //       new interpreter hosts.
                //
                SetupNewHostCallback(true);

                ///////////////////////////////////////////////////////////////

                if (windowRegistrar == null)
                    windowRegistrar = new WindowRegistrar();

                ///////////////////////////////////////////////////////////////

                if (TryCreateApplication(windowRegistrar))
                {
                    WindowType inputWindowType;
                    WindowType outputWindowType;

                    GetInteractiveWindowTypes(
                        null, out inputWindowType, out outputWindowType);

                    IHostWindow window = new InteractiveWindow(
                        windowRegistrar, 0, inputWindowType, outputWindowType,
                        Window_Opened, Window_Closed, args);

                    windowRegistrar.RegisterWindow(
                        CommonOps.WindowTypeToName(WindowType.Interactive),
                        window, false);

                    ///////////////////////////////////////////////////////////

                    if (applicationCreated)
                        application.Run(window as System.Windows.Window);
                    else
                        window.ShowDialog();
                }
            }

            ///////////////////////////////////////////////////////////////////

            ExitCode exitCode = (windowRegistrar != null) ?
                windowRegistrar.ExitCode : Utility.SuccessExitCode();

            return (int)exitCode;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Application Shutdown Point
        /// <summary>
        /// Shuts down the windowed shell: removes the new-host callback, shuts
        /// down all registered windows, and shuts down the WPF application
        /// when this shell created it.
        /// </summary>
        /// <returns>
        /// Non-zero if the application was shut down; otherwise, zero.
        /// </returns>
        public static bool Shutdown()
        {
            bool result = false;

            if (Interlocked.Increment(ref shutdownCount) == 1)
            {
                SetupNewHostCallback(false);

                ///////////////////////////////////////////////////////////////

                if (windowRegistrar != null)
                {
                    windowRegistrar.Shutdown(applicationCreated); /* IGNORED */
                    windowRegistrar = null;
                }

                ///////////////////////////////////////////////////////////////

                if (applicationCreated)
                {
                    CommonOps.Invoke(
                            application, new DelegateWithNoArgs(delegate()
                    {
                        application.Shutdown();

                        result = true;
                    }));

                    applicationCreated = false;
                }

                application = null;
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Host "Factory" Methods
        /// <summary>
        /// Creates a new windowed host using the interactive window's factory.
        /// Installed as the interpreter new-host callback.
        /// </summary>
        /// <param name="hostData">
        /// The data used to create and configure the host.
        /// </param>
        /// <returns>
        /// The new host, or null on failure.
        /// </returns>
        public static IHost NewHost(
            IHostData hostData /* in */
            )
        {
            IHostWindowRegistrar localWindowRegistrar = windowRegistrar;

            if (localWindowRegistrar == null)
                return null;

            IHostWindow window = localWindowRegistrar.FindWindow(null,
                WindowType.Interactive);

            if (window == null)
                return null;

            IHostWindowFactory windowFactory = window.WindowFactory;

            if (windowFactory == null)
                windowFactory = window as IHostWindowFactory;

            if (windowFactory == null)
                return null;

            Interpreter localInterpreter = null;

            if (hostData != null)
                localInterpreter = hostData.Interpreter;

            if (localInterpreter == null)
                localInterpreter = GetPluginInterpreter();

            return windowFactory.NewHost(localInterpreter, hostData,
                false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Installs or removes the interpreter new-host callback (using a
        /// cross-application-domain bridge when the plugin is isolated).
        /// </summary>
        /// <param name="setup">
        /// Non-zero to install the callback; zero to remove it.
        /// </param>
        private static void SetupNewHostCallback(
            bool setup /* in */
            )
        {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            if (Utility.IsCrossAppDomain(GetPluginInterpreter(), null))
            {
                if (setup)
                {
                    Result error = null;

                    NewHostCallbackBridge callbackBridge =
                        NewHostCallbackBridge.Create(
                            new WindowNewHostCallback(), ref error);

                    if (callbackBridge == null)
                    {
                        Utility.DebugTrace(String.Format(
                            "SetupNewHostCallback: error = {0}",
                            Utility.FormatWrapOrNull(error)),
                            typeof(Window).Name, TracePriority.Medium |
                                TracePriority.FromPlugin);

                        return;
                    }

                    Interpreter.NewHostCallback = new NewHostCallback(
                        callbackBridge.NewHostCallback);
                }
                else
                {
                    Interpreter.NewHostCallback = null;
                }
            }
            else
#endif
            {
                if (setup)
                {
                    if (Interpreter.NewHostCallback == null)
                        Interpreter.NewHostCallback = NewHost;
                }
                else
                {
                    if (Interpreter.NewHostCallback == NewHost)
                        Interpreter.NewHostCallback = null;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Event Handlers
        /// <summary>
        /// Handles a window opened event by incrementing the active window
        /// count.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private static void Window_Opened(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            //
            // NOTE: Add the window to the total number of active windows.
            //
            Interlocked.Increment(ref windowCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles a window closed event by decrementing the active window
        /// count and shutting down when it reaches zero.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private static void Window_Closed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            //
            // NOTE: Remove the window from the total number of active windows.
            //       When the number of active windows reaches zero, explicitly
            //       shutdown the application.
            //
            if (Interlocked.Decrement(ref windowCount) == 0)
                Shutdown();
        }
        #endregion
    }
}
