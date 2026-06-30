/*
 * Remote.cs --
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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;

namespace Licensing.Providers
{
    /// <summary>
    /// Provides a remote, server-backed implementation of the
    /// <see cref="IRfc2898DataProvider" /> interface that obtains password
    /// derivation data over a network connection.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("f3984f86-ba84-4259-ad35-8242451060c1")]
    public sealed class Remote :
#if ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IHaveInterpreter,
        IHaveCultureInfo,
        IRfc2898DataProvider
    {
        #region Private Data
        /// <summary>
        /// Holds the local RFC 2898 password derivation data managed by this
        /// provider.
        /// </summary>
        private Rfc2898Data rfc2898Data;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Remote" /> class.
        /// </summary>
        public Remote()
        {
            rfc2898Data = new Rfc2898Data();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Converts the specified salt string into an array of bytes
        /// suitable for use with password derivation, generating a new
        /// random salt when none is provided.
        /// </summary>
        /// <param name="salt">
        /// The optional salt value to convert.  When null, a new random salt
        /// is generated.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the salt bytes could
        /// not be obtained.
        /// </param>
        /// <returns>
        /// The array of salt bytes, or null if the salt could not be
        /// obtained.
        /// </returns>
        private byte[] GetSaltBytes(
            string salt,     /* in: OPTIONAL */
            ref Result error /* out */
            )
        {
            if (salt == null)
            {
                return CertificateDataOps.GetNewId(
                    false).ToByteArray();
            }

            byte[] saltBytes = null;
            Result localError = null;

            if (Utility.GetBytesFromString(
                    salt, cultureInfo, ref saltBytes,
                    ref localError) != ReturnCode.Ok)
            {
                saltBytes = CertificateDataOps.GetRawBytes(salt);

                if (saltBytes == null)
                {
                    ResultList errors = new ResultList();

                    if (localError != null)
                        errors.Add(localError);

                    errors.Add("could not get raw salt bytes");

                    error = errors;
                    return null;
                }
            }

            int guidSize = Marshal.SizeOf(typeof(Guid));

            if (saltBytes.Length != guidSize)
                Array.Resize(ref saltBytes, guidSize);

            return saltBytes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the named text encoding, using the associated interpreter
        /// when available and falling back to a utility lookup otherwise.
        /// </summary>
        /// <param name="encodingName">
        /// The optional name of the encoding to resolve.  When null, no
        /// encoding is resolved and success is returned.
        /// </param>
        /// <param name="encoding">
        /// Upon success, receives the resolved encoding.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the encoding could
        /// not be resolved.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private ReturnCode GetEncoding(
            string encodingName,   /* in: OPTIONAL */
            ref Encoding encoding, /* out */
            ref Result error       /* out */
            )
        {
            if (encodingName == null)
                return ReturnCode.Ok;

            if (interpreter != null)
            {
                return interpreter.GetEncodingOrDefault(
                    encodingName, LookupFlags.Default,
                    ref encoding, ref error);
            }
            else
            {
                encoding = Utility.GetEncoding(
                    encodingName, ref error);

                return (encoding != null) ?
                    ReturnCode.Ok : ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the URI used to contact the remote licensing service,
        /// honoring the configured secret data and falling back to the
        /// assembly default when no URI has been set.
        /// </summary>
        /// <returns>
        /// The URI to use for remote requests.
        /// </returns>
        private Uri GetUri()
        {
            if (useSecretData)
            {
                Result error = null; /* NOT USED */

                return SecretOps.GetUri(
                    uri, cultureInfo, ref error);
            }
            else
            {
                Uri localUri = uri;

                if (uri != null)
                    return uri;

                return Utility.GetAssemblyUri(
                    CertificateAssemblyOps.GetObject(),
                    Constants.PasswordUriName);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the API key bytes used to authenticate with the remote
        /// licensing service.
        /// </summary>
        /// <returns>
        /// The API key bytes to use for remote requests.
        /// </returns>
        private byte[] GetApiKey()
        {
            Result error = null; /* NOT USED */

            return SecretOps.GetApiKey(apiKey, cultureInfo, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the API identifier bytes used to identify this client
        /// to the remote licensing service.
        /// </summary>
        /// <returns>
        /// The API identifier bytes to use for remote requests.
        /// </returns>
        private byte[] GetApiId()
        {
            Result error = null; /* NOT USED */

            return SecretOps.GetApiId(apiId, cultureInfo, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the network timeout, in milliseconds, to use for remote
        /// requests.
        /// </summary>
        /// <returns>
        /// The timeout to use, or null to use the default timeout.
        /// </returns>
        private int? GetTimeout()
        {
            return CertificateSharedOps.GetTimeout(interpreter, null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the password derivation data from the specified
        /// <see cref="IRfc2898Data" /> instance into the supplied output
        /// parameters when it is a recognized data type.
        /// </summary>
        /// <param name="rfc2898Data">
        /// The source RFC 2898 data to copy from.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite existing values in the output parameters.
        /// </param>
        /// <param name="password">
        /// Receives the password value copied from the source data.
        /// </param>
        /// <param name="salt">
        /// Receives the salt value copied from the source data.
        /// </param>
        /// <param name="iterationCount">
        /// Receives the iteration count copied from the source data.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the hash algorithm name copied from the source data.
        /// </param>
        /// <param name="signature">
        /// Receives the signature value copied from the source data.
        /// </param>
        private void PopulateFromIRfc2898Data(
            IRfc2898Data rfc2898Data,     /* in */
            bool overwrite,               /* in */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature          /* in, out */
            )
        {
            Rfc2898Data localRfc2898Data = rfc2898Data as Rfc2898Data;

            if (localRfc2898Data == null)
                return;

            localRfc2898Data.GetData(
                overwrite, ref password, ref salt, ref iterationCount,
                ref hashAlgorithmName, ref signature);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter / ISetInterpreter Members
        /// <summary>
        /// Holds the interpreter associated with this provider.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// Gets or sets the interpreter associated with this provider.
        /// </summary>
        public Interpreter Interpreter
        {
            get { return interpreter; }
            set { interpreter = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveCultureInfo Members
        /// <summary>
        /// Holds the culture information associated with this provider.
        /// </summary>
        private CultureInfo cultureInfo;
        /// <summary>
        /// Gets or sets the culture information associated with this
        /// provider.
        /// </summary>
        public CultureInfo CultureInfo
        {
            get { return cultureInfo; }
            set { cultureInfo = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// Holds the unique identifier of the licensing client.
        /// </summary>
        private Guid clientId;
        /// <summary>
        /// Gets or sets the unique identifier of the licensing client.
        /// </summary>
        public Guid ClientId
        {
            get { return clientId; }
            set { clientId = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the name of the hash algorithm used by the client.
        /// </summary>
        private string clientHashAlgorithmName;
        /// <summary>
        /// Gets or sets the name of the hash algorithm used by the client.
        /// </summary>
        public string ClientHashAlgorithmName
        {
            get { return clientHashAlgorithmName; }
            set { clientHashAlgorithmName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the name of the hash algorithm used for signatures.
        /// </summary>
        private string signatureHashAlgorithmName;
        /// <summary>
        /// Gets or sets the name of the hash algorithm used for signatures.
        /// </summary>
        public string SignatureHashAlgorithmName
        {
            get { return signatureHashAlgorithmName; }
            set { signatureHashAlgorithmName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the name of the key ring used for licensing requests.
        /// </summary>
        private string keyRingName;
        /// <summary>
        /// Gets or sets the name of the key ring used for licensing requests.
        /// </summary>
        public string KeyRingName
        {
            get { return keyRingName; }
            set { keyRingName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the policy type used for licensing requests.
        /// </summary>
        private PolicyType policyType;
        /// <summary>
        /// Gets or sets the policy type used for licensing requests.
        /// </summary>
        public PolicyType PolicyType
        {
            get { return policyType; }
            set { policyType = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds a value indicating whether the key ring name must be
        /// matched.
        /// </summary>
        private bool matchKeyRingName;
        /// <summary>
        /// Gets or sets a value indicating whether the key ring name must be
        /// matched.
        /// </summary>
        public bool MatchKeyRingName
        {
            get { return matchKeyRingName; }
            set { matchKeyRingName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the URI of the remote licensing service.
        /// </summary>
        private Uri uri;
        /// <summary>
        /// Gets or sets the URI of the remote licensing service.
        /// </summary>
        public Uri Uri
        {
            get { return uri; }
            set { uri = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the API key bytes used to authenticate with the remote
        /// licensing service.
        /// </summary>
        private byte[] apiKey;
        /// <summary>
        /// Gets or sets the API key bytes used to authenticate with the
        /// remote licensing service.
        /// </summary>
        public byte[] ApiKey
        {
            get { return apiKey; }
            set { apiKey = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds the API identifier bytes used to identify this client to the
        /// remote licensing service.
        /// </summary>
        private byte[] apiId;
        /// <summary>
        /// Gets or sets the API identifier bytes used to identify this client
        /// to the remote licensing service.
        /// </summary>
        public byte[] ApiId
        {
            get { return apiId; }
            set { apiId = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Holds a value indicating whether secret data should be used when
        /// resolving connection parameters.
        /// </summary>
        private bool useSecretData;
        /// <summary>
        /// Gets or sets a value indicating whether secret data should be used
        /// when resolving connection parameters.
        /// </summary>
        public bool UseSecretData
        {
            get { return useSecretData; }
            set { useSecretData = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898DataProvider Members
        //
        // BUGBUG: The use of a plain string here instead of something like
        //         the SecureString class is due to the requirements of the
        //         Rfc2898DeriveBytes class.
        //
        /// <summary>
        /// Obtains the RFC 2898 password derivation data, contacting the
        /// remote licensing service as needed to retrieve a password and the
        /// associated parameters.
        /// </summary>
        /// <param name="fileName">
        /// The optional file name associated with the request.  This
        /// parameter is not used.
        /// </param>
        /// <param name="encodingName">
        /// The optional name of the text encoding to use when interpreting
        /// salt and password bytes.
        /// </param>
        /// <param name="password">
        /// On input, an optional existing password; on output, receives the
        /// resolved password.
        /// </param>
        /// <param name="salt">
        /// On input, an optional existing salt; on output, receives the
        /// resolved salt.
        /// </param>
        /// <param name="iterationCount">
        /// On input, an optional iteration count; on output, receives the
        /// resolved iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, an optional hash algorithm name; on output, receives the
        /// resolved hash algorithm name.
        /// </param>
        /// <param name="signature">
        /// On input, an optional signature; on output, receives the resolved
        /// signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the data could not be
        /// obtained.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public ReturnCode GetData(
            string fileName,              /* in: OPTIONAL, NOT USED */
            string encodingName,          /* in: OPTIONAL */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (rfc2898Data != null)
            {
                rfc2898Data.GetData(true,
                    ref password, ref salt, ref iterationCount,
                    ref hashAlgorithmName, ref signature);
            }

            Encoding encoding = null;

            if (GetEncoding(encodingName,
                    ref encoding, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            byte[] saltBytes;

            if (password != null)
            {
                saltBytes = GetSaltBytes(salt, ref error);

                if (saltBytes == null)
                    return ReturnCode.Error;
            }
            else
            {
                if (useSecretData)
                {
                    IRfc2898Data serverRfc2898Data = null;

                    if (SecretOps.MakeLookupRequest(
                            interpreter, null, cultureInfo, clientId,
                            encoding, clientHashAlgorithmName,
                            GetUri(), GetApiId(), GetApiKey(),
                            signatureHashAlgorithmName, keyRingName,
                            policyType, GetTimeout(),
                            matchKeyRingName, ref serverRfc2898Data,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    PopulateFromIRfc2898Data(
                        serverRfc2898Data, true, ref password,
                        ref salt, ref iterationCount,
                        ref hashAlgorithmName, ref signature);

                    saltBytes = GetSaltBytes(salt, ref error);

                    if (saltBytes == null)
                        return ReturnCode.Error;
                }
                else
                {
                    saltBytes = GetSaltBytes(salt, ref error);

                    if (saltBytes == null)
                        return ReturnCode.Error;

                    Result result = null;

                    if (CryptographyOps.GetPasswordViaUriAndSalt(
                            interpreter, null, GetUri(), encoding,
                            saltBytes, GetTimeout(), ref password,
                            ref result) != ReturnCode.Ok)
                    {
                        error = result;
                        return ReturnCode.Error;
                    }
                }
            }

            if ((salt == null) && (saltBytes != null))
            {
                if (encoding != null)
                {
                    Value.ReverseGuidBytes(saltBytes);
                    salt = encoding.GetString(saltBytes);
                }
                else
                {
                    salt = new Guid(saltBytes).ToString(
                        Constants.RawGuidFormat);
                }
            }

            if (iterationCount <= 0)
                iterationCount = Constants.Rfc2898IterationCount;

            if (hashAlgorithmName == null)
                hashAlgorithmName = Constants.Rfc2898HashAlgorithmName;

            if (signature == null)
                signature = Constants.Rfc2898Signature;

            if (rfc2898Data != null)
            {
                rfc2898Data.SetData(
                    true, password, salt, iterationCount,
                    hashAlgorithmName, signature);
            }

            return ReturnCode.Ok;
        }
        #endregion
    }
}
