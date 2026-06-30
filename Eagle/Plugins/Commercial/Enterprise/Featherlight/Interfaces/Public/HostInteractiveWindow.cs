/*
 * HostInteractiveWindow.cs --
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

namespace Featherlight.Interfaces.Public
{
    /// <summary>
    /// Represents the top-level interactive window: a stream window that also
    /// owns an interpreter and its interactive loop and can manufacture
    /// further hosts.  This is the window the shell opens first; it joins the
    /// full stream capability with the window factory and adds the operations
    /// for starting, matching, resetting, and shutting down the interactive
    /// interpreter that runs inside it.
    /// </summary>
    [ObjectId("045ccb9c-de29-422d-97ee-2517626d35c6")]
    public interface IHostInteractiveWindow :
            IHostStreamWindow, IHostWindowFactory
    {
        /// <summary>
        /// Determines whether an interactive interpreter is present.
        /// </summary>
        /// <returns>
        /// Non-zero if present; otherwise, zero.
        /// </returns>
        bool HaveInteractiveInterpreter();
        /// <summary>
        /// Determines whether the specified interpreter is the interactive
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to compare.
        /// </param>
        /// <returns>
        /// Non-zero if it matches; otherwise, zero.
        /// </returns>
        bool MatchInteractiveInterpreter(Interpreter interpreter);
        /// <summary>
        /// Resets the interactive interpreter.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool ResetInteractiveInterpreter();
        /// <summary>
        /// Resets the interactive interpreter when it matches the specified
        /// one.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to match before resetting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool MaybeResetInteractiveInterpreter(Interpreter interpreter);

        /// <summary>
        /// Starts the interactive interpreter loop.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool StartupInteractiveLoop();
        /// <summary>
        /// Shuts down the interactive interpreter loop.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool ShutdownInteractiveLoop();

        /// <summary>
        /// Sets the status text for the window.
        /// </summary>
        /// <param name="value">
        /// The status text to set.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool SetStatus(string value);

        /// <summary>
        /// Resets the input history.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        bool ResetHistory();
    }
}
