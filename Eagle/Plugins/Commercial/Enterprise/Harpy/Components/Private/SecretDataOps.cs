/*
 * SecretDataOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides operations that transform the byte payloads carried by an
    /// <see cref="ISecretData" /> instance, including no-op copying, salting,
    /// hashing, key derivation, encryption, decryption, signing, and
    /// signature verification.
    /// </summary>
    [ObjectId("29ee311f-3437-423d-b807-b218658b806e")]
    internal static class SecretDataOps
    {
        #region Public Methods
        /// <summary>
        /// Performs a no-op transformation that copies the input bytes of
        /// <paramref name="secretData" /> to its output, unless the input is
        /// being overwritten in place.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose input bytes are processed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Nop(
            ISecretData secretData, /* in */
            ref Result error        /* out */
            )
        {
            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckOutputBytes(
                        secretData, out overwriteInput, ref error))
                {
                    return ReturnCode.Error;
                }

                if (!overwriteInput)
                    secretData.OutputBytes = inputBytes;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prepends the auxiliary (salt) bytes of
        /// <paramref name="secretData" /> to its input bytes, storing the
        /// combined result as the new input or output.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose auxiliary and input bytes are combined.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Salt(
            ISecretData secretData, /* in */
            ref Result error        /* out */
            )
        {
            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                ByteList auxiliaryBytes;

                if (!CheckAuxiliaryBytes(
                        secretData, out auxiliaryBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckOutputBytes(
                        secretData, out overwriteInput, ref error))
                {
                    return ReturnCode.Error;
                }

                ByteList bytes = new ByteList(
                    auxiliaryBytes.Count + inputBytes.Count);

                bytes.AddRange(auxiliaryBytes);
                bytes.AddRange(inputBytes);

                if (overwriteInput)
                    secretData.InputBytes = bytes;
                else
                    secretData.OutputBytes = bytes;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes a cryptographic hash of the input bytes of
        /// <paramref name="secretData" /> using the named hash algorithm and
        /// stores the result as the new input or output.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose input bytes are hashed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Hash(
            ISecretData secretData,   /* in */
            string hashAlgorithmName, /* in */
            ref Result error          /* out */
            )
        {
            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckOutputBytes(
                        secretData, out overwriteInput, ref error))
                {
                    return ReturnCode.Error;
                }

                byte[] bytes = Utility.HashBytes(
                    hashAlgorithmName, inputBytes.ToArray(),
                    ref error);

                if (bytes == null)
                    return ReturnCode.Ok;

                if (overwriteInput)
                    secretData.InputBytes = new ByteList(bytes);
                else
                    secretData.OutputBytes = new ByteList(bytes);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Derives bytes from the configured password and salt using the
        /// PBKDF2 (RFC 2898) parameters and stores the result as the new
        /// input or output of <paramref name="secretData" />.
        /// </summary>
        /// <param name="rfc2898Data">
        /// The RFC 2898 parameters (password, salt, iteration count, and
        /// hash algorithm) used to derive the bytes.
        /// </param>
        /// <param name="secretData">
        /// The secret data whose bytes are updated with the derived result.
        /// </param>
        /// <param name="haveEncoding">
        /// Supplies the text encoding used to convert the salt to bytes.
        /// </param>
        /// <param name="count">
        /// The number of bytes to derive, or null to use the default.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Derive(
            IRfc2898Data rfc2898Data,   /* in */
            ISecretData secretData,     /* in */
            IHaveEncoding haveEncoding, /* in */
            int? count,                 /* in */
            ref Result error            /* out */
            )
        {
            if (rfc2898Data == null)
            {
                error = "invalid rfc data";
                return ReturnCode.Error;
            }

            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            if (haveEncoding == null)
            {
                error = "invalid have encoding";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckOutputBytes(
                        secretData, out overwriteInput, ref error))
                {
                    return ReturnCode.Error;
                }

                string password;
                string salt;
                int iterationCount;
                string hashAlgorithmName;

                if (SecretOps.ExtractData(
                        rfc2898Data, true, out password, out salt,
                        out iterationCount, out hashAlgorithmName,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] bytes = null;
                Encoding encoding = GetEncoding(haveEncoding);

                if (CryptographyOps.DeriveBytes(
                        password, encoding.GetBytes(salt),
                        iterationCount, hashAlgorithmName, count,
                        ref bytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (overwriteInput)
                    secretData.InputBytes = new ByteList(bytes);
                else
                    secretData.OutputBytes = new ByteList(bytes);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the input bytes of
        /// <paramref name="secretData" /> using the configured symmetric
        /// algorithm and RFC 2898 key derivation parameters, storing the
        /// result as the new input or output.
        /// </summary>
        /// <param name="cryptographyData">
        /// The symmetric algorithm, cipher mode, and padding mode used for
        /// the transformation.
        /// </param>
        /// <param name="rfc2898Data">
        /// The RFC 2898 parameters used to derive the encryption key.
        /// </param>
        /// <param name="secretData">
        /// The secret data whose input bytes are encrypted or decrypted.
        /// </param>
        /// <param name="haveEncoding">
        /// Supplies the text encoding used to convert the salt to bytes.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the input bytes; zero to decrypt them.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EncryptOrDecrypt(
            ICryptographyData cryptographyData, /* in */
            IRfc2898Data rfc2898Data,           /* in */
            ISecretData secretData,             /* in */
            IHaveEncoding haveEncoding,         /* in */
            bool encrypt,                       /* in */
            ref Result error                    /* out */
            )
        {
            if (cryptographyData == null)
            {
                error = "invalid cryptography data";
                return ReturnCode.Error;
            }

            if (rfc2898Data == null)
            {
                error = "invalid rfc data";
                return ReturnCode.Error;
            }

            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            if (haveEncoding == null)
            {
                error = "invalid have encoding";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckOutputBytes(
                        secretData, out overwriteInput, ref error))
                {
                    return ReturnCode.Error;
                }

                string password;
                string salt;
                int iterationCount;
                string hashAlgorithmName;

                if (SecretOps.ExtractData(
                        rfc2898Data, true, out password, out salt,
                        out iterationCount, out hashAlgorithmName,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] bytes = null;
                Encoding encoding = GetEncoding(haveEncoding);

                if (CryptographyOps.EncryptOrDecrypt(
                        cryptographyData.SymmetricAlgorithmName,
                        password, encoding.GetBytes(salt),
                        iterationCount, hashAlgorithmName,
                        cryptographyData.CipherMode,
                        cryptographyData.PaddingMode,
                        inputBytes.ToArray(), encrypt,
                        ref bytes, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (overwriteInput)
                    secretData.InputBytes = new ByteList(bytes);
                else
                    secretData.OutputBytes = new ByteList(bytes);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes a digital signature over the input bytes of
        /// <paramref name="secretData" /> using the specified key pair and
        /// hash algorithm, storing the signature as the new input or
        /// signature bytes.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose private key is used to create the signature.
        /// </param>
        /// <param name="secretData">
        /// The secret data whose input bytes are signed.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used when signing.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the operation could
        /// not be completed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Sign(
            IKeyPair keyPair,         /* in */
            ISecretData secretData,   /* in */
            string hashAlgorithmName, /* in */
            ref Result error          /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                bool overwriteInput;

                if (!CheckSignatureBytes(
                        secretData, true, out overwriteInput,
                        ref error))
                {
                    return ReturnCode.Error;
                }

                byte[] bytes = null;
                Result result = null;

                if (CryptographyOps.Sign(hashAlgorithmName,
                        null, inputBytes.ToArray(), keyPair,
                        ref bytes, ref result) != ReturnCode.Ok)
                {
                    error = result;
                    return ReturnCode.Error;
                }

                if (overwriteInput)
                    secretData.InputBytes = new ByteList(bytes);
                else
                    secretData.SignatureBytes = new ByteList(bytes);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature bytes of <paramref name="secretData" />
        /// against its input bytes using the specified key pair and hash
        /// algorithm.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose public key is used to verify the signature.
        /// </param>
        /// <param name="secretData">
        /// The secret data whose input and signature bytes are verified.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used when verifying.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why verification did not
        /// succeed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the signature is valid;
        /// otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Verify(
            IKeyPair keyPair,         /* in */
            ISecretData secretData,   /* in */
            string hashAlgorithmName, /* in */
            ref Result error          /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                ByteList inputBytes;

                if (!CheckInputBytes(
                        secretData, out inputBytes, ref error))
                {
                    return ReturnCode.Error;
                }

                ByteList signatureBytes;

                if (!CheckSignatureBytes(
                        secretData, false, out signatureBytes,
                        ref error))
                {
                    return ReturnCode.Error;
                }

                Result result = null;

                if (CryptographyOps.Verify(hashAlgorithmName,
                        null, inputBytes.ToArray(), keyPair,
                        signatureBytes.ToArray(),
                        ref result) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    error = result;
                    return ReturnCode.Error;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Returns the text encoding supplied by
        /// <paramref name="haveEncoding" />, falling back to the default
        /// encoding when none is available.  This method never returns null.
        /// </summary>
        /// <param name="haveEncoding">
        /// The object that may supply a preferred text encoding.
        /// </param>
        /// <returns>
        /// The encoding to use; never null.
        /// </returns>
        /* CANNOT RETURN NULL */
        private static Encoding GetEncoding(
            IHaveEncoding haveEncoding /* in */
            )
        {
            if (haveEncoding != null)
            {
                Encoding encoding = haveEncoding.Encoding;

                if (encoding != null)
                    return encoding;
            }

            return Constants.DefaultEncoding;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the input bytes from <paramref name="secretData" />,
        /// failing if the secret data or its input byte list is missing.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose input bytes are retrieved.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the input byte list.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the input bytes could
        /// not be obtained.
        /// </param>
        /// <returns>
        /// Non-zero if the input bytes were obtained; otherwise, zero.
        /// </returns>
        private static bool CheckInputBytes(
            ISecretData secretData, /* in */
            out ByteList bytes,     /* out */
            ref Result error        /* out */
            )
        {
            bytes = null;

            if (secretData == null)
            {
                error = "invalid secret data";
                return false;
            }

            lock (secretData.SyncRoot)
            {
                bytes = secretData.InputBytes;

                if (bytes == null)
                {
                    error = "input is not a byte list";
                    return false;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the auxiliary bytes from <paramref name="secretData" />,
        /// failing if the secret data or its auxiliary byte list is missing.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose auxiliary bytes are retrieved.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the auxiliary byte list.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the auxiliary bytes
        /// could not be obtained.
        /// </param>
        /// <returns>
        /// Non-zero if the auxiliary bytes were obtained; otherwise, zero.
        /// </returns>
        private static bool CheckAuxiliaryBytes(
            ISecretData secretData, /* in */
            out ByteList bytes,     /* out */
            ref Result error        /* out */
            )
        {
            bytes = null;

            if (secretData == null)
            {
                error = "invalid secret data";
                return false;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                bytes = secretData.AuxiliaryBytes;

                if (bytes == null)
                {
                    error = "auxiliary is not a byte list";
                    return false;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the output of <paramref name="secretData" />
        /// may be written, indicating via <paramref name="overwriteInput" />
        /// whether the input should be overwritten in place.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose output flags are examined.
        /// </param>
        /// <param name="overwriteInput">
        /// Upon return, non-zero if the input bytes should be overwritten
        /// instead of writing separate output bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the output cannot be
        /// written.
        /// </param>
        /// <returns>
        /// Non-zero if the output may be written; otherwise, zero.
        /// </returns>
        private static bool CheckOutputBytes(
            ISecretData secretData,  /* in */
            out bool overwriteInput, /* out */
            ref Result error         /* out */
            )
        {
            overwriteInput = false;

            if (secretData == null)
            {
                error = "invalid secret data";
                return false;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                SecretDataFlags flags = secretData.Flags;

                if (Utility.HasFlags(
                        flags, SecretDataFlags.OverwriteInput,
                        true))
                {
                    overwriteInput = true;
                }

                if (secretData.HaveOutput && !Utility.HasFlags(
                        flags, SecretDataFlags.OverwriteOutput,
                        true) &&
                    !overwriteInput)
                {
                    error = "cannot overwrite existing output";
                    return false;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the signature byte preconditions for
        /// <paramref name="secretData" /> and indicates whether the input
        /// should be overwritten in place.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose signature state is examined.
        /// </param>
        /// <param name="mustBeNull">
        /// Non-zero if an existing signature must be absent (when signing);
        /// zero if an existing signature is required (when verifying).
        /// </param>
        /// <param name="overwriteInput">
        /// Upon return, non-zero if the input bytes should be overwritten
        /// instead of writing separate signature bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the check failed.
        /// </param>
        /// <returns>
        /// Non-zero if the signature preconditions are satisfied; otherwise,
        /// zero.
        /// </returns>
        private static bool CheckSignatureBytes(
            ISecretData secretData,  /* in */
            bool mustBeNull,         /* in */
            out bool overwriteInput, /* out */
            ref Result error         /* out */
            )
        {
            ByteList bytes;

            return CheckSignatureBytes(
                secretData, mustBeNull, out bytes, out overwriteInput,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the signature byte preconditions for
        /// <paramref name="secretData" /> and retrieves the existing
        /// signature bytes.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose signature state is examined.
        /// </param>
        /// <param name="mustBeNull">
        /// Non-zero if an existing signature must be absent (when signing);
        /// zero if an existing signature is required (when verifying).
        /// </param>
        /// <param name="bytes">
        /// Upon return, receives the existing signature byte list, if any.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the check failed.
        /// </param>
        /// <returns>
        /// Non-zero if the signature preconditions are satisfied; otherwise,
        /// zero.
        /// </returns>
        private static bool CheckSignatureBytes(
            ISecretData secretData,  /* in */
            bool mustBeNull,         /* in */
            out ByteList bytes,      /* out */
            ref Result error         /* out */
            )
        {
            bool overwriteInput;

            return CheckSignatureBytes(
                secretData, mustBeNull, out bytes, out overwriteInput,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the signature byte preconditions for
        /// <paramref name="secretData" />, retrieving the existing signature
        /// bytes and indicating whether the input should be overwritten in
        /// place.
        /// </summary>
        /// <param name="secretData">
        /// The secret data whose signature state is examined.
        /// </param>
        /// <param name="mustBeNull">
        /// Non-zero if an existing signature must be absent (when signing);
        /// zero if an existing signature is required (when verifying).
        /// </param>
        /// <param name="bytes">
        /// Upon return, receives the existing signature byte list, if any.
        /// </param>
        /// <param name="overwriteInput">
        /// Upon return, non-zero if the input bytes should be overwritten
        /// instead of writing separate signature bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the check failed.
        /// </param>
        /// <returns>
        /// Non-zero if the signature preconditions are satisfied; otherwise,
        /// zero.
        /// </returns>
        private static bool CheckSignatureBytes(
            ISecretData secretData,  /* in */
            bool mustBeNull,         /* in */
            out ByteList bytes,      /* out */
            out bool overwriteInput, /* out */
            ref Result error         /* out */
            )
        {
            bytes = null;
            overwriteInput = false;

            if (secretData == null)
            {
                error = "invalid secret data";
                return false;
            }

            lock (secretData.SyncRoot) /* TRANSACTIONAL */
            {
                SecretDataFlags flags = secretData.Flags;

                if (Utility.HasFlags(
                        flags, SecretDataFlags.OverwriteInput,
                        true))
                {
                    overwriteInput = true;
                }

                if (mustBeNull)
                {
                    if (secretData.HaveSignature && !Utility.HasFlags(
                            flags, SecretDataFlags.OverwriteSignature,
                            true) &&
                        !overwriteInput)
                    {
                        error = "cannot overwrite existing signature";
                        return false;
                    }
                }
                else
                {
                    bytes = secretData.SignatureBytes;

                    if ((bytes == null) && !overwriteInput)
                    {
                        error = "signature is not a byte list";
                        return false;
                    }
                }

                return true;
            }
        }
        #endregion
    }
}
