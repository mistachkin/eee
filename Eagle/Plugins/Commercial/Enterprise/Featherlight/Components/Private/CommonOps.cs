/*
 * CommonOps.cs --
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
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Represents a method that accepts no arguments and returns no value,
    /// used for dispatcher invocations.
    /// </summary>
    [ObjectId("bffdb469-564f-4d11-ac3f-8d70c8a6a1b4")]
    internal delegate void DelegateWithNoArgs();

    /// <summary>
    /// Provides static helper utilities shared across the Featherlight plugin,
    /// including window creation and closing, text measurement, argument
    /// handling, and dispatcher invocation wrappers.
    /// </summary>
    [ObjectId("d5d8f8d1-b54a-438c-b85c-d0e0603c4a22")]
    internal static class CommonOps
    {
        #region Private Constants
        /// <summary>
        /// The window name used for the interactive window, which is the empty
        /// string.
        /// </summary>
        private static readonly string InteractiveWindowName = String.Empty;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window name used for the standard input window.
        /// </summary>
        private static readonly string InputWindowName = "Standard Input";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window name used for the standard output window.
        /// </summary>
        private static readonly string OutputWindowName = "Standard Output";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window name used for the standard error window.
        /// </summary>
        private static readonly string ErrorWindowName = "Standard Error";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window name used for the standard trace window.
        /// </summary>
        private static readonly string TraceWindowName = "Standard Trace";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The composite format string used to combine a window name with its
        /// numeric identifier.
        /// </summary>
        private static readonly string NameAndIdFormat = "{0}: #{1}";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The horizontal margin, in device-independent units, used when
        /// calculating window positions.
        /// </summary>
        private const int HorizontalMargin = 10;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The vertical margin, in device-independent units, used when
        /// calculating window positions.
        /// </summary>
        private const int VerticalMargin = 10;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the interpreter variable that holds the shell
        /// command-line arguments.
        /// </summary>
        private static readonly string ShellArgumentsVarName = "::argv";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default pre-initialization script text used when creating an
        /// interpreter, which is null.
        /// </summary>
        private static readonly string PreInitializeText = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constants
        #region Text Measurement Constants
        /// <summary>
        /// The divisor applied to the measured text width when computing
        /// window sizing.
        /// </summary>
        public static readonly int MeasureTextWidthDivisor = 3;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The divisor applied to the measured text height when computing
        /// window sizing.
        /// </summary>
        public static readonly int MeasureTextHeightDivisor = 3;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The sample string used to measure a representative text width.
        /// </summary>
        public static readonly string MeasureTextWidthString = "___";
        /// <summary>
        /// The sample string used to measure a representative text height.
        /// </summary>
        public static readonly string MeasureTextHeightString = "|\n|\n|";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the environment variable that, when set, suppresses
        /// creation of an interpreter.
        /// </summary>
        public static readonly string NoCreateInterpreterEnvVarName =
            "NoCreateInterpreter";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The window name used for box (message) windows.
        /// </summary>
        public static readonly string BoxWindowName = "box";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default window name, which is the interactive window name.
        /// </summary>
        public static readonly string DefaultWindowName = InteractiveWindowName;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Error Reporting Methods
        /// <summary>
        /// Writes the specified value to the error stream of the given stream
        /// host.
        /// </summary>
        /// <param name="streamHost">
        /// The stream host whose error stream is written to.
        /// </param>
        /// <param name="value">
        /// The text to write.
        /// </param>
        /// <param name="newLine">
        /// Non-zero to append a line terminator after the value.
        /// </param>
        public static void WriteError(
            IStreamHost streamHost, /* in */
            string value,           /* in */
            bool newLine            /* in */
            )
        {
            if ((streamHost == null) || (value == null))
                return;

            Stream stream = streamHost.Error;

            if (stream == null)
                return;

            //
            // WARNING: Do not close or dispose this because we do
            //          not want the underlying stream to be closed.
            //
            StreamWriter streamWriter = new StreamWriter(stream);

            if (newLine)
                streamWriter.WriteLine(value);
            else
                streamWriter.Write(value);

            streamWriter.Flush();
            streamWriter = null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Helper Methods
        /// <summary>
        /// Gets the list of define constants used by the plugin.
        /// </summary>
        /// <param name="result">
        /// Upon success, receives the list of define constants; upon failure,
        /// receives error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode GetDefineConstants(
            ref Result result /* out */
            )
        {
            StringList list = DefineConstants.OptionList;

            if (list != null)
            {
                result = new StringList(list, false);
                return ReturnCode.Ok;
            }
            else
            {
                result = "define constants not available";
                return ReturnCode.Error;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindow Helper Methods
        /// <summary>
        /// Determines whether the specified window type includes all of the
        /// given window type flags.
        /// </summary>
        /// <param name="windowType">
        /// The window type flags to test.
        /// </param>
        /// <param name="hasWindowType">
        /// The window type flags that must all be present.
        /// </param>
        /// <returns>
        /// true if all of the specified flags are present; otherwise, false.
        /// </returns>
        public static bool HasWindowType(
            WindowType windowType,   /* in */
            WindowType hasWindowType /* in */
            )
        {
            return ((windowType & hasWindowType) == hasWindowType);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a window name by combining the given name with its numeric
        /// identifier.
        /// </summary>
        /// <param name="name">
        /// The base window name.
        /// </param>
        /// <param name="id">
        /// The numeric window identifier.
        /// </param>
        /// <returns>
        /// The formatted window name.
        /// </returns>
        public static string FormatWindowName(
            string name, /* in */
            long id      /* in */
            )
        {
            return String.Format(NameAndIdFormat, name, id);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the display name for a window, using the bare name for
        /// interactive windows and the formatted name otherwise.
        /// </summary>
        /// <param name="name">
        /// The base window name.
        /// </param>
        /// <param name="id">
        /// The numeric window identifier.
        /// </param>
        /// <returns>
        /// The window display name.
        /// </returns>
        public static string GetWindowName(
            string name, /* in */
            long id      /* in */
            )
        {
            return IsInteractiveWindowName(name) ? name :
                FormatWindowName(name, id);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window name corresponding to the specified window type,
        /// using the default window name as a fallback.
        /// </summary>
        /// <param name="windowType">
        /// The window type to convert to a name.
        /// </param>
        /// <returns>
        /// The window name for the specified window type.
        /// </returns>
        public static string WindowTypeToName(
            WindowType windowType /* in */
            )
        {
            return WindowTypeToName(windowType, DefaultWindowName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window name corresponding to the specified window type,
        /// using the given fallback name when the type is unrecognized.
        /// </summary>
        /// <param name="windowType">
        /// The window type to convert to a name.
        /// </param>
        /// <param name="defaultName">
        /// The name to return when the window type is not recognized.
        /// </param>
        /// <returns>
        /// The window name for the specified window type.
        /// </returns>
        public static string WindowTypeToName(
            WindowType windowType, /* in */
            string defaultName     /* in */
            )
        {
            switch (windowType & WindowType.Mask)
            {
                case WindowType.Input:
                    return InputWindowName;
                case WindowType.Output:
                    return OutputWindowName;
                case WindowType.Error:
                    return ErrorWindowName;
                case WindowType.Trace:
                    return TraceWindowName;
                case WindowType.Interactive:
                    return InteractiveWindowName;
                default:
                    return defaultName;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified name is the interactive window
        /// name.
        /// </summary>
        /// <param name="name">
        /// The window name to test.
        /// </param>
        /// <returns>
        /// true if the name is the interactive window name; otherwise, false.
        /// </returns>
        public static bool IsInteractiveWindowName(
            string name /* in */
            )
        {
            return Utility.SystemStringEquals(name, InteractiveWindowName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the formatted display name for the specified host window based
        /// on its window type and identifier.
        /// </summary>
        /// <param name="window">
        /// The host window whose display name is computed.
        /// </param>
        /// <returns>
        /// The formatted window display name.
        /// </returns>
        public static string GetWindowName(
            IHostWindow window /* in */
            )
        {
            return FormatWindowName(WindowTypeToName(
                (window != null) ? window.WindowType : WindowType.None),
                (window != null) ? window.WindowId : 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bounding rectangle of the specified host window from its
        /// window position information.
        /// </summary>
        /// <param name="window">
        /// The host window whose rectangle is retrieved.
        /// </param>
        /// <returns>
        /// The window rectangle, or an empty rectangle when unavailable.
        /// </returns>
        public static Rect RectFromIHostWindow(
            IHostWindow window /* in */
            )
        {
            if (window != null)
            {
                WindowPositionInfo windowPositionInfo =
                    window.WindowPositionInfo;

                if (windowPositionInfo != null)
                    return windowPositionInfo.Rectangle;
            }

            return Rect.Empty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the specified host window, optionally shutting down its
        /// interactive loop and using asynchronous closing during shutdown.
        /// </summary>
        /// <param name="window">
        /// The host window to close.
        /// </param>
        /// <param name="noLoop">
        /// Non-zero to skip shutting down the interactive loop.
        /// </param>
        /// <param name="shutdown">
        /// Non-zero to close the window asynchronously as part of shutdown.
        /// </param>
        /// <returns>
        /// true if the window was closed; otherwise, false.
        /// </returns>
        public static bool CloseWindow(
            IHostWindow window, /* in */
            bool noLoop,        /* in */
            bool shutdown       /* in */
            )
        {
            if (window == null)
                return false;

            IHostInteractiveWindow interactiveWindow =
                window as IHostInteractiveWindow;

            if (interactiveWindow != null)
            {
                //
                // BUGBUG: This is probably not needed.  If the
                //         window is closed (below) while the
                //         interactive loop is still running, it
                //         should be fully shutdown within the
                //         Closing event and calling into the
                //         ShutdownInteractiveLoop method more
                //         than once should be harmless.  That
                //         being said, perhaps some callers want
                //         to avoid doing duplicate work?
                //
                if (!noLoop)
                {
                    /* IGNORED */
                    interactiveWindow.ShutdownInteractiveLoop();
                }

                if (shutdown)
                {
                    /* IGNORED */
                    interactiveWindow.CloseAsync();
                }
                else
                {
                    /* IGNORED */
                    interactiveWindow.Close();
                }

                return true;
            }
            else if (!shutdown)
            {
                window.Close();

                return true;
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region WindowPosition Helper Methods
        /// <summary>
        /// Determines whether the specified window position flags include the
        /// given flags, matching either all or any of them.
        /// </summary>
        /// <param name="flags">
        /// The window position flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The window position flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; otherwise, any of the flags
        /// suffice.
        /// </param>
        /// <returns>
        /// true if the requested flags are present according to the matching
        /// mode; otherwise, false.
        /// </returns>
        public static bool HasFlags(
            WindowPosition flags,    /* in */
            WindowPosition hasFlags, /* in */
            bool all                 /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != WindowPosition.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified window position requests automatic
        /// positioning.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position to test.
        /// </param>
        /// <returns>
        /// true if the position requests automatic positioning; otherwise,
        /// false.
        /// </returns>
        public static bool IsAutomaticPosition(
            WindowPosition windowPosition /* in */
            )
        {
            return HasFlags(
                windowPosition, WindowPosition.AutomaticMask, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Calculates the window rectangle for the specified relative position
        /// with respect to the parent rectangle.
        /// </summary>
        /// <param name="windowPosition">
        /// The relative position to compute.
        /// </param>
        /// <param name="parentRectangle">
        /// The parent rectangle used as the reference.
        /// </param>
        /// <param name="rectangle">
        /// On input and output, the rectangle to position; its location is
        /// updated in place.
        /// </param>
        public static void CalculatePosition(
            WindowPosition windowPosition, /* in */
            Rect parentRectangle,          /* in */
            ref Rect rectangle             /* in, out */
            )
        {
            double parentLeft = parentRectangle.Left;
            double parentTop = parentRectangle.Top;
            double parentWidth = parentRectangle.Width;
            double parentHeight = parentRectangle.Height;

            double left = rectangle.Left;
            double top = rectangle.Top;
            double width = rectangle.Width;
            double height = rectangle.Height;

            switch (windowPosition)
            {
                case WindowPosition.TopLeft:
                    {
                        left = parentLeft - width - HorizontalMargin;
                        top = parentTop;
                        break;
                    }
                case WindowPosition.TopCenter:
                    {
                        left = parentLeft + ((parentWidth - width) / 2);
                        top = parentTop - height - VerticalMargin;
                        break;
                    }
                case WindowPosition.TopRight:
                    {
                        left = parentLeft + parentWidth + HorizontalMargin;
                        top = parentTop;
                        break;
                    }
                case WindowPosition.MiddleLeft:
                    {
                        left = parentLeft - width - HorizontalMargin;
                        top = parentTop + ((parentHeight - height) / 2);
                        break;
                    }
                case WindowPosition.MiddleCenter: // NOTE: Not too great?
                    {
                        left = parentLeft + ((parentWidth - width) / 2);
                        top = parentTop + ((parentHeight - height) / 2);
                        break;
                    }
                case WindowPosition.MiddleRight:
                    {
                        left = parentLeft + parentWidth + HorizontalMargin;
                        top = parentTop + ((parentHeight - height) / 2);
                        break;
                    }
                case WindowPosition.BottomLeft:
                    {
                        left = parentLeft - width - HorizontalMargin;
                        top = parentTop + parentHeight - height;
                        break;
                    }
                case WindowPosition.BottomCenter:
                    {
                        left = parentLeft + ((parentWidth - width) / 2);
                        top = parentTop + parentHeight + VerticalMargin;
                        break;
                    }
                case WindowPosition.BottomRight:
                    {
                        left = parentLeft + parentWidth + HorizontalMargin;
                        top = parentTop + parentHeight - height;
                        break;
                    }
            }

            rectangle = new Rect(left, top, width, height);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Window Input Methods
        /// <summary>
        /// Extracts the key code from the specified event arguments, if they
        /// represent a key event.
        /// </summary>
        /// <param name="eventArgs">
        /// The event arguments to inspect.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the integer key code.
        /// </param>
        /// <returns>
        /// true if the event arguments were key event arguments; otherwise,
        /// false.
        /// </returns>
        public static bool ReadKey(
            EventArgs eventArgs, /* in */
            ref int value        /* out */
            )
        {
            KeyEventArgs keyEventArgs = eventArgs as KeyEventArgs;

            if (keyEventArgs != null)
            {
                value = (int)keyEventArgs.Key;
                return true;
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Window Threading Methods
        /// <summary>
        /// Shuts down the dispatcher associated with the current thread.
        /// </summary>
        /// <returns>
        /// true if the dispatcher shutdown was initiated; otherwise, false.
        /// </returns>
        public static bool Shutdown()
        {
            return Shutdown(Dispatcher.CurrentDispatcher);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shuts down the specified dispatcher.
        /// </summary>
        /// <param name="dispatcher">
        /// The dispatcher to shut down.
        /// </param>
        /// <returns>
        /// true if the dispatcher shutdown was initiated; otherwise, false.
        /// </returns>
        public static bool Shutdown(
            Dispatcher dispatcher /* in */
            )
        {
            if (dispatcher == null)
                return false;

            dispatcher.InvokeShutdown();
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the specified delegate on the dispatcher
        /// associated with the given object, discarding the result.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used for the invocation.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// true if the delegate was invoked; otherwise, false.
        /// </returns>
        public static bool Invoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            params object[] args     /* in */
            )
        {
            object result = null;

            return Invoke(dispatcherObject, method, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the specified delegate on the dispatcher
        /// associated with the given object and returns its result.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used for the invocation.
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
        /// true if the delegate was invoked; otherwise, false.
        /// </returns>
        public static bool Invoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            ref object result,       /* out */
            params object[] args     /* in */
            )
        {
            DispatcherObject localDispatcherObject =
                dispatcherObject as DispatcherObject;

            if (localDispatcherObject != null)
            {
                Dispatcher dispatcher = localDispatcherObject.Dispatcher;

                if (dispatcher != null)
                {
                    result = dispatcher.Invoke(method, args);

                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the specified delegate on the dispatcher
        /// associated with the given object, discarding the operation.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used for the invocation.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// true if the delegate invocation was queued; otherwise, false.
        /// </returns>
        public static bool BeginInvoke(
            object dispatcherObject, /* in */
            Delegate method,         /* in */
            params object[] args     /* in */
            )
        {
            DispatcherOperation result = null;

            return BeginInvoke(dispatcherObject, method, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the specified delegate on the dispatcher
        /// associated with the given object and returns the dispatcher
        /// operation.
        /// </summary>
        /// <param name="dispatcherObject">
        /// The object whose dispatcher is used for the invocation.
        /// </param>
        /// <param name="method">
        /// The delegate to invoke.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the dispatcher operation representing the
        /// queued invocation.
        /// </param>
        /// <param name="args">
        /// The arguments to pass to the delegate.
        /// </param>
        /// <returns>
        /// true if the delegate invocation was queued; otherwise, false.
        /// </returns>
        public static bool BeginInvoke(
            object dispatcherObject,        /* in */
            Delegate method,                /* in */
            ref DispatcherOperation result, /* out */
            params object[] args            /* in */
            )
        {
            DispatcherObject localDispatcherObject =
                dispatcherObject as DispatcherObject;

            if (localDispatcherObject != null)
            {
                Dispatcher dispatcher = localDispatcherObject.Dispatcher;

                if (dispatcher != null)
                {
                    result = dispatcher.BeginInvoke(method, args);

                    return true;
                }
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Text Helper Methods
        /// <summary>
        /// Measures the rendered width of the specified text using the font
        /// settings of the given text box.
        /// </summary>
        /// <param name="textBox">
        /// The text box whose font settings are used for measurement.
        /// </param>
        /// <param name="text">
        /// The text to measure.
        /// </param>
        /// <returns>
        /// The measured width of the text.
        /// </returns>
        public static double MeasureTextWidth(
            TextBoxBase textBox, /* in */
            string text          /* in */
            )
        {
            FormattedText formattedText = new FormattedText(
                text, CultureInfo.CurrentUICulture, textBox.FlowDirection,
                new Typeface(textBox.FontFamily, textBox.FontStyle,
                    textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize, textBox.Foreground);

            return formattedText.Width;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Measures the rendered height of the specified text using the font
        /// settings of the given text box.
        /// </summary>
        /// <param name="textBox">
        /// The text box whose font settings are used for measurement.
        /// </param>
        /// <param name="text">
        /// The text to measure.
        /// </param>
        /// <returns>
        /// The measured height of the text.
        /// </returns>
        public static double MeasureTextHeight(
            TextBoxBase textBox, /* in */
            string text          /* in */
            )
        {
            FormattedText formattedText = new FormattedText(
                text, CultureInfo.CurrentUICulture, textBox.FlowDirection,
                new Typeface(textBox.FontFamily, textBox.FontStyle,
                    textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize, textBox.Foreground);

            return formattedText.Height;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Eagle Helper Methods
        /// <summary>
        /// Creates host data describing the window host for the specified
        /// interpreter, deriving host creation flags from the interpreter's
        /// current host when available.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which host data is created.
        /// </param>
        /// <returns>
        /// The newly created host data.
        /// </returns>
        public static IHostData NewHostData(
            Interpreter interpreter /* in */
            )
        {
            HostCreateFlags hostCreateFlags = HostCreateFlags.Default;

            if (interpreter != null)
            {
                IHost host = interpreter.Host;

                if (host != null)
                {
                    hostCreateFlags = Utility.GetHostCreateFlags(
                        host.HostCreateFlags, host.UseAttach,
                        host.UseForce, host.NoColor, host.NoTitle,
                        host.NoIcon, host.NoProfile, host.NoCancel);
                }
            }

            return new HostData(
                null, null, null, ClientData.Empty,
                typeof(Hosts.Window).Name, interpreter,
                null, null, hostCreateFlags);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the command-line arguments for the interpreter, preferring the
        /// client data payload and falling back to the shell arguments
        /// variable.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose shell arguments variable is consulted.
        /// </param>
        /// <param name="clientData">
        /// The client data that may carry an argument collection.
        /// </param>
        /// <param name="strict">
        /// Non-zero to complain when retrieving or parsing the shell arguments
        /// fails.
        /// </param>
        /// <returns>
        /// The collection of arguments, or null when none are available.
        /// </returns>
        public static IEnumerable<string> GetArguments(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            bool strict              /* in */
            )
        {
            IEnumerable<string> result = null;

            if (clientData != null)
            {
                result = clientData.Data as IEnumerable<string>;

                if (result != null)
                    return new StringList(result).ToArray(); /* string[] */
            }

            if (interpreter != null)
            {
                ReturnCode code;
                Result value = null;
                Result error = null;

                code = interpreter.GetVariableValue(
                    VariableFlags.None, ShellArgumentsVarName,
                    ref value, ref error);

                if (code == ReturnCode.Ok)
                {
                    StringList list = null;

                    code = Parser.SplitList(
                        interpreter, value, 0, Length.Invalid,
                        false, ref list, ref error);

                    if (code == ReturnCode.Ok)
                        result = list.ToArray(); /* string[] */
                    else if (strict)
                        Utility.Complain(interpreter, code, error);
                }
                else if (strict)
                {
                    Utility.Complain(interpreter, code, error);
                }
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interpreter using the specified arguments, flags,
        /// initialization text, and library path.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="createFlags">
        /// The flags controlling interpreter creation.
        /// </param>
        /// <param name="hostCreateFlags">
        /// The flags controlling host creation.
        /// </param>
        /// <param name="text">
        /// The pre-initialization script text, or null to use the default.
        /// </param>
        /// <param name="libraryPath">
        /// The path to the script library.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The newly created interpreter, or null on failure.
        /// </returns>
        public static Interpreter CreateInterpreter(
            IEnumerable<string> args,        /* in */
            CreateFlags createFlags,         /* in */
            HostCreateFlags hostCreateFlags, /* in */
            string text,                     /* in */
            string libraryPath,              /* in */
            ref Result result                /* out */
            )
        {
            return Interpreter.Create(
                args, createFlags, hostCreateFlags, (text != null) ?
                text : PreInitializeText, libraryPath, null, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles an unknown command-line argument during interpreter setup
        /// by popping the first argument and reporting success.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter being configured.
        /// </param>
        /// <param name="interactiveHost">
        /// The interactive host associated with the interpreter.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the callback.
        /// </param>
        /// <param name="switchCount">
        /// The number of switches processed so far.
        /// </param>
        /// <param name="arg">
        /// The unknown argument encountered.
        /// </param>
        /// <param name="whatIf">
        /// Non-zero to indicate a what-if (non-mutating) pass.
        /// </param>
        /// <param name="argv">
        /// On input and output, the remaining argument list, with the first
        /// argument removed.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode PopUnknownArgumentCallback(
            Interpreter interpreter,          /* in */
            IInteractiveHost interactiveHost, /* in */
            IClientData clientData,           /* in */
            int switchCount,                  /* in */
            string arg,                       /* in */
            bool whatIf,                      /* in */
            ref IList<string> argv,           /* out */
            ref Result result                 /* out */
            )
        {
            /* IGNORED */
            Utility.PopFirstArgument(ref argv);

            /* SUCCESS */
            return ReturnCode.Ok;
        }
        #endregion
    }
}
