/*
 * ArgsOps.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using ArgsList = System.Collections.Generic.IEnumerable<string>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides helpers for determining and obtaining the automatic
    /// command-line arguments passed to sandboxed scripts.
    /// </summary>
    [ObjectId("2974a2f7-a82e-4a2d-b71f-680fa49e8bf6")]
    internal static class ArgsOps
    {
        #region Private Constants
        /// <summary>
        /// The marker value indicating that automatic arguments should be
        /// used.
        /// </summary>
        private static readonly ArgsList UseAutomatic =
            new List<string>(); /* SENTINEL */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether the supplied arguments request automatic
        /// argument expansion.
        /// </summary>
        /// <param name="args">
        /// The arguments to inspect.
        /// </param>
        /// <returns>
        /// Non-zero when automatic arguments are requested; otherwise, zero.
        /// </returns>
        public static bool ShouldUseAutomatic(
            ArgsList args /* in */
            )
        {
            return Object.ReferenceEquals(
                args, UseAutomatic); /* SENTINEL */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the automatic argument list.
        /// </summary>
        /// <returns>
        /// The automatic argument list.
        /// </returns>
        public static ArgsList DoUseAutomatic()
        {
            return UseAutomatic; /* SENTINEL */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the automatic argument list for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose automatic arguments are returned.
        /// </param>
        /// <returns>
        /// The automatic argument list.
        /// </returns>
        public static ArgsList GetAutomatic(
            Interpreter interpreter /* in */
            )
        {
            string[] args = Environment.GetCommandLineArgs();

            if ((args == null) || (args.Length < 2))
                return null;

            return new StringList(args, 1);
        }
        #endregion
    }
}
