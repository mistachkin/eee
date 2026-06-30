/*
 * WindowPositionInfo.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Windows;
using Eagle._Attributes;
using Eagle._Constants;
using Featherlight.Components.Private;

namespace Featherlight.Components.Public
{
    /// <summary>
    /// Describes the position and bounds of a host window.
    /// </summary>
    [ObjectId("6641fd1f-2720-441f-950b-5a5f35a79ae5")]
    public sealed class WindowPositionInfo
    {
        #region Private Data
        /// <summary>
        /// The position of the window.
        /// </summary>
        public WindowPosition WindowPosition;
        /// <summary>
        /// The bounding rectangle of the window.
        /// </summary>
        public Rect Rectangle;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Constructs a new instance from a position and the top-left corner.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position.
        /// </param>
        /// <param name="left">
        /// The left coordinate.
        /// </param>
        /// <param name="top">
        /// The top coordinate.
        /// </param>
        private WindowPositionInfo(
            WindowPosition windowPosition, /* in */
            double left,                   /* in */
            double top                     /* in */
            )
        {
            this.WindowPosition = windowPosition;
            this.Rectangle = new Rect(left, top, 0, 0);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance from a position and a bounding rectangle.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position.
        /// </param>
        /// <param name="rectangle">
        /// The bounding rectangle.
        /// </param>
        private WindowPositionInfo(
            WindowPosition windowPosition, /* in */
            Rect rectangle                 /* in */
            )
        {
            this.WindowPosition = windowPosition;
            this.Rectangle = rectangle;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance from a position and the current bounds of
        /// a window.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position.
        /// </param>
        /// <param name="window">
        /// The window whose bounds are captured.
        /// </param>
        private WindowPositionInfo(
            WindowPosition windowPosition, /* in */
            Window window                  /* in */
            )
        {
            this.WindowPosition = windowPosition;

            CommonOps.Invoke(window, new DelegateWithNoArgs(delegate()
            {
                this.Rectangle = new Rect(window.Left, window.Top,
                    window.ActualWidth, window.ActualHeight);
            }));
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Updates the bounds with the actual size of the specified window.
        /// </summary>
        /// <param name="window">
        /// The window whose actual size is captured.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool ActualSize(Window window)
        {
            bool result = false;

            CommonOps.Invoke(window, new DelegateWithNoArgs(delegate()
            {
                this.Rectangle = new Rect(
                    this.Rectangle.Left, this.Rectangle.Top,
                    window.ActualWidth, window.ActualHeight);

                result = true;
            }));

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
        /// <summary>
        /// Creates position information representing no position.
        /// </summary>
        /// <returns>
        /// The new position information.
        /// </returns>
        public static WindowPositionInfo None()
        {
            return new WindowPositionInfo(WindowPosition.None,
                new Rect(_Position.Invalid, _Position.Invalid, 0, 0));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates position information representing automatic placement.
        /// </summary>
        /// <returns>
        /// The new position information.
        /// </returns>
        public static WindowPositionInfo Automatic()
        {
            return new WindowPositionInfo(WindowPosition.Automatic,
                new Rect(_Position.Invalid, _Position.Invalid, 0, 0));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates position information from a position and the current bounds
        /// of a window.
        /// </summary>
        /// <param name="windowPosition">
        /// The window position.
        /// </param>
        /// <param name="window">
        /// The window whose bounds are captured.
        /// </param>
        /// <returns>
        /// The new position information.
        /// </returns>
        public static WindowPositionInfo FromWindow(
            WindowPosition windowPosition, /* in */
            Window window                  /* in */
            )
        {
            return new WindowPositionInfo(windowPosition, window);
        }
        #endregion
    }
}
