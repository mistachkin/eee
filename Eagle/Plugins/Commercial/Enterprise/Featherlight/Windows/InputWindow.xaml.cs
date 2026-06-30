/*
 * InputWindow.xaml.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Featherlight.Components.Private;
using Featherlight.Components.Public;
using Featherlight.Interfaces.Public;

namespace Featherlight.Windows
{
    /// <summary>
    /// The input window: a base window that exposes a single text box as the
    /// host's input stream.
    /// </summary>
    [ObjectId("e0ca836c-40d8-4a0f-a712-54c93c2aa343")]
    public sealed partial class InputWindow : BaseWindow, IHostInputWindow
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="InputWindow" /> class.
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
        public InputWindow(
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
                   windowId, windowType, WindowType.Input,
                   WindowType.None, openedHandler, closedHandler,
                   null, null, windowPositionInfo, minimumSize,
                   autoSize, autoClose, autoFlush)
        {
            InitializeComponent();

            this.InputBox = txtInput;

            this.Activated += new EventHandler(Window_Activated);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Event Handlers
        /// <summary>
        /// Handles window activation by moving focus to the input text box.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Window_Activated(object sender, EventArgs e)
        {
            Invoke(txtInput, new DelegateWithNoArgs(delegate()
            {
                if (txtInput.Focusable)
                    txtInput.Focus();
            }));
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHostWindowIdentifier Members
        /// <summary>
        /// Gets the window type used for input.  Setting this value is not
        /// supported.
        /// </summary>
        public override WindowType InputWindowType
        {
            get { return base.InputWindowType; }
            set { throw new NotSupportedException(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window type used for output (always none).  Setting this
        /// value is not supported.
        /// </summary>
        public override WindowType OutputWindowType
        {
            get { return WindowType.None; }
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

                Invoke(txtInput, new DelegateWithNoArgs(delegate()
                {
                    localWidth = txtInput.ViewportWidth;
                    localHeight = txtInput.ViewportHeight;
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
                Invoke(txtInput, new DelegateWithNoArgs(delegate()
                {
                    txtInput.Width = width;
                    txtInput.Height = height;
                }));

                return true;
            }

            return false;
        }
        #endregion
    }
}
