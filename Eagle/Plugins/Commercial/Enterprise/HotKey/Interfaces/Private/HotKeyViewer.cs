/*
 * HotKeyViewer.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace HotKey.Interfaces.Private
{
    //
    // NOTE: This interface is currently private; however, it may be "promoted"
    //       to public at some point.
    //
    /// <summary>
    /// Represents a viewer of hot-keys that can refresh its displayed
    /// contents.
    /// </summary>
    [ObjectId("b977b724-a8b8-4c4c-9e42-9e925c486840")]
    internal interface IHotKeyViewer
    {
        /// <summary>
        /// Refreshes the viewer's displayed hot-key information.
        /// </summary>
        /// <param name="interactive">
        /// Non-zero when the refresh was initiated by an interactive user
        /// action; zero for an automatic refresh.
        /// </param>
        void Refresh(bool interactive);
    }
}
