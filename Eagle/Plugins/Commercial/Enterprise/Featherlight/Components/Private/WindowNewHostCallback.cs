/*
 * WindowNewHostCallback.cs --
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
using Eagle._Interfaces.Public;

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Bridges the interpreter's new-host callback to the Featherlight shell,
    /// so the interpreter host subsystem obtains a windowed host; derives from
    /// a marshal-by-reference base so it works across application domains.
    /// </summary>
    [ObjectId("bcb9d63f-ac1e-46dd-9306-254f521d85d8")]
    internal sealed class WindowNewHostCallback :
        ScriptMarshalByRefObject, INewHostCallback
    {
        /// <summary>
        /// Creates a new windowed host for the interpreter host subsystem.
        /// </summary>
        /// <param name="hostData">
        /// The data used to create and configure the host.
        /// </param>
        /// <returns>
        /// The new host, or null on failure.
        /// </returns>
        public IHost NewHost(
            IHostData hostData /* in */
            )
        {
            return Shell.Window.NewHost(hostData);
        }
    }
}
