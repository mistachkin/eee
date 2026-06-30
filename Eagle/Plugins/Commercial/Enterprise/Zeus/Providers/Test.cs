/*
 * Test.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

///////////////////////////////////////////////////////////////////////////////
//                                                                           //
// WARNING: THIS CLASS IS FOR DEVELOPMENT AND TEST USE ONLY.  DO *NOT* USE   //
//          THIS CLASS IN A PRODUCTION SYSTEM THAT REQUIRES REAL SECURITY.   //
//                                                                           //
// WARNING: USE OF THIS CLASS MAY RESULT IN IT BEING TRIVIAL TO DECRYPT ANY  //
//          OF THE GENERATED ENCRYPTED CIPHERTEXT.                           //
//                                                                           //
///////////////////////////////////////////////////////////////////////////////

using System;

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Zeus.Providers
{
    /// <summary>
    /// Implements an RFC 2898 (PBKDF2) data provider intended solely for
    /// development and testing.  It takes its key-derivation parameters from
    /// an object array in the caller data and falls back to hard-coded
    /// default values when that data is missing or malformed.  As the file
    /// header warns, this provider must never be used where real security is
    /// required, since its predictable parameters can make ciphertext
    /// trivial to decrypt.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("dd29fced-5bd9-43f1-b54c-fc55939d41ea")]
    public sealed class Test : Core
    {
        #region Private Constants
        //
        // NOTE: This value is "cGFzc3dvcmQ=" when Base64 encoded.
        //
        /// <summary>
        /// The default password used when none is supplied in the caller
        /// data.
        /// </summary>
        private static readonly string DefaultPassword = "password";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This value is "dGVzdDEyMzQ=" when Base64 encoded.
        //
        /// <summary>
        /// The default salt used when none is supplied in the caller data.
        /// </summary>
        private static readonly string DefaultSalt = "test1234";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default iteration count used when none is supplied in the
        /// caller data.
        /// </summary>
        private static readonly int DefaultIterationCount = 1000;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default hash algorithm name used when none is supplied in the
        /// caller data; null selects the underlying default.
        /// </summary>
        private static readonly string DefaultHashAlgorithmName = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This signature block value assumes use of the library key
        //       pair "EagleEnterpriseClass0RootPrivate.snk" with a public
        //       key token of "9559f6017247e3e2".
        //
        /// <summary>
        /// The default signature block used when none is supplied in the
        /// caller data.
        /// </summary>
        private static readonly string DefaultSignature = @"
! EncryptedData: v1.0
! publicKeyToken: 9559f6017247e3e2
! timeStamp: 2023-04-12T04:29:43.8495058Z

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
";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Internal Constructors
        /// <summary>
        /// Constructs a new <see cref="Test" /> provider instance associated
        /// with the specified interpreter and caller data.  When the caller
        /// data cannot be unpacked into valid parameters, the hard-coded
        /// default values are used instead.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter this provider is associated with.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller; expected to carry an object
        /// array of key-derivation parameters.
        /// </param>
        internal Test(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
            : base(interpreter, clientData)
        {
            Result error = null;

            if (!TryUnpackData(clientData, ref error))
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(
                    error, typeof(Test).Name,
                    TracePriority.MediumHigh |
                        TracePriority.FromPlugin);
#endif

                UseDefaultData();
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to unpack the key-derivation parameters from the object
        /// array carried in the supplied caller data and store them on the
        /// base provider.  The array must contain at least a password
        /// (string), salt (string), and iteration count (integer), and may
        /// optionally contain a hash algorithm name and signature (both
        /// strings).
        /// </summary>
        /// <param name="clientData">
        /// The caller data expected to carry the object array of parameters.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero if the parameters were successfully unpacked and stored;
        /// otherwise, zero.
        /// </returns>
        private bool TryUnpackData(
            IClientData clientData, /* in */
            ref Result error        /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return false;
            }

            object[] data = clientData.Data as object[];

            if (data == null)
            {
                error = "invalid object array";
                return false;
            }

            int length = data.Length;

            if (length < 3)
            {
                error = String.Format(
                    "object array has {0} elements, need at least 3",
                    length);

                return false;
            }

            if (!(data[0] is string))
            {
                error = "first element of object array is not a string";
                return false;
            }

            if (!(data[1] is string))
            {
                error = "second element of object array is not a string";
                return false;
            }

            if (!(data[2] is int))
            {
                error = "third element of object array is not an integer";
                return false;
            }

            if ((length >= 4) && !(data[3] is string))
            {
                error = "fourth element of object array is not a string";
                return false;
            }

            if ((length >= 5) && !(data[4] is string))
            {
                error = "fifth element of object array is not a string";
                return false;
            }

            base.Password = (string)data[0];
            base.Salt = (string)data[1];
            base.IterationCount = (int)data[2];

            if (length >= 4)
                base.HashAlgorithmName = (string)data[3];

            if (length >= 5)
                base.Signature = (string)data[4];

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the hard-coded default key-derivation parameters on the
        /// base provider, used as a fallback when valid caller data is not
        /// available.
        /// </summary>
        private void UseDefaultData()
        {
            base.Password = DefaultPassword;
            base.Salt = DefaultSalt;
            base.IterationCount = DefaultIterationCount;
            base.HashAlgorithmName = DefaultHashAlgorithmName;
            base.Signature = DefaultSignature;
        }
        #endregion
    }
}
