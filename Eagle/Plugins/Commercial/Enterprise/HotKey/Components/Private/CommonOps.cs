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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Provides small common helper values and methods shared across the
    /// HotKey plugin.
    /// </summary>
    [ObjectId("768d5c6b-3dfa-46d4-982d-24d7eb4876b4")]
    internal static class CommonOps
    {
        #region Public Constants
        //
        // NOTE: The default special folder used by this plugin.  This should
        //       be the same as the default for the .NET Framework itself.
        //
        /// <summary>
        /// The default special folder used by this plugin, matching the .NET
        /// Framework default.
        /// </summary>
        public static readonly Environment.SpecialFolder DefaultSpecialFolder =
            Environment.SpecialFolder.Desktop;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Helper Methods
        /// <summary>
        /// Gets the list of conditional compilation symbols that were active
        /// when the plugin was built.  This backs the plugin's options
        /// reporting.
        /// </summary>
        /// <param name="result">
        /// On output, receives the option list, or an error message when it is
        /// unavailable.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
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
    }
}
