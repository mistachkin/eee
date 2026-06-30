/*
 * Constants.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides shared constant values used across the Kapok private
    /// components.
    /// </summary>
    [ObjectId("2320603a-19ed-44fd-a63d-1c9f129fb603")]
    internal static class Constants
    {
        #region Certificate Formatting Constants
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //    Changing these values WILL break ALL existing certificate      //
        //    renewal requests.                                              //
        //                                                                   //
        //    Do not change any of these values unless you know exactly what //
        //    they do.                                                       //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Dashes only.  Must keep this in sync with the format defined
        //       in the Harpy LicenseOps class.
        //
        /// <summary>
        /// The format string used to render GUID identifiers.
        /// </summary>
        public static readonly string IdFormat = "D";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Must keep this in sync with the format defined in the Harpy
        //       LicenseOps class.
        //
        /// <summary>
        /// The format string used to render time stamps.
        /// </summary>
        public const string TimeStampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffK";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Trace Formatting Constants
        /// <summary>
        /// The text used to display a null value.
        /// </summary>
        public static readonly string DisplayNull = "<null>";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The text used to display an empty value.
        /// </summary>
        public static readonly string DisplayEmpty = "<empty>";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default value indicating whether stored data is encrypted.
        /// </summary>
        public const bool DefaultEncrypted = true;
    }
}
