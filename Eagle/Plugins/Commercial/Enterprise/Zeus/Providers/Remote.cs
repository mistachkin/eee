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

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Zeus.Components.Private;
using UriPair = Eagle._Interfaces.Public.IAnyPair<System.Uri, bool?>;

namespace Zeus.Providers
{
    /// <summary>
    /// Implements an RFC 2898 (PBKDF2) data provider that obtains its
    /// key-derivation parameters from a remote location.  The target URI and
    /// its trust flag are carried in the caller data as a URI/boolean pair;
    /// <see cref="GetData" /> fetches the remote data, populates the base
    /// provider, and then defers to it.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("134a5066-87ee-4db2-b4ab-af95532dac39")]
    public sealed class Remote : Core
    {
        #region Internal Constructors
        /// <summary>
        /// Constructs a new <see cref="Remote" /> provider instance
        /// associated with the specified interpreter and caller data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter this provider is associated with.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller; expected to carry the
        /// remote URI and its trust flag as a URI/boolean pair.
        /// </param>
        internal Remote(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
            : base(interpreter, clientData)
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to extract the remote URI and its trust flag from the
        /// caller data attached to this provider.
        /// </summary>
        /// <param name="uri">
        /// Upon success, receives the remote URI to fetch data from.
        /// </param>
        /// <param name="trusted">
        /// Upon success, receives the flag indicating whether the remote
        /// location is to be trusted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero if the URI and trust flag were successfully extracted;
        /// otherwise, zero.
        /// </returns>
        private bool TryUnpackClientData(
            ref Uri uri,       /* out */
            ref bool? trusted, /* out */
            ref Result error   /* out */
            )
        {
            IClientData clientData = base.ClientData;

            if (clientData == null)
            {
                error = "invalid clientData";
                return false;
            }

            UriPair anyPair = clientData.Data as UriPair;

            if (anyPair == null)
            {
                error = "invalid uri/bool pair";
                return false;
            }

            uri = anyPair.X;
            trusted = anyPair.Y;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the supplied key-derivation parameters into the base
        /// provider, marking each as explicitly set so the base
        /// <see cref="Core.GetData" /> will supply them when missing.
        /// </summary>
        /// <param name="password">
        /// The password to store on the base provider.
        /// </param>
        /// <param name="salt">
        /// The salt to store on the base provider.
        /// </param>
        /// <param name="iterationCount">
        /// The iteration count to store on the base provider.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to store on the base provider.
        /// </param>
        /// <param name="signature">
        /// The signature to store on the base provider.
        /// </param>
        private void PopulateBaseData(
            string password,          /* in */
            string salt,              /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in */
            string signature          /* in */
            )
        {
            base.Password = password;
            base.Salt = salt;
            base.IterationCount = iterationCount;
            base.HashAlgorithmName = hashAlgorithmName;
            base.Signature = signature;
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
        /// Supplies the RFC 2898 key-derivation parameters by fetching them
        /// from the remote URI carried in this provider's caller data.  The
        /// retrieved values are stored on the base provider and the call then
        /// defers to the base implementation to fill in any still-missing
        /// parameters.
        /// </summary>
        /// <param name="fileName">
        /// An optional file name passed through to the base provider.
        /// </param>
        /// <param name="encodingName">
        /// An optional encoding name used when decoding the remote data.
        /// </param>
        /// <param name="password">
        /// On input, the caller-supplied password, if any; on output, may
        /// receive the remotely obtained password.
        /// </param>
        /// <param name="salt">
        /// On input, the caller-supplied salt, if any; on output, may receive
        /// the remotely obtained salt.
        /// </param>
        /// <param name="iterationCount">
        /// On input, the caller-supplied iteration count, if any; on output,
        /// may receive the remotely obtained iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, the caller-supplied hash algorithm name, if any; on
        /// output, may receive the remotely obtained hash algorithm name.
        /// </param>
        /// <param name="signature">
        /// On input, the caller-supplied signature, if any; on output, may
        /// receive the remotely obtained signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode GetData(
            string fileName,              /* in: OPTIONAL */
            string encodingName,          /* in: OPTIONAL */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            Result localError; /* REUSED */
            Uri uri = null;
            bool? trusted = null;

            localError = null;

            if (!TryUnpackClientData(
                    ref uri, ref trusted, ref localError))
            {
                error = localError;
                return ReturnCode.Error;
            }

            localError = null;

            ReturnCode code = Rfc2898Ops.GetRemoteData(
                base.Interpreter, encodingName, uri, trusted,
                true, ref password, ref salt, ref iterationCount,
                ref hashAlgorithmName, ref signature,
                ref localError);

            if (code != ReturnCode.Ok)
            {
                error = localError;
                return code;
            }

            PopulateBaseData(
                password, salt, iterationCount, hashAlgorithmName,
                signature);

            return base.GetData(
                fileName, encodingName, ref password, ref salt,
                ref iterationCount, ref hashAlgorithmName,
                ref signature, ref error);
        }
        #endregion
    }
}
