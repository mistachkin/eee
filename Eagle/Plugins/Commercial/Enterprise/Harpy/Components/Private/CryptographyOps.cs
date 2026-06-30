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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NETWORK && WEB
using Helpers = Licensing.Components.Private.Commands.Helpers;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides private helper routines for symmetric encryption, key
    /// derivation, and asymmetric signing and verification used by the
    /// licensing components.
    /// </summary>
    [ObjectId("980d04d6-218b-48b1-bedc-a85ccc6a4060")]
    internal static class CryptographyOps
    {
        #region Symmetric Encryption Methods
        /// <summary>
        /// Determines whether only FIPS-compliant algorithms should be
        /// allowed, based on the configuration environment variable or the
        /// current cryptographic policy.
        /// </summary>
        /// <returns>
        /// Non-zero if only FIPS-compliant algorithms are permitted;
        /// otherwise, zero.
        /// </returns>
        private static bool AllowOnlyFipsAlgorithms()
        {
            if (Configuration.DoesVariableExist(
                    Constants.ForceAllowOnlyFipsAlgorithmsEnvVarName))
            {
                return true;
            }

#if NET_40
            if (CryptoConfig.AllowOnlyFipsAlgorithms)
                return true;
#endif

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the specified iteration count so that it falls within the
        /// configured default, minimum, and maximum bounds.
        /// </summary>
        /// <param name="iterations">
        /// The requested number of iterations.
        /// </param>
        /// <returns>
        /// The possibly adjusted number of iterations.
        /// </returns>
        private static int MaybeMutateIterations(
            int iterations /* in */
            )
        {
            if ((Constants.DefaultIterations > 0) &&
                (iterations <= 0))
            {
                iterations = Constants.DefaultIterations;
            }

            if ((Constants.MinimumIterations > 0) &&
                (iterations < Constants.MinimumIterations))
            {
                iterations = Constants.MinimumIterations;
            }

#pragma warning disable 162
            if ((Constants.MaximumIterations > 0) &&
                (iterations > Constants.MaximumIterations))
            {
                iterations = Constants.MaximumIterations;
            }
#pragma warning restore 162

            return iterations;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the default symmetric algorithm, selecting a
        /// FIPS-compliant algorithm when required.
        /// </summary>
        /// <param name="onlyFips">
        /// Non-zero to force selection of a FIPS-compliant algorithm.
        /// </param>
        /// <returns>
        /// The name of the default symmetric algorithm.
        /// </returns>
        private static string GetDefaultSymmetricAlgorithmName(
            bool onlyFips /* in */
            )
        {
            if (onlyFips || AllowOnlyFipsAlgorithms())
            {
                return String.Format(
                    Constants.DefaultFipsSymmetricAlgorithmFormat,
                    Constants.DefaultFipsSymmetricAlgorithmVersion,
                    PublicKeyToken.Ecma);
            }

            return Constants.DefaultSymmetricAlgorithmName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the namespace name that contains the symmetric algorithm
        /// types.
        /// </summary>
        /// <returns>
        /// The namespace name for the symmetric algorithm types.
        /// </returns>
        private static string GetNamespaceNameForAlgorithms()
        {
            return typeof(SymmetricAlgorithm).Namespace;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the assembly that contains the symmetric algorithm types.
        /// </summary>
        /// <returns>
        /// The assembly that contains the symmetric algorithm types.
        /// </returns>
        private static Assembly GetAssemblyForAlgorithms()
        {
            return typeof(RijndaelManaged).Assembly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the type name for the named symmetric algorithm using the
        /// algorithm namespace and assembly.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The name of the symmetric algorithm.
        /// </param>
        /// <returns>
        /// The resolved type name for the symmetric algorithm.
        /// </returns>
        private static string AlgorithmNameToTypeName(
            string symmetricAlgorithmName /* in */
            )
        {
            return Utility.GetFactoryTypeName(
                GetNamespaceNameForAlgorithms(), symmetricAlgorithmName,
                GetAssemblyForAlgorithms());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a symmetric algorithm instance using the factory type
        /// resolution mechanism.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The name of the symmetric algorithm to create, or null to use the
        /// default algorithm name.
        /// </param>
        /// <param name="algorithm">
        /// Upon success, receives the created symmetric algorithm.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        private static void CreateSymmetricAlgorithmViaFactory(
            string symmetricAlgorithmName,    /* in: OPTIONAL */
            ref SymmetricAlgorithm algorithm, /* out */
            ref Result error                  /* out */
            )
        {
            Type type = Utility.LookupFactoryType(AlgorithmNameToTypeName(
                (symmetricAlgorithmName != null) ? symmetricAlgorithmName :
                GetDefaultSymmetricAlgorithmName(false)), true);

            if (type == null)
            {
                error = String.Format(
                    "unrecognized symmetric algorithm {0}",
                    Utility.FormatWrapOrNull(symmetricAlgorithmName));

                return;
            }

            bool success = false;
            object @object = null;

            try
            {
                @object = Utility.CreateViaFactory(type, ref error);

                if (@object == null)
                    return;

                SymmetricAlgorithm localAlgorithm = @object as SymmetricAlgorithm;

                if (localAlgorithm == null)
                {
                    error = String.Format(
                        "type {0} mismatch for symmetric algorithm {1}",
                        Utility.FormatTypeNameOrFullName(@object),
                        Utility.FormatWrapOrNull(symmetricAlgorithmName));

                    return;
                }

                algorithm = localAlgorithm;
                success = true;
            }
            finally
            {
                if (!success && (@object != null))
                {
                    /* IGNORED */
                    Utility.TryDisposeObjectOrTrace<object>(ref @object);

                    @object = null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter associated with the specified RFC 2898 data
        /// provider.
        /// </summary>
        /// <param name="provider">
        /// The RFC 2898 data provider from which to obtain the interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The associated interpreter, or null if it could not be obtained.
        /// </returns>
        private static Interpreter GetInterpreter(
            IRfc2898DataProvider provider, /* in */
            ref Result error               /* out */
            )
        {
            if (provider == null)
            {
                error = "invalid RFC 2898 data provider";
                return null;
            }

            IGetInterpreter getInterpreter = provider as IGetInterpreter;

            if (getInterpreter == null)
            {
                error = "cannot get interpreter for RFC 2898 data provider";
                return null;
            }

            try
            {
                Interpreter interpreter = getInterpreter.Interpreter; /* throw */

                if (interpreter == null)
                    error = "RFC 2898 data provider has invalid interpreter";

                return interpreter;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a key-derivation object from the specified password, salt,
        /// iteration count, and optional hash algorithm name.
        /// </summary>
        /// <param name="password">
        /// The password from which to derive key material.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm to use.
        /// </param>
        /// <returns>
        /// The created key-derivation object.
        /// </returns>
        private static DeriveBytes CreateDeriveBytes(
            string password,         /* in */
            byte[] salt,             /* in */
            int iterations,          /* in */
            string hashAlgorithmName /* in: OPTIONAL */
            )
        {
#if NET_472 || NET_48 || NET_481
            if (hashAlgorithmName != null)
            {
                return new Rfc2898DeriveBytes(
                    password, salt, iterations,
                    new HashAlgorithmName(
                        hashAlgorithmName));
            }
            else
#endif
            {
                return new Rfc2898DeriveBytes(
                    password, salt, iterations);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and configures a symmetric algorithm using parameters
        /// obtained from the specified RFC 2898 data provider.
        /// </summary>
        /// <param name="provider">
        /// The RFC 2898 data provider used to obtain key material.
        /// </param>
        /// <param name="fileName">
        /// The optional name of the file associated with the data.
        /// </param>
        /// <param name="encodingName">
        /// The optional name of the text encoding to use.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to create.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created symmetric algorithm, or null on failure.
        /// </returns>
        private static SymmetricAlgorithm CreateSymmetricAlgorithm(
            IRfc2898DataProvider provider, /* in */
            string fileName,               /* in: OPTIONAL */
            string encodingName,           /* in: OPTIONAL */
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            ref Result error               /* out */
            )
        {
            if (provider == null)
            {
                error = "invalid RFC 2898 data provider";
                return null;
            }

            Interpreter interpreter = GetInterpreter(provider, ref error);

            if (interpreter == null)
                return null;

            Encoding encoding = null;
            Result localError = null; /* REUSED */

            if (interpreter.GetEncodingOrDefault(
                    encodingName, LookupFlags.Default,
                    ref encoding, ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return null;
            }

            string password = null;
            string saltString = null;
            int iterations = 0;
            string hashAlgorithmName = null;
            string signature = null; /* NOT USED */

            try
            {
                localError = null;

                if (provider.GetData(
                        fileName, encodingName, ref password,
                        ref saltString, ref iterations,
                        ref hashAlgorithmName, ref signature,
                        ref localError) != ReturnCode.Ok) /* throw */
                {
                    error = localError;
                    return null;
                }
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }

            byte[] salt = encoding.GetBytes(saltString);

            localError = null;

            SymmetricAlgorithm algorithm = CreateSymmetricAlgorithm(
                symmetricAlgorithmName, password, salt, iterations,
                hashAlgorithmName, cipherMode, paddingMode,
                ref localError);

            if (algorithm == null)
            {
                error = localError;
                return null;
            }

            return algorithm;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a symmetric algorithm with the specified name and applies
        /// the best available key and block sizes.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to create.
        /// </param>
        /// <param name="algorithm">
        /// Upon success, receives the created symmetric algorithm.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        private static void CreateSpecificSymmetricAlgorithm(
            string symmetricAlgorithmName,    /* in: OPTIONAL */
            ref SymmetricAlgorithm algorithm, /* out */
            ref Result error                  /* out */
            )
        {
            if (Utility.IsDotNetCore())
            {
                try
                {
                    CreateSymmetricAlgorithmViaFactory(
                        symmetricAlgorithmName, ref algorithm,
                        ref error);

                    if (algorithm == null)
                        return;
                }
                catch (Exception e)
                {
                    error = e;
                    return;
                }
            }
            else
            {
                try
                {
                    algorithm = SymmetricAlgorithm.Create(
                        symmetricAlgorithmName);
                }
                catch (Exception e)
                {
                    error = e;
                    return;
                }
            }

            UseBestSizes(algorithm);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the default symmetric algorithm and applies the default
        /// key and block sizes.
        /// </summary>
        /// <param name="algorithm">
        /// Upon success, receives the created symmetric algorithm.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        private static void CreateDefaultSymmetricAlgorithm(
            ref SymmetricAlgorithm algorithm, /* out */
            ref Result error                  /* out */
            )
        {
            if (Utility.IsDotNetCore())
            {
                try
                {
                    CreateSymmetricAlgorithmViaFactory(
                        null, ref algorithm, ref error);

                    if (algorithm == null)
                        return;
                }
                catch (Exception e)
                {
                    error = e;
                    return;
                }
            }
            else
            {
                try
                {
                    algorithm = SymmetricAlgorithm.Create();
                }
                catch (Exception e)
                {
                    error = e;
                    return;
                }
            }

            UseDefaultSizes(algorithm);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a symmetric algorithm, using the named algorithm when
        /// specified or the default algorithm otherwise.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to create.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created symmetric algorithm, or null on failure.
        /// </returns>
        private static SymmetricAlgorithm CreateSymmetricAlgorithm(
            string symmetricAlgorithmName, /* in: OPTIONAL */
            ref Result error               /* out */
            )
        {
            SymmetricAlgorithm algorithm = null;
            Result localError; /* REUSED */

            if (symmetricAlgorithmName != null)
            {
                localError = null;

                CreateSpecificSymmetricAlgorithm(
                    symmetricAlgorithmName, ref algorithm,
                    ref localError);

                if (algorithm == null)
                {
                    if (localError != null)
                    {
                        error = localError;
                    }
                    else
                    {
                        error = String.Format(
                            "could not create specific " +
                            "symmetric algorithm {0}",
                            Utility.FormatWrapOrNull(
                            symmetricAlgorithmName));
                    }
                }
            }
            else
            {
                localError = null;

                CreateDefaultSymmetricAlgorithm(
                    ref algorithm, ref localError);

                if (algorithm == null)
                {
                    if (localError != null)
                    {
                        error = localError;
                    }
                    else
                    {
                        error = String.Format(
                            "could not create default " +
                            "symmetric algorithm {0}",
                            Utility.FormatWrapOrNull(
                            symmetricAlgorithmName));
                    }
                }
            }

            return algorithm;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the specified algorithm to use the greatest maximum key
        /// size and least minimum block size that it supports.
        /// </summary>
        /// <param name="algorithm">
        /// The symmetric algorithm to configure.
        /// </param>
        private static void UseBestSizes(
            SymmetricAlgorithm algorithm /* in */
            )
        {
            if (algorithm != null)
            {
                //
                // NOTE: *HACK* Maybe rely on the default key size,
                //       block size, and feedback size.  Hopefully,
                //       the defaults will be most secure possible.
                //
                int keySize = algorithm.KeySize;
                int blockSize = algorithm.BlockSize;

                bool[] found =
                    Utility.GetGreatestMaxKeySizeAndLeastMinBlockSize(
                        algorithm, ref keySize, ref blockSize);

                if (found != null)
                {
                    if ((found.Length >= 1) && found[0])
                        algorithm.KeySize = keySize;

                    if ((found.Length >= 2) && found[1])
                    {
                        algorithm.BlockSize = blockSize;
                        algorithm.FeedbackSize = blockSize;
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the specified algorithm to use the default key, block,
        /// and feedback sizes.
        /// </summary>
        /// <param name="algorithm">
        /// The symmetric algorithm to configure.
        /// </param>
        private static void UseDefaultSizes(
            SymmetricAlgorithm algorithm /* in */
            )
        {
            if (algorithm != null)
            {
                //
                // NOTE: *HACK: Use the "best" key sizes, block
                //       sizes, and feedback sizes that we know
                //       about.
                //
                algorithm.KeySize = Constants.DefaultKeyBits;
                algorithm.BlockSize = Constants.DefaultBlockBits;
                algorithm.FeedbackSize = Constants.DefaultFeedbackBits;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cipher mode, padding mode, initialization vector, and key
        /// on the specified symmetric algorithm.
        /// </summary>
        /// <param name="algorithm">
        /// The symmetric algorithm to configure.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to set.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to set.
        /// </param>
        /// <param name="iv">
        /// The initialization vector to set, if any.
        /// </param>
        /// <param name="key">
        /// The key to set, if any.
        /// </param>
        private static void SetModesAndIvAndKey(
            SymmetricAlgorithm algorithm, /* in */
            CipherMode cipherMode,        /* in */
            PaddingMode paddingMode,      /* in */
            byte[] iv,                    /* in */
            byte[] key                    /* in */
            )
        {
            if (algorithm != null)
            {
                algorithm.Mode = cipherMode;
                algorithm.Padding = paddingMode;

                if (iv != null)
                    algorithm.IV = iv;

                if (key != null)
                    algorithm.Key = key;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Derives the initialization vector and key for the specified
        /// algorithm from the supplied key-derivation object.
        /// </summary>
        /// <param name="algorithm">
        /// The symmetric algorithm whose block and key sizes determine the
        /// amount of derived material.
        /// </param>
        /// <param name="deriveBytes">
        /// The key-derivation object from which to obtain bytes.
        /// </param>
        /// <param name="iv">
        /// Receives the derived initialization vector.
        /// </param>
        /// <param name="key">
        /// Receives the derived key.
        /// </param>
        private static void GetIvAndKeyViaDeriveBytes(
            SymmetricAlgorithm algorithm, /* in */
            DeriveBytes deriveBytes,      /* in */
            ref byte[] iv,                /* out */
            ref byte[] key                /* out */
            )
        {
            //
            // NOTE: This code (obviously?) assumes there are
            //       exactly eight bits per byte.
            //
            if ((algorithm == null) || (deriveBytes == null))
                return;

            int blockBytes = /* bits */ algorithm.BlockSize / 8;
            int keyBytes = /* bits */ algorithm.KeySize / 8;

            iv = deriveBytes.GetBytes(blockBytes);
            key = deriveBytes.GetBytes(keyBytes);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and fully configures a symmetric algorithm using a password
        /// and salt to derive the initialization vector and key.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to create.
        /// </param>
        /// <param name="password">
        /// The password from which to derive key material.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use during key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created symmetric algorithm, or null on failure.
        /// </returns>
        private static SymmetricAlgorithm CreateSymmetricAlgorithm(
            string symmetricAlgorithmName, /* in: OPTIONAL */
            string password,               /* in */
            byte[] salt,                   /* in */
            int iterations,                /* in */
            string hashAlgorithmName,      /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            ref Result error               /* out */
            )
        {
            SymmetricAlgorithm algorithm = CreateSymmetricAlgorithm(
                symmetricAlgorithmName, ref error);

            if (algorithm == null)
                return null;

            iterations = MaybeMutateIterations(iterations);

            DeriveBytes deriveBytes = CreateDeriveBytes(
                password, salt, iterations, hashAlgorithmName);

            byte[] iv = null;
            byte[] key = null;

            GetIvAndKeyViaDeriveBytes(
                algorithm, deriveBytes, ref iv, ref key);

            SetModesAndIvAndKey(
                algorithm, cipherMode, paddingMode, iv, key);

            return algorithm;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Creates and configures a symmetric algorithm using an explicit
        /// initialization vector and key.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to create.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="iv">
        /// The initialization vector to use.
        /// </param>
        /// <param name="key">
        /// The key to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created symmetric algorithm, or null on failure.
        /// </returns>
        private static SymmetricAlgorithm CreateSymmetricAlgorithm(
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] iv,                     /* in */
            byte[] key,                    /* in */
            ref Result error               /* out */
            )
        {
            SymmetricAlgorithm algorithm = CreateSymmetricAlgorithm(
                symmetricAlgorithmName, ref error);

            if (algorithm == null)
                return null;

            SetModesAndIvAndKey(
                algorithm, cipherMode, paddingMode, iv, key);

            return algorithm;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs an encryption or decryption transform on the specified
        /// data using the given symmetric algorithm.
        /// </summary>
        /// <param name="algorithm">
        /// The symmetric algorithm used to create the transform.
        /// </param>
        /// <param name="data">
        /// The data to transform.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the data; zero to decrypt it.
        /// </param>
        /// <returns>
        /// The transformed data.
        /// </returns>
        private static byte[] PerformTransform(
            SymmetricAlgorithm algorithm, /* in */
            byte[] data,                  /* in */
            bool encrypt                  /* in */
            )
        {
            using (ICryptoTransform cryptoTransform = encrypt ?
                    algorithm.CreateEncryptor() : algorithm.CreateDecryptor())
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(
                            memoryStream, cryptoTransform,
                            CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                    }

                    return memoryStream.ToArray();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if XML
        /// <summary>
        /// Initializes the cryptographic parameters to their default values.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// Receives the default symmetric algorithm name.
        /// </param>
        /// <param name="password">
        /// Receives the default password.
        /// </param>
        /// <param name="salt">
        /// Receives the default salt.
        /// </param>
        /// <param name="iterations">
        /// Receives the default iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the default hash algorithm name.
        /// </param>
        /// <param name="cipherMode">
        /// Receives the default cipher mode.
        /// </param>
        /// <param name="paddingMode">
        /// Receives the default padding mode.
        /// </param>
        /// <param name="oldData">
        /// Receives the default (null) source data.
        /// </param>
        public static void InitializeParameters(
            out string symmetricAlgorithmName, /* out */
            out string password,               /* out */
            out byte[] salt,                   /* out */
            out int iterations,                /* out */
            out string hashAlgorithmName,      /* out */
            out CipherMode cipherMode,         /* out */
            out PaddingMode paddingMode,       /* out */
            out byte[] oldData                 /* out */
            )
        {
            symmetricAlgorithmName = null;
            password = null;
            salt = null;
            iterations = Constants.DefaultIterations;
            hashAlgorithmName = null;
            cipherMode = Constants.DefaultCipherMode;
            paddingMode = Constants.DefaultPaddingMode;
            oldData = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the cryptographic parameters and source data from the
        /// specified encoded value, optionally validating the data version
        /// header.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter providing context for the operation.
        /// </param>
        /// <param name="value">
        /// The encoded value from which to extract parameters.
        /// </param>
        /// <param name="encoding">
        /// The text encoding (not used by this method).
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used to parse numeric values.
        /// </param>
        /// <param name="checkDataHeader">
        /// Non-zero to validate the encrypted data version header.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// On input, the current symmetric algorithm name; on output, the
        /// extracted value when present.
        /// </param>
        /// <param name="password">
        /// On input, the current password; on output, the extracted value
        /// when present.
        /// </param>
        /// <param name="salt">
        /// On input, the current salt; on output, the extracted value when
        /// present.
        /// </param>
        /// <param name="iterations">
        /// On input, the current iteration count; on output, the extracted
        /// value when present.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, the current hash algorithm name; on output, the
        /// extracted value when present.
        /// </param>
        /// <param name="cipherMode">
        /// On input, the current cipher mode; on output, the extracted value
        /// when present.
        /// </param>
        /// <param name="paddingMode">
        /// On input, the current padding mode; on output, the extracted value
        /// when present.
        /// </param>
        /// <param name="oldData">
        /// Receives the extracted source data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode ExtractParameters(
            Interpreter interpreter,           /* in: OPTIONAL */
            string value,                      /* in */
            Encoding encoding,                 /* in: NOT USED */
            CultureInfo cultureInfo,           /* in: OPTIONAL */
            bool checkDataHeader,              /* in */
            ref string symmetricAlgorithmName, /* in, out */
            ref string password,               /* in, out */
            ref byte[] salt,                   /* in, out */
            ref int iterations,                /* in, out */
            ref string hashAlgorithmName,      /* in, out */
            ref CipherMode cipherMode,         /* in, out */
            ref PaddingMode paddingMode,       /* in, out */
            ref byte[] oldData,                /* out */
            ref Result error                   /* out */
            )
        {
            StringDictionary dictionary = null;
            string text = null;

            if (CertificateDataOps.ExtractParameters(
                    value, ref dictionary, ref text,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (text == null)
            {
                error = "invalid old text";
                return ReturnCode.Error;
            }

            string dataVersion = null;

            if (checkDataHeader && ((dictionary == null) ||
                !dictionary.TryGetValue(
                    Constants.EncryptedDataVersionName,
                    out dataVersion) ||
                !CertificateDataOps.StringEquals(dataVersion,
                    Constants.EncryptedDataVersionValue)))
            {
                error = String.Format(
                    "unrecognized encrypted data version {0}",
                    Utility.FormatWrapOrNull(dataVersion));

                return ReturnCode.Error;
            }

            byte[] localOldData;

            try
            {
                localOldData = Convert.FromBase64String(
                    text); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }

            string localSymmetricAlgorithmName = symmetricAlgorithmName;
            string localPassword = password;
            byte[] localSalt = salt;
            int localIterations = iterations;
            string localHashAlgorithmName = hashAlgorithmName;
            CipherMode localCipherMode = cipherMode;
            PaddingMode localPaddingMode = paddingMode;

            if (dictionary != null)
            {
                string parameterValue; /* REUSED */
                object enumValue; /* REUSED */

                ///////////////////////////////////////////////////////////////

                if (AllowOnlyFipsAlgorithms())
                {
                    localSymmetricAlgorithmName =
                        GetDefaultSymmetricAlgorithmName(true);
                }
                else
                {
                    if (dictionary.TryGetValue(
                            "symmetricAlgorithmName", out parameterValue))
                    {
                        localSymmetricAlgorithmName = parameterValue;
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "password", out parameterValue))
                {
                    try
                    {
                        localPassword = CertificateDataOps.GetRawString(
                            Convert.FromBase64String(parameterValue)); /* throw */
                    }
                    catch (Exception e)
                    {
                        error = e;
                        return ReturnCode.Error;
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "salt", out parameterValue))
                {
                    try
                    {
                        localSalt = Convert.FromBase64String(
                            parameterValue); /* throw */
                    }
                    catch (Exception e)
                    {
                        error = e;
                        return ReturnCode.Error;
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "iterations", out parameterValue))
                {
                    if (Value.GetInteger2(
                            parameterValue, ValueFlags.AnyInteger,
                            cultureInfo, ref localIterations,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "hashAlgorithmName", out parameterValue))
                {
                    localHashAlgorithmName = parameterValue;
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "cipherMode", out parameterValue))
                {
                    enumValue = Utility.TryParseEnum(
                        typeof(CipherMode), parameterValue, true,
                        true, ref error);

                    if (!(enumValue is CipherMode))
                        return ReturnCode.Error;

                    localCipherMode = (CipherMode)enumValue;
                }

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "paddingMode", out parameterValue))
                {
                    enumValue = Utility.TryParseEnum(
                        typeof(PaddingMode), parameterValue, true,
                        true, ref error);

                    if (!(enumValue is PaddingMode))
                        return ReturnCode.Error;

                    localPaddingMode = (PaddingMode)enumValue;
                }
            }

            ///////////////////////////////////////////////////////////////////

            symmetricAlgorithmName = localSymmetricAlgorithmName;
            password = localPassword;
            salt = localSalt;
            iterations = localIterations;
            hashAlgorithmName = localHashAlgorithmName;
            cipherMode = localCipherMode;
            paddingMode = localPaddingMode;
            oldData = localOldData;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the public key token and source data from the specified
        /// encoded value, optionally validating the data version header.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter (not used by this method).
        /// </param>
        /// <param name="value">
        /// The encoded value from which to extract data.
        /// </param>
        /// <param name="encoding">
        /// The text encoding (not used by this method).
        /// </param>
        /// <param name="checkDataHeader">
        /// Non-zero to validate the encrypted data version header.
        /// </param>
        /// <param name="publicKeyToken">
        /// On input, the current public key token; on output, the extracted
        /// value when present.
        /// </param>
        /// <param name="oldData">
        /// Receives the extracted source data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode ExtractParameters(
            Interpreter interpreter,   /* in: NOT USED */
            string value,              /* in */
            Encoding encoding,         /* in: NOT USED */
            bool checkDataHeader,      /* in */
            ref byte[] publicKeyToken, /* in, out */
            ref byte[] oldData,        /* out */
            ref Result error           /* out */
            )
        {
            StringDictionary dictionary = null;
            string text = null;

            if (CertificateDataOps.ExtractParameters(
                    value, ref dictionary, ref text,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (text == null)
            {
                error = "invalid old text";
                return ReturnCode.Error;
            }

            string dataVersion = null;

            if (checkDataHeader && ((dictionary == null) ||
                !dictionary.TryGetValue(
                    Constants.EncryptedDataVersionName,
                    out dataVersion) ||
                !CertificateDataOps.StringEquals(dataVersion,
                    Constants.EncryptedDataVersionValue)))
            {
                error = String.Format(
                    "unrecognized encrypted data version {0}",
                    Utility.FormatWrapOrNull(dataVersion));

                return ReturnCode.Error;
            }

            byte[] localOldData;

            try
            {
                localOldData = Convert.FromBase64String(
                    text); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }

            byte[] localPublicKeyToken = publicKeyToken;

            if (dictionary != null)
            {
                string parameterValue; /* REUSED */

                ///////////////////////////////////////////////////////////////

                if (dictionary.TryGetValue(
                        "publicKeyToken", out parameterValue))
                {
                    if (CertificateDataOps.ParsePublicKeyToken(
                            parameterValue, ref localPublicKeyToken,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            publicKeyToken = localPublicKeyToken;
            oldData = localOldData;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified salt bytes into a <see cref="Guid" />
        /// value.
        /// </summary>
        /// <param name="salt">
        /// The salt bytes to convert.
        /// </param>
        /// <param name="saltGuid">
        /// Receives the resulting <see cref="Guid" /> value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetGuidFromSalt(
            byte[] salt,       /* in */
            out Guid saltGuid, /* out */
            ref Result error   /* out */
            )
        {
            saltGuid = default(Guid);

            if (salt == null)
            {
                error = "password and salt are missing";
                return ReturnCode.Error;
            }

            int saltLength = salt.Length;

            if (saltLength != Marshal.SizeOf(typeof(Guid)))
            {
                error = String.Format(
                    "password missing, bad salt length {0}",
                    saltLength);

                return ReturnCode.Error;
            }

            try
            {
                saltGuid = new Guid(salt); /* throw */
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a cached password from the plugin application domain
        /// using the specified salt.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data identifying the application domain.
        /// </param>
        /// <param name="salt">
        /// The salt used to compute the lookup key.
        /// </param>
        /// <param name="password">
        /// Receives the retrieved password.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetPasswordViaAppDomainAndSalt(
            IPluginData pluginData, /* in: OPTIONAL */
            byte[] salt,            /* in */
            ref string password,    /* out */
            ref Result result       /* out */
            )
        {
            Guid saltGuid;

            if (GetGuidFromSalt(
                    salt, out saltGuid, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                AppDomain appDomain = SharedOps.GetAppDomainFromPlugin(
                    pluginData);

                if (appDomain == null)
                {
                    result = "password missing, no application domain";
                    return ReturnCode.Error;
                }

                string localPassword = appDomain.GetData(String.Format(
                    Constants.GetDataFormat, saltGuid.ToString(),
                    Utility.GetCurrentProcessId())) as string;

                result = String.Format(
                    "fetched password of length {0} from " +
                    "application domain {1} with salt {2}",
                    (localPassword != null) ?
                        localPassword.Length : Length.Invalid,
                    Utility.FormatWrapOrNull(appDomain.Id),
                    Utility.FormatWrapOrNull(saltGuid));

                password = localPassword;
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a password from the process or parent process
        /// environment using the specified salt.
        /// </summary>
        /// <param name="salt">
        /// The salt used to compute the environment variable name.
        /// </param>
        /// <param name="password">
        /// Receives the retrieved password.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetPasswordViaEnvironmentAndSalt(
            byte[] salt,         /* in */
            ref string password, /* out */
            ref Result result    /* out */
            )
        {
            Guid saltGuid;

            if (GetGuidFromSalt(
                    salt, out saltGuid, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            long[] processIds = {
                Utility.GetCurrentProcessId(),
                Utility.GetParentProcessId()
            };

            string localPassword = null;
            long localProcessId = 0;

            foreach (long processId in processIds)
            {
                if (processId == 0)
                    continue;

                localPassword = Utility.GetEnvironmentVariable(
                    String.Format(Constants.GetDataFormat,
                    saltGuid.ToString(), processId));

                if (localPassword != null)
                {
                    localProcessId = processId;
                    break;
                }
            }

            result = String.Format(
                "fetched password of length {0} from " +
                "process {1} environment with salt {2}",
                (localPassword != null) ?
                    localPassword.Length : Length.Invalid,
                Utility.FormatWrapOrNull(localProcessId),
                Utility.FormatWrapOrNull(saltGuid));

            password = localPassword;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Caches a password in the plugin application domain using the
        /// specified salt.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data identifying the application domain.
        /// </param>
        /// <param name="salt">
        /// The salt used to compute the storage key.
        /// </param>
        /// <param name="password">
        /// The password to cache.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode SetPasswordViaAppDomainAndSalt(
            IPluginData pluginData, /* in: OPTIONAL */
            byte[] salt,            /* in */
            string password,        /* in */
            ref Result error        /* out */
            )
        {
            Guid saltGuid;

            if (GetGuidFromSalt(
                    salt, out saltGuid, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                AppDomain appDomain = SharedOps.GetAppDomainFromPlugin(
                    pluginData);

                if (appDomain == null)
                {
                    error = "cannot cache, no application domain";
                    return ReturnCode.Error;
                }

                appDomain.SetData(String.Format(
                    Constants.GetDataFormat, saltGuid.ToString(),
                    Utility.GetCurrentProcessId()), password);

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if NETWORK && WEB
        /// <summary>
        /// Retrieves a password from the specified URI using the given salt
        /// and caches the result in the application domain.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to make the request.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data identifying the application domain used
        /// for caching.
        /// </param>
        /// <param name="uri">
        /// The URI from which to retrieve the password.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding for the request.
        /// </param>
        /// <param name="salt">
        /// The salt identifying the password to retrieve.
        /// </param>
        /// <param name="timeout">
        /// The optional request timeout, in milliseconds.
        /// </param>
        /// <param name="password">
        /// Receives the retrieved password.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetPasswordViaUriAndSalt(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            Uri uri,                 /* in */
            Encoding encoding,       /* in: OPTIONAL */
            byte[] salt,             /* in */
            int? timeout,            /* in: OPTIONAL */
            ref string password,     /* out */
            ref Result result        /* out */
            )
        {
            Guid saltGuid;

            if (GetGuidFromSalt(
                    salt, out saltGuid, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringDictionary input = new StringDictionary();

            input.Add("method", "lookup");
            input.Add("id", saltGuid.ToString());
            input.Add("raw", 1.ToString());

            string text = null;

            if (Helpers.MakeUriRequest(
                    interpreter, uri, null, null, input,
                    encoding, null, timeout, false, false,
                    ref text, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringList list = null;

            if (Parser.SplitList(
                    interpreter, text, 0, Length.Invalid, true,
                    ref list, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int count = list.Count;

            if (count < 2)
            {
                result = "wrong length for password list";
                return ReturnCode.Error;
            }

            if (CertificateDataOps.StringEquals(
                    list[0], Constants.ErrorResult))
            {
                if (!String.IsNullOrEmpty(list[1]))
                    result = list[1];
                else
                    result = "unknown server error";

                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[0], Constants.OkResult))
            {
                result = "bad password result code";
                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[1], saltGuid.ToString()))
            {
                result = "mismatched identifier for password";
                return ReturnCode.Error;
            }

            string localPassword = (count >= 3) ? list[2] : null;

            if (SetPasswordViaAppDomainAndSalt( /* CACHE */
                    pluginData, salt, localPassword,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            result = String.Format(
                "fetched password of length {0} from " +
                "URI {1} with salt {2}",
                (localPassword != null) ?
                    localPassword.Length : Length.Invalid,
                Utility.FormatWrapOrNull(uri),
                Utility.FormatWrapOrNull(saltGuid));

            password = localPassword;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Inserts a secret on the server identified by the specified URI
        /// using the supplied API credentials and RFC 2898 data.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to make the request.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data (not used by this method).
        /// </param>
        /// <param name="uri">
        /// The URI of the server that stores the secret.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used to parse the server response.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding for the request.
        /// </param>
        /// <param name="apiKey">
        /// The API key used to authenticate the request.
        /// </param>
        /// <param name="apiId">
        /// The API identifier used to locate the secret.
        /// </param>
        /// <param name="rfc2898Data">
        /// The RFC 2898 data describing the secret to insert.
        /// </param>
        /// <param name="timeout">
        /// The optional request timeout, in milliseconds.
        /// </param>
        /// <param name="encrypted">
        /// Non-zero if the secret data is encrypted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode InsertSecretViaUriAndSalt(
            Interpreter interpreter,  /* in: OPTIONAL */
            IPluginData pluginData,   /* in: NOT USED */
            Uri uri,                  /* in */
            CultureInfo cultureInfo,  /* in: OPTIONAL */
            Encoding encoding,        /* in: OPTIONAL */
            byte[] apiKey,            /* in */
            byte[] apiId,             /* in */
            IRfc2898Data rfc2898Data, /* in */
            int? timeout,             /* in: OPTIONAL */
            bool encrypted,           /* in */
            ref Result error          /* out */
            )
        {
            Guid apiIdGuid;

            if (GetGuidFromSalt(
                    apiId, out apiIdGuid, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string password;
            string salt;
            int iterationCount;
            string hashAlgorithmName;
            string signature;

            if (SecretOps.ExtractData(
                    rfc2898Data, true, out password, out salt,
                    out iterationCount, out hashAlgorithmName,
                    out signature, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringDictionary input = new StringDictionary();

            input.Add("method", "insert");
            input.Add("id", apiIdGuid.ToString());
            input.Add("raw", 1.ToString());

            input.Add("apiKey",
                CertificateDataOps.FormatHexadecimal(apiKey, false));

            SecretDataFlags flags = SecretDataFlags.Default;

            flags |= SecretDataFlags.ProtocolV1R0;

            if (encrypted)
                flags |= SecretDataFlags.Encrypted;

            if (signature != null)
                flags |= SecretDataFlags.Signed;

            input.Add("flags", flags.ToString());
            input.Add("password", password);
            input.Add("salt", salt);
            input.Add("iterationCount", iterationCount.ToString());
            input.Add("hashAlgorithmName", hashAlgorithmName);
            input.Add("signature", signature);

            string text = null;

            if (Helpers.MakeUriRequest(
                    interpreter, uri, null, Constants.PostMethod,
                    input, encoding, null, timeout, false, false,
                    ref text, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringList list = null;

            if (Parser.SplitList(
                    interpreter, text, 0, Length.Invalid, true,
                    ref list, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int count = list.Count;

            if (count < 2)
            {
                error = "wrong length for secret list";
                return ReturnCode.Error;
            }

            if (CertificateDataOps.StringEquals(
                    list[0], Constants.ErrorResult))
            {
                if (!String.IsNullOrEmpty(list[1]))
                    error = list[1];
                else
                    error = "unknown server error";

                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[0], Constants.OkResult))
            {
                error = "bad secret result code";
                return ReturnCode.Error;
            }

            long sequenceNumber = 0;

            if (Value.GetWideInteger2(
                    list[1], ValueFlags.AnyInteger, cultureInfo,
                    ref sequenceNumber, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (sequenceNumber == 0)
            {
                error = "new sequence number cannot be zero";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes a secret on the server identified by the specified URI
        /// using the supplied API credentials.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to make the request.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data (not used by this method).
        /// </param>
        /// <param name="uri">
        /// The URI of the server that stores the secret.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding for the request.
        /// </param>
        /// <param name="apiKey">
        /// The API key used to authenticate the request.
        /// </param>
        /// <param name="apiId">
        /// The API identifier used to locate the secret.
        /// </param>
        /// <param name="timeout">
        /// The optional request timeout, in milliseconds.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode DeleteSecretViaUriAndSalt(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: NOT USED */
            Uri uri,                 /* in */
            Encoding encoding,       /* in: OPTIONAL */
            byte[] apiKey,           /* in */
            byte[] apiId,            /* in */
            int? timeout,            /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            Guid apiIdGuid;

            if (GetGuidFromSalt(
                    apiId, out apiIdGuid, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringDictionary input = new StringDictionary();

            input.Add("method", "delete");
            input.Add("id", apiIdGuid.ToString());
            input.Add("raw", 1.ToString());

            input.Add("apiKey",
                CertificateDataOps.FormatHexadecimal(apiKey, false));

            string text = null;

            if (Helpers.MakeUriRequest(
                    interpreter, uri, null, null, input,
                    encoding, null, timeout, false, false,
                    ref text, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringList list = null;

            if (Parser.SplitList(
                    interpreter, text, 0, Length.Invalid, true,
                    ref list, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int count = list.Count;

            if (count < 2)
            {
                error = "wrong length for secret list";
                return ReturnCode.Error;
            }

            if (CertificateDataOps.StringEquals(
                    list[0], Constants.ErrorResult))
            {
                if (!String.IsNullOrEmpty(list[1]))
                    error = list[1];
                else
                    error = "unknown server error";

                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[0], Constants.OkResult))
            {
                error = "bad secret result code";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a secret from the server identified by the specified URI
        /// using the supplied API credentials.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to make the request.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data (not used by this method).
        /// </param>
        /// <param name="uri">
        /// The URI of the server that stores the secret.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used to parse the server response.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding for the request.
        /// </param>
        /// <param name="apiKey">
        /// The API key used to authenticate the request.
        /// </param>
        /// <param name="apiId">
        /// The API identifier used to locate the secret.
        /// </param>
        /// <param name="timeout">
        /// The optional request timeout, in milliseconds.
        /// </param>
        /// <param name="secretData">
        /// Receives the retrieved secret data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetSecretViaUriAndSalt(
            Interpreter interpreter,    /* in: OPTIONAL */
            IPluginData pluginData,     /* in: NOT USED */
            Uri uri,                    /* in */
            CultureInfo cultureInfo,    /* in: OPTIONAL */
            Encoding encoding,          /* in: OPTIONAL */
            byte[] apiKey,              /* in */
            byte[] apiId,               /* in */
            int? timeout,               /* in: OPTIONAL */
            ref ISecretData secretData, /* out */
            ref Result error            /* out */
            )
        {
            Guid apiIdGuid;

            if (GetGuidFromSalt(
                    apiId, out apiIdGuid, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringDictionary input = new StringDictionary();

            input.Add("method", "lookup");
            input.Add("id", apiIdGuid.ToString());
            input.Add("raw", 1.ToString());

            input.Add("apiKey",
                CertificateDataOps.FormatHexadecimal(apiKey, false));

            string text = null;

            if (Helpers.MakeUriRequest(
                    interpreter, uri, null, null, input,
                    encoding, null, timeout, false, false,
                    ref text, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringList list = null;

            if (Parser.SplitList(
                    interpreter, text, 0, Length.Invalid, true,
                    ref list, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int count = list.Count;

            if ((count < 2) || ((count % 2) != 0))
            {
                error = "wrong length for secret list";
                return ReturnCode.Error;
            }

            if (CertificateDataOps.StringEquals(
                    list[0], Constants.ErrorResult))
            {
                if (!String.IsNullOrEmpty(list[1]))
                    error = list[1];
                else
                    error = "unknown server error";

                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[0], Constants.OkResult))
            {
                error = "bad secret result code";
                return ReturnCode.Error;
            }

            if (!CertificateDataOps.StringEquals(
                    list[1], apiIdGuid.ToString()))
            {
                error = "mismatched identifier for secret";
                return ReturnCode.Error;
            }

            StringDictionary output = new StringDictionary();

            for (int index = 2; index < count; index += 2)
            {
                if (String.IsNullOrEmpty(list[index]))
                    continue;

                output[list[index]] = list[index + 1];
            }

            output["id"] = list[1];

            SecretData localSecretData = new SecretData();

            if (SecretOps.SetData(output, cultureInfo,
                    localSecretData as IIdentifier,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (SecretOps.SetData(output, cultureInfo,
                    localSecretData as IHaveEncoding,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (SecretOps.SetData(output, cultureInfo,
                    localSecretData as IRfc2898Data,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (SecretOps.SetData(output, cultureInfo,
                    localSecretData as ICryptographyData,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (SecretOps.SetData(output, cultureInfo,
                    localSecretData as ISecretData,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (!Utility.HasFlags(localSecretData.Flags,
                    SecretDataFlags.ProtocolV1R0, true))
            {
                error = "unsupported secret data protocol";
                return ReturnCode.Error;
            }

            secretData = localSecretData;
            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts cryptographic parameters from the specified text, locates
        /// the password through the available sources, and decrypts the
        /// associated data.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter providing context for the operation.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data identifying the application domain.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding to use.
        /// </param>
        /// <param name="fileName">
        /// The name of the file associated with the data.
        /// </param>
        /// <param name="text">
        /// The encoded text from which to extract parameters and data.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used to parse numeric values.
        /// </param>
        /// <param name="timeout">
        /// The optional request timeout, in milliseconds.
        /// </param>
        /// <param name="traceOnError">
        /// Non-zero to emit trace output when an error occurs.
        /// </param>
        /// <param name="newData">
        /// Receives the decrypted data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode ObtainParametersAndDecrypt(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            Encoding encoding,       /* in: OPTIONAL */
            string fileName,         /* in */
            string text,             /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            int? timeout,            /* in: OPTIONAL */
            bool traceOnError,       /* in */
            ref byte[] newData,      /* out */
            ref Result error         /* out */
            )
        {
            string symmetricAlgorithmName;
            string password;
            byte[] salt;
            int iterations;
            string hashAlgorithmName;
            CipherMode cipherMode;
            PaddingMode paddingMode;
            byte[] oldData;

            ///////////////////////////////////////////////////////////////////

            /* NO RESULT */
            InitializeParameters(
                out symmetricAlgorithmName, out password, out salt,
                out iterations, out hashAlgorithmName, out cipherMode,
                out paddingMode, out oldData);

            ///////////////////////////////////////////////////////////////////

            Result localResult; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            localResult = null;

            if (ExtractParameters(
                    interpreter, text, encoding, cultureInfo, true,
                    ref symmetricAlgorithmName, ref password,
                    ref salt, ref iterations, ref hashAlgorithmName,
                    ref cipherMode, ref paddingMode, ref oldData,
                    ref localResult) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                if (traceOnError)
                {
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "ObtainParametersAndDecrypt: " +
                            "could not extract parameters from " +
                            "file name {0}: {1}",
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(localResult)),
                        typeof(CryptographyOps).Name,
                        TracePriority.Highest, 0);
                }
#endif

                error = localResult;
                return ReturnCode.Error;
            }
#if DEBUG || FORCE_TRACE
            else
            {
                if (traceOnError)
                {
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "ObtainParametersAndDecrypt: " +
                            "fetched password of length {0} " +
                            "from file name {1}", (password != null) ?
                                password.Length : Length.Invalid,
                            Utility.FormatWrapOrNull(fileName)),
                        typeof(CryptographyOps).Name,
                        TracePriority.Highest, 0);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: If password was not explicitly specified in the encrypted
            //       file, attempt to find via the AppDomain, using the salt
            //       specified as part of the GetData lookup.  The salt must
            //       be exactly sixteen bytes, which will be used to create
            //       a Guid instance, presumably with a value of the original
            //       unencrypted certificate identifier.  Finally, a formatted
            //       Guid string and (current) process identifier will be used
            //       to lookup the password within the current AppDomain.
            //
            if (password == null)
            {
                localResult = null;

#if DEBUG || FORCE_TRACE
                try
                {
#endif
                    if (GetPasswordViaAppDomainAndSalt(
                            pluginData, salt, ref password,
                            ref localResult) != ReturnCode.Ok)
                    {
                        error = localResult;
                        return ReturnCode.Error;
                    }
#if DEBUG || FORCE_TRACE
                }
                finally
                {
                    if (traceOnError)
                    {
                        CertificateTraceOps.MaybeLogAndDebugTrace(
                            String.Format(
                                "ObtainParametersAndDecrypt: " +
                                "via appDomain, {0}",
                                /* USE VERBATIM */ localResult),
                            typeof(CryptographyOps).Name,
                            TracePriority.Highest, 0);
                    }
                }
#endif
            }

            ///////////////////////////////////////////////////////////////////

            if (password == null)
            {
                localResult = null;

#if DEBUG || FORCE_TRACE
                try
                {
#endif
                    if (GetPasswordViaEnvironmentAndSalt(
                            salt, ref password,
                            ref localResult) != ReturnCode.Ok)
                    {
                        error = localResult;
                        return ReturnCode.Error;
                    }
#if DEBUG || FORCE_TRACE
                }
                finally
                {
                    if (traceOnError)
                    {
                        CertificateTraceOps.MaybeLogAndDebugTrace(
                            String.Format(
                                "ObtainParametersAndDecrypt: " +
                                "via environment, {0}",
                                /* USE VERBATIM */ localResult),
                            typeof(CryptographyOps).Name,
                            TracePriority.Highest, 0);
                    }
                }
#endif
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK && WEB
            if ((password == null) && Configuration.DoesVariableExist(
                    Constants.UseRemotePasswordsEnvVarName))
            {
                Uri uri = Utility.GetAssemblyUri(
                    CertificateAssemblyOps.GetObject(),
                    Constants.PasswordUriName);

                if (uri != null)
                {
                    localResult = null;

#if DEBUG || FORCE_TRACE
                    try
                    {
#endif
                        if (GetPasswordViaUriAndSalt(
                                interpreter, pluginData, uri,
                                encoding, salt, timeout, ref password,
                                ref localResult) != ReturnCode.Ok)
                        {
                            error = localResult;
                            return ReturnCode.Error;
                        }
#if DEBUG || FORCE_TRACE
                    }
                    finally
                    {
                        if (traceOnError)
                        {
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                String.Format(
                                    "ObtainParametersAndDecrypt: " +
                                    "via remote {0}",
                                    /* USE VERBATIM */ localResult),
                                typeof(CryptographyOps).Name,
                                TracePriority.Highest, 0);
                        }
                    }
#endif
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            localResult = null;

            if (EncryptOrDecrypt(
                    symmetricAlgorithmName, password, salt, iterations,
                    hashAlgorithmName, cipherMode, paddingMode, oldData,
                    false, ref newData, ref localResult) != ReturnCode.Ok)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Derives a sequence of bytes from the specified password and salt
        /// using the given iteration count and optional hash algorithm.
        /// </summary>
        /// <param name="password">
        /// The password from which to derive bytes.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm to use.
        /// </param>
        /// <param name="count">
        /// The optional number of bytes to derive; the default count is used
        /// when not specified.
        /// </param>
        /// <param name="bytes">
        /// Receives the derived bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode DeriveBytes(
            string password,          /* in */
            byte[] salt,              /* in */
            int iterations,           /* in */
            string hashAlgorithmName, /* in: OPTIONAL */
            int? count,               /* in: OPTIONAL */
            ref byte[] bytes,         /* out */
            ref Result error          /* out */
            )
        {
            try
            {
                iterations = MaybeMutateIterations(iterations);

                DeriveBytes deriveBytes = CreateDeriveBytes(
                    password, salt, iterations, hashAlgorithmName);

                bytes = deriveBytes.GetBytes(
                    (count != null) ? (int)count :
                    Constants.DefaultDeriveBytes);

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the specified data using a symmetric algorithm
        /// keyed from the given password and salt.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="password">
        /// The password from which to derive key material.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use during key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to encrypt or decrypt.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the data; zero to decrypt it.
        /// </param>
        /// <param name="newData">
        /// Receives the transformed data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode EncryptOrDecrypt(
            string symmetricAlgorithmName, /* in: OPTIONAL */
            string password,               /* in */
            byte[] salt,                   /* in */
            int iterations,                /* in */
            string hashAlgorithmName,      /* in */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] oldData,                /* in */
            bool encrypt,                  /* in */
            ref byte[] newData,            /* out */
            ref Result error               /* out */
            )
        {
            if (password == null)
            {
                error = "invalid password";
                return ReturnCode.Error;
            }

            if (salt == null)
            {
                error = "invalid salt";
                return ReturnCode.Error;
            }

            if (salt.Length < Constants.MinimumSaltBytes)
            {
                error = String.Format(
                    "not enough salt, need {0} bytes",
                    Constants.MinimumSaltBytes);

                return ReturnCode.Error;
            }

            if (oldData == null)
            {
                error = "invalid old data";
                return ReturnCode.Error;
            }

            try
            {
                using (SymmetricAlgorithm localAlgorithm =
                    CreateSymmetricAlgorithm(
                        symmetricAlgorithmName, password, salt,
                        iterations, hashAlgorithmName,
                        cipherMode, paddingMode, ref error))
                {
                    if (localAlgorithm == null)
                        return ReturnCode.Error;

                    newData = PerformTransform(
                        localAlgorithm, oldData, encrypt);

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Encrypts or decrypts the specified data using a symmetric algorithm
        /// configured with an explicit initialization vector and key.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="iv">
        /// The initialization vector to use.
        /// </param>
        /// <param name="key">
        /// The key to use.
        /// </param>
        /// <param name="oldData">
        /// The data to encrypt or decrypt.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the data; zero to decrypt it.
        /// </param>
        /// <param name="newData">
        /// Receives the transformed data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode EncryptOrDecrypt(
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] iv,                     /* in */
            byte[] key,                    /* in */
            byte[] oldData,                /* in */
            bool encrypt,                  /* in */
            ref byte[] newData,            /* out */
            ref Result error               /* out */
            )
        {
            if (iv == null)
            {
                error = "invalid iv";
                return ReturnCode.Error;
            }

            if (key == null)
            {
                error = "invalid key";
                return ReturnCode.Error;
            }

            if (oldData == null)
            {
                error = "invalid old data";
                return ReturnCode.Error;
            }

            try
            {
                using (SymmetricAlgorithm localAlgorithm =
                    CreateSymmetricAlgorithm(
                        symmetricAlgorithmName, cipherMode,
                        paddingMode, iv, key, ref error))
                {
                    if (localAlgorithm == null)
                        return ReturnCode.Error;

                    newData = PerformTransform(
                        localAlgorithm, oldData, encrypt);

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the specified data using a symmetric algorithm
        /// configured from the given RFC 2898 data provider.
        /// </summary>
        /// <param name="provider">
        /// The RFC 2898 data provider used to obtain key material.
        /// </param>
        /// <param name="fileName">
        /// The name of the file associated with the data.
        /// </param>
        /// <param name="encodingName">
        /// The name of the text encoding to use.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to encrypt or decrypt.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the data; zero to decrypt it.
        /// </param>
        /// <param name="newData">
        /// Receives the transformed data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode EncryptOrDecrypt(
            IRfc2898DataProvider provider, /* in */
            string fileName,               /* in */
            string encodingName,           /* in */
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] oldData,                /* in */
            bool encrypt,                  /* in */
            ref byte[] newData,            /* out */
            ref Result error               /* out */
            )
        {
            if (oldData == null)
            {
                error = "invalid old data";
                return ReturnCode.Error;
            }

            try
            {
                using (SymmetricAlgorithm localAlgorithm =
                    CreateSymmetricAlgorithm(
                        provider, fileName, encodingName,
                        symmetricAlgorithmName, cipherMode,
                        paddingMode, ref error))
                {
                    if (localAlgorithm == null)
                        return ReturnCode.Error;

                    newData = PerformTransform(
                        localAlgorithm, oldData, encrypt);

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts the specified data using a password and salt, then signs
        /// the resulting ciphertext with the given key pair.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="password">
        /// The password from which to derive key material.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use during key derivation.
        /// </param>
        /// <param name="encryptHashAlgorithmName">
        /// The optional name of the hash algorithm used for key derivation.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to encrypt.
        /// </param>
        /// <param name="signHashAlgorithmName">
        /// The optional name of the hash algorithm used for signing.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing for the signature.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to sign the encrypted data.
        /// </param>
        /// <param name="newData">
        /// Receives the encrypted data.
        /// </param>
        /// <param name="signature">
        /// Receives the signature of the encrypted data.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode EncryptAndSign(
            string symmetricAlgorithmName,   /* in: OPTIONAL */
            string password,                 /* in */
            byte[] salt,                     /* in */
            int iterations,                  /* in */
            string encryptHashAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,           /* in */
            PaddingMode paddingMode,         /* in */
            byte[] oldData,                  /* in */
            string signHashAlgorithmName,    /* in: OPTIONAL */
            byte[] hashKey,                  /* in: OPTIONAL */
            IKeyPair keyPair,                /* in */
            ref byte[] newData,              /* out */
            ref byte[] signature,            /* out */
            ref Result result                /* out */
            )
        {
            byte[] localNewData = null;

            if (EncryptOrDecrypt(
                    symmetricAlgorithmName, password, salt,
                    iterations, encryptHashAlgorithmName, cipherMode,
                    paddingMode, oldData, true, ref localNewData,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] localSignature = null;
            Result localResult = null;

            if (Sign(
                    signHashAlgorithmName, hashKey, localNewData,
                    keyPair, ref localSignature,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            newData = localNewData;
            signature = localSignature;
            result = localResult;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts the specified data using an RFC 2898 data provider, then
        /// signs the resulting ciphertext with the given key pair.
        /// </summary>
        /// <param name="provider">
        /// The RFC 2898 data provider used to obtain key material.
        /// </param>
        /// <param name="fileName">
        /// The name of the file associated with the data.
        /// </param>
        /// <param name="encodingName">
        /// The name of the text encoding to use.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to encrypt.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used for signing.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing for the signature.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to sign the encrypted data.
        /// </param>
        /// <param name="newData">
        /// Receives the encrypted data.
        /// </param>
        /// <param name="signature">
        /// Receives the signature of the encrypted data.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode EncryptAndSign(
            IRfc2898DataProvider provider, /* in */
            string fileName,               /* in */
            string encodingName,           /* in */
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] oldData,                /* in */
            string hashAlgorithmName,      /* in: OPTIONAL */
            byte[] hashKey,                /* in: OPTIONAL */
            IKeyPair keyPair,              /* in */
            ref byte[] newData,            /* out */
            ref byte[] signature,          /* out */
            ref Result result              /* out */
            )
        {
            byte[] localNewData = null;

            if (EncryptOrDecrypt(
                    provider, fileName, encodingName,
                    symmetricAlgorithmName, cipherMode,
                    paddingMode, oldData, true,
                    ref localNewData, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] localSignature = null;
            Result localResult = null;

            if (Sign(
                    hashAlgorithmName, hashKey, localNewData, keyPair,
                    ref localSignature, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            newData = localNewData;
            signature = localSignature;
            result = localResult;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified data and then decrypts it
        /// using a password and salt.
        /// </summary>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="password">
        /// The password from which to derive key material.
        /// </param>
        /// <param name="salt">
        /// The salt to combine with the password.
        /// </param>
        /// <param name="iterations">
        /// The number of iterations to use during key derivation.
        /// </param>
        /// <param name="decryptHashAlgorithmName">
        /// The optional name of the hash algorithm used for key derivation.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to verify and decrypt.
        /// </param>
        /// <param name="verifyHashAlgorithmName">
        /// The optional name of the hash algorithm used for verification.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing for verification.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify.
        /// </param>
        /// <param name="newData">
        /// Receives the decrypted data.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode VerifyAndDecrypt(
            string symmetricAlgorithmName,   /* in: OPTIONAL */
            string password,                 /* in */
            byte[] salt,                     /* in */
            int iterations,                  /* in */
            string decryptHashAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,           /* in */
            PaddingMode paddingMode,         /* in */
            byte[] oldData,                  /* in */
            string verifyHashAlgorithmName,  /* in: OPTIONAL */
            byte[] hashKey,                  /* in: OPTIONAL */
            IKeyPair keyPair,                /* in */
            byte[] signature,                /* in */
            ref byte[] newData,              /* out */
            ref Result result                /* out */
            )
        {
            Result localResult = null;

            if (Verify(
                    verifyHashAlgorithmName, hashKey, oldData, keyPair,
                    signature, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            byte[] localNewData = null;

            if (EncryptOrDecrypt(
                    symmetricAlgorithmName, password, salt,
                    iterations, decryptHashAlgorithmName, cipherMode,
                    paddingMode, oldData, false, ref localNewData,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            newData = localNewData;
            result = localResult;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified data and then decrypts it
        /// using an RFC 2898 data provider.
        /// </summary>
        /// <param name="provider">
        /// The RFC 2898 data provider used to obtain key material.
        /// </param>
        /// <param name="fileName">
        /// The name of the file associated with the data.
        /// </param>
        /// <param name="encodingName">
        /// The name of the text encoding to use.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// The optional name of the symmetric algorithm to use.
        /// </param>
        /// <param name="cipherMode">
        /// The cipher mode to use.
        /// </param>
        /// <param name="paddingMode">
        /// The padding mode to use.
        /// </param>
        /// <param name="oldData">
        /// The data to verify and decrypt.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm used for verification.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing for verification.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify.
        /// </param>
        /// <param name="newData">
        /// Receives the decrypted data.
        /// </param>
        /// <param name="result">
        /// Receives a descriptive result on success or error information on
        /// failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode VerifyAndDecrypt(
            IRfc2898DataProvider provider, /* in */
            string fileName,               /* in */
            string encodingName,           /* in */
            string symmetricAlgorithmName, /* in: OPTIONAL */
            CipherMode cipherMode,         /* in */
            PaddingMode paddingMode,       /* in */
            byte[] oldData,                /* in */
            string hashAlgorithmName,      /* in: OPTIONAL */
            byte[] hashKey,                /* in: OPTIONAL */
            IKeyPair keyPair,              /* in */
            byte[] signature,              /* in */
            ref byte[] newData,            /* out */
            ref Result result              /* out */
            )
        {
            Result localResult = null;

            if (Verify(
                    hashAlgorithmName, hashKey, oldData, keyPair,
                    signature, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            byte[] localNewData = null;

            if (EncryptOrDecrypt(
                    provider, fileName, encodingName,
                    symmetricAlgorithmName, cipherMode,
                    paddingMode, oldData, false,
                    ref localNewData, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            newData = localNewData;
            result = localResult;

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Support Methods
        /// <summary>
        /// Resolves the named interpreter object as an RFC 2898 data provider.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the object to resolve.
        /// </param>
        /// <param name="provider">
        /// Receives the resolved RFC 2898 data provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode GetRfc2898DataProvider(
            Interpreter interpreter,           /* in */
            string objectName,                 /* in */
            ref IRfc2898DataProvider provider, /* out: may NOT be NULL if Ok. */
            ref Result error                   /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ResultList errors = null;
            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                error = errors;
                return ReturnCode.Error;
            }

            IRfc2898DataProvider localProvider = (@object != null) ?
                @object.Value as IRfc2898DataProvider : null;

            if (localProvider == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid key pair");

                error = errors;
                return ReturnCode.Error;
            }

            provider = localProvider;
            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Asymmetric Encryption Methods
        /// <summary>
        /// Computes a hash of the specified data and signs it using the given
        /// key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing the data.
        /// </param>
        /// <param name="data">
        /// The data to sign.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to sign the hash.
        /// </param>
        /// <param name="signature">
        /// Receives the computed signature.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Sign(
            string hashAlgorithmName, /* in: OPTIONAL */
            byte[] hashKey,           /* in: OPTIONAL */
            byte[] data,              /* in */
            IKeyPair keyPair,         /* in */
            ref byte[] signature,     /* out */
            ref Result result         /* out */
            )
        {
            byte[] hashBytes = null;

            if (SharedOps.HashBytes(
                    hashAlgorithmName, hashKey, data,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                if (CommandOps.SignHash(
                        hashAlgorithmName, hashBytes, keyPair,
                        ref signature, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes a hash of the specified data and verifies it against the
        /// given signature using the supplied key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The optional name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing the data.
        /// </param>
        /// <param name="data">
        /// The data whose signature is to be verified.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Verify(
            string hashAlgorithmName, /* in: OPTIONAL */
            byte[] hashKey,           /* in: OPTIONAL */
            byte[] data,              /* in */
            IKeyPair keyPair,         /* in */
            byte[] signature,         /* in */
            ref Result result         /* out */
            )
        {
            byte[] hashBytes = null;

            if (SharedOps.HashBytes(
                    hashAlgorithmName, hashKey, data,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                if (SharedOps.VerifyHash(
                        hashBytes, hashAlgorithmName, signature,
                        keyPair, ref result) != ReturnCode.Ok)
                {
                    if (result == null)
                        result = "bytes signature could not be verified";

                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
        #endregion
    }
}
