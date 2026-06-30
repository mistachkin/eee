/*
 * Utility.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Reflection;
using Eagle._Attributes;
using Licensing.Components.Private;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Provides public helper methods for formatting and parsing values used
    /// by the licensing components, such as GUID and time stamp formats and
    /// public key tokens.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("be019983-cf01-459d-bcfc-1c3d1edd1491")]
    public static class Utility
    {
        /// <summary>
        /// Gets the default format string used when formatting GUID values.
        /// </summary>
        /// <returns>
        /// The default GUID format string.
        /// </returns>
        public static string GetDefaultGuidFormat()
        {
            return CertificateDataOps.GetGuidFormat();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default format string used when formatting time stamp
        /// values.
        /// </summary>
        /// <returns>
        /// The default time stamp format string.
        /// </returns>
        public static string GetDefaultTimeStampFormat()
        {
            return CertificateDataOps.GetTimeStampFormat();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified public key token bytes as a hexadecimal
        /// string.
        /// </summary>
        /// <param name="value">
        /// The public key token bytes to format.
        /// </param>
        /// <returns>
        /// The hexadecimal string representation of
        /// <paramref name="value" />.
        /// </returns>
        public static string FormatPublicKeyToken(
            byte[] value /* in */
            )
        {
            return CertificateDataOps.FormatHexadecimal(value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string into the bytes of a public key token.
        /// </summary>
        /// <param name="value">
        /// The string representation of the public key token to parse.
        /// </param>
        /// <returns>
        /// The parsed public key token bytes.
        /// </returns>
        public static byte[] ParsePublicKeyToken(
            string value /* in */
            ) /* throw */
        {
            return CertificateDataOps.ParsePublicKeyToken(value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether per-machine storage should be used.
        /// </summary>
        /// <returns>
        /// Non-zero if per-machine storage should be used; otherwise, zero.
        /// </returns>
        public static bool ShouldUsePerMachine()
        {
            return CertificateSharedOps.ShouldUsePerMachine(null);
        }
    }
}
