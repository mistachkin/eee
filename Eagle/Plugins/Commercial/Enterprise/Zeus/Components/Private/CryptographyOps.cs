/*
 * CryptographyOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Zeus.Components.Private
{
    /// <summary>
    /// Provides the symmetric cryptography helpers used by the Zeus plugin.
    /// It derives key material from a password and salt via RFC 2898
    /// (PBKDF2), and encrypts or decrypts text with the Rijndael algorithm
    /// using the derived key, returning base64-encoded ciphertext.  These
    /// routines underlie the <c>zeus derive</c> command and the procedure
    /// obfuscation feature.
    /// </summary>
    [ObjectId("7fe11da1-e651-401f-9694-51840c0c5591")]
    internal static class CryptographyOps
    {
        #region Private Constants
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The default text encoding used when converting between strings and
        /// bytes.
        /// </summary>
        private static Encoding DefaultEncoding = Encoding.UTF8;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The default symmetric key size, in bits.
        /// </summary>
        private static int DefaultKeySize = 256; /* bits */

        /// <summary>
        /// The default cipher block size, in bits.
        /// </summary>
        private static int DefaultBlockSize = 128; /* bits */

        /// <summary>
        /// The default cipher mode used by the symmetric algorithm.
        /// </summary>
        private static CipherMode DefaultCipherMode = CipherMode.CBC;

        /// <summary>
        /// The default padding mode used by the symmetric algorithm.
        /// </summary>
        private static PaddingMode DefaultPaddingMode = PaddingMode.PKCS7;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The default number of key bytes to derive (the key size in bytes).
        /// </summary>
        public static int DefaultDeriveCount = DefaultKeySize / 8; /* bytes */

        /// <summary>
        /// The default RFC 2898 iteration count used for key derivation.
        /// </summary>
        public static int DefaultIterationCount = 100001;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Encryption Support Methods
        /// <summary>
        /// Converts the supplied text into bytes.  When encrypting, the text
        /// is encoded using the supplied (or default) encoding; when
        /// decrypting, the text is treated as base64 and decoded into the raw
        /// ciphertext bytes.
        /// </summary>
        /// <param name="encoding">
        /// The text encoding to use; when null, the default encoding is used.
        /// </param>
        /// <param name="name">
        /// An optional descriptive name for the text, used in error messages.
        /// </param>
        /// <param name="text">
        /// The text to convert into bytes.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero when encrypting (encode the text); zero when decrypting
        /// (base64-decode the text).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The resulting bytes, or null on failure.
        /// </returns>
        private static byte[] GetBytes(
            Encoding encoding, /* in: OPTIONAL */
            string name,       /* in: OPTIONAL */
            string text,       /* in */
            bool encrypt,      /* in */
            ref Result error   /* out */
            )
        {
            if (text == null)
            {
                error = String.Format(
                    "invalid {0}", (name != null) ? name : "text");

                return null;
            }

            if (encrypt)
            {
                if (encoding == null)
                    encoding = DefaultEncoding;

                if (encoding == null)
                {
                    error = "invalid encoding";
                    return null;
                }

                return encoding.GetBytes(text); /* throw */
            }
            else
            {
                return Convert.FromBase64String(text); /* throw */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the supplied bytes into a string.  When encrypting, the
        /// bytes (the ciphertext) are base64-encoded; when decrypting, the
        /// bytes (the plaintext) are decoded using the supplied (or default)
        /// encoding.
        /// </summary>
        /// <param name="encoding">
        /// The text encoding to use; when null, the default encoding is used.
        /// </param>
        /// <param name="name">
        /// An optional descriptive name for the bytes, used in error
        /// messages.
        /// </param>
        /// <param name="bytes">
        /// The bytes to convert into a string.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero when encrypting (base64-encode the bytes); zero when
        /// decrypting (decode the bytes as text).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The resulting string, or null on failure.
        /// </returns>
        private static string GetString(
            Encoding encoding, /* in: OPTIONAL */
            string name,       /* in: OPTIONAL */
            byte[] bytes,      /* in */
            bool encrypt,      /* in */
            ref Result error   /* out */
            )
        {
            if (bytes == null)
            {
                error = String.Format(
                    "invalid {0}", (name != null) ? name : "bytes");

                return null;
            }

            if (encrypt)
            {
                return Convert.ToBase64String(bytes,
                    Base64FormattingOptions.InsertLineBreaks); /* throw */
            }
            else
            {
                if (encoding == null)
                    encoding = DefaultEncoding;

                if (encoding == null)
                {
                    error = "invalid encoding";
                    return null;
                }

                return encoding.GetString(bytes); /* throw */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates an RFC 2898 (PBKDF2) key-derivation object from the
        /// supplied password, salt, and iteration count.  On supporting
        /// frameworks, a non-empty hash algorithm name selects the digest
        /// used; otherwise the platform default digest is used.
        /// </summary>
        /// <param name="password">
        /// The password used as input material for key derivation.
        /// </param>
        /// <param name="saltBytes">
        /// The salt bytes used for key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// The number of iterations to perform.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, or null/empty to use the
        /// default.
        /// </param>
        /// <param name="error">
        /// Reserved for error reporting; not used by this implementation.
        /// </param>
        /// <returns>
        /// A new key-derivation object.
        /// </returns>
        private static DeriveBytes CreateDeriveBytes(
            string password,          /* in */
            byte[] saltBytes,         /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in */
            ref Result error          /* out: NOT USED */
            )
        {
#if NET_472 || NET_48 || NET_481
            if (!String.IsNullOrEmpty(hashAlgorithmName))
            {
                return new Rfc2898DeriveBytes(
                    password, saltBytes, iterationCount,
                    new HashAlgorithmName(hashAlgorithmName));
            }
            else
#endif
            {
                return new Rfc2898DeriveBytes(
                    password, saltBytes, iterationCount);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and configures a Rijndael symmetric algorithm whose key
        /// and initialization vector are derived from the supplied password,
        /// salt, and iteration count via RFC 2898.  The algorithm is set up
        /// with the default key size, block size, cipher mode, and padding
        /// mode.
        /// </summary>
        /// <param name="password">
        /// The password used as input material for key derivation.
        /// </param>
        /// <param name="saltBytes">
        /// The salt bytes used for key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// The number of iterations to perform; must be at least one.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, or null/empty to use the
        /// default.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// A configured symmetric algorithm, or null on failure.
        /// </returns>
        private static SymmetricAlgorithm CreateSymmetricAlgorithm(
            string password,          /* in */
            byte[] saltBytes,         /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in */
            ref Result error          /* out */
            )
        {
            if (password == null)
            {
                error = "invalid password";
                return null;
            }

            if (saltBytes == null)
            {
                error = "invalid salt bytes";
                return null;
            }

            if (iterationCount < 1)
            {
                error = "invalid iteration count";
                return null;
            }

            DeriveBytes deriveBytes = CreateDeriveBytes(
                password, saltBytes, iterationCount, hashAlgorithmName,
                ref error);

            if (deriveBytes == null)
                return null;

            SymmetricAlgorithm algorithm = new RijndaelManaged();

            algorithm.KeySize = DefaultKeySize;
            algorithm.BlockSize = DefaultBlockSize;
            algorithm.FeedbackSize = DefaultBlockSize;

            algorithm.IV = deriveBytes.GetBytes(algorithm.BlockSize / 8);
            algorithm.Key = deriveBytes.GetBytes(algorithm.KeySize / 8);

            algorithm.Mode = DefaultCipherMode;
            algorithm.Padding = DefaultPaddingMode;

            return algorithm;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Encryption Support Methods
        /// <summary>
        /// Derives a number of key bytes from the supplied password and salt
        /// using RFC 2898 (PBKDF2).  This implements the key-derivation
        /// performed by the <c>zeus derive</c> command.
        /// </summary>
        /// <param name="encoding">
        /// The text encoding used to convert the salt to bytes; when null,
        /// the default encoding is used.
        /// </param>
        /// <param name="password">
        /// The password used as input material for key derivation.
        /// </param>
        /// <param name="salt">
        /// The salt string used for key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// The number of iterations to perform.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, or null/empty to use the
        /// default.
        /// </param>
        /// <param name="deriveCount">
        /// The number of key bytes to derive.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the derived key bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode DeriveBytes(
            Encoding encoding,        /* in: OPTIONAL */
            string password,          /* in */
            string salt,              /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in: OPTIONAL */
            int deriveCount,          /* in */
            ref byte[] bytes,         /* out */
            ref Result error          /* out */
            )
        {
            byte[] saltBytes = GetBytes(
                encoding, "salt string", salt, true, ref error);

            if (saltBytes == null)
                return ReturnCode.Error;

            DeriveBytes deriveBytes = CreateDeriveBytes(
                password, saltBytes, iterationCount, hashAlgorithmName,
                ref error);

            if (deriveBytes == null)
                return ReturnCode.Error;

            bytes = deriveBytes.GetBytes(deriveCount);
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the supplied text using a Rijndael algorithm
        /// keyed from the supplied password and salt via RFC 2898.  When
        /// encrypting, plaintext is transformed into base64-encoded
        /// ciphertext; when decrypting, base64 ciphertext is transformed back
        /// into plaintext.
        /// </summary>
        /// <param name="encoding">
        /// The text encoding used to convert text and salt to and from bytes;
        /// when null, the default encoding is used.
        /// </param>
        /// <param name="password">
        /// The password used as input material for key derivation.
        /// </param>
        /// <param name="salt">
        /// The salt string used for key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// The number of iterations to perform.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, or null/empty to use the
        /// default.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the text; zero to decrypt it.
        /// </param>
        /// <param name="text">
        /// On input, the text to transform; on output, receives the
        /// transformed text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Transform(
            Encoding encoding,        /* in: OPTIONAL */
            string password,          /* in */
            string salt,              /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in: OPTIONAL */
            bool encrypt,             /* in */
            ref string text,          /* in, out */
            ref Result error          /* out */
            )
        {
            byte[] saltBytes = GetBytes(
                encoding, "salt string", salt, true, ref error);

            if (saltBytes == null)
                return ReturnCode.Error;

            using (SymmetricAlgorithm algorithm = CreateSymmetricAlgorithm(
                    password, saltBytes, iterationCount, hashAlgorithmName,
                    ref error))
            {
                if (algorithm == null)
                    return ReturnCode.Error;

                ICryptoTransform transform = encrypt ?
                    algorithm.CreateEncryptor() : algorithm.CreateDecryptor();

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    byte[] textBytes = GetBytes(
                        encoding, encrypt ? "plain text" : "cipher text",
                        text, encrypt, ref error);

                    using (CryptoStream cryptoStream = new CryptoStream(
                            memoryStream, transform,
                            CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(
                            textBytes, 0, textBytes.Length);
                    }

                    string localText = GetString(
                        encoding, encrypt ? "cipher text" : "plain text",
                        memoryStream.ToArray(), encrypt, ref error);

                    if (localText == null)
                        return ReturnCode.Error;

                    text = localText;
                    return ReturnCode.Ok;
                }
            }
        }
        #endregion
    }
}
