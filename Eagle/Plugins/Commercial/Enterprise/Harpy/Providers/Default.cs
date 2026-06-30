/*
 * Default.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Public;

namespace Licensing.Providers
{
    /// <summary>
    /// Provides the default implementation of the
    /// <see cref="IRfc2898DataProvider" /> interface, supplying the
    /// password, salt, iteration count, hash algorithm name, and signature
    /// used for RFC 2898 key derivation from license
    /// <see cref="ICertificate" /> data.  The associated
    /// <see cref="Interpreter" /> and <see cref="IClientData" /> are exposed
    /// via the <see cref="IHaveInterpreter" /> and
    /// <see cref="IHaveClientData" /> interfaces, respectively.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("d43cbbf9-e6ac-4e47-915e-04d43b43775a")]
    public sealed class Default :
#if ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IHaveInterpreter,
        IHaveClientData,
        IRfc2898DataProvider
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="Default" /> RFC 2898
        /// data provider.
        /// </summary>
        public Default()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Attempts to extract the RFC 2898 key derivation parameters from
        /// the license <see cref="ICertificate" /> contained within the
        /// <see cref="IClientData.Data" /> of the specified client data,
        /// delegating to <see cref="TryGetFromCertificateData" />.
        /// </summary>
        /// <param name="clientData">
        /// The <see cref="IClientData" /> expected to contain the license
        /// <see cref="ICertificate" /> to read the parameters from.  May be
        /// null, in which case the method fails.
        /// </param>
        /// <param name="password">
        /// Receives the password used for RFC 2898 key derivation.
        /// </param>
        /// <param name="salt">
        /// Receives the salt used for RFC 2898 key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// Receives the iteration count used for RFC 2898 key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the name of the hash algorithm used for RFC 2898 key
        /// derivation.
        /// </param>
        /// <param name="signature">
        /// Receives the signature associated with the RFC 2898 key
        /// derivation parameters.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a <see cref="Result" /> describing the
        /// error that was encountered.
        /// </param>
        /// <returns>
        /// <c>true</c> if the parameters were obtained successfully;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool TryGetFromLicenseCertificateData(
            IClientData clientData,       /* in */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return false;
            }

            return TryGetFromCertificateData(
                clientData.Data as ICertificate, ref password,
                ref salt, ref iterationCount, ref hashAlgorithmName,
                ref signature, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to extract the RFC 2898 key derivation parameters from
        /// the license <see cref="ICertificate" /> associated with the
        /// current assembly, as obtained from
        /// <c>CertificateLicenseState.GetCertificate</c> and delegated to
        /// <see cref="TryGetFromCertificateData" />.
        /// </summary>
        /// <param name="password">
        /// Receives the password used for RFC 2898 key derivation.
        /// </param>
        /// <param name="salt">
        /// Receives the salt used for RFC 2898 key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// Receives the iteration count used for RFC 2898 key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the name of the hash algorithm used for RFC 2898 key
        /// derivation.
        /// </param>
        /// <param name="signature">
        /// Receives the signature associated with the RFC 2898 key
        /// derivation parameters.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a <see cref="Result" /> describing the
        /// error that was encountered.
        /// </param>
        /// <returns>
        /// <c>true</c> if the parameters were obtained successfully;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool TryGetFromAssemblyLicenseCertificateData(
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            return TryGetFromCertificateData(
                CertificateLicenseState.GetCertificate(),
                ref password, ref salt, ref iterationCount,
                ref hashAlgorithmName, ref signature,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to extract the RFC 2898 key derivation parameters from
        /// the specified license <see cref="ICertificate" />.  The password
        /// is taken from the certificate serial number, the salt from the
        /// certificate identifier, and the iteration count, hash algorithm
        /// name, and signature from their respective <c>Constants</c>
        /// values.
        /// </summary>
        /// <param name="certificate">
        /// The license <see cref="ICertificate" /> from which to read the
        /// key derivation parameters.  May be null, in which case the
        /// method fails.
        /// </param>
        /// <param name="password">
        /// Receives the password used for RFC 2898 key derivation.
        /// </param>
        /// <param name="salt">
        /// Receives the salt used for RFC 2898 key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// Receives the iteration count used for RFC 2898 key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the name of the hash algorithm used for RFC 2898 key
        /// derivation.
        /// </param>
        /// <param name="signature">
        /// Receives the signature associated with the RFC 2898 key
        /// derivation parameters.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a <see cref="Result" /> describing the
        /// error that was encountered.
        /// </param>
        /// <returns>
        /// <c>true</c> if the parameters were obtained successfully;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool TryGetFromCertificateData(
            ICertificate certificate,     /* in */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (certificate == null)
            {
                error = "certificate unavailable";
                return false;
            }

            string localPassword = certificate.SerialNumber;

            if (localPassword == null)
            {
                error = "certificate serial number unavailable";
                return false;
            }

            Guid id = certificate.Id;

            if (id.Equals(Guid.Empty))
            {
                error = "certificate identifier unavailable";
                return false;
            }

            string localSalt = id.ToString();

            if (localSalt == null)
            {
                error = "certificate identifier string unavailable";
                return false;
            }

            password = localPassword;
            salt = localSalt;
            iterationCount = Constants.Rfc2898IterationCount;
            hashAlgorithmName = Constants.Rfc2898HashAlgorithmName;
            signature = Constants.Rfc2898Signature;

            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetClientData / ISetClientData Members
        /// <summary>
        /// The <see cref="IClientData" /> associated with this provider.
        /// </summary>
        private IClientData clientData;
        /// <summary>
        /// Gets or sets the <see cref="IClientData" /> associated with this
        /// provider.
        /// </summary>
        /// <value>
        /// The <see cref="IClientData" /> associated with this provider,
        /// which is expected to carry the license
        /// <see cref="ICertificate" />.
        /// </value>
        public IClientData ClientData
        {
            get { return clientData; }
            set { clientData = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter / ISetInterpreter Members
        /// <summary>
        /// The <see cref="Interpreter" /> associated with this provider.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// Gets or sets the <see cref="Interpreter" /> associated with this
        /// provider.
        /// </summary>
        /// <value>
        /// The <see cref="Interpreter" /> associated with this provider.
        /// </value>
        public Interpreter Interpreter
        {
            get { return interpreter; }
            set { interpreter = value; }
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
        /// Obtains the RFC 2898 key derivation parameters, first attempting
        /// to read them from the license <see cref="ICertificate" /> within
        /// the associated <see cref="ClientData" /> via
        /// <see cref="TryGetFromLicenseCertificateData" /> and then falling
        /// back to the certificate associated with the current assembly via
        /// <see cref="TryGetFromAssemblyLicenseCertificateData" />.  This
        /// method implements <see cref="IRfc2898DataProvider.GetData" />.
        /// </summary>
        /// <param name="fileName">
        /// Optional file name.  This parameter is not used.
        /// </param>
        /// <param name="encodingName">
        /// Optional encoding name.  This parameter is not used.
        /// </param>
        /// <param name="password">
        /// Receives the password used for RFC 2898 key derivation.
        /// </param>
        /// <param name="salt">
        /// Receives the salt used for RFC 2898 key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// Receives the iteration count used for RFC 2898 key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// Receives the name of the hash algorithm used for RFC 2898 key
        /// derivation.
        /// </param>
        /// <param name="signature">
        /// Receives the signature associated with the RFC 2898 key
        /// derivation parameters.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a <see cref="Result" /> (a
        /// <see cref="ResultList" /> aggregating each attempt) describing
        /// the error that was encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the parameters were obtained
        /// successfully; otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        public ReturnCode GetData(
            string fileName,              /* in: OPTIONAL, NOT USED */
            string encodingName,          /* in: OPTIONAL, NOT USED */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            ResultList errors = null;
            Result localError = null; /* REUSED */

            if (TryGetFromLicenseCertificateData(
                    this.ClientData, ref password, ref salt,
                    ref iterationCount, ref hashAlgorithmName,
                    ref signature, ref localError))
            {
                return ReturnCode.Ok;
            }

            if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            localError = null;

            if (TryGetFromAssemblyLicenseCertificateData(
                    ref password, ref salt, ref iterationCount,
                    ref hashAlgorithmName, ref signature,
                    ref localError))
            {
                return ReturnCode.Ok;
            }

            if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            error = errors;
            return ReturnCode.Error;
        }
        #endregion
    }
}
