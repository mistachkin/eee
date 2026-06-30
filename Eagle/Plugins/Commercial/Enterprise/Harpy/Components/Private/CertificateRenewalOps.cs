/*
 * CertificateRenewalOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !NETWORK
#error "This file cannot be compiled or used properly with network support disabled."
#endif

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using StringDictionary = Eagle._Containers.Public.StringDictionary;
using LicenseState = Licensing.Components.Private.CertificateLicenseState;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using NetworkState = Licensing.Components.Private.CertificateNetworkState;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
using PolicyState = Licensing.Components.Private.CertificatePolicyState;
#endif

using PolicyDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.ExecutionPolicy>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the private operations used to renew a certificate by
    /// building a renewal request, sending it to the licensing server, and
    /// processing the response into a new certificate.
    /// </summary>
    [ObjectId("cd563c28-eec6-4128-b367-22fe8eeb1890")]
    internal static class CertificateRenewalOps
    {
        /// <summary>
        /// Gets the relative URI used when constructing the certificate
        /// renewal request URI.
        /// </summary>
        /// <returns>
        /// The configured relative URI, null if relative URIs have been
        /// explicitly disabled via configuration, or the default relative URI
        /// when neither configuration variable is present.
        /// </returns>
        public static string GetRelativeUri()
        {
            string value = Configuration.GetVariable(
                Constants.RenewalRelativeUriEnvVarName);

            if (!String.IsNullOrEmpty(value))
                return value;

            if (Configuration.DoesVariableExist(
                    Constants.NoRenewalRelativeUriEnvVarName))
            {
                return null;
            }

            return Constants.DefaultRenewalRelativeUri;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the certificate renewal request URI and the collection of
        /// name/value pairs to be sent to the licensing server for the
        /// specified certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the request, if any.  This may be
        /// null.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with the request, if any.  This may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to hash the request, if any.
        /// </param>
        /// <param name="hashKey">
        /// The key bytes used when hashing the request.  When null, the
        /// serial number bytes or the certificate identifier bytes are used.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert request strings to bytes.
        /// </param>
        /// <param name="certificate">
        /// The certificate that is to be renewed.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use, if any.  This may be null.
        /// </param>
        /// <param name="uri">
        /// Upon success, receives the renewal request URI.
        /// </param>
        /// <param name="collection">
        /// Upon success, receives the collection of name/value pairs that
        /// comprise the renewal request.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode BuildRequest(
            Interpreter interpreter,            /* in: OPTIONAL */
            Assembly assembly,                  /* in: OPTIONAL */
            IPlugin plugin,                     /* in: OPTIONAL */
            string hashAlgorithmName,           /* in: OPTIONAL */
            byte[] hashKey,                     /* in: OPTIONAL */
            Encoding encoding,                  /* in */
            ICertificate certificate,           /* in */
            CultureInfo cultureInfo,            /* in: OPTIONAL */
            ref Uri uri,                        /* out */
            ref NameValueCollection collection, /* out */
            ref Result error                    /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            Uri authority = null;
            UriComponents components = (UriComponents)0;

            if (CertificateNetworkOps.GetAuthorityAndComponents(
                    interpreter, assembly, plugin, certificate,
                    cultureInfo, ref authority, ref components,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: This treats data strings / files the same as scripts
            //       for the purposes of certificate renewal.  The entity
            //       name and values will not be sent to the server:
            //
            //       1. Because script certificates do not have an entity
            //          name defined -AND- may not properly verify if one
            //          is defined.
            //
            //       2. Because the server should already have the entity
            //          value for the script certificate, which is the
            //          script text itself.  Script text is never sent to
            //          the server with the current protocol.
            //
            bool scriptMode = false;

            if (SharedOps.HasFlags(
                    certificate.EntityType, EntityType.DataMask, false))
            {
                scriptMode = true;
            }

            string serialNumber = certificate.SerialNumber;

            if (!scriptMode && (serialNumber == null))
            {
                error = "invalid serial number";
                return ReturnCode.Error;
            }

            Guid requestId = CertificateDataOps.GetNewId(false);
            DateTime requestTimeStamp = Utility.GetUtcNow();
            Guid certificateId = certificate.Id;

            //
            // HACK: This assumes that script certificates will NOT make
            //       use of the EntityName and/or EntityValue properties
            //       EXCEPT that the EntityValue property will be set to
            //       the script text for embedded script certificates.
            //
            // NOTE: Neither of these properties will ever be sent to the
            //       server for script certificates because the server
            //       already has the script text and it also assumes that
            //       the EntityName is never used for script certificates.
            //
            string entityName = !scriptMode ? certificate.EntityName : null;

            string savedEntityValue = certificate.EntityValue;
            string entityValue = !scriptMode ? savedEntityValue : null;

            string request = String.Format(Constants.RenewalRequestFormat,
                CertificateDataOps.FormatId(requestId),
                CertificateDataOps.FormatTimeStamp(requestTimeStamp),
                CertificateDataOps.FormatId(certificateId), serialNumber,
                entityName, entityValue);

            //
            // HACK: If no key, use the serial number bytes, if any;
            //       otherwise, use the certificate Id bytes.
            //
            if (hashKey == null)
            {
                if (serialNumber != null)
                {
                    hashKey = encoding.GetBytes(serialNumber);
                }
                else
                {
                    hashKey = encoding.GetBytes(
                        CertificateDataOps.FormatId(certificateId));
                }
            }

            byte[] hashBytes = null;

            if (SharedOps.HashString(
                    hashAlgorithmName, hashKey, null, null, encoding,
                    request, ref hashBytes, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            NameValueCollection localCollection = new NameValueCollection();

            localCollection.Add("requestId",
                CertificateDataOps.FormatId(requestId));

            localCollection.Add("requestTimeStamp",
                CertificateDataOps.FormatTimeStamp(requestTimeStamp));

            localCollection.Add("certificateId",
                CertificateDataOps.FormatId(certificateId));

            localCollection.Add("requestHash",
                CertificateDataOps.FormatHexadecimal(hashBytes));

            localCollection.Add("scriptMode", scriptMode.ToString());

            //
            // HACK: This assumes that all (and only) embedded script
            //       certificates have the EntityValue property set.
            //
            localCollection.Add("embedded",
                (scriptMode && (savedEntityValue != null)).ToString());

            if (entityName != null)
                localCollection.Add("entityName", entityName);

            if (entityValue != null)
                localCollection.Add("entityValue", entityValue);

            string relativeUri = GetRelativeUri();

            if (relativeUri != null)
                components |= Constants.DefaultRelativeUriComponents;

            Uri localUri = Utility.TryCombineUris(
                authority, relativeUri, encoding, components,
                UriFormat.Unescaped, UriFlags.None, ref error);

            if (localUri == null)
                return ReturnCode.Error;

            uri = localUri;
            collection = localCollection;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the raw renewal response bytes and divides them into their
        /// constituent parts, i.e. the certificate data, the key ring data,
        /// the key ring signature, and the optional server information.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used to split the response into a list.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the response bytes to text.
        /// </param>
        /// <param name="bytes">
        /// The raw response bytes returned by the licensing server.
        /// </param>
        /// <param name="certificateBytes">
        /// Upon success, receives the decoded certificate data bytes.
        /// </param>
        /// <param name="keyRingDataBytes">
        /// Upon success, receives the decoded key ring data bytes.
        /// </param>
        /// <param name="keyRingSignatureBytes">
        /// Upon success, receives the decoded key ring signature bytes.
        /// </param>
        /// <param name="serverInfo">
        /// Upon success, receives the optional server information string, if
        /// present in the response.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode ParseAndDivideResponseData(
            Interpreter interpreter,          /* in */
            Encoding encoding,                /* in */
            byte[] bytes,                     /* in */
            ref byte[] certificateBytes,      /* out */
            ref byte[] keyRingDataBytes,      /* out */
            ref byte[] keyRingSignatureBytes, /* out */
            ref string serverInfo,            /* out */
            ref Result error                  /* out */
            )
        {
            try
            {
                if (encoding == null)
                {
                    error = "invalid encoding";
                    return ReturnCode.Error;
                }

                if (bytes == null)
                {
                    error = "invalid byte array";
                    return ReturnCode.Error;
                }

                string text = encoding.GetString(bytes); /* throw */

                if (String.IsNullOrEmpty(text))
                {
                    error = "invalid response text";
                    return ReturnCode.Error;
                }

                StringList list = null;
                Result localError = null;

                if (Parser.SplitList(
                        interpreter, text, 0, Length.Invalid, true,
                        ref list, ref localError) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (1): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "malformed response text: bad list";
                    return ReturnCode.Error;
                }

                StringDictionary dictionary;

                try
                {
                    dictionary = new StringDictionary(
                        list, true, true); /* throw */
                }
                catch
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (2): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "malformed response text: bad dictionary";
                    return ReturnCode.Error;
                }

                string[] values = { null, null, null, null };

                if (!dictionary.TryGetValue(
                        Constants.RenewalCertificateDataName,
                        out values[0])) /* REQUIRED */
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (3): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response missing certificate data";
                    return ReturnCode.Error;
                }

                if (!dictionary.TryGetValue(
                        Constants.RenewalKeyRingDataName,
                        out values[1])) /* REQUIRED */
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (4): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response missing key ring data";
                    return ReturnCode.Error;
                }

                if (!dictionary.TryGetValue(
                        Constants.RenewalKeyRingSignatureName,
                        out values[2])) /* REQUIRED */
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (5): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response missing key ring signature";
                    return ReturnCode.Error;
                }

                //
                // HACK: This is (currently) optional within the
                //       response data.
                //
                if (!dictionary.TryGetValue(
                        Constants.RenewalServerInfoName,
                        out values[3])) /* OPTIONAL */
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Missing optional server information (6): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif
                }

                if (!Utility.IsBase64(values[0])) /* REQUIRED */
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (7): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response certificate data not base64";
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(values[1]) && /* OPTIONAL */
                    !Utility.IsBase64(values[1]))
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (8): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response key ring data not base64";
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(values[2]) && /* OPTIONAL */
                    !Utility.IsBase64(values[2]))
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed renewal response text (9): {0}",
                            Utility.FormatWrapOrNull(text)),
                        typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "response key ring signature not base64";
                    return ReturnCode.Error;
                }

                certificateBytes = Convert.FromBase64String(
                    values[0]); /* throw */

                keyRingDataBytes = Convert.FromBase64String(
                    values[1]); /* throw */

                keyRingSignatureBytes = Convert.FromBase64String(
                    values[2]); /* throw */

                serverInfo = values[3];

                return ReturnCode.Ok;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateRenewalOps).Name,
                    TracePriority.MediumHigh);
#endif

                error = "failed to parse response";
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the certificate and key ring information from the raw
        /// renewal response bytes, imports the renewed certificate, and
        /// optionally writes and loads the associated key ring data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the response bytes to text.
        /// </param>
        /// <param name="entityValue">
        /// The entity value associated with the certificate, e.g. the script
        /// text for an embedded script certificate.
        /// </param>
        /// <param name="entityType">
        /// The entity type associated with the certificate.
        /// </param>
        /// <param name="keyName">
        /// The name of the key to match when re-fetching key pairs.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to load and query.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use, if any.  This may be null.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use, if any.  This may be null.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the imported certificate.
        /// </param>
        /// <param name="loadKeyRings">
        /// Non-zero to write and load the key ring data from the response.
        /// </param>
        /// <param name="getKeyPairs">
        /// Non-zero to re-fetch the key pairs from the loaded key ring.
        /// </param>
        /// <param name="matchKeyName">
        /// Non-zero to restrict the re-fetched key pairs to those matching
        /// <paramref name="keyName" />.
        /// </param>
        /// <param name="enforceKeyUsage">
        /// Non-zero to filter the re-fetched key pairs by their permitted key
        /// usage for the specified entity type.
        /// </param>
        /// <param name="bytes">
        /// On input, the raw renewal response bytes; on success, receives the
        /// extracted certificate data bytes.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported, renewed certificate.
        /// </param>
        /// <param name="certificateHashFlags">
        /// Upon success, receives the certificate hashing flags to use when
        /// verifying the renewed certificate, if any.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, optionally receives the re-fetched key pairs.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error; otherwise, may
        /// receive additional information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode ExtractAndLoadResponseData(
            Interpreter interpreter,                        /* in */
            Encoding encoding,                              /* in */
            string entityValue,                             /* in */
            EntityType entityType,                          /* in */
            string keyName,                                 /* in */
            string keyRingName,                             /* in */
            CultureInfo cultureInfo,                        /* in: OPTIONAL */
            ExecutionPolicy? policy,                        /* in: OPTIONAL */
            bool validate,                                  /* in */
            bool loadKeyRings,                              /* in */
            bool getKeyPairs,                               /* in */
            bool matchKeyName,                              /* in */
            bool enforceKeyUsage,                           /* in */
            ref byte[] bytes,                               /* in, out */
            ref ICertificate certificate,                   /* out */
            ref CertificateHashFlags? certificateHashFlags, /* out */
            ref IEnumerable<IKeyPair> keyPairs,             /* out */
            ref Result result                               /* out */
            )
        {
            #region Parse & Divide Raw Response Data (Phase 0: Required)
            byte[] certificateBytes = null;
            byte[] keyRingDataBytes = null;
            byte[] keyRingSignatureBytes = null;
            string serverInfo = null;

            if (ParseAndDivideResponseData(
                    interpreter, encoding, bytes, ref certificateBytes,
                    ref keyRingDataBytes, ref keyRingSignatureBytes,
                    ref serverInfo, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION
            #region Import Certificate (Phase 1: Required)
            ICertificate localCertificate = null;
            CertificateHashFlags? localCertificateHashFlags = null;
            Result localResult = null;

            if (CertificateXmlOps.Import(null,
                    certificateBytes, validate, ref localCertificate,
                    ref localResult) == ReturnCode.Ok)
            {
                //
                // NOTE: If available, save the renewal server information
                //       for later use.
                //
                localCertificate.ServerInfo = serverInfo;

                //
                // HACK: For embedded script certificates, use the correct
                //       certificate hashing flags; otherwise, subsequent
                //       certificate verification may fail.
                //
                Certificate.MaybeAdjustForEmbedded(
                    localCertificate, entityValue,
                    ref localCertificateHashFlags);
            }
            else
            {
                result = localResult;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Write & Load Response Key Ring (Phase 2: Optional)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            //
            // NOTE: If there is some key ring data, attempt to load it now.
            //       Technically, this is an optional step; however, if the
            //       certificate was signed with a key pair that not present
            //       on the associated (local) key ring, its verification
            //       will fail UNLESS this key ring data can be successfully
            //       loaded -AND- contains that key pair -AND- the key pair
            //       is considered valid and trusted.
            //
            IEnumerable<IKeyPair> localKeyPairs1 = null;

            if (loadKeyRings &&
                (keyRingDataBytes != null) && (keyRingDataBytes.Length > 0))
            {
                string temporaryFileName = null;
                string temporaryCertificateFileName = null;

                try
                {
                    #region Write Response Key Ring Data
                    temporaryFileName = Utility.GetTempFileName(
                        Constants.TemporaryKeyRingPrefix);

                    File.WriteAllBytes(
                        temporaryFileName, keyRingDataBytes); /* throw */
                    #endregion

                    ///////////////////////////////////////////////////////////

                    #region Write Response Key Ring Signature (Optional)
                    //
                    // HACK: This is marked as "optional" by the enclosing
                    //       region; however, it is unlikely that unsigned
                    //       key rings will be loaded here.
                    //
                    if ((keyRingSignatureBytes != null) &&
                        (keyRingSignatureBytes.Length > 0))
                    {
                        temporaryCertificateFileName =
                            CertificateDataOps.FormatFileName(
                                temporaryFileName);

                        File.WriteAllBytes(
                            temporaryCertificateFileName,
                            keyRingSignatureBytes); /* throw */
                    }
                    #endregion

                    ///////////////////////////////////////////////////////////

                    #region Determine Policy Type for Target Key Ring
                    //
                    // HACK: Is this logic too fragile for future work on
                    //       this subsystem?
                    //
                    PolicyType policyType;

                    if (Certificate.CanBeEmbedded(localCertificate))
                        policyType = PolicyType.Script;
                    else
                        policyType = PolicyType.License;
                    #endregion

                    ///////////////////////////////////////////////////////////

                    #region Load Response (Trusted?) Key Ring File
                    #region Save Policies & Forcibly Enable (Global)
                    PolicyDictionary policies = null;

                    if (CertificatePolicyOps.SavePolicies(
                            null, false, ref policies,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    if (CertificatePolicyOps.EnableForCommand(
                            null, true, false, false, false,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                    #endregion

                    ///////////////////////////////////////////////////////////

                    try
                    {
                        CertificateKeyRingState.BeginRenewalPending();

                        try
                        {
                            if (CertificateKeyRingOps.LoadKeyPairsPublicOnly(
                                    interpreter, keyRingName, policyType,
                                    temporaryFileName, cultureInfo, policy,
                                    true, true, ref result) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }
                        }
                        finally
                        {
                            CertificateKeyRingState.EndRenewalPending();
                        }
                    }
                    finally
                    {
                        #region Restore Policies (Global)
                        ReturnCode restoreCode;
                        Result restoreError = null;

                        restoreCode = CertificatePolicyOps.RestorePolicies(
                            null, policies, false, true, ref restoreError);

                        if (restoreCode != ReturnCode.Ok)
                        {
                            Utility.Complain(
                                interpreter, restoreCode, restoreError);
                        }
                        #endregion
                    }
                    #endregion

                    ///////////////////////////////////////////////////////////

                    #region Get (New?) Key Pairs
                    //
                    // BUGBUG: Without this option, any new key pair metadata
                    //         from the server will not actually be used when
                    //         verifying the renewed certificates because the
                    //         trusted key rings are not actually consulted
                    //         by the Verify method in this class.
                    //
                    if (getKeyPairs)
                    {
                        IEnumerable<IKeyPair> localKeyPairs2 = null;

                        if (CertificateKeyRingOps.GetKeyPairs(
                                interpreter, keyRingName, policyType,
                                matchKeyName ? keyName : null, false,
                                ref localKeyPairs2,
                                ref result) == ReturnCode.Ok)
                        {
                            if (enforceKeyUsage)
                            {
                                localKeyPairs1 =
                                    CertificateKeyPairOps.Filter(
                                        localKeyPairs2, entityType);
                            }
                            else
                            {
                                localKeyPairs1 = localKeyPairs2;
                            }
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
                    }
                    #endregion
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificateRenewalOps).Name,
                        TracePriority.MediumHigh);
#endif

                    result = "failed to handle key ring in response";
                    return ReturnCode.Error;
                }
                finally
                {
                    try
                    {
                        //
                        // NOTE: Attempt to delete the temporary file
                        //       that we created to hold the key ring
                        //       signature.
                        //
                        if (temporaryCertificateFileName != null)
                            File.Delete(temporaryCertificateFileName); /* throw */
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        //
                        // NOTE: This should basically never happen;
                        //       emit a trace message.  This used to
                        //       complain; however, that is overkill
                        //       for non-debug builds.
                        //
                        CertificateTraceOps.DebugTrace(
                            e, typeof(CertificateRenewalOps).Name,
                            TracePriority.MediumHigh);
#endif
                    }

                    ///////////////////////////////////////////////////////////

                    try
                    {
                        //
                        // NOTE: Attempt to delete the temporary file
                        //       that we created to hold the key ring
                        //       data.
                        //
                        if (temporaryFileName != null)
                            File.Delete(temporaryFileName); /* throw */
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        //
                        // NOTE: This should basically never happen;
                        //       emit a trace message.  This used to
                        //       complain; however, that is overkill
                        //       for non-debug builds.
                        //
                        CertificateTraceOps.DebugTrace(
                            e, typeof(CertificateRenewalOps).Name,
                            TracePriority.MediumHigh);
#endif
                    }
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: If we get to this point, we have totally succeeded.
            //
            bytes = certificateBytes;
            certificate = localCertificate;
            certificateHashFlags = localCertificateHashFlags;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            //
            // NOTE: If requested by the caller, replace their list of
            //       key pairs with the ones that we re-fetched.  This
            //       may or may not be a superset of the original list
            //       of key pairs specified by the caller.
            //
            if (getKeyPairs && (localKeyPairs1 != null))
                keyPairs = localKeyPairs1;
#endif

            return ReturnCode.Ok;
#else
            result = "not implemented";
            return ReturnCode.Error;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the file name used to back up an existing certificate file
        /// prior to renewal by appending a time stamp and backup extension.
        /// </summary>
        /// <param name="fileName">
        /// The file name of the certificate that is being renewed.
        /// </param>
        /// <returns>
        /// The backup file name, or the original
        /// <paramref name="fileName" /> when it is null or empty.
        /// </returns>
        private static string GetBackupFileName(
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return fileName;

            StringBuilder builder = new StringBuilder(
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName)));

            builder.Append(Characters.MinusSign);

            builder.Append(CertificateDataOps.GetTimeStamp().ToString(
                Constants.BackupDateTimeFormat));

            builder.Append(Constants.BackupFileExtension);

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate renewal callback to use, preferring the one
        /// configured on the license manager associated with the specified
        /// plugin data and falling back to the default callback.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to locate the associated license manager, if
        /// any.  This may be null.
        /// </param>
        /// <param name="defaultOnNull">
        /// Non-zero to fall back to the default callback when the license
        /// manager callback is null.
        /// </param>
        /// <returns>
        /// The renewal callback to use.
        /// </returns>
        public static RenewCallback GetRenewCallback(
            IPluginData pluginData, /* in */
            bool defaultOnNull      /* in */
            )
        {
#if CERTIFICATE_PLUGIN || LICENSE_MANAGER
            if (pluginData == null)
                goto fallback;

            ILicensePluginManagerData licensePluginManagerData =
                SharedOps.GetLicensePluginManagerData(pluginData);

            if (licensePluginManagerData == null)
                goto fallback;

            ILicenseManager licenseManager =
                licensePluginManagerData.LicenseManager;

            if (licenseManager == null)
                goto fallback;

            RenewCallback renewCallback = licenseManager.RenewCallback;

            if (defaultOnNull && (renewCallback == null))
                goto fallback;

            return renewCallback;

        fallback:
#endif

            return DefaultRenewCallback;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the renewed certificate against the specified key pairs,
        /// using file-based policy verification for non-embedded certificates
        /// and in-memory verification otherwise.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use during verification.
        /// </param>
        /// <param name="hashKey">
        /// The key bytes to use during verification.
        /// </param>
        /// <param name="certificate">
        /// The certificate to verify.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The certificate hashing flags to use, if any.  This may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use, if any.  This may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the certificate against.
        /// </param>
        /// <param name="fileName">
        /// The file name associated with the certificate, if any.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use, if any.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require the public key token to match.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check whether the certificate has been revoked.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode Verify(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string fileName,                            /* in: OPTIONAL */
            int? timeout,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
#if XML && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            bool embedded = false;

            if (Certificate.CanBeEmbedded(
                    certificate, ref embedded) && !embedded)
            {
                if (CertificatePolicyOps.VerifyFile(
                        hashAlgorithmName, hashKey, certificate,
                        certificateHashFlags, encoding, keyPairs,
                        fileName, timeout, matchPublicKeyToken,
                        checkRevocation, ref keyPair,
                        ref result) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }
            }
            else
#endif
            {
                if (CertificateVerifyOps.Process(
                        SharedOps.GetHashAlgorithm(
                            hashAlgorithmName, keyPairs, certificate,
                            HashAlgorithmType.Legacy),
                        hashKey, certificate, certificateHashFlags,
                        encoding, keyPairs, true, checkRevocation,
                        ref keyPair, ref result) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default <see cref="RenewCallback" /> implementation, which
        /// forwards its arguments to the <see cref="Process" /> method in
        /// order to renew the specified certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the request, if any.  This may be
        /// null.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name associated with the request.  This is not used.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with the request, if any.  This may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The key bytes to use when hashing, if any.
        /// </param>
        /// <param name="hashValue">
        /// The hash value associated with the request, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to use, as an opaque object.
        /// </param>
        /// <param name="anyClientData">
        /// The client data associated with the request.  This is not used.
        /// </param>
        /// <param name="features">
        /// The features associated with the request.  This is not used.
        /// </param>
        /// <param name="restrictions">
        /// The restrictions associated with the request.  This is not used.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use, if any.  This may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type to use, if any.  This may be null.
        /// </param>
        /// <param name="keyName">
        /// The name of the key associated with the request.  This is not
        /// used.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring associated with the request.  This is not
        /// used.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use, if any.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded.  This is not used.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the renewed certificate.
        /// </param>
        /// <param name="fileName">
        /// On input, the file name of the certificate; on success, may
        /// receive the file name of the renewed certificate.
        /// </param>
        /// <param name="certificate">
        /// On input, the certificate to renew; on success, receives the
        /// renewed certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        /* Licensing.Components.Public.Delegates.RenewCallback */
        public static ReturnCode DefaultRenewCallback(
            Interpreter interpreter,      /* in: OPTIONAL, May be null. */
            Assembly assembly,            /* in: OK, OPTIONAL, May be null. */
            AssemblyName assemblyName,    /* in: OK, NOT USED */
            IPlugin plugin,               /* in: OPTIONAL, May be null. */
            string hashAlgorithmName,     /* in: OPTIONAL */
            byte[] hashKey,               /* in: OPTIONAL */
            byte[] hashValue,             /* in: OPTIONAL */
            Encoding encoding,            /* in */
            object keyPairs,              /* in */
            IAnyClientData anyClientData, /* in: NOT USED */
            string features,              /* in: NOT USED */
            string restrictions,          /* in: NOT USED */
            ExecutionPolicy? policy,      /* in: OPTIONAL */
            PolicyType? policyType,       /* in: OPTIONAL */
            string keyName,               /* in: NOT USED */
            string keyRingName,           /* in: NOT USED */
            int? timeout,                 /* in: OPTIONAL */
            bool embedded,                /* in: NOT USED */
            bool validate,                /* in */
            ref string fileName,          /* in, out */
            ref ICertificate certificate, /* in, out */
            ref Result result             /* out */
            )
        {
            return Process(
                interpreter, assembly, assemblyName, plugin,
                hashAlgorithmName, hashKey, hashValue, encoding,
                keyPairs as IEnumerable<IKeyPair>, anyClientData,
                features, restrictions, policy, policyType,
                keyName, keyRingName, timeout, embedded, validate,
                ref fileName, ref certificate, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Renews the specified certificate by building and sending a renewal
        /// request to the licensing server, processing the response,
        /// verifying the renewed certificate, and writing it out to the file
        /// system when applicable.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the request, if any.  This may be
        /// null.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name associated with the request.  This is not used.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with the request, if any.  This may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The key bytes to use when hashing, if any.
        /// </param>
        /// <param name="hashValue">
        /// The hash value used to derive the hash-based file name, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to use when verifying the renewed certificate.
        /// </param>
        /// <param name="anyClientData">
        /// The client data associated with the request.  This is not used.
        /// </param>
        /// <param name="features">
        /// The features associated with the request.  This is not used.
        /// </param>
        /// <param name="restrictions">
        /// The restrictions associated with the request.  This is not used.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use, if any.  This may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type to use, if any.  This may be null.
        /// </param>
        /// <param name="keyName">
        /// The name of the key associated with the request.  This is not
        /// used.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to load and query.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use, if any.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded.  This is not used.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the renewed certificate.
        /// </param>
        /// <param name="fileName">
        /// On input, the file name of the certificate; on success, used as
        /// the destination for the renewed certificate.
        /// </param>
        /// <param name="certificate">
        /// On input, the certificate to renew; on success, receives the
        /// renewed certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode Process(
            Interpreter interpreter,        /* in: OPTIONAL, May be null. */
            Assembly assembly,              /* in: OK, OPTIONAL, May be null. */
            AssemblyName assemblyName,      /* in: OK, NOT USED */
            IPlugin plugin,                 /* in: OPTIONAL, May be null. */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            byte[] hashValue,               /* in: OPTIONAL */
            Encoding encoding,              /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            IAnyClientData anyClientData,   /* in: NOT USED */
            string features,                /* in: NOT USED */
            string restrictions,            /* in: NOT USED */
            ExecutionPolicy? policy,        /* in: OPTIONAL */
            PolicyType? policyType,         /* in: OPTIONAL */
            string keyName,                 /* in: NOT USED */
            string keyRingName,             /* in */
            int? timeout,                   /* in: OPTIONAL */
            bool embedded,                  /* in: NOT USED */
            bool validate,                  /* in */
            ref string fileName,            /* in, out */
            ref ICertificate certificate,   /* in, out */
            ref Result result               /* out */
            )
        {
            if (Utility.InOfflineMode())
            {
                result = "cannot renew certificate in offline mode";
                return ReturnCode.Error;
            }

            CultureInfo cultureInfo;
            bool disposed;

            /* NO RESULT */
            CertificateDataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                result = "interpreter is disposed";
                return ReturnCode.Error;
            }

            //
            // TODO: Validate the current file name, backup the file, and
            //       use the same file name for the renewed certificate.
            //
            // HACK: Hard-code the request hash algorithm here.
            //
            Uri uri = null;
            NameValueCollection collection = null;

            try
            {
                if (BuildRequest(
                        interpreter, assembly, /* throw */ plugin,
                        Constants.RequestHashAlgorithmName, hashKey,
                        encoding, certificate, cultureInfo, ref uri,
                        ref collection, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
            catch (Exception e)
            {
                result = e;
                return ReturnCode.Error;
            }

            if (uri == null)
            {
                result = "invalid renewal request uri";
                return ReturnCode.Error;
            }

            if (collection == null)
            {
                result = "invalid renewal request collection";
                return ReturnCode.Error;
            }

            //
            // HACK: Must normally set the 'trusted' argument to the
            //       DownloadData method to true here; otherwise,
            //       using the default Eagle server via HTTPS may
            //       fail.
            //
            // NOTE: This does not apply on .NET Core because it does
            //       not have the underlying support from the runtime
            //       for ICertificatePolicy and ServicePointManager.
            //
            bool? trusted = true;

            if (Utility.IsDotNetCore() ||
                Configuration.DoesVariableExist(
                    Constants.NoTrustedRenewalEnvVarName))
            {
                trusted = null;
            }

#if TEST
            //
            // HACK: This should not be (strictly) necessary here.
            //       Most of the (possible) entry points to this
            //       method will have already called this method,
            //       e.g. policy subsystem, (license) certificate
            //       verification subsystem(s), etc.
            //
            if (Utility.SetWebSecurityProtocol(
                    false, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

            byte[] bytes = null;

            if (Utility.UploadValues(
                    interpreter, null, uri, null, collection,
                    NetworkState.GetMaximumRetries(), timeout,
                    trusted, ref bytes, ref result) == ReturnCode.Ok)
            {
                bool renewKeyRings = !Utility.HasFlags(
                    policy, ExecutionPolicy.NoRenewKeyRings, true);

                bool getKeyRings = !Utility.HasFlags(
                    policy, ExecutionPolicy.NoGetKeyPairs, true);

                bool allowAnyPublicKey = Utility.HasFlags(
                    policy, ExecutionPolicy.AllowAnyPublicKey, true);

                bool enforceKeyUsage = Utility.HasFlags(
                    policy, ExecutionPolicy.EnforceKeyUsage, true);

                bool checkRevocation = Utility.HasFlags(
                    policy, ExecutionPolicy.CheckRevocation, true);

                string entityValue = (certificate != null) ?
                    certificate.EntityValue : null;

                EntityType entityType = (certificate != null) ?
                    certificate.EntityType : EntityType.None;

                PolicyType localPolicyType;

                if (policyType != null)
                    localPolicyType = (PolicyType)policyType;
                else
                    localPolicyType = PolicyType.License;

                NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                    localPolicyType);

                if (SharedOps.HasFlags(
                        entityType, EntityType.LicenseTypeMask, false))
                {
                    if (LicenseState.GetForceNetwork())
                        networkFlags |= NetworkFlags.ForceMask;
                }
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                else
                {
                    if (policyType != null)
                        localPolicyType = (PolicyType)policyType;
                    else
                        localPolicyType = PolicyType.Script;

                    if (PolicyState.GetForceNetwork())
                        networkFlags |= NetworkFlags.ForceMask;
                }
#endif

                //
                // NOTE: The flags should indicate the request is coming
                //       from the renewal subsystem.
                //
                networkFlags |= NetworkFlags.ViaRenewal;

                //
                // HACK: If the certificate is null here, just default to
                //       using an NTP server for time queries (i.e. since
                //       the "HttpNetworkTime" flag is a restriction that
                //       is not technically present in that case.
                //
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if ((certificate != null) && SharedOps.MatchFlags(
                        certificate, FlagType.Restriction, flagsKey,
                        null, Restrictions.HttpNetworkTime, false,
                        false, true) != ReturnCode.Ok)
                {
                    networkFlags |= NetworkFlags.ViaHttp;
                }
                else
                {
                    networkFlags &= ~NetworkFlags.ViaHttp;
                }

                ICertificate localCertificate = null;
                CertificateHashFlags? certificateHashFlags = null;
                IEnumerable<IKeyPair> localKeyPairs = keyPairs;
                IKeyPair localKeyPair = null;
                Result localResult = null;

                if ((ExtractAndLoadResponseData(
                        interpreter, encoding, entityValue, entityType,
                        keyName, keyRingName, cultureInfo, policy,
                        validate, renewKeyRings, getKeyRings,
                        !allowAnyPublicKey, enforceKeyUsage,
                        ref bytes, ref localCertificate,
                        ref certificateHashFlags,
                        ref localKeyPairs,
                        ref localResult) == ReturnCode.Ok) &&
                    (Verify(
                        SharedOps.GetHashAlgorithm(
                            hashAlgorithmName, localKeyPairs,
                            localCertificate,
                            HashAlgorithmType.Legacy),
                        hashKey, localCertificate,
                        certificateHashFlags, encoding,
                        localKeyPairs, fileName, timeout,
                        true, checkRevocation, ref localKeyPair,
                        ref localResult) == ReturnCode.Ok) &&
                    (!checkRevocation ||
                    (CertificateRevocationOps.IsRevoked( /* OK */
                        interpreter, assembly, plugin,
                        hashAlgorithmName, hashKey, encoding,
                        localKeyPairs, localCertificate, cultureInfo,
                        timeout, networkFlags & ~NetworkFlags.Strict,
                        ref localResult) == ReturnCode.Ok)) &&
                    (SharedOps.IsExpired(interpreter,
                        assembly, plugin, localCertificate,
                        localKeyPairs, localKeyPair, cultureInfo,
                        null, timeout, localPolicyType, networkFlags,
                        ref localResult) == ReturnCode.Ok))
                {
                    try
                    {
                        byte[] oldKey = (certificate != null) ?
                            certificate.Key : null;

                        byte[] newKey = (localCertificate != null) ?
                            localCertificate.Key : null;

#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(String.Format(
                            "Certificate renewed successfully{0}, " +
                            "fileName = {1}, localCertificate = {2}, " +
                            "localKeyPair = {3}, localResult = {4}",
                            CertificateDataOps.MatchPublicKeyToken(
                                oldKey, newKey) ? String.Empty :
                                " WITH DIFFERENT KEY",
                            Utility.FormatWrapOrNull(fileName),
                            SharedOps.ToString(localCertificate),
                            Utility.FormatWrapOrNull(localKeyPair),
                            Utility.FormatWrapOrNull(true, false, localResult)),
                            typeof(CertificateRenewalOps).Name,
                            TracePriority.MediumHigh);
#endif

                        //
                        // NOTE: This method may be called with an invalid
                        //       file name, because the script is transient
                        //       in nature (e.g. script policy callbacks).
                        //
                        // HACK: For now, renewed script certificates are
                        //       NEVER written out to the file system.
                        //
                        if (!String.IsNullOrEmpty(fileName) &&
                            !Certificate.CanBeEmbedded(localCertificate))
                        {
                            if (!Configuration.DoesVariableExist(
                                    Constants.NoBackupCertificateFileEnvVarName))
                            {
                                string backupFileName = GetBackupFileName(
                                    fileName);

                                if (!String.IsNullOrEmpty(backupFileName))
                                {
                                    File.Move(
                                        fileName, backupFileName); /* throw */
                                }
                            }

                            File.WriteAllBytes(fileName, bytes); /* throw */
                        }
                        else
                        {
                            if (!Configuration.DoesVariableExist(
                                    Constants.NoBackupCertificateFileEnvVarName))
                            {
                                string hashFileName = SharedOps.GetHashFileName(
                                    plugin, hashValue, true);

                                if (!String.IsNullOrEmpty(hashFileName))
                                {
                                    File.WriteAllBytes(
                                        hashFileName, bytes); /* throw */
                                }
                            }
                        }

                        //
                        // HACK: Preserve the existing "Notes" property
                        //       because it will not be sent to us from
                        //       the server.
                        //
                        if ((certificate != null) &&
                            (localCertificate != null))
                        {
                            localCertificate.Notes = certificate.Notes;
                        }

                        certificate = localCertificate;

                        return ReturnCode.Ok;
                    }
                    catch (Exception e)
                    {
                        result = e;
                    }
                }
                else
                {
                    result = localResult;
                }
            }
#if DEBUG || FORCE_TRACE
            else
            {
                CertificateNetworkOps.DebugTraceUriError(
                    "Process", uri, timeout, result,
                    TracePriority.High);
            }
#endif

            return ReturnCode.Error;
        }
    }
}
