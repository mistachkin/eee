/*
 * SecretOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if XML && NETWORK && WEB
using System.Collections.Generic;
#endif

using System.Globalization;

#if XML && NETWORK && WEB
using System.Runtime.InteropServices;
#endif

using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if XML && NETWORK && WEB
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using LicenseState = Licensing.Components.Private.CertificateLicenseState;
#endif

using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper routines for extracting and populating
    /// secret-related data and for making remote secret generate,
    /// lookup, and delete requests.
    /// </summary>
    [ObjectId("ba5698a2-cd76-4533-9ce3-86a2d8c6ccf8")]
    internal static class SecretOps
    {
        #region Public Methods
        /// <summary>
        /// Extracts the RFC 2898 password, salt, iteration count, and hash
        /// algorithm name from the specified data object.
        /// </summary>
        /// <param name="rfc2898Data">
        /// The RFC 2898 data object to extract values from.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to return an error when the data object is null;
        /// otherwise, success is returned without extracting any values.
        /// </param>
        /// <param name="password">
        /// Upon success, receives the extracted password.
        /// </param>
        /// <param name="salt">
        /// Upon success, receives the extracted salt.
        /// </param>
        /// <param name="iterationCount">
        /// Upon success, receives the extracted iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Upon success, receives the extracted hash algorithm name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ExtractData(
            IRfc2898Data rfc2898Data,     /* in */
            bool errorOnNull,             /* in */
            out string password,          /* out */
            out string salt,              /* out */
            out int iterationCount,       /* out */
            out string hashAlgorithmName, /* out */
            ref Result error              /* out */
            )
        {
            string signature;

            return ExtractData(
                rfc2898Data, errorOnNull, out password, out salt,
                out iterationCount, out hashAlgorithmName,
                out signature, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the RFC 2898 password, salt, iteration count, hash
        /// algorithm name, and signature from the specified data object.
        /// </summary>
        /// <param name="rfc2898Data">
        /// The RFC 2898 data object to extract values from.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to return an error when the data object is null;
        /// otherwise, success is returned without extracting any values.
        /// </param>
        /// <param name="password">
        /// Upon success, receives the extracted password.
        /// </param>
        /// <param name="salt">
        /// Upon success, receives the extracted salt.
        /// </param>
        /// <param name="iterationCount">
        /// Upon success, receives the extracted iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Upon success, receives the extracted hash algorithm name.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the extracted signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ExtractData(
            IRfc2898Data rfc2898Data,     /* in */
            bool errorOnNull,             /* in */
            out string password,          /* out */
            out string salt,              /* out */
            out int iterationCount,       /* out */
            out string hashAlgorithmName, /* out */
            out string signature,         /* out */
            ref Result error              /* out */
            )
        {
            password = null;
            salt = null;
            iterationCount = 0;
            hashAlgorithmName = null;
            signature = null;

            if (rfc2898Data == null)
            {
                if (errorOnNull)
                {
                    error = "invalid rfc data";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            Rfc2898Data localRfc2898Data = rfc2898Data as Rfc2898Data;

            if (localRfc2898Data == null)
            {
                if (errorOnNull)
                {
                    error = "invalid rfc data type";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            /* NO RESULT */
            localRfc2898Data.GetData(
                false, ref password, ref salt, ref iterationCount,
                ref hashAlgorithmName, ref signature);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the identifier values found in the specified dictionary
        /// to the given identifier object.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the values to apply.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing values; this value may be null.
        /// </param>
        /// <param name="identifier">
        /// The identifier object to update.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetData(
            StringDictionary dictionary, /* in */
            CultureInfo cultureInfo,     /* in */
            IIdentifier identifier,      /* in, out */
            ref Result error             /* out */
            )
        {
            if (identifier == null)
            {
                error = "invalid identifier";
                return ReturnCode.Error;
            }

            string name;
            string kindString;
            string idString;
            string group;
            string description;

            /* IGNORED */
            dictionary.TryGetValue("name", out name);

            if (String.IsNullOrEmpty(name))
                name = null;

            /* IGNORED */
            dictionary.TryGetValue("kind", out kindString);

            if (String.IsNullOrEmpty(kindString))
                kindString = null;

            /* IGNORED */
            dictionary.TryGetValue("id", out idString);

            if (String.IsNullOrEmpty(idString))
                idString = null;

            /* IGNORED */
            dictionary.TryGetValue("group", out group);

            if (String.IsNullOrEmpty(group))
                group = null;

            /* IGNORED */
            dictionary.TryGetValue("description", out description);

            if (String.IsNullOrEmpty(description))
                description = null;

            IdentifierKind? kind = null;

            if (kindString != null)
            {
                object enumValue = _Utility.TryParseEnum(
                    typeof(IdentifierKind), kindString,
                    true, true, ref error);

                if (!(enumValue is IdentifierKind))
                    return ReturnCode.Error;

                kind = (IdentifierKind)enumValue;
            }

            Guid? id = null;

            if (idString != null)
            {
                Guid guid = Guid.Empty;

                if (Value.GetGuid(
                        idString, cultureInfo, ref guid,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                id = guid;
            }

            if (name != null)
                identifier.Name = name;

            if (kind != null)
                identifier.Kind = (IdentifierKind)kind;

            if (id != null)
                identifier.Id = (Guid)id;

            if (group != null)
                identifier.Group = group;

            if (description != null)
                identifier.Description = description;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the encoding value found in the specified dictionary to
        /// the given object.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the values to apply.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing values; this value may be null.
        /// </param>
        /// <param name="haveEncoding">
        /// The object whose encoding is to be updated.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetData(
            StringDictionary dictionary, /* in */
            CultureInfo cultureInfo,     /* in */
            IHaveEncoding haveEncoding,  /* in, out */
            ref Result error             /* out */
            )
        {
            if (haveEncoding == null)
            {
                error = "invalid have encoding";
                return ReturnCode.Error;
            }

            string encodingName;

            /* IGNORED */
            dictionary.TryGetValue("encodingName", out encodingName);

            if (String.IsNullOrEmpty(encodingName))
                encodingName = null;

            Encoding encoding = null;

            if (encodingName != null)
            {
                encoding = _Utility.GetEncoding(encodingName, ref error);

                if (encoding == null)
                    return ReturnCode.Error;
            }

            if (encoding != null)
                haveEncoding.Encoding = encoding;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the RFC 2898 values found in the specified dictionary to
        /// the given data object.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the values to apply.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing values; this value may be null.
        /// </param>
        /// <param name="rfc2898Data">
        /// The RFC 2898 data object to update.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetData(
            StringDictionary dictionary, /* in */
            CultureInfo cultureInfo,     /* in */
            IRfc2898Data rfc2898Data,    /* in, out */
            ref Result error             /* out */
            )
        {
            if (rfc2898Data == null)
            {
                error = "invalid rfc data";
                return ReturnCode.Error;
            }

            string password;
            string salt;
            string iterationCountString;
            string hashAlgorithmName;
            string signature;

            /* IGNORED */
            dictionary.TryGetValue("password", out password);

            if (String.IsNullOrEmpty(password))
                password = null;

            /* IGNORED */
            dictionary.TryGetValue("salt", out salt);

            if (String.IsNullOrEmpty(salt))
                salt = null;

            /* IGNORED */
            dictionary.TryGetValue(
                "iterationCount", out iterationCountString);

            if (String.IsNullOrEmpty(iterationCountString))
                iterationCountString = null;

            /* IGNORED */
            dictionary.TryGetValue(
                "hashAlgorithmName", out hashAlgorithmName);

            if (String.IsNullOrEmpty(hashAlgorithmName))
                hashAlgorithmName = null;

            /* IGNORED */
            dictionary.TryGetValue("signature", out signature);

            if (String.IsNullOrEmpty(signature))
                signature = null;

            int iterationCount = 0;

            if (iterationCountString != null)
            {
                if (Value.GetInteger2(iterationCountString,
                        ValueFlags.AnyInteger,
                        cultureInfo, ref iterationCount,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            if (password != null)
                rfc2898Data.Password = password;

            if (salt != null)
                rfc2898Data.Salt = salt;

            if (iterationCount != 0)
                rfc2898Data.IterationCount = iterationCount;

            if (hashAlgorithmName != null)
                rfc2898Data.HashAlgorithmName = hashAlgorithmName;

            if (signature != null)
                rfc2898Data.Signature = signature;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the cryptography values found in the specified dictionary
        /// to the given data object.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the values to apply.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing values; this value may be null.
        /// </param>
        /// <param name="cryptographyData">
        /// The cryptography data object to update.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetData(
            StringDictionary dictionary,        /* in */
            CultureInfo cultureInfo,            /* in */
            ICryptographyData cryptographyData, /* in, out */
            ref Result error                    /* out */
            )
        {
            if (cryptographyData == null)
            {
                error = "invalid cryptography data";
                return ReturnCode.Error;
            }

            string symmetricAlgorithmName;

            /* IGNORED */
            dictionary.TryGetValue(
                "symmetricAlgorithmName", out symmetricAlgorithmName);

            if (String.IsNullOrEmpty(symmetricAlgorithmName))
                symmetricAlgorithmName = null;

            string cipherModeString;

            /* IGNORED */
            dictionary.TryGetValue("cipherMode", out cipherModeString);

            if (String.IsNullOrEmpty(cipherModeString))
                cipherModeString = null;

            string paddingModeString;

            /* IGNORED */
            dictionary.TryGetValue("paddingMode", out paddingModeString);

            if (String.IsNullOrEmpty(paddingModeString))
                paddingModeString = null;

            string ivString;

            /* IGNORED */
            dictionary.TryGetValue("iv", out ivString);

            if (String.IsNullOrEmpty(ivString))
                ivString = null;

            string keyString;

            /* IGNORED */
            dictionary.TryGetValue("key", out keyString);

            if (String.IsNullOrEmpty(keyString))
                keyString = null;

            object enumValue; /* REUSED */
            CipherMode? cipherMode = null;

            if (cipherModeString != null)
            {
                enumValue = _Utility.TryParseEnum(
                    typeof(CipherMode), cipherModeString,
                    true, true, ref error);

                if (!(enumValue is CipherMode))
                    return ReturnCode.Error;

                cipherMode = (CipherMode)enumValue;
            }

            PaddingMode? paddingMode = null;

            if (paddingModeString != null)
            {
                enumValue = _Utility.TryParseEnum(
                    typeof(PaddingMode), paddingModeString,
                    true, true, ref error);

                if (!(enumValue is PaddingMode))
                    return ReturnCode.Error;

                paddingMode = (PaddingMode)enumValue;
            }

            byte[] bytes; /* REUSED */
            ByteList iv = null;

            if (ivString != null)
            {
                bytes = null;

                if (_Utility.GetBytesFromString(
                        ivString, cultureInfo, ref bytes,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                iv = new ByteList(bytes);
            }

            ByteList key = null;

            if (keyString != null)
            {
                bytes = null;

                if (_Utility.GetBytesFromString(
                        keyString, cultureInfo, ref bytes,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                key = new ByteList(bytes);
            }

            if (symmetricAlgorithmName != null)
            {
                cryptographyData.SymmetricAlgorithmName =
                    symmetricAlgorithmName;
            }

            if (cipherMode != null)
                cryptographyData.CipherMode = (CipherMode)cipherMode;

            if (paddingMode != null)
                cryptographyData.PaddingMode = (PaddingMode)paddingMode;

            if (iv != null)
                cryptographyData.Iv = iv;

            if (key != null)
                cryptographyData.Key = key;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the secret data values found in the specified dictionary
        /// to the given data object.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the values to apply.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing values; this value may be null.
        /// </param>
        /// <param name="secretData">
        /// The secret data object to update.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetData(
            StringDictionary dictionary, /* in */
            CultureInfo cultureInfo,     /* in */
            ISecretData secretData,      /* in, out */
            ref Result error             /* out */
            )
        {
            if (secretData == null)
            {
                error = "invalid secret data";
                return ReturnCode.Error;
            }

            string flagsString;
            string inputString;
            string auxiliaryString;
            string outputString;
            string signatureString;

            /* IGNORED */
            dictionary.TryGetValue("flags", out flagsString);

            if (String.IsNullOrEmpty(flagsString))
                flagsString = null;

            /* IGNORED */
            dictionary.TryGetValue("input", out inputString);

            if (String.IsNullOrEmpty(inputString))
                inputString = null;

            /* IGNORED */
            dictionary.TryGetValue("auxiliary", out auxiliaryString);

            if (String.IsNullOrEmpty(auxiliaryString))
                auxiliaryString = null;

            /* IGNORED */
            dictionary.TryGetValue("output", out outputString);

            if (String.IsNullOrEmpty(outputString))
                outputString = null;

            /* IGNORED */
            dictionary.TryGetValue("signature", out signatureString);

            if (String.IsNullOrEmpty(signatureString))
                signatureString = null;

            SecretDataFlags? flags = null;

            if (flagsString != null)
            {
                object enumValue = _Utility.TryParseFlagsEnum(
                    null, typeof(SecretDataFlags), flags.ToString(),
                    flagsString, cultureInfo, true, true, true,
                    ref error);

                if (!(enumValue is SecretDataFlags))
                    return ReturnCode.Error;

                flags = (SecretDataFlags)enumValue;
            }

            byte[] bytes; /* REUSED */
            object input = null;

            if (inputString != null)
            {
                if (_Utility.HasFlags(
                        flags, SecretDataFlags.ParseInput, true))
                {
                    bytes = null;

                    if (_Utility.GetBytesFromString(
                            inputString, cultureInfo, ref bytes,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    input = new ByteList(bytes);
                }
                else
                {
                    input = inputString;
                }
            }

            object auxiliary = null;

            if (auxiliaryString != null)
            {
                if (_Utility.HasFlags(
                        flags, SecretDataFlags.ParseAuxiliary, true))
                {
                    bytes = null;

                    if (_Utility.GetBytesFromString(
                            auxiliaryString, cultureInfo, ref bytes,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    auxiliary = new ByteList(bytes);
                }
                else
                {
                    auxiliary = auxiliaryString;
                }
            }

            object output = null;

            if (outputString != null)
            {
                if (_Utility.HasFlags(
                        flags, SecretDataFlags.ParseOutput, true))
                {
                    bytes = null;

                    if (_Utility.GetBytesFromString(
                            outputString, cultureInfo, ref bytes,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    output = new ByteList(bytes);
                }
                else
                {
                    output = outputString;
                }
            }

            object signature = null;

            if (signatureString != null)
            {
                if (_Utility.HasFlags(
                        flags, SecretDataFlags.ParseSignature, true))
                {
                    bytes = null;

                    if (_Utility.GetBytesFromString(
                            signatureString, cultureInfo, ref bytes,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    signature = new ByteList(bytes);
                }
                else
                {
                    signature = signatureString;
                }
            }

            if (flags != null)
                secretData.Flags = (SecretDataFlags)flags;

            if (input is string)
                secretData.InputString = (string)input;
            else if (input is ByteList)
                secretData.InputBytes = (ByteList)input;

            if (auxiliary is string)
                secretData.AuxiliaryString = (string)auxiliary;
            else if (auxiliary is ByteList)
                secretData.AuxiliaryBytes = (ByteList)auxiliary;

            if (output is string)
                secretData.OutputString = (string)output;
            else if (output is ByteList)
                secretData.OutputBytes = (ByteList)output;

            if (signature is string)
                secretData.SignatureString = (string)signature;
            else if (signature is ByteList)
                secretData.SignatureBytes = (ByteList)signature;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && NETWORK && WEB
        /// <summary>
        /// Gets the secret server URI, falling back to configuration or the
        /// assembly when one is not supplied.
        /// </summary>
        /// <param name="uri">
        /// The URI to use; if null, one is resolved automatically.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing; this value may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// The resolved server URI, or null if one cannot be determined.
        /// </returns>
        /* MAY RETURN NULL */
        public static Uri GetUri(
            Uri uri,                 /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            if (uri != null)
                return uri;

            string value = Configuration.GetVariable(
                Constants.HarpySecretUriEnvVarName);

            Uri localUri; /* REUSED */

            if (!String.IsNullOrEmpty(value))
            {
                localUri = null;

                if (Value.GetUri(
                        value, UriKind.Absolute,
                        cultureInfo, ref localUri,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }

                return localUri;
            }

            localUri = _Utility.GetAssemblyUri(
                CertificateAssemblyOps.GetObject(),
                Constants.SecretUriName);

            if (localUri == null)
                error = "invalid server uri";

            return localUri;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the secret server API key, falling back to configuration
        /// when one is not supplied.
        /// </summary>
        /// <param name="apiKey">
        /// The API key to use; if null, one is resolved automatically.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing; this value may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// The resolved API key bytes, or null if one cannot be determined.
        /// </returns>
        public static byte[] GetApiKey(
            byte[] apiKey,           /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            if (apiKey != null)
                return apiKey;

            string value = Configuration.GetVariable(
                Constants.HarpyApiKeyEnvVarName);

            if (String.IsNullOrEmpty(value))
            {
                error = "invalid API key";
                return null;
            }

            byte[] bytes = null;

            if (_Utility.GetBytesFromString(
                    value, cultureInfo, ref bytes,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return bytes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the secret server API identifier, falling back to
        /// configuration when one is not supplied.
        /// </summary>
        /// <param name="apiId">
        /// The API identifier to use; if null, one is resolved automatically.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing; this value may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// The resolved API identifier bytes, or null if unavailable.
        /// </returns>
        public static byte[] GetApiId(
            byte[] apiId,            /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            if (apiId != null)
                return apiId;

            string value = Configuration.GetVariable(
                Constants.HarpyApiIdEnvVarName);

            if (String.IsNullOrEmpty(value))
            {
                error = "invalid API identifier";
                return null;
            }

            byte[] bytes = null;

            if (_Utility.GetBytesFromString(
                    value, cultureInfo, ref bytes,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return bytes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates a new identifier together with random RFC 2898
        /// password and salt data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this value may be null.
        /// </param>
        /// <param name="iterationCount">
        /// The iteration count to store in the generated data.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use; this value may be null.
        /// </param>
        /// <param name="id">
        /// Upon success, receives the newly generated identifier bytes.
        /// </param>
        /// <param name="rfc2898Data">
        /// Upon success, receives the newly generated RFC 2898 data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GenerateData(
            Interpreter interpreter,      /* in: OPTIONAL */
            int iterationCount,           /* in */
            string hashAlgorithmName,     /* in: OPTIONAL */
            ref byte[] id,                /* out */
            ref IRfc2898Data rfc2898Data, /* out */
            ref Result error              /* out */
            )
        {
            byte[] bytes; /* REUSED */

            bytes = new byte[Constants.GenerateEntropyBytes];

            if (_Utility.GetRandomBytes(interpreter,
                    ref bytes, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] password = null;
            byte[] salt = null;

            if (EntropyToPasswordAndSalt(
                    bytes, ref password, ref salt,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bytes = new byte[Marshal.SizeOf(typeof(Guid))];

            if (_Utility.GetRandomBytes(interpreter,
                    ref bytes, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IRfc2898Data localRfc2898Data = new Rfc2898Data();

            localRfc2898Data.Password = Convert.ToBase64String(
                password, Base64FormattingOptions.InsertLineBreaks);

            localRfc2898Data.Salt = Convert.ToBase64String(
                salt, Base64FormattingOptions.InsertLineBreaks);

            localRfc2898Data.IterationCount = iterationCount;
            localRfc2898Data.HashAlgorithmName = hashAlgorithmName;

            id = bytes;
            rfc2898Data = localRfc2898Data;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds and submits a request that stores a newly generated secret
        /// on the server, optionally encrypting and signing it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data context to use; this value may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing and formatting; may be null.
        /// </param>
        /// <param name="clientId">
        /// The client certificate identifier.
        /// </param>
        /// <param name="clientEncoding">
        /// The encoding used for the client; this value may be null.
        /// </param>
        /// <param name="clientHashAlgorithmName">
        /// The hash algorithm used for the client; this value may be null.
        /// </param>
        /// <param name="serverUri">
        /// The server URI; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverId">
        /// The server API identifier; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverApiKey">
        /// The server API key; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverRfc2898Data">
        /// The server RFC 2898 data containing the secret to store.
        /// </param>
        /// <param name="signatureHashAlgorithmName">
        /// The hash algorithm used for signing; this value may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to sign the request; this value may be null.
        /// </param>
        /// <param name="timeout">
        /// The request timeout, in milliseconds; this value may be null.
        /// </param>
        /// <param name="encrypted">
        /// Non-zero if the secret data should be encrypted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode MakeGenerateRequest(
            Interpreter interpreter,           /* in: OPTIONAL */
            IPluginData pluginData,            /* in: OPTIONAL */
            CultureInfo cultureInfo,           /* in: OPTIONAL */
            Guid clientId,                     /* in */
            Encoding clientEncoding,           /* in: OPTIONAL */
            string clientHashAlgorithmName,    /* in: OPTIONAL */
            Uri serverUri,                     /* in: OPTIONAL */
            byte[] serverId,                   /* in */
            byte[] serverApiKey,               /* in */
            IRfc2898Data serverRfc2898Data,    /* in */
            string signatureHashAlgorithmName, /* in: OPTIONAL */
            IKeyPair keyPair,                  /* in: OPTIONAL */
            int? timeout,                      /* in: OPTIONAL */
            bool encrypted,                    /* in */
            ref Result error                   /* out */
            )
        {
            serverUri = GetUri(
                serverUri, cultureInfo, ref error);

            if (serverUri == null)
                return ReturnCode.Error;

            serverApiKey = GetApiKey(
                serverApiKey, cultureInfo, ref error);

            if (serverApiKey == null)
                return ReturnCode.Error;

            serverId = GetApiId(
                serverId, cultureInfo, ref error);

            if (serverId == null)
                return ReturnCode.Error;

            string serverPassword;
            string serverSalt;
            int serverIterationCount;
            string serverHashAlgorithmName;

            if (ExtractData(
                    serverRfc2898Data, true, out serverPassword,
                    out serverSalt, out serverIterationCount,
                    out serverHashAlgorithmName,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] oldData = Convert.FromBase64String(serverPassword);
            byte[] newData; /* REUSED */

            if (encrypted)
            {
                byte[] clientBytes = null;

                if (GetEntropyFromCertificate(
                        clientId, null, clientEncoding,
                        clientHashAlgorithmName, ref clientBytes,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] clientPassword = null;
                byte[] clientSalt = null;

                if (EntropyToPasswordAndSalt(
                        clientBytes, ref clientPassword,
                        ref clientSalt, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                string serverSymmetricAlgorithmName;
                CipherMode serverCipherMode;
                PaddingMode serverPaddingMode;

                if (ExtractData(
                        serverRfc2898Data as ICryptographyData,
                        false, out serverSymmetricAlgorithmName,
                        out serverCipherMode, out serverPaddingMode,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (clientEncoding == null)
                    clientEncoding = Constants.DefaultEncoding;

                if (serverCipherMode == (CipherMode)0)
                    serverCipherMode = Constants.DefaultCipherMode;

                if (serverPaddingMode == (PaddingMode)0)
                    serverPaddingMode = Constants.DefaultPaddingMode;

                newData = null;

                if (CryptographyOps.EncryptOrDecrypt(
                        serverSymmetricAlgorithmName,
                        clientEncoding.GetString(clientPassword),
                        clientSalt, serverIterationCount,
                        serverHashAlgorithmName, serverCipherMode,
                        serverPaddingMode, oldData, true,
                        ref newData, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                newData = oldData;
            }

            serverRfc2898Data.Password = Convert.ToBase64String(
                newData, Base64FormattingOptions.InsertLineBreaks);

            if (keyPair != null)
            {
                byte[] signatureData = null;

                Serialize(
                    null, newData, serverSalt, serverIterationCount,
                    serverHashAlgorithmName, ref signatureData);

                byte[] signature = null;
                Result localResult = null;

                if (CryptographyOps.Sign(
                        CertificateSharedOps.GetHashAlgorithm(
                            signatureHashAlgorithmName,
                            new IKeyPair[] { keyPair },
                            null, HashAlgorithmType.CommandUse),
                        null, signatureData, keyPair, ref signature,
                        ref localResult) != ReturnCode.Ok)
                {
                    error = localResult;
                    return ReturnCode.Error;
                }

                string signatureString = CreateSignatureBlock(
                    keyPair, signature, ref error);

                if (signatureString == null)
                    return ReturnCode.Error;

                serverRfc2898Data.Signature = signatureString;
            }

            if (CryptographyOps.InsertSecretViaUriAndSalt(
                    interpreter, pluginData, serverUri,
                    cultureInfo, clientEncoding, serverApiKey,
                    serverId, serverRfc2898Data, timeout,
                    encrypted, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Submits a request that retrieves a secret from the server,
        /// verifying its signature and decrypting it as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data context to use; this value may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing and formatting; may be null.
        /// </param>
        /// <param name="clientId">
        /// The client certificate identifier.
        /// </param>
        /// <param name="clientEncoding">
        /// The encoding used for the client; this value may be null.
        /// </param>
        /// <param name="clientHashAlgorithmName">
        /// The hash algorithm used for the client; this value may be null.
        /// </param>
        /// <param name="serverUri">
        /// The server URI; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverId">
        /// The server API identifier; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverApiKey">
        /// The server API key; if null, it is resolved automatically.
        /// </param>
        /// <param name="signatureHashAlgorithmName">
        /// The hash algorithm used for signing; this value may be null.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to search; this value may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when locating the signing key pair.
        /// </param>
        /// <param name="timeout">
        /// The request timeout, in milliseconds; this value may be null.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero to require the key ring name to match.
        /// </param>
        /// <param name="rfc2898Data">
        /// Upon success, receives the RFC 2898 data for the secret.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode MakeLookupRequest(
            Interpreter interpreter,           /* in: OPTIONAL */
            IPluginData pluginData,            /* in: OPTIONAL */
            CultureInfo cultureInfo,           /* in: OPTIONAL */
            Guid clientId,                     /* in */
            Encoding clientEncoding,           /* in: OPTIONAL */
            string clientHashAlgorithmName,    /* in: OPTIONAL */
            Uri serverUri,                     /* in: OPTIONAL */
            byte[] serverId,                   /* in */
            byte[] serverApiKey,               /* in */
            string signatureHashAlgorithmName, /* in: OPTIONAL */
            string keyRingName,                /* in: OPTIONAL */
            PolicyType policyType,             /* in */
            int? timeout,                      /* in: OPTIONAL */
            bool matchKeyRingName,             /* in */
            ref IRfc2898Data rfc2898Data,      /* out */
            ref Result error                   /* out */
            )
        {
            serverUri = GetUri(
                serverUri, cultureInfo, ref error);

            if (serverUri == null)
                return ReturnCode.Error;

            serverApiKey = GetApiKey(
                serverApiKey, cultureInfo, ref error);

            if (serverApiKey == null)
                return ReturnCode.Error;

            serverId = GetApiId(
                serverId, cultureInfo, ref error);

            if (serverId == null)
                return ReturnCode.Error;

            ISecretData serverSecretData = null;

            if (CryptographyOps.GetSecretViaUriAndSalt(
                    interpreter, pluginData, serverUri,
                    cultureInfo, clientEncoding, serverApiKey,
                    serverId, timeout, ref serverSecretData,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string serverPassword;
            string serverSalt;
            int serverIterationCount;
            string serverHashAlgorithmName;
            string serverSignature;

            if (ExtractData(
                    serverSecretData as IRfc2898Data, true,
                    out serverPassword, out serverSalt,
                    out serverIterationCount,
                    out serverHashAlgorithmName,
                    out serverSignature,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] oldData = Convert.FromBase64String(serverPassword);

            if (_Utility.HasFlags(
                    serverSecretData.Flags, SecretDataFlags.Signed,
                    true))
            {
                byte[] signature = null;
                IKeyPair keyPair = null;

                if (ParseSignatureBlock(
                        interpreter, serverSignature, null, keyRingName,
                        policyType, matchKeyRingName, ref signature,
                        ref keyPair, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] signatureData = null;

                Serialize(
                    null, oldData, serverSalt, serverIterationCount,
                    serverHashAlgorithmName, ref signatureData);

                Result localResult = null;

                if (CryptographyOps.Verify(
                        CertificateSharedOps.GetHashAlgorithm(
                            signatureHashAlgorithmName,
                            new IKeyPair[] { keyPair },
                            null, HashAlgorithmType.CommandUse),
                        null, signatureData, keyPair, signature,
                        ref localResult) != ReturnCode.Ok)
                {
                    error = localResult;
                    return ReturnCode.Error;
                }

                string keyUsage;

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
                if (CertificateGlobalState.HaveExtraFeatures(
                        Features.RelaxedSecretsKeyUsageOrAll, false))
                {
                    keyUsage = KeyUsage.Secret;
                }
                else
#endif
                {
                    keyUsage = KeyUsage.RemoteSecret;
                }

                if ((keyUsage != null) &&
                    (CertificateScriptOps.CheckKeyUsage(
                        keyPair, keyUsage, EntityType.File,
                        ref error) != ReturnCode.Ok))
                {
                    return ReturnCode.Error;
                }
            }

            byte[] newData; /* REUSED */

            if (_Utility.HasFlags(
                    serverSecretData.Flags, SecretDataFlags.Encrypted,
                    true))
            {
                byte[] clientBytes = null;

                if (GetEntropyFromCertificate(
                        clientId, null, clientEncoding,
                        clientHashAlgorithmName, ref clientBytes,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] clientPassword = null;
                byte[] clientSalt = null;

                if (EntropyToPasswordAndSalt(
                        clientBytes, ref clientPassword,
                        ref clientSalt, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                string serverSymmetricAlgorithmName;
                CipherMode serverCipherMode;
                PaddingMode serverPaddingMode;

                if (ExtractData(
                        serverSecretData as ICryptographyData, true,
                        out serverSymmetricAlgorithmName,
                        out serverCipherMode, out serverPaddingMode,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (clientEncoding == null)
                    clientEncoding = Constants.DefaultEncoding;

                if (serverCipherMode == (CipherMode)0)
                    serverCipherMode = Constants.DefaultCipherMode;

                if (serverPaddingMode == (PaddingMode)0)
                    serverPaddingMode = Constants.DefaultPaddingMode;

                newData = null;

                if (CryptographyOps.EncryptOrDecrypt(
                        serverSymmetricAlgorithmName,
                        clientEncoding.GetString(clientPassword),
                        clientSalt, serverIterationCount,
                        serverHashAlgorithmName, serverCipherMode,
                        serverPaddingMode, oldData, false,
                        ref newData, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                newData = oldData;
            }

            rfc2898Data = BuildResponse(
                newData, serverSalt, serverIterationCount,
                serverHashAlgorithmName, serverSignature);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Submits a request that deletes a secret from the server.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use; this value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data context to use; this value may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for parsing; this value may be null.
        /// </param>
        /// <param name="clientEncoding">
        /// The encoding used for the client; this value may be null.
        /// </param>
        /// <param name="serverUri">
        /// The server URI; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverId">
        /// The server API identifier; if null, it is resolved automatically.
        /// </param>
        /// <param name="serverApiKey">
        /// The server API key; if null, it is resolved automatically.
        /// </param>
        /// <param name="timeout">
        /// The request timeout, in milliseconds; this value may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode MakeDeleteRequest(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            Encoding clientEncoding, /* in: OPTIONAL */
            Uri serverUri,           /* in: OPTIONAL */
            byte[] serverId,         /* in */
            byte[] serverApiKey,     /* in */
            int? timeout,            /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            serverUri = GetUri(
                serverUri, cultureInfo, ref error);

            if (serverUri == null)
                return ReturnCode.Error;

            serverApiKey = GetApiKey(
                serverApiKey, cultureInfo, ref error);

            if (serverApiKey == null)
                return ReturnCode.Error;

            serverId = GetApiId(
                serverId, cultureInfo, ref error);

            if (serverId == null)
                return ReturnCode.Error;

            if (CryptographyOps.DeleteSecretViaUriAndSalt(
                    interpreter, pluginData, serverUri,
                    clientEncoding, serverApiKey, serverId,
                    timeout, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the RFC 2898 values from the specified data object and
        /// formats them as a name/value list.
        /// </summary>
        /// <param name="rfc2898Data">
        /// The RFC 2898 data object to extract values from.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to return an error when the data object is null;
        /// otherwise, success is returned without extracting any values.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the extracted values as a name/value
        /// list; upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ExtractData(
            IRfc2898Data rfc2898Data, /* in */
            bool errorOnNull,         /* in */
            ref Result result         /* out */
            )
        {
            string password;
            string salt;
            int iterationCount;
            string hashAlgorithmName;
            string signature;

            if (ExtractData(rfc2898Data,
                    errorOnNull, out password, out salt,
                    out iterationCount, out hashAlgorithmName,
                    out signature, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            StringList list = new StringList();

            if (password != null)
            {
                list.Add("password");
                list.Add(password);
            }

            if (salt != null)
            {
                list.Add("salt");
                list.Add(salt);
            }

            if (iterationCount != 0)
            {
                list.Add("iterationCount");
                list.Add(iterationCount.ToString());
            }

            if (hashAlgorithmName != null)
            {
                list.Add("hashAlgorithmName");
                list.Add(hashAlgorithmName);
            }

            if (signature != null)
            {
                list.Add("signature");
                list.Add(signature);
            }

            result = list;
            return ReturnCode.Ok;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if XML && NETWORK && WEB
        /// <summary>
        /// Serializes the specified RFC 2898 values into a single byte
        /// array suitable for signing.
        /// </summary>
        /// <param name="encoding">
        /// The encoding used for the hash algorithm name; may be null.
        /// </param>
        /// <param name="password">
        /// The password bytes to serialize; this value may be null.
        /// </param>
        /// <param name="saltString">
        /// The base64-encoded salt to serialize; this value may be null.
        /// </param>
        /// <param name="iterationCount">
        /// The iteration count to serialize; this value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to serialize; this value may be null.
        /// </param>
        /// <param name="data">
        /// Upon return, receives the serialized data bytes.
        /// </param>
        private static void Serialize(
            Encoding encoding,        /* in: OPTIONAL */
            byte[] password,          /* in: OPTIONAL */
            string saltString,        /* in: OPTIONAL */
            int? iterationCount,      /* in: OPTIONAL */
            string hashAlgorithmName, /* in: OPTIONAL */
            ref byte[] data           /* out */
            )
        {
            ByteList bytes = new ByteList();

            if (password != null)
                bytes.AddRange(password);

            if (saltString != null)
                bytes.AddRange(Convert.FromBase64String(saltString));

            if (iterationCount != null)
            {
                bytes.AddRange(
                    BitConverter.GetBytes((int)iterationCount));
            }

            if (hashAlgorithmName != null)
            {
                if (encoding == null)
                    encoding = Constants.DefaultEncoding;

                bytes.AddRange(
                    encoding.GetBytes(hashAlgorithmName));
            }

            data = bytes.ToArray();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a signature block, extracting the signature bytes and
        /// locating the key pair that produced it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="signatureString">
        /// The signature block string to parse.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to parse the block; this value may be null.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to search; this value may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type used when locating the key pair.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero to require the key ring name to match.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the extracted signature bytes.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that signed the data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode ParseSignatureBlock(
            Interpreter interpreter, /* in */
            string signatureString,  /* in */
            Encoding encoding,       /* in: OPTIONAL */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            bool matchKeyRingName,   /* in */
            ref byte[] signature,    /* out */
            ref IKeyPair keyPair,    /* out */
            ref Result error         /* out */
            )
        {
            byte[] publicKeyToken = null;
            byte[] localSignature = null;

            if (CryptographyOps.ExtractParameters(
                    interpreter, signatureString, encoding,
                    true, ref publicKeyToken, ref localSignature,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair = null;

            if (CertificateKeyPairOps.GetOne( /* OK */
                    keyRingName, policyType, matchKeyRingName,
                    CertificateAssemblyOps.GetObject(),
                    CertificateAssemblyOps.GetName(), interpreter,
                    CertificateDataOps.FormatPublicKeyToken(
                        publicKeyToken, false, false), false, true,
                    ref localKeyPair, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            signature = localSignature;
            keyPair = localKeyPair;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a formatted signature block containing the signer public
        /// key token, a time stamp, and the base64-encoded signature.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose public key token is recorded in the block.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to encode into the block.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// The formatted signature block, or null if an error occurs.
        /// </returns>
        private static string CreateSignatureBlock(
            IKeyPair keyPair, /* in */
            byte[] signature, /* in */
            ref Result error  /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return null;
            }

            if (signature == null)
            {
                error = "invalid signature";
                return null;
            }

            StringBuilder builder = new StringBuilder();

            builder.Append(Constants.EncryptedDataHeader);
            builder.AppendLine();

            builder.AppendFormat(Constants.EncryptedDataHeaderFormat,
                "publicKeyToken", CertificateDataOps.FormatPublicKeyToken(
                keyPair.PublicKeyToken, false, false));

            builder.AppendLine();

            builder.AppendFormat(Constants.EncryptedDataHeaderFormat,
                "timeStamp", CertificateDataOps.FormatTimeStamp(
                _Utility.GetUtcNow()));

            builder.AppendLine();
            builder.AppendLine();

            builder.AppendLine(Convert.ToBase64String(
                signature, Base64FormattingOptions.InsertLineBreaks));

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the symmetric algorithm name, cipher mode, and padding
        /// mode from the specified cryptography data object.
        /// </summary>
        /// <param name="cryptographyData">
        /// The cryptography data object to extract values from.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to return an error when the data object is null;
        /// otherwise, success is returned without extracting any values.
        /// </param>
        /// <param name="symmetricAlgorithmName">
        /// Upon success, receives the symmetric algorithm name.
        /// </param>
        /// <param name="cipherMode">
        /// Upon success, receives the cipher mode.
        /// </param>
        /// <param name="paddingMode">
        /// Upon success, receives the padding mode.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode ExtractData(
            ICryptographyData cryptographyData, /* in */
            bool errorOnNull,                   /* in */
            out string symmetricAlgorithmName,  /* out */
            out CipherMode cipherMode,          /* out */
            out PaddingMode paddingMode,        /* out */
            ref Result error                    /* out */
            )
        {
            symmetricAlgorithmName = null;
            cipherMode = (CipherMode)0;
            paddingMode = (PaddingMode)0;

            if (cryptographyData == null)
            {
                if (errorOnNull)
                {
                    error = "invalid cryptography data";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            symmetricAlgorithmName =
                cryptographyData.SymmetricAlgorithmName;

            cipherMode = cryptographyData.CipherMode;
            paddingMode = cryptographyData.PaddingMode;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds an RFC 2898 data object from the specified secret values.
        /// </summary>
        /// <param name="password">
        /// The password bytes to encode into the response; may be null.
        /// </param>
        /// <param name="salt">
        /// The salt to store in the response; this value may be null.
        /// </param>
        /// <param name="iterationCount">
        /// The iteration count to store in the response.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to store; this value may be null.
        /// </param>
        /// <param name="signature">
        /// The signature to store; this value may be null.
        /// </param>
        /// <returns>
        /// The newly constructed RFC 2898 data object.
        /// </returns>
        private static IRfc2898Data BuildResponse(
            byte[] password,          /* in: OPTIONAL */
            string salt,              /* in: OPTIONAL */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in: OPTIONAL */
            string signature          /* in: OPTIONAL */
            )
        {
            IRfc2898Data rfc2898Data = new Rfc2898Data();

            rfc2898Data.Password = Convert.ToBase64String(
                password, Base64FormattingOptions.InsertLineBreaks);

            rfc2898Data.Salt = salt;
            rfc2898Data.IterationCount = iterationCount;
            rfc2898Data.HashAlgorithmName = hashAlgorithmName;
            rfc2898Data.Signature = signature;

            return rfc2898Data;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Collects any extra entropy configured via environment variables
        /// within the specified index range.
        /// </summary>
        /// <param name="minimumIndex">
        /// The inclusive starting index of the entropy variables to check.
        /// </param>
        /// <param name="maximumIndex">
        /// The exclusive ending index of the entropy variables to check.
        /// </param>
        /// <param name="bytes">
        /// Upon return, receives the combined extra entropy, or null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode CheckForExtraEntropy(
            int minimumIndex, /* in */
            int maximumIndex, /* in */
            ref byte[] bytes, /* out */
            ref Result error  /* out */
            )
        {
            ByteList entropy = null;

            for (int index = minimumIndex; index < maximumIndex; index++)
            {
                string value = Configuration.GetVariable(
                    String.Format(Constants.SecretEntropyFormat,
                    index));

                if (String.IsNullOrEmpty(value))
                    continue;

                byte[] localBytes = null;

                if (_Utility.GetBytesFromString(
                        value, null, ref localBytes,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (entropy == null)
                    entropy = new ByteList();

                entropy.AddRange(localBytes);
            }

            bytes = (entropy != null) ? entropy.ToArray() : null;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts entropy from the specified certificate, combining it
        /// with any configured extra entropy and optionally hashing it.
        /// </summary>
        /// <param name="id">
        /// The identifier of the certificate to extract entropy from.
        /// </param>
        /// <param name="salt">
        /// The salt used when extracting entropy; this value may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when extracting entropy; may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm used to hash the entropy; may be null.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the extracted entropy bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode GetEntropyFromCertificate(
            Guid id,                  /* in */
            byte[] salt,              /* in: OPTIONAL */
            Encoding encoding,        /* in: OPTIONAL */
            string hashAlgorithmName, /* in: OPTIONAL */
            ref byte[] bytes,         /* out */
            ref Result error          /* out */
            )
        {
            ICertificate certificate;

            certificate = id.Equals(Guid.Empty) ?
                LicenseState.GetCertificate() :
                LicenseState.GetCertificate(id, ref error);

            if (certificate == null)
            {
                error = String.Format(
                    "certificate {0} unavailable",
                    _Utility.FormatWrapOrNull(id));

                return ReturnCode.Error;
            }

            byte[] part1 = certificate.ExtractEntropy(
                salt, encoding, ref error);

            if (part1 == null)
                return ReturnCode.Error;

            byte[] part2 = null;

            if (CheckForExtraEntropy(
                    Constants.MinimumEntropyIndex,
                    Constants.MaximumEntropyIndex,
                    ref part2, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ByteList entropy = new ByteList(part1);

            if (part2 != null)
                entropy.AddRange(part2);

            if (hashAlgorithmName != null)
            {
                byte[] localBytes = entropy.ToArray();

                localBytes = _Utility.HashBytes(
                    hashAlgorithmName, localBytes, ref error);

                if (localBytes == null)
                    return ReturnCode.Error;

                bytes = localBytes;
            }
            else
            {
                bytes = entropy.ToArray();
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Splits the specified entropy bytes into a password and a salt,
        /// enforcing the configured minimum and maximum lengths.
        /// </summary>
        /// <param name="bytes">
        /// The entropy bytes to split into a password and salt.
        /// </param>
        /// <param name="password">
        /// Upon success, receives the password bytes.
        /// </param>
        /// <param name="salt">
        /// Upon success, receives the salt bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes the
        /// problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode EntropyToPasswordAndSalt(
            byte[] bytes,        /* in */
            ref byte[] password, /* out */
            ref byte[] salt,     /* out */
            ref Result error     /* out */
            )
        {
            if (bytes == null)
            {
                error = "invalid bytes";
                return ReturnCode.Error;
            }

            int length = bytes.Length;
            int passwordLength = (length / 3) * 2;

            if ((Constants.MinimumPasswordBytes > 0) &&
                (passwordLength < Constants.MinimumPasswordBytes))
            {
                error = "not enough entropy for password";
                return ReturnCode.Error;
            }

            if ((Constants.MaximumPasswordBytes > 0) &&
                (passwordLength > Constants.MaximumPasswordBytes))
            {
                passwordLength = Constants.MaximumPasswordBytes;
            }

            int saltLength = (length / 3);

#pragma warning disable 162 // HACK: Compiler is wrong.
            if (Constants.MinimumSaltBytes > 0) /* 8 > 0 */
            {
                if (saltLength < Constants.MinimumSaltBytes)
                    saltLength = length - passwordLength;

                if (saltLength < Constants.MinimumSaltBytes)
                {
                    error = "not enough entropy for salt";
                    return ReturnCode.Error;
                }
            }
#pragma warning restore 162

#pragma warning disable 162
            if ((Constants.MaximumSaltBytes > 0) &&
                (saltLength > Constants.MaximumSaltBytes))
            {
                saltLength = Constants.MaximumSaltBytes;
            }
#pragma warning restore 162

            int passwordOffset = 0;
            int saltOffset = passwordLength;

            password = new byte[passwordLength];
            salt = new byte[saltLength];

            Array.Copy(bytes, passwordOffset, password, 0, passwordLength);
            Array.Copy(bytes, saltOffset, salt, 0, saltLength);

            return ReturnCode.Ok;
        }
#endif
        #endregion
    }
}
