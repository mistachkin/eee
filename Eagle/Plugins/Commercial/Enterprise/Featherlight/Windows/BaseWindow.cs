/*
 * BaseWindow.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;

using EventWaitHandleList = System.Collections.Generic.List<
    System.Threading.EventWaitHandle>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Featherlight.Windows
{
    /// <summary>
    /// Abstract base WPF window shared by the Featherlight input, output, and
    /// interactive windows that bridges Eagle's synchronous host I/O to the
    /// asynchronous WPF controls.
    /// </summary>
    [ObjectId("cd4723e2-c701-462f-a128-e1b2d827f1ee")]
    public class BaseWindow : Window, IHostWindow, IHostStreamManager
    {
        #region Private Constants
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The set of line-terminator characters used to detect when a written
        /// value requires line-ending normalization.
        /// </summary>
        private static char[] LineTerminatorChars = {
            Characters.LineFeed, Characters.CarriageReturn
        };
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Constants
        /// <summary>
        /// The platform-specific newline string used when appending output
        /// lines.
        /// </summary>
        protected static readonly string NewLine = System.Environment.NewLine;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The minimum window width, in device-independent units, applied when
        /// a minimum size is requested.
        /// </summary>
        protected const int MinimumWidth = 400;
        /// <summary>
        /// The minimum window height, in device-independent units, applied
        /// when a minimum size is requested.
        /// </summary>
        protected const int MinimumHeight = 200;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// The object used to synchronize access to the static window-position
        /// state.
        /// </summary>
        private static readonly object staticSyncRoot = new object();
        /// <summary>
        /// The next window position to be handed out by the automatic window
        /// positioning logic.
        /// </summary>
        private static WindowPosition nextWindowPosition = WindowPosition.First;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the per-instance state of
        /// this window.
        /// </summary>
        private object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window manager responsible for tracking and unregistering this
        /// window.
        /// </summary>
        private IHostWindowManager windowManager;
        /// <summary>
        /// The window factory that created this window and is treated as its
        /// parent for positioning.
        /// </summary>
        private IHostWindowFactory windowFactory;
        /// <summary>
        /// The unique identifier assigned to this window.
        /// </summary>
        private long windowId;
        /// <summary>
        /// The type of this window.
        /// </summary>
        private WindowType windowType;
        /// <summary>
        /// The window type used for input associated with this window.
        /// </summary>
        private WindowType inputWindowType;
        /// <summary>
        /// The window type used for output associated with this window.
        /// </summary>
        private WindowType outputWindowType;
        /// <summary>
        /// The registrar used to query and set the exit code for this window.
        /// </summary>
        private IHostWindowRegistrar windowRegistrar;
        /// <summary>
        /// The event handler invoked when this window is opened.
        /// </summary>
        private EventHandler openedHandler;
        /// <summary>
        /// The event handler invoked when this window is closed.
        /// </summary>
        private EventHandler closedHandler;
        /// <summary>
        /// The text control used as the input box for this window.
        /// </summary>
        private object inputBox;
        /// <summary>
        /// The text control used as the output box for this window.
        /// </summary>
        private object outputBox;
        /// <summary>
        /// The position and sizing information associated with this window.
        /// </summary>
        private WindowPositionInfo windowPositionInfo;
        /// <summary>
        /// Non-zero if a minimum size should be enforced for this window.
        /// </summary>
        private bool minimumSize;
        /// <summary>
        /// Non-zero if this window should automatically size itself to its
        /// content.
        /// </summary>
        private bool autoSize;
        /// <summary>
        /// Non-zero if this window should be closed automatically.
        /// </summary>
        private bool autoClose;
        /// <summary>
        /// Non-zero if output written to this window should be flushed
        /// automatically.
        /// </summary>
        private bool autoFlush;
        /// <summary>
        /// The most recently captured key event arguments for this window.
        /// </summary>
        private EventArgs key;
        /// <summary>
        /// Non-zero if this window is in the process of closing.
        /// </summary>
        private bool isClosing;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The event signaled when a key is available to be read.
        /// </summary>
        private EventWaitHandle keyEvent;
        /// <summary>
        /// The event signaled when a line of input is available to be read.
        /// </summary>
        private EventWaitHandle lineEvent;
        /// <summary>
        /// The event signaled when a pending read operation has been canceled.
        /// </summary>
        private EventWaitHandle cancelEvent;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        //
        // WARNING: This is for the Visual Studio WPF designer use only.
        //          DO NOT USE THIS CONSTRUCTOR.
        //
        /// <summary>
        /// Initializes a new instance of the BaseWindow class for use by the
        /// Visual Studio WPF designer only.
        /// </summary>
        public BaseWindow()
            : this(null, null, null, 0, WindowType.None, WindowType.None,
                   WindowType.None, null, null, null, null,
                   WindowPositionInfo.None(), false, false, false, false)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes a new instance of the BaseWindow class with the
        /// specified managers, identity, window types, handlers, text
        /// controls, and behavior options.
        /// </summary>
        /// <param name="windowManager">
        /// The window manager responsible for this window.
        /// </param>
        /// <param name="windowFactory">
        /// The window factory that created this window.
        /// </param>
        /// <param name="windowRegistrar">
        /// The registrar used to query and set the exit code.
        /// </param>
        /// <param name="windowId">
        /// The unique identifier to assign to this window.
        /// </param>
        /// <param name="windowType">
        /// The type of this window.
        /// </param>
        /// <param name="inputWindowType">
        /// The window type used for input.
        /// </param>
        /// <param name="outputWindowType">
        /// The window type used for output.
        /// </param>
        /// <param name="openedHandler">
        /// The event handler to invoke when this window is opened.
        /// </param>
        /// <param name="closedHandler">
        /// The event handler to invoke when this window is closed.
        /// </param>
        /// <param name="inputBox">
        /// The text control to use as the input box.
        /// </param>
        /// <param name="outputBox">
        /// The text control to use as the output box.
        /// </param>
        /// <param name="windowPositionInfo">
        /// The position and sizing information for this window.
        /// </param>
        /// <param name="minimumSize">
        /// Non-zero to enforce a minimum size for this window.
        /// </param>
        /// <param name="autoSize">
        /// Non-zero to automatically size this window to its content.
        /// </param>
        /// <param name="autoClose">
        /// Non-zero to close this window automatically.
        /// </param>
        /// <param name="autoFlush">
        /// Non-zero to flush output to this window automatically.
        /// </param>
        public BaseWindow(
            IHostWindowManager windowManager,      /* in */
            IHostWindowFactory windowFactory,      /* in */
            IHostWindowRegistrar windowRegistrar,  /* in */
            long windowId,                         /* in */
            WindowType windowType,                 /* in */
            WindowType inputWindowType,            /* in */
            WindowType outputWindowType,           /* in */
            EventHandler openedHandler,            /* in */
            EventHandler closedHandler,            /* in */
            object inputBox,                       /* in */
            object outputBox,                      /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool minimumSize,                      /* in */
            bool autoSize,                         /* in */
            bool autoClose,                        /* in */
            bool autoFlush                         /* in */
            )
            : base()
        {
            this.windowManager = windowManager;
            this.windowFactory = windowFactory;
            this.windowRegistrar = windowRegistrar;
            this.windowId = windowId;
            this.windowType = windowType;
            this.inputWindowType = inputWindowType;
            this.outputWindowType = outputWindowType;
            this.openedHandler = openedHandler;
            this.closedHandler = closedHandler;
            this.inputBox = inputBox;
            this.outputBox = outputBox;
            this.windowPositionInfo = windowPositionInfo;
            this.minimumSize = minimumSize;
            this.autoSize = autoSize;
            this.autoClose = autoClose;
            this.autoFlush = autoFlush;

            ///////////////////////////////////////////////////////////////////

            this.key = null;
            this.isClosing = false;

            ///////////////////////////////////////////////////////////////////

            this.keyEvent = new ManualResetEvent(false);
            this.lineEvent = new ManualResetEvent(false);
            this.cancelEvent = new ManualResetEvent(false);

            ///////////////////////////////////////////////////////////////////

            this.Initialized += new EventHandler(Window_Initialized);
            this.Loaded += new RoutedEventHandler(Window_Loaded);
            this.Closing += new CancelEventHandler(Window_Closing);
            this.Closed += new EventHandler(Window_Closed);

            ///////////////////////////////////////////////////////////////////

            this.SizeToContent = autoSize ?
                SizeToContent.WidthAndHeight : SizeToContent.Manual;

            if (minimumSize)
            {
                this.MinWidth = MinimumWidth;
                this.MinHeight = MinimumHeight;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Static Methods
        /// <summary>
        /// Returns the next automatic window position and advances the shared
        /// position state.
        /// </summary>
        /// <returns>
        /// The next automatic window position to use.
        /// </returns>
        protected static WindowPosition GetNextWindowPosition()
        {
            lock (staticSyncRoot) /* TRANSACTIONAL */
            {
                WindowPosition result = nextWindowPosition;

                do
                {
                    int intValue = (int)nextWindowPosition * 2;
                    nextWindowPosition = (WindowPosition)intValue;

                    if (nextWindowPosition > WindowPosition.Last)
                        nextWindowPosition = WindowPosition.First;
                }
                while (!CommonOps.IsAutomaticPosition(nextWindowPosition));

                return result;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Creates window position information derived from the specified
        /// window.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position to associate with the result.
        /// </param>
        /// <param name="window">
        /// The window from which to derive the position information.
        /// </param>
        /// <returns>
        /// The window position information derived from the specified window.
        /// </returns>
        protected virtual WindowPositionInfo PositionInfoFromWindow(
            WindowPosition windowPosition, /* in */
            Window window                  /* in */
            )
        {
            return WindowPositionInfo.FromWindow(windowPosition, window);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reports an error condition associated with the specified
        /// interpreter, return code, and result.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the error, if any.
        /// </param>
        /// <param name="code">
        /// The return code describing the error.
        /// </param>
        /// <param name="result">
        /// The result describing the error.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void Complain(
            Interpreter interpreter, /* in */
            ReturnCode code,         /* in */
            Result result            /* in */
            )
        {
            Utility.Complain(interpreter, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits a diagnostic trace message on behalf of this window.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void DebugTrace(
            string message /* in */
            )
        {
            try
            {
                Utility.DebugTrace(
                    message, typeof(BaseWindow).Name,
                    TracePriority.MediumLow |
                        TracePriority.ViaWrapperFromPlugin);
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits a diagnostic trace message annotated with this window's
        /// identifier, name, and type.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void DebugTraceMe(
            string message /* in */
            )
        {
            try
            {
                DebugTrace(String.Format(
                    "{0}, windowId = {1}, windowName = {2}, windowType = {3}",
                    message, WindowId, Utility.FormatWrapOrNull(WindowName),
                    WindowType));
            }
            catch (Exception e)
            {
                Complain(ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the title to use for message boxes shown by this window.
        /// </summary>
        /// <returns>
        /// The message box title, or null if none is available.
        /// </returns>
        protected virtual string GetMessageBoxTitle()
        {
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows an informational message box with the specified message.
        /// </summary>
        /// <param name="message">
        /// The message to display.
        /// </param>
        /// <returns>
        /// The result indicating which button the user selected.
        /// </returns>
        protected MessageBoxResult MessageBox(
            string message /* in */
            )
        {
            return MessageBox(message, MessageBoxImage.Information);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows a message box with the specified message and icon.
        /// </summary>
        /// <param name="message">
        /// The message to display.
        /// </param>
        /// <param name="icon">
        /// The icon to display in the message box.
        /// </param>
        /// <returns>
        /// The result indicating which button the user selected.
        /// </returns>
        protected virtual MessageBoxResult MessageBox(
            string message,      /* in */
            MessageBoxImage icon /* in */
            )
        {
            string title = GetMessageBoxTitle();

            if (title == null)
            {
                title = Utility.GetAssemblyTitle(
                    Assembly.GetExecutingAssembly());
            }

            if (title == null)
            {
                /* IGNORED */
                GetTitle(ref title);
            }

            return System.Windows.MessageBox.Show(
                this, message, title, MessageBoxButton.OK, icon);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Reports an error condition associated with the specified return
        /// code and result.
        /// </summary>
        /// <param name="code">
        /// The return code describing the error.
        /// </param>
        /// <param name="result">
        /// The result describing the error.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Complain(
            ReturnCode code, /* in */
            Result result    /* in */
            )
        {
            Complain(null, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bounding rectangle of the factory window, if any, treated
        /// as the parent of this window.
        /// </summary>
        /// <returns>
        /// The bounding rectangle of the factory window.
        /// </returns>
        private Rect RectForFactoryWindow()
        {
            return CommonOps.RectFromIHostWindow(windowFactory as IHostWindow);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the actual size from the specified position information to
        /// this window.
        /// </summary>
        /// <param name="windowPositionInfo">
        /// The position information containing the actual size to apply.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool SetupActualSize(
            WindowPositionInfo windowPositionInfo /* in */
            )
        {
            if (windowPositionInfo == null)
                return false;

            return windowPositionInfo.ActualSize(this);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Positions this window according to the specified position
        /// information, resolving automatic positions as needed.
        /// </summary>
        /// <param name="windowPositionInfo">
        /// The position information describing where to place this window.
        /// </param>
        private void PositionWindow(
            WindowPositionInfo windowPositionInfo /* in */
            )
        {
            if (windowPositionInfo == null)
                return;

            WindowPosition windowPosition = windowPositionInfo.WindowPosition;

            if (windowPosition != WindowPosition.None)
            {
                if (windowPosition == WindowPosition.Automatic)
                {
                    windowPosition = GetNextWindowPosition();
                    windowPositionInfo.WindowPosition = windowPosition;
                }

                //
                // NOTE: This code assumes that the IHostWindowFactory,
                //       if any, is the parent of this window.
                //
                CommonOps.CalculatePosition(
                    windowPosition, RectForFactoryWindow(),
                    ref windowPositionInfo.Rectangle);
            }

            double left = windowPositionInfo.Rectangle.Left;
            double top = windowPositionInfo.Rectangle.Top;

            if ((left != _Position.Invalid) || (top != _Position.Invalid))
            {
                Invoke(this, new DelegateWithNoArgs(delegate()
                {
                    if (left != _Position.Invalid)
                        this.Left = left;

                    if (top != _Position.Invalid)
                        this.Top = top;
                }));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Normalizes the line endings of the specified value when it contains
        /// line-terminator characters.
        /// </summary>
        /// <param name="value">
        /// The value to conditionally normalize.
        /// </param>
        /// <returns>
        /// The value with normalized line endings, or the original value if no
        /// normalization was needed.
        /// </returns>
        private static string MaybeMutateValue(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            if ((LineTerminatorChars == null) ||
                value.IndexOfAny(LineTerminatorChars) == Index.Invalid)
            {
                return value;
            }

            return Utility.NormalizeLineEndings(value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends or replaces the text of the specified output text box,
        /// optionally enforcing a maximum length and flushing the view.
        /// </summary>
        /// <param name="outputTextBox">
        /// The output text box to write to.
        /// </param>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <param name="length">
        /// The maximum allowed text length, or zero for no limit.
        /// </param>
        /// <param name="flush">
        /// Non-zero to scroll the text box to the end after writing.
        /// </param>
        private static void WriteCore(
            TextBox outputTextBox, /* in */
            string value,          /* in */
            int length,            /* in */
            bool flush             /* in */
            ) /* NOT THREAD-SAFE */
        {
            if (outputTextBox != null)
            {
                string text = outputTextBox.Text;

                if (text != null)
                {
                    if (length > 0)
                    {
                        int textLength = text.Length;

                        if (value != null)
                        {
                            value = MaybeMutateValue(value);

                            if (value != null)
                            {
                                textLength += value.Length;

                                if (textLength <= length)
                                    outputTextBox.Text += value;
                                else
                                    outputTextBox.Text = value;
                            }
                        }
                    }
                    else if (value != null)
                    {
                        value = MaybeMutateValue(value);

                        if (value != null)
                            outputTextBox.Text += value;
                    }
                }
                else if (value != null)
                {
                    value = MaybeMutateValue(value);

                    if (value != null)
                        outputTextBox.Text = value;
                }

                if (flush)
                {
                    outputTextBox.Select(outputTextBox.Text.Length, 0);
                    outputTextBox.ScrollToEnd();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the cancel event to the specified list of events if it is
        /// available.
        /// </summary>
        /// <param name="events">
        /// The list of events to which the cancel event is added; created if
        /// necessary.
        /// </param>
        private void AddCancelEvent(
            ref EventWaitHandleList events /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (cancelEvent != null)
                {
                    if (events == null)
                        events = new EventWaitHandleList();

                    events.Add(cancelEvent);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the array of events to wait on while reading a key, including
        /// the cancel event.
        /// </summary>
        /// <returns>
        /// The array of events to wait on, or null if no key event is
        /// available.
        /// </returns>
        private EventWaitHandle[] GetReadKeyEvents()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyEvent == null)
                    return null;

                EventWaitHandleList events = new EventWaitHandleList();

                events.Add(keyEvent);
                AddCancelEvent(ref events);

                return events.ToArray();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the array of events to wait on while reading a line, including
        /// the cancel event.
        /// </summary>
        /// <returns>
        /// The array of events to wait on, or null if no line event is
        /// available.
        /// </returns>
        private EventWaitHandle[] GetReadLineEvents()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (lineEvent == null)
                    return null;

                EventWaitHandleList events = new EventWaitHandleList();

                events.Add(lineEvent);
                AddCancelEvent(ref events);

                return events.ToArray();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets all of the specified events.
        /// </summary>
        /// <param name="events">
        /// The events to reset.
        /// </param>
        /// <returns>
        /// Non-zero if all events were reset successfully; otherwise, zero.
        /// </returns>
        private static bool ResetEvents(
            EventWaitHandle[] events /* in */
            )
        {
            if (events == null)
                return false;

            int count = 0;

            foreach (EventWaitHandle @event in events)
            {
                if (@event == null)
                    continue;

                if (@event.Reset())
                    count++;
            }

            return (count == events.Length);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the specified events and then waits until the first event is
        /// signaled.
        /// </summary>
        /// <param name="events">
        /// The events to reset and wait on.
        /// </param>
        /// <returns>
        /// Non-zero if the first event was signaled; otherwise, zero.
        /// </returns>
        private static bool WaitAnyEvent(
            EventWaitHandle[] events /* in */
            )
        {
            if (!ResetEvents(events))
                return false;

            try
            {
                if (EventWaitHandle.WaitAny(events) == 0)
                    return true;
            }
            catch (AbandonedMutexException)
            {
                // do nothing.
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Event Handlers
        /// <summary>
        /// Handles the window initialized event by invoking the configured
        /// opened handler.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        private void Window_Initialized(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            DebugTraceMe("Window_Initialized: entered");

            EventHandler openedHandler = this.OpenedHandler;

            if (openedHandler != null)
                openedHandler(sender, e);

            DebugTraceMe("Window_Initialized: exited");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window loaded event by refreshing the window's size and
        /// position.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        private void Window_Loaded(
            object sender,    /* in */
            RoutedEventArgs e /* in */
            )
        {
            DebugTraceMe("Window_Loaded: entered");

            Refresh();

            DebugTraceMe("Window_Loaded: exited");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window closing event by marking the window as closing
        /// and unregistering it from the window manager.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The cancellation event arguments.
        /// </param>
        private void Window_Closing(
            object sender,    /* in */
            CancelEventArgs e /* in */
            )
        {
            DebugTraceMe("Window_Closing: entered");

            lock (syncRoot)
            {
                isClosing = true;
            }

            IHostWindowManager windowManager = this.WindowManager;

            if ((windowManager != null) && !windowManager.IsDisposing())
            {
                string name = null;

                if (GetTitle(ref name))
                    windowManager.UnregisterWindow(name, WindowType, false);
            }

            DebugTraceMe("Window_Closing: exited");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the window closed event by invoking the configured closed
        /// handler.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        private void Window_Closed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            DebugTraceMe("Window_Closed: entered");

            EventHandler closedHandler = this.ClosedHandler;

            if (closedHandler != null)
                closedHandler(sender, e);

            DebugTraceMe("Window_Closed: exited");
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowIdentifier Members
        /// <summary>
        /// Gets or sets the unique identifier assigned to this window.
        /// </summary>
        public virtual long WindowId
        {
            get { lock (syncRoot) { return windowId; } }
            set { lock (syncRoot) { windowId = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the name of this window.
        /// </summary>
        public virtual string WindowName
        {
            get { lock (syncRoot) { return null; } }
            set { lock (syncRoot) { throw new NotSupportedException(); } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of this window.
        /// </summary>
        public virtual WindowType WindowType
        {
            get { lock (syncRoot) { return windowType; } }
            set { lock (syncRoot) { windowType = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the window type used for input associated with this
        /// window.
        /// </summary>
        public virtual WindowType InputWindowType
        {
            get { lock (syncRoot) { return inputWindowType; } }
            set { lock (syncRoot) { inputWindowType = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the window type used for output associated with this
        /// window.
        /// </summary>
        public virtual WindowType OutputWindowType
        {
            get { lock (syncRoot) { return outputWindowType; } }
            set { lock (syncRoot) { outputWindowType = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostEventManager Members
        /// <summary>
        /// Gets or sets the event handler invoked when this window is opened.
        /// </summary>
        public virtual EventHandler OpenedHandler
        {
            get { lock (syncRoot) { return openedHandler; } }
            set { lock (syncRoot) { openedHandler = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the event handler invoked when this window is closed.
        /// </summary>
        public virtual EventHandler ClosedHandler
        {
            get { lock (syncRoot) { return closedHandler; } }
            set { lock (syncRoot) { closedHandler = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindow Members
        /// <summary>
        /// Gets or sets the window manager responsible for this window.
        /// </summary>
        public virtual IHostWindowManager WindowManager
        {
            get { lock (syncRoot) { return windowManager; } }
            set { lock (syncRoot) { windowManager = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the window factory that created this window.
        /// </summary>
        public virtual IHostWindowFactory WindowFactory
        {
            get { lock (syncRoot) { return windowFactory; } }
            set { lock (syncRoot) { windowFactory = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the registrar used to query and set the exit code for
        /// this window.
        /// </summary>
        public virtual IHostWindowRegistrar WindowRegistrar
        {
            get { lock (syncRoot) { return windowRegistrar; } }
            set { lock (syncRoot) { windowRegistrar = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the position and sizing information associated with
        /// this window.
        /// </summary>
        public virtual WindowPositionInfo WindowPositionInfo
        {
            get { lock (syncRoot) { return windowPositionInfo; } }
            set { lock (syncRoot) { windowPositionInfo = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether a minimum size is enforced
        /// for this window.
        /// </summary>
        public virtual bool MinimumSize
        {
            get { lock (syncRoot) { return minimumSize; } }
            set { lock (syncRoot) { minimumSize = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether this window sizes itself
        /// automatically to its content.
        /// </summary>
        public virtual bool AutoSize
        {
            get { lock (syncRoot) { return autoSize; } }
            set { lock (syncRoot) { autoSize = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether this window is closed
        /// automatically.
        /// </summary>
        public virtual bool AutoClose
        {
            get { lock (syncRoot) { return autoClose; } }
            set { lock (syncRoot) { autoClose = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether this window is in the
        /// process of closing.
        /// </summary>
        public virtual bool IsClosing
        {
            get { lock (syncRoot) { return isClosing; } }
            set { lock (syncRoot) { isClosing = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes this window by marshaling the close operation onto the
        /// dispatcher thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual new bool Close()
        {
            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                base.Close();
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes this window asynchronously by marshaling the close operation
        /// onto the dispatcher thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool CloseAsync()
        {
            BeginInvoke(this, new DelegateWithNoArgs(delegate()
            {
                base.Close();
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Activates this window by marshaling the activate operation onto the
        /// dispatcher thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual new bool Activate()
        {
            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                base.Activate();
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Refreshes the window by applying its actual size and repositioning
        /// it.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Refresh()
        {
            WindowPositionInfo windowPositionInfo = this.WindowPositionInfo;

            SetupActualSize(windowPositionInfo);
            PositionWindow(windowPositionInfo);

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Positions this window according to the specified position
        /// information by marshaling onto the dispatcher thread.
        /// </summary>
        /// <param name="windowPositionInfo">
        /// The position information describing where to place this window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Position(
            WindowPositionInfo windowPositionInfo /* in */
            )
        {
            if (windowPositionInfo == null)
                return false;

            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                this.Left = windowPositionInfo.Rectangle.Left;
                this.Top = windowPositionInfo.Rectangle.Top;
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current size of this window by marshaling onto the
        /// dispatcher thread.
        /// </summary>
        /// <param name="hostSizeType">
        /// The type of size being requested.
        /// </param>
        /// <param name="width">
        /// Upon success, receives the width of this window.
        /// </param>
        /// <param name="height">
        /// Upon success, receives the height of this window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool GetSize(
            HostSizeType hostSizeType, /* in */
            ref double width,          /* out */
            ref double height          /* out */
            )
        {
            double localWidth = 0.0;
            double localHeight = 0.0;

            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                localWidth = this.Width;
                localHeight = this.Height;
            }));

            width = localWidth;
            height = localHeight;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the size of this window by marshaling onto the dispatcher
        /// thread.
        /// </summary>
        /// <param name="hostSizeType">
        /// The type of size being set.
        /// </param>
        /// <param name="width">
        /// The width to apply to this window.
        /// </param>
        /// <param name="height">
        /// The height to apply to this window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SetSize(
            HostSizeType hostSizeType, /* in */
            double width,              /* in */
            double height              /* in */
            )
        {
            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                this.Width = width;
                this.Height = height;
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the title of this window by marshaling onto the dispatcher
        /// thread.
        /// </summary>
        /// <param name="value">
        /// Upon success, receives the title of this window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool GetTitle(ref string value)
        {
            string localValue = null;

            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                localValue = this.Title;
            }));

            value = localValue;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the title of this window by marshaling onto the dispatcher
        /// thread.
        /// </summary>
        /// <param name="value">
        /// The title to apply to this window.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SetTitle(string value)
        {
            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                this.Title = value;
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the exit code from the window registrar, avoiding a deadlock
        /// when the registrar is locked.
        /// </summary>
        /// <param name="exitCode">
        /// Upon success, receives the exit code from the registrar.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool GetExitCode(
            ref ExitCode exitCode /* out */
            )
        {
            IHostWindowRegistrar windowRegistrar = this.WindowRegistrar;

            //
            // BUGFIX: To prevent a deadlock, avoid getting the exit
            //         code when the registrar is locked by another
            //         thread.
            //
            if ((windowRegistrar == null) || windowRegistrar.IsLocked)
                return false;

            exitCode = windowRegistrar.ExitCode;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the exit code on the window registrar, avoiding a deadlock
        /// when the registrar is locked.
        /// </summary>
        /// <param name="exitCode">
        /// The exit code to set on the registrar.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SetExitCode(
            ExitCode exitCode /* in */
            )
        {
            IHostWindowRegistrar windowRegistrar = this.WindowRegistrar;

            //
            // BUGFIX: To prevent a deadlock, avoid setting the exit
            //         code when the registrar is locked by another
            //         thread.
            //
            if ((windowRegistrar == null) || windowRegistrar.IsLocked)
                return false;

            windowRegistrar.ExitCode = exitCode;
            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostStreamManager Members
        /// <summary>
        /// Gets or sets the text control used as the input box for this
        /// window.
        /// </summary>
        public virtual object InputBox
        {
            get { lock (syncRoot) { return inputBox; } }
            set { lock (syncRoot) { inputBox = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the text control used as the output box for this
        /// window.
        /// </summary>
        public virtual object OutputBox
        {
            get { lock (syncRoot) { return outputBox; } }
            set { lock (syncRoot) { outputBox = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the specified delegate on the dispatcher
        /// associated with the given object.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used to invoke the delegate.
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
        public virtual bool Invoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            params object[] args     /* in */
            )
        {
            return CommonOps.Invoke(dispatcherObject, method, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the specified delegate on the dispatcher
        /// associated with the given object and captures its result.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used to invoke the delegate.
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
        public virtual bool Invoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            ref object result,       /* out */
            params object[] args     /* in */
            )
        {
            return CommonOps.Invoke(
                dispatcherObject, method, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the specified delegate on the dispatcher
        /// associated with the given object.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used to invoke the delegate.
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
        public virtual bool BeginInvoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            params object[] args     /* in */
            )
        {
            return CommonOps.BeginInvoke(dispatcherObject, method, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the specified delegate on the dispatcher
        /// associated with the given object and captures the resulting
        /// operation.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used to invoke the delegate.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the dispatcher operation representing the
        /// pending invocation.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool BeginInvoke(
            object dispatcherObject,        /* in */
            Delegate method,                /* in */
            ref DispatcherOperation result, /* out */
            params object[] args            /* in */
            )
        {
            return CommonOps.BeginInvoke(
                dispatcherObject, method, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals that a key is available to be read.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SignalReadKey()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyEvent != null)
                {
                    keyEvent.Set();
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals that a line of input is available to be read.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SignalReadLine()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (lineEvent != null)
                {
                    lineEvent.Set();
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals that a pending read operation has been canceled.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SignalCanceled()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (cancelEvent != null)
                {
                    cancelEvent.Set();
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits until a key is available to be read or the read is canceled.
        /// </summary>
        /// <returns>
        /// Non-zero if a key became available; otherwise, zero.
        /// </returns>
        public virtual bool WaitReadKey()
        {
            EventWaitHandle[] events = GetReadKeyEvents();

            if (events == null)
                return false;

            return WaitAnyEvent(events);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Waits until a line of input is available to be read or the read is
        /// canceled.
        /// </summary>
        /// <returns>
        /// Non-zero if a line became available; otherwise, zero.
        /// </returns>
        public virtual bool WaitReadLine()
        {
            EventWaitHandle[] events = GetReadLineEvents();

            if (events == null)
                return false;

            return WaitAnyEvent(events);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Activates this window, waits for a key to become available, and
        /// returns it.
        /// </summary>
        /// <returns>
        /// The captured key event arguments.
        /// </returns>
        public virtual EventArgs ReadKey()
        {
            //
            // NOTE: Make sure that this window has the focus.
            //
            Activate();

            //
            // NOTE: Now, reset the event and then wait on it forever
            //       while not holding the lock.
            //
            WaitReadKey();

            //
            // NOTE: Now, grab the key while holding the lock and then
            //       return it after leaving the lock.
            //
            return GetKey();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Activates this window, waits for a line of input, echoes it to the
        /// output, and returns it.
        /// </summary>
        /// <returns>
        /// The line of input read from the window.
        /// </returns>
        public virtual string ReadLine()
        {
            //
            // NOTE: Make sure that this window has the focus.
            //
            Activate();

            //
            // NOTE: Now, reset the event and then wait on it forever
            //       while not holding the lock.
            //
            WaitReadLine();

            //
            // NOTE: Now, read the contents of the text box using the
            //       dispatcher.
            //
            string text = GetInput();

            //
            //
            // HACK: Since the host is forcing prompt to be displayed,
            //       we want to echo the input as well.
            //
            AddOutputLine(text);

            //
            // NOTE: Finally, return the input text to the caller.
            //
            return text;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the most recently captured key event arguments.
        /// </summary>
        /// <returns>
        /// The captured key event arguments.
        /// </returns>
        public virtual EventArgs GetKey()
        {
            EventArgs localKey;

            lock (syncRoot)
            {
                localKey = key;
            }

            return localKey;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the most recently captured key event arguments.
        /// </summary>
        /// <param name="value">
        /// The key event arguments to store.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SetKey(
            EventArgs value /* in */
            )
        {
            lock (syncRoot)
            {
                key = value;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current text of the input box by marshaling onto the
        /// dispatcher thread.
        /// </summary>
        /// <returns>
        /// The current text of the input box.
        /// </returns>
        public virtual string GetInput()
        {
            object inputBox = this.InputBox;
            string localText = null;

            Invoke(inputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox inputTextBox = inputBox as TextBox;

                if (inputTextBox != null)
                    localText = inputTextBox.Text;
            }));

            return localText;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified value to the input box by marshaling onto the
        /// dispatcher thread.
        /// </summary>
        /// <param name="value">
        /// The value to append to the input box.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool AddInput(
            string value /* in */
            )
        {
            object inputBox = this.InputBox;

            Invoke(inputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox inputTextBox = inputBox as TextBox;

                if (inputTextBox != null)
                    inputTextBox.Text += value;
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Inserts the specified value into the input box at the current word,
        /// replacing the surrounding word token, by marshaling onto the
        /// dispatcher thread.
        /// </summary>
        /// <param name="value">
        /// The value to insert into the input box.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool InsertInput(
            string value /* in */
            )
        {
            object inputBox = this.InputBox;

            Invoke(inputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox inputTextBox = inputBox as TextBox;

                if (inputTextBox != null)
                {
                    string text = inputTextBox.Text;
                    int index = inputTextBox.CaretIndex - 1;

                    if (index < 0)
                        index = 0;

                    int textLength = text.Length;

                    for (; index >= 0 && index < textLength; index--)
                        if (Char.IsWhiteSpace(text[index]))
                            break;

                    if (index >= 0)
                        index++;
                    else
                        index = 0;

                    int length = 0;
                    bool found = false;

                    for (; index + length < textLength; length++)
                    {
                        if (Char.IsWhiteSpace(text[index + length]))
                        {
                            found = true;
                            break;
                        }
                    }

                    char space = Characters.Space;

                    if (found || (length > 0))
                    {
                        inputTextBox.Select(index, length);
                        inputTextBox.SelectedText = value + space;
                        inputTextBox.SelectionLength = 0;
                    }
                    else
                    {
                        inputTextBox.Text += value + space;
                    }

                    inputTextBox.CaretIndex = index + value.Length + 1;
                }
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Replaces the text of the input box with the specified value by
        /// marshaling onto the dispatcher thread.
        /// </summary>
        /// <param name="value">
        /// The value to set as the input box text.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool SetInput(
            string value /* in */
            )
        {
            object inputBox = this.InputBox;

            Invoke(inputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox inputTextBox = inputBox as TextBox;

                if (inputTextBox != null)
                    inputTextBox.Text = value;
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified value as a trimmed line to the output box by
        /// marshaling onto the dispatcher thread.
        /// </summary>
        /// <param name="value">
        /// The value to append as an output line, or null to append a blank
        /// line.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool AddOutputLine(
            string value /* in */
            )
        {
            object outputBox = this.OutputBox;

            Invoke(outputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox outputTextBox = outputBox as TextBox;

                if (outputTextBox != null)
                {
                    if (value != null)
                        outputTextBox.Text += value.Trim() + NewLine;
                    else
                        outputTextBox.Text += NewLine;
                }
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether output written to this
        /// window is flushed automatically.
        /// </summary>
        public virtual bool AutoFlush
        {
            get { lock (syncRoot) { return autoFlush; } }
            set { lock (syncRoot) { autoFlush = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the output box and, when auto-sizing, forces the window to
        /// shrink so it re-expands to its content.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Clear()
        {
            object outputBox = this.OutputBox;

            Invoke(outputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox outputTextBox = outputBox as TextBox;

                if (outputTextBox != null)
                    outputTextBox.Text = String.Empty;
            }));

            bool autoSize = this.AutoSize;

            Invoke(this, new DelegateWithNoArgs(delegate()
            {
                if (autoSize)
                {
                    //
                    // HACK: Force window to shrink and then re-expand
                    //       when the output text content is filled in.
                    //
                    this.Width = 1;
                    this.Height = 1;
                }
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool Write(
            string value /* in */
            )
        {
            return Write(value, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box, enforcing the given
        /// maximum length.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <param name="length">
        /// The maximum allowed output length, or zero for no limit.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Write(
            string value, /* in */
            int length    /* in */
            )
        {
            return Write(value, length, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box by marshaling onto the
        /// dispatcher thread, enforcing the given maximum length and
        /// optionally flushing.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <param name="length">
        /// The maximum allowed output length, or zero for no limit.
        /// </param>
        /// <param name="flush">
        /// Non-zero to scroll the output to the end after writing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Write(
            string value, /* in */
            int length,   /* in */
            bool flush    /* in */
            )
        {
            object outputBox = this.OutputBox;
            bool autoFlush = this.AutoFlush;

            Invoke(outputBox, new DelegateWithNoArgs(delegate()
            {
                WriteCore(
                    outputBox as TextBox, value, length,
                    flush || autoFlush);
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box asynchronously.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool WriteAsync(
            string value /* in */
            )
        {
            return Write(value, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box asynchronously,
        /// enforcing the given maximum length.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <param name="length">
        /// The maximum allowed output length, or zero for no limit.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool WriteAsync(
            string value, /* in */
            int length    /* in */
            )
        {
            return Write(value, length, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the specified value to the output box asynchronously by
        /// marshaling onto the dispatcher thread, enforcing the given maximum
        /// length and optionally flushing.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <param name="length">
        /// The maximum allowed output length, or zero for no limit.
        /// </param>
        /// <param name="flush">
        /// Non-zero to scroll the output to the end after writing.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool WriteAsync(
            string value, /* in */
            int length,   /* in */
            bool flush    /* in */
            )
        {
            object outputBox = this.OutputBox;
            bool autoFlush = this.AutoFlush;

            BeginInvoke(outputBox, new DelegateWithNoArgs(delegate()
            {
                WriteCore(
                    outputBox as TextBox, value, length,
                    flush || autoFlush);
            }));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Scrolls the output box to the end by marshaling onto the dispatcher
        /// thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public virtual bool Flush()
        {
            object outputBox = this.OutputBox;

            Invoke(outputBox, new DelegateWithNoArgs(delegate()
            {
                TextBox outputTextBox = outputBox as TextBox;

                if (outputTextBox != null)
                {
                    outputTextBox.Select(outputTextBox.Text.Length, 0);
                    outputTextBox.ScrollToEnd();
                }
            }));

            return true;
        }
        #endregion
    }
}
