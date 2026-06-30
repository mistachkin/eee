/*
 * LogClientData.cs --
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
using Eagle._Interfaces.Public;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Represents the per-instance state associated with logging of client
    /// data.  This combines a number of common capability interfaces (e.g.
    /// culture, interpreter, plugin, file name, and execution policy support)
    /// with the ability to append messages to a backing log file.
    /// </summary>
    [ObjectId("5bf38d7e-e9f4-45af-a2de-0c42fbe04389")]
    internal interface ILogClientData :
            IClientData, IMaybeDisposed, IDisposable,
            IHaveCultureInfo, IHaveInterpreter, IHavePlugin,
            IHaveFileName, IHaveExecutionPolicy /* CORE */
    {
        /// <summary>
        /// Appends the specified message to the backing log file.
        /// </summary>
        /// <param name="message">
        /// The message text to append to the log file.
        /// </param>
        /// <returns>
        /// Non-zero if the message was successfully appended; otherwise,
        /// zero.
        /// </returns>
        bool AppendToFile(string message);
    }
}
