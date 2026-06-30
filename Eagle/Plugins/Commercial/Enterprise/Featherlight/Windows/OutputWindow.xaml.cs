/*
 * OutputWindow.xaml.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Windows.Input;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Windows
{
    /// <summary>
    /// The output window: a base window that exposes a single text box as the
    /// host's output stream.
    /// </summary>
    [ObjectId("e5056ac3-a9b2-4158-b16c-096655afe4ff")]
    public sealed partial class OutputWindow : BaseWindow, IHostOutputWindow
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="OutputWindow" /> class.
        /// </summary>
        /// <param name="windowManager">
        /// The window manager that owns the window.
        /// </param>
        /// <param name="windowFactory">
        /// The factory used to create windows.
        /// </param>
        /// <param name="windowRegistrar">
        /// The registrar that tracks the window.
        /// </param>
        /// <param name="windowId">
        /// The window id.
        /// </param>
        /// <param name="windowType">
        /// The type of this window.
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
        public OutputWindow(
            IHostWindowManager windowManager,      /* in */
            IHostWindowFactory windowFactory,      /* in */
            IHostWindowRegistrar windowRegistrar,  /* in */
            long windowId,                         /* in */
            WindowType windowType,                 /* in */
            EventHandler openedHandler,            /* in */
            EventHandler closedHandler,            /* in */
            WindowPositionInfo windowPositionInfo, /* in */
            bool minimumSize,                      /* in */
            bool autoSize,                         /* in */
            bool autoClose,                        /* in */
            bool autoFlush                         /* in */
            )
            : base(windowManager, windowFactory, windowRegistrar,
                   windowId, windowType, WindowType.None,
                   WindowType.Output, openedHandler, closedHandler,
                   null, null, windowPositionInfo, minimumSize,
                   autoSize, autoClose, autoFlush)
        {
            InitializeComponent();

            this.OutputBox = txtOutput;

            txtOutput.MouseDoubleClick +=
                new MouseButtonEventHandler(txtOutput_MouseDoubleClick);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Event Handlers
        /// <summary>
        /// Handles a double-click in the output by injecting the clicked line
        /// back as input.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The mouse event data.
        /// </param>
        private void txtOutput_MouseDoubleClick(
            object sender,         /* in */
            MouseButtonEventArgs e /* in */
            )
        {
            if (e == null)
                return;

            IHostWindowManager windowManager = this.WindowManager;

            if (windowManager != null)
            {
                Invoke(txtOutput, new DelegateWithNoArgs(delegate()
                {
                    int index = txtOutput.GetCharacterIndexFromPoint(
                        e.GetPosition(txtOutput), false);

                    if (index != _Position.Invalid)
                    {
                        string value = txtOutput.GetLineText(
                            txtOutput.GetLineIndexFromCharacterIndex(index));

                        if (value != null)
                            windowManager.InjectInput(value);
                    }
                }));
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowIdentifier Members
        /// <summary>
        /// Gets the window type used for input (always none).  Setting this
        /// value is not supported.
        /// </summary>
        public override WindowType InputWindowType
        {
            get { return WindowType.None; }
            set { throw new NotSupportedException(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window type used for output.  Setting this value is not
        /// supported.
        /// </summary>
        public override WindowType OutputWindowType
        {
            get { return base.OutputWindowType; }
            set { throw new NotSupportedException(); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindow Members
        /// <summary>
        /// Gets the window or viewport size of the specified kind.
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
        /// Sets the window or viewport size of the specified kind.
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

        #region IHostOutputWindow Members
        /// <summary>
        /// Gets the size of a single character in the output, assuming a
        /// fixed-width font.
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
    }
}
