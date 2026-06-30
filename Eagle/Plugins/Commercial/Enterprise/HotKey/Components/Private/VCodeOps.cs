/*
 * VCodeOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Computes and formats short "verification codes" for text.  A code is
    /// derived from the text (wrapped with fixed prefix/suffix bytes) using
    /// RFC 2898 (PBKDF2) followed by a SHA-512 hash, truncated to a small
    /// fixed length, so identical text always yields the same code.
    /// </summary>
    [ObjectId("8558f236-f581-4970-99dc-b73ed1e6263c")]
    internal static class VCodeOps
    {
        #region Private Constants
        //
        // WARNING: These values cannot be changed without breaking any
        //          prior verification codes.  That might be perfectly
        //          OK if they are intended to change for each software
        //          revision.
        //
        /// <summary>
        /// The fixed bytes prepended to the text before deriving a
        /// verification code.
        /// </summary>
        private static readonly byte[] PrefixBytes = {
            0x10, 0xFF, 0x75, 0x02, 0xC1, 0x13, 0x39, 0xAF
        };

        /// <summary>
        /// The fixed bytes appended to the text before deriving a
        /// verification code.
        /// </summary>
        private static readonly byte[] SuffixBytes = {
            0x6B, 0x43, 0xCA, 0xC2, 0xBD, 0xC3, 0x43, 0x93
        };

        /// <summary>
        /// The all-zero verification code returned for empty text.
        /// </summary>
        private static readonly byte[] EmptyResult = {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The format string used to render a single byte as two uppercase
        /// hexadecimal digits.
        /// </summary>
        private const string ByteFormat = "{0:X2}";

        /// <summary>
        /// The length, in bytes, of a computed verification code.
        /// </summary>
        private const int ResultLength = 8;

        ///////////////////////////////////////////////////////////////////////

        #region RFC 2898 Constants
        /// <summary>
        /// The salt bytes used for the RFC 2898 key derivation.
        /// </summary>
        private static readonly byte[] SaltBytes = {
            0x5B, 0xC0, 0x4B, 0x25, 0x09, 0x7D, 0x90, 0xED
        };

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The RFC 2898 iteration count used for the key derivation.
        /// </summary>
        private const int IterationCount = 1000001;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the hash algorithm applied to the derived bytes.
        /// </summary>
        private const string HashAlgorithmName = "SHA512";
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Calculates the verification code for the supplied text.  The text
        /// is encoded, wrapped with the fixed prefix and suffix bytes, run
        /// through RFC 2898 key derivation and a SHA-512 hash, and truncated
        /// to the fixed result length.  Empty text yields the all-zero code.
        /// </summary>
        /// <param name="encoding">
        /// The encoding used to convert the text to bytes.
        /// </param>
        /// <param name="text">
        /// The text to compute a verification code for.
        /// </param>
        /// <returns>
        /// The verification code bytes, or null on failure or invalid input.
        /// </returns>
        public static byte[] Calculate(
            Encoding encoding, /* in */
            string text        /* in */
            )
        {
            try
            {
                if ((encoding == null) || (text == null))
                    return null;

                int length = text.Length;

                if (length == 0)
                    return EmptyResult;

                ByteList bytes = new ByteList();

                if (PrefixBytes != null)
                    bytes.AddRange(PrefixBytes);

                bytes.AddRange(encoding.GetBytes(text));

                if (SuffixBytes != null)
                    bytes.AddRange(SuffixBytes);

                DeriveBytes deriveBytes = new Rfc2898DeriveBytes(
                    bytes.ToArray(), SaltBytes, IterationCount);

                byte[] hashValue;
                Result error = null;

                hashValue = Utility.HashBytes(
                    HashAlgorithmName, deriveBytes.GetBytes(length),
                    ref error);

                if (hashValue == null)
                {
                    Utility.DebugTrace(String.Format(
                        "Calculate: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(VCodeOps).Name,
                        TracePriority.Higher |
                            TracePriority.FromPlugin);

                    return null;
                }

                Array.Resize(ref hashValue, ResultLength);

                return hashValue;
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(VCodeOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats verification code bytes as uppercase hexadecimal, grouping
        /// the bytes in pairs separated by spaces.
        /// </summary>
        /// <param name="bytes">
        /// The verification code bytes to format.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string, or null when the input is null.
        /// </returns>
        public static string Format(
            byte[] bytes /* in */
            )
        {
            if (bytes == null)
                return null;

            StringBuilder builder = new StringBuilder();
            int count = bytes.Length;

            for (int index = 0; index < count; )
            {
                if (builder.Length > 0)
                    builder.Append(Characters.Space);

                builder.AppendFormat(
                    ByteFormat, bytes[index++]);

                if (index < count)
                {
                    builder.AppendFormat(
                        ByteFormat, bytes[index++]);
                }
            }

            return builder.ToString();
        }
        #endregion
    }
}
