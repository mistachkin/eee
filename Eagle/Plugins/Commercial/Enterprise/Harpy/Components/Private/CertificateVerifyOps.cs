/*
 * CertificateVerifyOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;
using CLS = Licensing.Components.Private.CertificateLicenseState;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
using KRS = Licensing.Components.Private.CertificateKeyRingState;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the internal helper methods used to locate, import, and
    /// verify license certificates for an assembly, plugin, or interpreter.
    /// </summary>
    [ObjectId("fbb9682c-8e40-4681-9a85-7eade872cae5")]
    internal static class CertificateVerifyOps
    {
        #region Private Static Methods
        /// <summary>
        /// Determines whether the specified plugin and/or assembly
        /// belongs to this assembly.  The plugin instance is checked
        /// first, via its public properties; failing that, the assembly
        /// and assembly name are matched directly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to match against this assembly.  This is optional
        /// and is only used together with
        /// <paramref name="assemblyName" /> when no plugin is supplied.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name to match against this assembly.  This is
        /// optional and is only used together with
        /// <paramref name="assembly" />.
        /// </param>
        /// <param name="pluginData">
        /// The plugin instance to match against this assembly.  This is
        /// optional and, when supplied, is checked before the assembly.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin and/or assembly belongs to this
        /// assembly; otherwise, zero.
        /// </returns>
        public static bool MatchThisAssembly( /* CORE */
            Assembly assembly,         /* in: EXEMPT, OPTIONAL With assemblyName AND plugin only. */
            AssemblyName assemblyName, /* in: EXEMPT, OPTIONAL With assembly only. */
            IPluginData pluginData     /* in: OPTIONAL With assembly only. */
            )
        {
            //
            // NOTE: First, check the plugin instance itself, via its public
            //       properties for the containing assembly and/or assembly
            //       name.
            //
            if (AssemblyOps.MatchObjectOrName(pluginData))
                return true;

            //
            // NOTE: If the caller specified a plugin instance and it is not
            //       one from this assembly, bail out now; otherwise, attempt
            //       further matching against the assembly (name) itself.
            //
            if (pluginData != null)
                return false;

            //
            // NOTE: Finally, check the assembly and/or assembly name passed
            //       by the caller directly.
            //
            if (AssemblyOps.MatchObject(assembly) ||
                AssemblyOps.MatchName(assemblyName))
            {
                return true;
            }

            //
            // NOTE: If this point is reached, the plugin and/or assembly
            //       parameters do not match this assembly.
            //
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the public key token of the specified key
        /// pair matches a public key token associated with the given
        /// assembly, optionally consulting trusted key groups.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose public key token is to be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly whose public key pairs are used for matching.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name whose key pairs are used for matching.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if a match was found; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchKeyGroup( /* CORE */
            IKeyPair keyPair,          /* in */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref Result error           /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            //
            // WARNING: Do not "optimize" this method to accept the list of
            //          key pairs from the calling method.  This method may
            //          only use assembly key pairs (i.e. none from trusted
            //          key rings).
            //
            IEnumerable<IKeyPair> keyPairs = null;

            if (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref keyPairs,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (keyPairs == null)
            {
                error = "invalid key pair list";
                return ReturnCode.Error;
            }

            foreach (IKeyPair localKeyPair in keyPairs)
            {
                //
                // NOTE: Skip over invalid key pairs in the returned list.
                //
                if (localKeyPair == null)
                    continue;

                //
                // NOTE: Grab the public key token for this key pair.
                //
                byte[] localPublicKeyToken = localKeyPair.PublicKeyToken;

                //
                // NOTE: First, check for a direct public key token match.
                //       This is always allowed.  Historically, this has
                //       been the only check.
                //
                if (CertificateKeyPairOps.MatchPublicKeyToken(
                        keyPair, localPublicKeyToken))
                {
                    return ReturnCode.Ok;
                }

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                //
                // NOTE: Finally, check if the public key token for the
                //       assembly is found in the list of key groups (i.e.
                //       other trusted public key tokens) for the selected
                //       (ring?) key pair.
                //
                if (keyPair.HaveKeyGroup(localPublicKeyToken))
                    return ReturnCode.Ok;
#endif
            }

            error = "assembly key not present in key group";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the product named by the specified certificate is
        /// one of the supported products.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose product name is to be checked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the product is supported;
        /// otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchProduct( /* CORE */
            ICertificate certificate, /* in */
            ref Result error          /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            string product = certificate.Product;

            if (product == null)
            {
                error = "invalid product";
                return ReturnCode.Error;
            }

            StringDictionary products = Constants.Products;

            if (products.ContainsKey(product))
                return ReturnCode.Ok;

            error = "unsupported product";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Extracts the email domain embedded in the specified entity name
        /// and constructs an absolute HTTPS URI from it.
        /// </summary>
        /// <param name="entityName">
        /// The entity name that contains an embedded email address.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing the resulting URI.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The constructed URI, or null if one could not be built.
        /// </returns>
        private static Uri GetUriFromEntityName( /* CORE? */
            string entityName,       /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            string newEntityName = entityName;

            if (newEntityName == null)
            {
                error = "invalid entity name";
                return null;
            }

            newEntityName = newEntityName.Trim();

            int length = newEntityName.Length;

            if (length < Constants.MinimumEmailLength)
            {
                error = "entity name too short for embedded email";
                return null;
            }

            Regex regEx = Constants.EntityEmailRegEx;
            string domainName;

            if (regEx != null)
            {
                Match match = regEx.Match(newEntityName);

                if ((match != null) && match.Success)
                {
                    try
                    {
                        domainName = match.Groups[1].Value; /* throw */
                    }
                    catch (Exception e)
                    {
                        error = e;
                        return null;
                    }
                }
                else
                {
                    error = "entity name has no embedded email domain (1)";
                    return null;
                }
            }
            else /* RARE */
            {
                int startIndex = newEntityName.LastIndexOf(Characters.AtSign);

                if (startIndex == -1)
                {
                    error = "entity name has no embedded email domain (2)";
                    return null;
                }

                startIndex++; /* NOTE: Skip at-sign we just found. */

                if (startIndex >= length)
                {
                    error = "entity name has bad embedded email domain";
                    return null;
                }

                for (int newLength = length; newLength > startIndex; newLength--)
                {
                    int newIndex = newLength - 1;

                    if (newEntityName[newIndex] == Characters.GreaterThanSign)
                    {
                        newEntityName = newEntityName.Substring(0, newIndex);
                        break;
                    }
                }

                domainName = newEntityName.Substring(startIndex);
            }

            string schemeAndDomainName = String.Format("{0}://{1}",
                Uri.UriSchemeHttps, domainName);

            Uri uri = null;

            if (Value.GetUri(
                    schemeAndDomainName, UriKind.Absolute, cultureInfo,
                    ref uri, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return uri;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the email domain embedded in the certificate
        /// entity name matches one of the key domains associated with the
        /// specified key pair.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose key domains are to be matched.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose entity name supplies the domain.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing the embedded domain.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the domain matches (or none is
        /// required); otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchAnyKeyDomain( /* CORE? */
            IKeyPair keyPair,         /* in */
            ICertificate certificate, /* in */
            CultureInfo cultureInfo,  /* in */
            ref Result error          /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            string entityName = certificate.EntityName;

            if (entityName == null)
            {
                error = "invalid entity name";
                return ReturnCode.Error;
            }

            if (!keyPair.HasAnyKeyDomain())
                return ReturnCode.Ok;

            Uri uri = GetUriFromEntityName(
                entityName, cultureInfo, ref error);

            if (uri == null)
                return ReturnCode.Error;

            if (!keyPair.MatchAnyKeyDomain(
                    uri, cultureInfo, ref error))
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Combines the individual boolean options into a single
        /// <see cref="FileNameFlags" /> value.
        /// </summary>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow a remote URI to be used as a file name.
        /// </param>
        /// <param name="looksLikeXml">
        /// Non-zero to require that candidate data look like XML.
        /// </param>
        /// <param name="preValidateXml">
        /// Non-zero to pre-validate candidate data against the schema.
        /// </param>
        /// <param name="useResource">
        /// Non-zero to permit loading data from an embedded resource.
        /// </param>
        /// <param name="traceOnError">
        /// Non-zero to emit trace output when an error occurs.
        /// </param>
        /// <param name="traceOnFound">
        /// Non-zero to emit trace output when a file name is found.
        /// </param>
        /// <param name="traceOnNotFound">
        /// Non-zero to emit trace output when no file name is found.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to allow an embedded resource signed with any public
        /// key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero if the operation is for this assembly.
        /// </param>
        /// <returns>
        /// The combined <see cref="FileNameFlags" /> value.
        /// </returns>
        private static FileNameFlags GetFileNameFlags( /* CORE */
            bool allowRemoteUri,       /* in */
            bool looksLikeXml,         /* in */
            bool preValidateXml,       /* in */
            bool useResource,          /* in */
            bool traceOnError,         /* in */
            bool traceOnFound,         /* in */
            bool traceOnNotFound,      /* in */
            bool anyResourcePublicKey, /* in */
            bool isForThisAssembly     /* in */
            )
        {
            FileNameFlags result = FileNameFlags.None;

            if (allowRemoteUri)
                result |= FileNameFlags.AllowRemoteUri;

            if (looksLikeXml)
                result |= FileNameFlags.LooksLikeXml;

            if (preValidateXml)
                result |= FileNameFlags.PreValidateXml;

            if (useResource)
                result |= FileNameFlags.UseResource;

            if (traceOnError)
                result |= FileNameFlags.TraceOnError;

            if (traceOnFound)
                result |= FileNameFlags.TraceOnFound;

            if (traceOnNotFound)
                result |= FileNameFlags.TraceOnNotFound;

            if (anyResourcePublicKey)
                result |= FileNameFlags.AnyResourcePublicKey;

            if (isForThisAssembly)
                result |= FileNameFlags.IsForThisAssembly;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the verification parameters carried by the specified
        /// client data, when it is a <see cref="VerifyClientData" />
        /// instance.
        /// </summary>
        /// <param name="clientData">
        /// The client data from which to extract the parameters.
        /// </param>
        /// <param name="interpreter">
        /// Receives the interpreter to use.
        /// </param>
        /// <param name="pluginData">
        /// Receives the plugin data to use.
        /// </param>
        /// <param name="encoding">
        /// Receives the encoding to use.
        /// </param>
        /// <param name="logClientData">
        /// Receives the logging client data to use.
        /// </param>
        /// <param name="cultureInfo">
        /// Receives the culture to use.
        /// </param>
        /// <param name="timeout">
        /// Receives the timeout, in milliseconds, to use.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Receives non-zero if a remote URI is allowed.
        /// </param>
        /// <param name="looksLikeXml">
        /// Receives non-zero if candidate data must look like XML.
        /// </param>
        /// <param name="preValidateXml">
        /// Receives non-zero if candidate data is pre-validated.
        /// </param>
        /// <param name="useResource">
        /// Receives non-zero if an embedded resource may be used.
        /// </param>
        /// <param name="traceOnError">
        /// Receives non-zero if errors are traced.
        /// </param>
        /// <param name="traceOnFound">
        /// Receives non-zero if found file names are traced.
        /// </param>
        /// <param name="traceOnNotFound">
        /// Receives non-zero if missing file names are traced.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Receives non-zero if any resource public key is allowed.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Receives non-zero if the operation is for this assembly.
        /// </param>
        private static void GetParametersFromClientData( /* CORE */
            IClientData clientData,           /* in */
            ref Interpreter interpreter,      /* out */
            ref IPluginData pluginData,       /* out */
            ref Encoding encoding,            /* out */
            ref ILogClientData logClientData, /* out */
            ref CultureInfo cultureInfo,      /* out */
            ref int? timeout,                 /* out */
            ref bool allowRemoteUri,          /* out */
            ref bool looksLikeXml,            /* out */
            ref bool preValidateXml,          /* out */
            ref bool useResource,             /* out */
            ref bool traceOnError,            /* out */
            ref bool traceOnFound,            /* out */
            ref bool traceOnNotFound,         /* out */
            ref bool anyResourcePublicKey,    /* out */
            ref bool isForThisAssembly        /* out */
            )
        {
            VerifyClientData verifyClientData =
                clientData as VerifyClientData;

            if (verifyClientData == null)
                return;

            FileNameFlags flags = verifyClientData.FileNameFlags;

            interpreter = verifyClientData.Interpreter;
            pluginData = verifyClientData.PluginData;
            encoding = verifyClientData.Encoding;
            logClientData = verifyClientData.LogClientData;
            cultureInfo = verifyClientData.CultureInfo;
            timeout = verifyClientData.Timeout;

            allowRemoteUri = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.AllowRemoteUri, true);

            looksLikeXml = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.LooksLikeXml, true);

            preValidateXml = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.PreValidateXml, true);

            useResource = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.UseResource, true);

            traceOnError = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.TraceOnError, true);

            traceOnFound = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.TraceOnFound, true);

            traceOnNotFound = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.TraceOnNotFound, true);

            anyResourcePublicKey = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.AnyResourcePublicKey, true);

            isForThisAssembly = CertificateSharedOps.HasFlags(
                flags, FileNameFlags.IsForThisAssembly, true);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified client data as having been validated, when it
        /// is a <see cref="VerifyClientData" /> instance.
        /// </summary>
        /// <param name="clientData">
        /// The client data to mark as validated.
        /// </param>
        private static void SetWasValidatedIntoClientData( /* CORE */
            IClientData clientData /* in */
            )
        {
            VerifyClientData verifyClientData =
                clientData as VerifyClientData;

            if (verifyClientData == null)
                return;

            verifyClientData.WasValidated = true;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the accumulated errors from the specified client data,
        /// when it is a <see cref="VerifyClientData" /> instance.
        /// </summary>
        /// <param name="clientData">
        /// The client data from which to extract the errors.
        /// </param>
        /// <param name="errors">
        /// Receives the accumulated errors, if any.
        /// </param>
        private static void GetErrorsFromClientData( /* CORE */
            IClientData clientData, /* in */
            out ResultList errors   /* out */
            )
        {
            errors = null;

            VerifyClientData verifyClientData =
                clientData as VerifyClientData;

            if (verifyClientData == null)
                return;

            errors = verifyClientData.Errors;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified errors into the client data, when it is a
        /// <see cref="VerifyClientData" /> instance.
        /// </summary>
        /// <param name="clientData">
        /// The client data into which to store the errors.
        /// </param>
        /// <param name="errors">
        /// The errors to store.
        /// </param>
        private static void SetErrorsIntoClientData( /* CORE */
            IClientData clientData, /* in, out */
            ResultList errors       /* in */
            )
        {
            VerifyClientData verifyClientData =
                clientData as VerifyClientData;

            if (verifyClientData == null)
                return;

            verifyClientData.Errors = errors;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the installation date and time from the specified client
        /// data, leaving it unchanged when it is missing or invalid.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.
        /// </param>
        /// <param name="clientData">
        /// The client data from which to read the installation date.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="installed">
        /// Receives the parsed installation date and time, when available.
        /// </param>
        private static void GetInstalledFromClientData( /* CORE */
            Interpreter interpreter,      /* in */
            IClientData clientData,       /* in */
            ILogClientData logClientData, /* in: OPTIONAL */
            ref DateTime? installed       /* out */
            )
        {
            Result error; /* REUSED */
            object value = null;

            error = null;

            if (!CertificateSharedOps.TryGetDataValue(
                    clientData, "installed", ref value, ref error))
            {
#if DEBUG || FORCE_TRACE
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Missing installation date unchanged: {0}",
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                return;
            }

            error = null;

            if (!DataOps.TryParseTimeStampWithKind(
                    Utility.GetStringFromObject(value),
                    DateTimeKind.Utc, ref installed, ref error))
            {
#if DEBUG || FORCE_TRACE
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Bad installation date unchanged {0}: {1}",
                    Utility.FormatWrapOrNull(value),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.Medium, 0);
#endif

                return;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the first file name from the specified list that yields
        /// usable (and, when requested, valid) license certificate data.
        /// </summary>
        /// <param name="fileNames">
        /// The candidate file names, in priority order.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying the verification parameters.  This is
        /// optional.
        /// </param>
        /// <returns>
        /// The first valid file name, or null if none was found.
        /// </returns>
        public static string GetFirstValidFileName( /* CORE */
            IEnumerable<string> fileNames, /* in */
            IClientData clientData         /* in: OPTIONAL */
            ) /* EComPD.ElementSelectionCallback */
        {
            ResultList errors = null;

            if (fileNames == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid file name list");

                goto fail;
            }

            Interpreter interpreter = null;
            IPluginData pluginData = null;
            Encoding encoding = null;
            ILogClientData logClientData = null;
            CultureInfo cultureInfo = null;
            int? timeout = null;
            bool allowRemoteUri = false;
            bool looksLikeXml = false;
            bool preValidateXml = false;
            bool useResource = false;
            bool traceOnError = false;
            bool traceOnFound = false;
            bool traceOnNotFound = false;
            bool anyResourcePublicKey = false;
            bool isForThisAssembly = false;

            GetParametersFromClientData(
                clientData, ref interpreter, ref pluginData,
                ref encoding, ref logClientData, ref cultureInfo,
                ref timeout, ref allowRemoteUri, ref looksLikeXml,
                ref preValidateXml, ref useResource,
                ref traceOnError, ref traceOnFound,
                ref traceOnNotFound, ref anyResourcePublicKey,
                ref isForThisAssembly);

            if (encoding == null)
                encoding = DataOps.GetDefaultEncoding();

            foreach (string fileName in fileNames)
            {
                object data;
                Result error; /* REUSED */
                bool localUseResource = useResource;

                error = null;

                data = CertificateSharedOps.GetDataFromFile(
                    interpreter, encoding, fileName, timeout,
                    allowRemoteUri, anyResourcePublicKey,
                    false, ref localUseResource, ref error);

                if (data == null)
                {
#if DEBUG || FORCE_TRACE
                    if (traceOnError)
                    {
                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Skipping file name {0}, no data from file: {1}",
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(error)),
                            typeof(CertificateVerifyOps).Name,
                            TracePriority.Low, 0);
                    }
#endif

                    continue;
                }

                string text;

                if (localUseResource)
                {
                    if (encoding == null)
                        continue;

                    byte[] bytes = data as byte[];

                    error = null;

                    bytes = CertificateSharedOps.GetEmbeddedBytes(fileName,
                        bytes, CertificateSharedOps.ResourceNameFromFileName(
                        fileName), anyResourcePublicKey, isForThisAssembly,
                        ref error);

                    if (bytes == null)
                    {
#if DEBUG || FORCE_TRACE
                        if (traceOnError)
                        {
                            /* NO RESULT */
                            TraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Skipping file name {0}, no embedded bytes: {1}",
                                Utility.FormatWrapOrNull(fileName),
                                Utility.FormatWrapOrNull(error)),
                                typeof(CertificateVerifyOps).Name,
                                TracePriority.High, 0);
                        }
#endif

                        continue;
                    }

                    text = encoding.GetString(bytes);
                }
                else
                {
                    text = data as string;
                }

                if (text == null)
                    continue;

#if XML && SERIALIZATION
                //
                // HACK: (#1) Encrypted license certificate files require
                //       some special treatment.
                //
                // HACK: (#2) If the text was read from embedded resource
                //            the file name will not cause this method to
                //            return true; therefore, check the resulting
                //            text header itself.
                //
                bool encrypted;

                if (CertificateSharedOps.IsEncryptedFileName(fileName))
                {
                    encrypted = true;
                }
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                else if (localUseResource &&
                    DataOps.HasEncryptedDataHeader(text))
                {
                    encrypted = true;
                }
#endif
                else
                {
                    encrypted = false;
                }

                //
                // NOTE: If the contents of this file do not look like XML,
                //       skip it, if requested.
                //
                if (looksLikeXml &&
                    !CertificateXmlOps.LooksLikeDocument(text, encrypted))
                {
#if DEBUG || FORCE_TRACE
                    /* NO RESULT */
                    TraceOps.MaybeLogAndDebugTrace(
                        logClientData, String.Format(
                        "Skipping file name {0}, not a certificate...",
                        Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateVerifyOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    continue;
                }

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
                //
                // NOTE: Pre-validate the file against the XML schema?  This
                //       only really makes sense when the "looks like XML"
                //       check above passed; however, that is not a strict
                //       requirement.
                //
                if (preValidateXml)
                {
                    //
                    // HACK: Just use the Import method to validate the XML
                    //       schema.  This overload discards the certificate
                    //       itself, which is fine because we do not want it
                    //       at this point.
                    //
                    ICertificate certificate = null; /* NOT USED */

                    error = null;

                    if (Import(
                            interpreter, pluginData, encoding,
                            logClientData, fileName, text, cultureInfo,
                            timeout, encrypted, traceOnError, allowRemoteUri,
                            anyResourcePublicKey, isForThisAssembly, true,
                            ref certificate, ref error) == ReturnCode.Ok)
                    {
                        //
                        // NOTE: At this point, we know the license certificate
                        //       has successfully passed XML schema validation;
                        //       therefore, mark it so in the clientData passed
                        //       by our caller.
                        //
                        SetWasValidatedIntoClientData(clientData);
                    }
                    else
                    {
                        if (error != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(error);
                        }

#if DEBUG || FORCE_TRACE
                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Skipping file name {0}, could not import certificate: {1}",
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(error)),
                            typeof(CertificateVerifyOps).Name,
                            TracePriority.MediumHigh, 0);
#endif

                        continue;
                    }
                }
#endif
#endif

#if DEBUG || FORCE_TRACE
                if (traceOnFound)
                {
                    /* NO RESULT */
                    TraceOps.MaybeLogAndDebugTrace(
                        logClientData, String.Format(
                        "Found valid file name {0} within candidate list {1}",
                        Utility.FormatWrapOrNull(fileName),
                        Utility.FormatWrapOrNull(fileNames)),
                        typeof(CertificateVerifyOps).Name,
                        TracePriority.MediumHigh, 0);
                }
#endif

                return fileName;
            }

#if DEBUG || FORCE_TRACE
            if (traceOnNotFound)
            {
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Did not find valid file name within candidate list {0}",
                    Utility.FormatWrapOrNull(fileNames)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.High, 0);
            }
#endif

        fail:

            SetErrorsIntoClientData(clientData, errors);
            return null;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name is usable, optionally
        /// allowing remote URIs.
        /// </summary>
        /// <param name="fileName">
        /// The file name to check.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying any accumulated errors.  This is
        /// optional.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file name to be a remote URI.
        /// </param>
        /// <returns>
        /// Non-zero if the file name is usable; otherwise, zero.
        /// </returns>
        private static bool CheckFileName( /* CORE */
            string fileName,        /* in */
            IClientData clientData, /* in: OPTIONAL */
            bool allowRemoteUri     /* in */
            )
        {
            Result error = null;

            return CheckFileName(
                fileName, clientData, allowRemoteUri, ref error);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name is usable, optionally
        /// allowing remote URIs and reporting any error.
        /// </summary>
        /// <param name="fileName">
        /// The file name to check.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying any accumulated errors.  This is
        /// optional.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file name to be a remote URI.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the file name is usable; otherwise, zero.
        /// </returns>
        private static bool CheckFileName( /* CORE */
            string fileName,        /* in */
            IClientData clientData, /* in: OPTIONAL */
            bool allowRemoteUri,    /* in */
            ref Result error        /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                ResultList errors;

                GetErrorsFromClientData(clientData, out errors);

                if (errors != null)
                    error = errors;
                else
                    error = "invalid file name";

                return false;
            }

            if (Utility.IsRemoteUri(fileName)) /* EXEMPT */
            {
                if (!allowRemoteUri)
                {
                    error = "file name cannot be a remote uri";
                    return false;
                }

#if NETWORK && TEST
                //
                // TODO: Why is this method call here?  Is this
                //       part of what callers expect from this
                //       method?  Consider removing this after
                //       careful testing.
                //
                // NOTE: Perhaps the justification for calling
                //       this method here is that callers may
                //       wish to know if it will be *possible*
                //       to reliably download a given URI prior
                //       to actually making a decision about a
                //       specific candidate license certificate
                //       file name (i.e. the URI)?  This could
                //       be important if a URI-based candidate
                //       license certificate cannot be used due
                //       to a protocol mismatch -AND- at least
                //       one (viable?) alternative candidate
                //       license certificate exists.
                //
                if (Utility.SetWebSecurityProtocol(
                        false, ref error) != ReturnCode.Ok)
                {
                    return false;
                }
#endif
            }
            else
            {
                if (!CLS.HaveCachedFile(fileName) &&
                    !File.Exists(fileName))
                {
                    error = "file name does not exist";
                    return false;
                }
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the entire contents of the specified stream to a new file.
        /// </summary>
        /// <param name="stream">
        /// The stream whose contents are to be written.
        /// </param>
        /// <param name="fileName">
        /// The name of the file to create.  It must not already exist.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode WriteStreamToFile( /* CORE */
            Stream stream,   /* in */
            string fileName, /* in */
            ref Result error /* out */
            )
        {
            if (stream == null)
            {
                error = "invalid stream";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid file name";
                return ReturnCode.Error;
            }

            if (File.Exists(fileName))
            {
                error = String.Format(
                    "cannot write {0}: file already exists",
                    Utility.FormatWrapOrNull(fileName));

                return ReturnCode.Error;
            }

            try
            {
                long length = stream.Length;
                byte[] bytes = new byte[(int)length];

                using (FileStream fileStream = new FileStream(
                        fileName, FileMode.CreateNew,
                        FileAccess.Write))
                {
                    stream.Read(bytes, 0, (int)length);
                    fileStream.Write(bytes, 0, (int)length);
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Hashes the specified certificate and verifies its signature
        /// against each of the supplied key pairs, returning the key pair
        /// that successfully verified it.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when computing a keyed hash.
        /// </param>
        /// <param name="certificate">
        /// The certificate to hash and verify.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags that control how the certificate is hashed.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the certificate.
        /// </param>
        /// <param name="keyPairs">
        /// The candidate key pairs used to verify the signature.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check the revocation status.
        /// </param>
        /// <param name="keyPair">
        /// Receives the key pair that verified the certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error; upon
        /// success, may receive an informational result.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Process( /* CORE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in */
            Encoding encoding,                          /* in */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.Hash(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "certificate", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify certificate";

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Hashes the specified certificate and verifies its signature
        /// against a single key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when computing a keyed hash.
        /// </param>
        /// <param name="certificate">
        /// The certificate to hash and verify.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags that control how the certificate is hashed.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the certificate.
        /// </param>
        /// <param name="keyPair">
        /// The key pair used to verify the signature.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check the revocation status.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error; upon
        /// success, may receive an informational result.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Process( /* CORE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in */
            Encoding encoding,                          /* in */
            IKeyPair keyPair,                           /* in */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                ReturnCode code;
                byte[] hashBytes = null;

                code = CertificateSharedOps.Hash(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding,
                    ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = CertificateSharedOps.VerifyHash(
                        "certificate", hashBytes, hashAlgorithmName,
                        certificate, keyPair, matchPublicKeyToken,
                        checkRevocation, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Loads (or merges) the trusted license key ring files that are
        /// associated with the specified assembly, plugin, and search
        /// paths.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while loading the key rings.
        /// </param>
        /// <param name="assembly">
        /// The assembly whose directories contribute search paths.  This is
        /// optional.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name whose directory contributes a search path.
        /// This is optional.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose directory contributes a search path.  This
        /// is optional.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while loading the key rings.  This is optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// A file name whose directory contributes a search path.  This is
        /// optional.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to load or merge.  This is optional.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use for any trace output.
        /// </param>
        /// <param name="ignoreKeyRingError">
        /// Non-zero to ignore errors that occur while loading key rings.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode LoadKeyRings( /* CORE? */
            Interpreter interpreter,      /* in */
            Assembly assembly,            /* in: OK, OPTIONAL */
            AssemblyName assemblyName,    /* in: OK, OPTIONAL */
            IPluginData pluginData,       /* in: OPTIONAL */
            ILogClientData logClientData, /* in: OPTIONAL */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            ExecutionPolicy? policy,      /* in: OPTIONAL */
            string fileName,              /* in: OPTIONAL */
            string keyRingName,           /* in: OPTIONAL */
            TracePriority priority,       /* in */
            bool ignoreKeyRingError,      /* in */
            ref Result error              /* out */
            )
        {
            Result localError; /* REUSED */
            TracePriority localPriority; /* REUSED */

            if (!KRS.IsLicensePending())
            {
                /* NO RESULT */
                KRS.BeginLicensePending();

                try
                {
                    ExecutionPolicy localPolicy;

                    if (policy != null)
                    {
                        localPolicy = (ExecutionPolicy)policy;
                    }
                    else
                    {
                        localPolicy = CertificatePolicyOps.GetPolicy(
                            pluginData, PolicyType.License);
                    }

                    StringList paths = null;

                    /* IGNORED */
                    CertificatePathOps.MaybeAddBootstrapDirectories(
                        ref paths);

                    /* IGNORED */
                    CertificatePathOps.MaybeAddDirectoryNames(
                        assembly, ref paths);

                    /* IGNORED */
                    CertificatePathOps.MaybeAddDirectoryName(
                        assemblyName, ref paths);

                    /* IGNORED */
                    CertificatePathOps.MaybeAddDirectoryName(
                        pluginData, ref paths);

                    /* IGNORED */
                    CertificatePathOps.MaybeAddDirectoryName(
                        fileName, ref paths);

                    if (paths != null)
                        paths = Utility.GetUniqueElements(paths);

                    int loaded = 0;

                    localError = null;

                    if (CertificateKeyRingOps.LoadLicenseKeyPairsPublicOnly(
                            interpreter, keyRingName, pluginData, paths,
                            cultureInfo, localPolicy, priority, true,
                            true, true, true, ref loaded,
                            ref localError) == ReturnCode.Ok) /* RECURSIVE */
                    {
#if DEBUG || FORCE_TRACE
                        localPriority = priority;

                        Utility.AdjustTracePriority(ref localPriority, 0);

                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Loaded {0} license key pair files for plugin {1}",
                            loaded, Utility.FormatWrapOrNull(pluginData)),
                            typeof(CertificateVerifyOps).Name,
                            localPriority, 0);
#endif
                    }
                    else if (ignoreKeyRingError)
                    {
#if DEBUG || FORCE_TRACE
                        localPriority = priority;

                        Utility.AdjustTracePriority(ref localPriority, 2);

                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Ignored license key pair file load error for plugin {1}: {0}",
                            Utility.FormatWrapOrNull(true, false, localError),
                            Utility.FormatWrapOrNull(pluginData)),
                            typeof(CertificateVerifyOps).Name,
                            localPriority, 0);
#endif
                    }
                    else
                    {
                        error = localError;
                        return ReturnCode.Error;
                    }
                }
                finally
                {
                    /* NO RESULT */
                    KRS.EndLicensePending();
                }
            }
            else
            {
                //
                // BUGFIX: *HACK* There does not seem to be any reason why
                //         we cannot use the pre-existing trusted license
                //         key ring here.
                //
                int merged = 0;

                localError = null;

                if (KRS.MergeAnyTrusted(
                        interpreter, keyRingName, keyRingName,
                        true, true, true, ref merged,
                        ref localError) == ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, 0);

                    /* NO RESULT */
                    TraceOps.MaybeLogAndDebugTrace(
                        logClientData, String.Format(
                        "Merged {0} license key pairs for plugin {1}",
                        merged, Utility.FormatWrapOrNull(pluginData)),
                        typeof(CertificateVerifyOps).Name,
                        localPriority, 0);
#endif
                }
                else if (ignoreKeyRingError)
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, 2);

                    /* NO RESULT */
                    TraceOps.MaybeLogAndDebugTrace(
                        logClientData, String.Format(
                        "Ignored license key ring merge error for plugin {1}: {0}",
                        Utility.FormatWrapOrNull(true, false, localError),
                        Utility.FormatWrapOrNull(pluginData)),
                        typeof(CertificateVerifyOps).Name,
                        localPriority, 0);
#endif
                }
                else
                {
                    error = localError;
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Optionally loads the license key ring files and then gathers the
        /// public-only license key pairs that may be used to verify a
        /// certificate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while loading and gathering key pairs.
        /// </param>
        /// <param name="assembly">
        /// The assembly used to gather key pairs.  This is optional.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used to gather key pairs.  This is optional.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to gather key pairs.  This is optional.
        /// </param>
        /// <param name="keyPair">
        /// An extra key pair to include.  This is optional.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while gathering key pairs.  This is optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use.  This is optional.
        /// </param>
        /// <param name="keyName">
        /// The name of the key to gather.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// A file name whose directory contributes a search path.  This is
        /// optional.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to use.  This is optional.
        /// </param>
        /// <param name="policyType">
        /// The type of policy governing the operation.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use for any trace output.
        /// </param>
        /// <param name="allowAssemblyPublicKey">
        /// Non-zero to allow public keys from the assembly.
        /// </param>
        /// <param name="allowEmbeddedPublicKey">
        /// Non-zero to allow embedded public keys.
        /// </param>
        /// <param name="allowRingPublicKey">
        /// Non-zero to allow public keys from the key ring.
        /// </param>
        /// <param name="allowAnyPublicKey">
        /// Non-zero to allow any public key.
        /// </param>
        /// <param name="enforceKeyUsage">
        /// Non-zero to enforce the allowed key usage.
        /// </param>
        /// <param name="ignoreKeyRingError">
        /// Non-zero to ignore errors that occur while loading key rings.
        /// </param>
        /// <param name="keyPairs">
        /// Receives the gathered public-only key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MaybeLoadKeyRingsAndThenGetKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter,            /* in */
            Assembly assembly,                  /* in: OK, OPTIONAL */
            AssemblyName assemblyName,          /* in: OK, OPTIONAL */
            IPluginData pluginData,             /* in: OPTIONAL */
            IKeyPair keyPair,                   /* in: OPTIONAL */
            ILogClientData logClientData,       /* in: OPTIONAL */
            CultureInfo cultureInfo,            /* in: OPTIONAL */
            ExecutionPolicy? policy,            /* in: OPTIONAL */
            string keyName,                     /* in: OPTIONAL */
            string fileName,                    /* in: OPTIONAL */
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            TracePriority priority,             /* in */
            bool allowAssemblyPublicKey,        /* in */
            bool allowEmbeddedPublicKey,        /* in */
            bool allowRingPublicKey,            /* in */
            bool allowAnyPublicKey,             /* in */
            bool enforceKeyUsage,               /* in */
            bool ignoreKeyRingError,            /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            ) /* THREAD-SAFE, RE-ENTRANT */
        {
            Result localError; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            #region Phase 1: Maybe Load All License Key Ring Files
            if (!Configuration.DoesVariableExist(
                    Constants.NoLoadLicenseKeyRingsEnvVarName))
            {
                if (allowRingPublicKey &&
                    CertificateKeyRingOps.CanLoadKeyPairs(
                        pluginData, policyType, policy))
                {
                    localError = null;

                    if (LoadKeyRings(
                            interpreter, assembly, assemblyName, pluginData,
                            logClientData, cultureInfo, policy, fileName,
                            keyRingName, priority, ignoreKeyRingError,
                            ref localError) != ReturnCode.Ok)
                    {
                        error = localError;
                        return ReturnCode.Error;
                    }
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Phase 2: Always Gather All License Key Pairs
            //
            // NOTE: These key pairs are only used locally within this method
            //       and its caller and are NOT used by evaluate scripts -OR-
            //       load any other key pairs.
            //
            localError = null;

            if (CertificateKeyPairOps.GetPublicOnly( /* OK */
                    keyRingName, policyType, false, assembly,
                    assemblyName, keyPair, keyName, false,
                    interpreter, EntityType.LicenseTypeMask,
                    allowAssemblyPublicKey, allowEmbeddedPublicKey,
                    allowRingPublicKey, !allowAnyPublicKey,
                    enforceKeyUsage, ref keyPairs,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables policy tracing when the associated environment variable
        /// is set and the specified certificate has the corresponding
        /// feature flag.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose feature flags are examined.
        /// </param>
        private static void MaybeEnablePolicyTracing( /* CORE? */
            ICertificate certificate /* in */
            )
        {
            //
            // NOTE: If the EnablePolicyTracing environment variable is
            //       set, check license certificate for the associated
            //       feature flag and then enable the policy tracing.
            //
            if (Configuration.DoesVariableExist(
                    Constants.EnablePolicyTracingEnvVarName))
            {
                //
                // NOTE: If the license certificate for Harpy itself
                //       has the "enable policy tracing" flag enabled,
                //       do that now, by force if necessary.
                //
                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if (CertificateSharedOps.MatchFlags(
                        certificate, FlagType.Feature,
                        flagsKey, Features.EnablePolicyTracingOrAll,
                        null, false, false, true) == ReturnCode.Ok)
                {
                    CertificatePolicyOps.EnablePolicyTracing(true);
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to request a new license certificate from the official
        /// server by evaluating the request script, then saves the result
        /// to a new file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the request script.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="fileNames">
        /// The list of candidate file names, updated as needed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode TryRequest( /* CORE */
            Interpreter interpreter,      /* in */
            ILogClientData logClientData, /* in: OPTIONAL */
            ref StringList fileNames,     /* in, out */
            ref Result error              /* out */
            )
        {
            //
            // NOTE: Since the primary purpose of this method is to evaluate
            //       a script, an interpreter is required.
            //
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            //
            // NOTE: Attempt to obtain the name of the directory that should
            //       be used to save a license certificate that was obtained
            //       from the official server (e.g. probably a temporary one
            //       of some kind).
            //
            string directory = CertificatePathOps.MaybeCreateSaveDirectory(
                BootstrapType.License, false, true);

            if (String.IsNullOrEmpty(directory))
            {
                error = "could not create save directory";
                return ReturnCode.Error;
            }

            //
            // NOTE: Next, grab the default file name (only) for a license
            //       certificate.  Should be the value "certificate.xml".
            //
            string fileNameOnly = CertificatePathOps.GetDefaultFileName(
                false); /* COMPAT: Eagle beta. */

            if (String.IsNullOrEmpty(fileNameOnly))
            {
                error = "default file name is unavailable";
                return ReturnCode.Error;
            }

            //
            // NOTE: Combine both of the above path fragments into a fully
            //       qualified file name, where the newly requested license
            //       certificate should be saved.  This file cannot already
            //       exist (i.e. for that case, it should have already been
            //       picked up by the license certificate search routines,
            //       e.g. CertificatePathOps.GetFileNames, etc).
            //
            string newFileName = Path.Combine(directory, fileNameOnly);

            if (File.Exists(newFileName))
            {
                error = "saved file name already exists";
                return ReturnCode.Error;
            }

            //
            // NOTE: Attempt to evaluate the license certificate request
            //       script.  There are a lot of ways this could fail,
            //       e.g. lack of a complete core script library within
            //       the interpreter, corrupted interpreter state, no
            //       network access, etc.
            //
            // TODO: Perhaps it would be better here to use a completely
            //       new interpreter?
            //
            // WARNING: *SECURITY* Since the origin of this interpreter
            //          is unknown and the script library procedure to
            //          call could have been overridden, do not treat
            //          this as a trusted script evaluation.
            //
            Result result = null;

            if (interpreter.EvaluateScript(
                    Constants.RequestScript, ref result) == ReturnCode.Ok)
            {
                //
                // NOTE: The result of the license certificate request
                //       script will be the local temporary file name
                //       upon success.  Save it to the old file name.
                //
                string oldFileName = result;

                try
                {
                    //
                    // NOTE: Make sure the license certificate file
                    //       actually exists before trying to move
                    //       it to its final "saved" location.  If
                    //       not, fail now.
                    //
                    if (!File.Exists(oldFileName))
                    {
                        error = "response file name does not exist";
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Physically move the license certificate
                    //       file to its "saved" location.  This may
                    //       fail.  In that case, an exception will
                    //       be thrown.
                    //
                    File.Move(oldFileName, newFileName); /* throw */

                    //
                    // NOTE: Grab path to (temporary) directory that
                    //       held the temporary license certificate
                    //       file.  It should now be empty; so, try
                    //       to delete it.
                    //
                    string oldDirectory = Path.GetDirectoryName(
                        oldFileName);

                    if (!String.IsNullOrEmpty(oldDirectory) &&
                        Directory.Exists(oldDirectory))
                    {
                        Directory.Delete(oldDirectory, false); /* throw */
                    }

                    //
                    // NOTE: If we reach this point, we have totally
                    //       succeeded.
                    //
                    return ReturnCode.Ok;
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    /* NO RESULT */
                    TraceOps.MaybeLogAndDebugTrace(
                        logClientData, String.Format(
                        "Failed to save response file {0}: {1}",
                        Utility.FormatWrapOrNull(oldFileName),
                        Utility.FormatTraceException(e)),
                        typeof(CertificateVerifyOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    error = "failed to save response file";
                    return ReturnCode.Error;
                }
            }
            else
            {
#if DEBUG || FORCE_TRACE
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Failed to evaluate request script {0}: {1}",
                    Utility.FormatWrapOrNull(Constants.RequestScript),
                    Utility.FormatWrapOrNull(true, false, result)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                error = "failed to evaluate request script";
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether at least one usable license certificate file
        /// name is available among the specified candidates.
        /// </summary>
        /// <param name="fileNames">
        /// The candidate file names to consider.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// A single candidate file name to consider.  This is optional.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying any accumulated errors.  This is
        /// optional.
        /// </param>
        /// <param name="existingOnly">
        /// Non-zero to require that a candidate file actually exist.
        /// </param>
        /// <returns>
        /// Non-zero if a usable file name is available; otherwise, zero.
        /// </returns>
        private static bool HaveFileName( /* CORE */
            StringList fileNames,   /* in: OPTIONAL */
            string fileName,        /* in: OPTIONAL */
            IClientData clientData, /* in: OPTIONAL */
            bool existingOnly       /* in */
            )
        {
            if (fileName != null)
            {
                if (!existingOnly)
                    return true;

                if (CheckFileName(
                        fileName, clientData, false))
                {
                    return true;
                }
            }

            if ((fileNames != null) &&
                (fileNames.Count > 0))
            {
                if (!existingOnly)
                    return true;

                foreach (string localFileName in fileNames)
                {
                    if (CheckFileName(
                            localFileName, clientData, false))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Verifies that the machine identifier embedded in the certificate
        /// matches the current machine, optionally provisioning a new
        /// certificate for this machine when permitted.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the certificate.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with the certificate.
        /// </param>
        /// <param name="encoding">
        /// The encoding used while provisioning.  This is optional.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// The file name of the certificate being checked.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while parsing identifiers.  This is optional.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used while provisioning.  This is
        /// optional.
        /// </param>
        /// <param name="traceOnError">
        /// Non-zero to emit trace output when an error occurs.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow a remote URI to be used.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to allow an embedded resource signed with any public
        /// key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero if the operation is for this assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate against the schema.
        /// </param>
        /// <param name="wasReimported">
        /// Non-zero if the certificate has already been reimported.
        /// </param>
        /// <param name="certificate">
        /// The certificate to check, replaced upon provisioning.
        /// </param>
        /// <param name="reimported">
        /// Receives non-zero when the certificate was reimported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchMachineId( /* CORE? */
            Interpreter interpreter,      /* in */
            Assembly assembly,            /* in: EXEMPT */
            IPlugin plugin,               /* in */
            Encoding encoding,            /* in: OPTIONAL */
            ILogClientData logClientData, /* in: OPTIONAL */
            string fileName,              /* in */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            int? timeout,                 /* in: OPTIONAL */
            bool traceOnError,            /* in */
            bool allowRemoteUri,          /* in */
            bool anyResourcePublicKey,    /* in */
            bool isForThisAssembly,       /* in */
            bool validate,                /* in */
            bool wasReimported,           /* in */
            ref ICertificate certificate, /* in, out */
            ref bool reimported,          /* out */
            ref Result error              /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            if (!CertificateSharedOps.HasFlags(
                    certificate.EntityType, EntityType.Machine, true))
            {
                return ReturnCode.Ok;
            }

            Guid? machineId1 = CertificatePolicyOps.TryParseAsMachineId(
                certificate.EntityName, cultureInfo, ref error);

            if (machineId1 == null)
                return ReturnCode.Error;

            PathFlags pathFlags = CLS.GetPathFlagsOrDefault();

            Guid? machineId2 = CertificatePolicyOps.GetMachineId(
                interpreter, null, cultureInfo, pathFlags, ref error);

            if (machineId2 == null)
                return ReturnCode.Error;

            if (((Guid)machineId1).Equals((Guid)machineId2))
            {
                return ReturnCode.Ok;
            }
            else
            {
#if NETWORK && WEB
                if (wasReimported)
                {
                    error = String.Format(
                        "cannot automatically provision license " +
                        "certificate for machine {0} ({1}), the " +
                        "previous attempt may have failed?",
                        Utility.FormatWrapOrNull(machineId2),
                        Utility.FormatWrapOrNull(pathFlags));

                    return ReturnCode.Error;
                }

                long flagsKey = Utility.DefaultAttributeFlagsKey();

                if (CertificateSharedOps.MatchFlags(
                        certificate, FlagType.Feature, flagsKey,
                        Features.AutoProvisionOrAll, null, false,
                        false, true) == ReturnCode.Ok)
                {
                    //
                    // NOTE: Before doing anything else, make sure
                    //       the existing license certificate file
                    //       name can be backed up prior to being
                    //       replaced.
                    //
                    string backupFileName = String.Format(
                        "{0}-provision-{1}{2}", fileName,
                        machineId1, FileExtension.Backup);

                    if (File.Exists(backupFileName))
                    {
                        error = String.Format(
                            "cannot automatically provision license " +
                            "certificate for machine {0} ({1}), the " +
                            "associated backup file named {2} already " +
                            "appears to exists",
                            Utility.FormatWrapOrNull(machineId2),
                            Utility.FormatWrapOrNull(pathFlags),
                            Utility.FormatWrapOrNull(backupFileName));

                        return ReturnCode.Error;
                    }

                    //
                    // HACK: The entity name from the certificate,
                    //       which we now know parses as a GUID,
                    //       will be used as the API key for the
                    //       remote URI call used to provision a
                    //       license certificate for this machine.
                    //
                    DateTime now = Utility.GetUtcNow();

                    TimeSpan? duration =
                        CertificateSharedOps.RemainingDuration(
                            certificate, now, ref error);

                    if (duration == null)
                        return ReturnCode.Error;

                    Uri baseUri = Helpers.GetProvisionBaseUri(
                        assembly, null, ref error);

                    if (baseUri == null)
                        return ReturnCode.Error;

                    string xml = null;

                    if (Helpers.ProvisionLicense(
                            interpreter, baseUri, encoding,
                            (Guid)machineId1, (Guid)machineId2,
                            (TimeSpan)duration, timeout, ref xml,
                            ref error) == ReturnCode.Ok)
                    {
                        try
                        {
                            File.Move(fileName,
                                backupFileName); /* throw */

                            File.WriteAllText(
                                fileName, xml); /* throw */

                            ICertificate provisionCertificate = null;
                            Result provisionResult = null;

                            if (Import(
                                    interpreter, plugin, encoding,
                                    logClientData, fileName, cultureInfo,
                                    timeout, traceOnError, allowRemoteUri,
                                    anyResourcePublicKey, isForThisAssembly,
                                    validate, ref provisionCertificate,
                                    ref provisionResult) == ReturnCode.Ok)
                            {
#if DEBUG || FORCE_TRACE
                                /* NO RESULT */
                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Provisioned license certificate {0} " +
                                    "for machine {1} ({2}) via URI {3}: {4}",
                                    Utility.FormatWrapOrNull(fileName),
                                    Utility.FormatWrapOrNull(machineId2),
                                    Utility.FormatWrapOrNull(pathFlags),
                                    Utility.FormatWrapOrNull(baseUri),
                                    DebugOnlyOps.FormatCertificate(
                                        provisionCertificate)),
                                    typeof(CertificateVerifyOps).Name,
                                    TracePriority.Highest, 0);
#endif

                                certificate = provisionCertificate;
                                reimported = true;

                                return ReturnCode.Ok;
                            }
                            else
                            {
                                error = provisionResult;
                            }
                        }
                        catch (Exception e)
                        {
                            error = e;
                        }
                    }

                    return ReturnCode.Error;
                }
#endif

                error = String.Format(
                    "wrong machine identifier {0} ({1}) " +
                    "for certificate {2}, must be {3}",
                    Utility.FormatWrapOrNull(machineId2),
                    Utility.FormatWrapOrNull(pathFlags),
                    CertificateSharedOps.ToString(certificate),
                    Utility.FormatWrapOrNull(machineId1));

                return ReturnCode.Error;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// Determines whether the public key token of the specified
        /// certificate is present among the supplied key pairs.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose public key token is sought.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to search.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the key pair is present;
        /// otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode CheckForKeyPairPresent(
            ICertificate certificate,       /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            ref Result error                /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            if (keyPairs == null)
            {
                error = "invalid key pairs";
                return ReturnCode.Error;
            }

            foreach (IKeyPair keyPair in keyPairs)
            {
                if (keyPair == null)
                    continue;

                //
                // HACK: We do not care if the Key property is null
                //       here, i.e. if there really exists a public
                //       key with a null public key token, so be it.
                //
                if (CertificateDataOps.MatchPublicKeyToken(
                        certificate.Key, keyPair.PublicKeyToken))
                {
                    return ReturnCode.Ok;
                }
            }

            error = String.Format(
                "key pair {0} not present in list {1} for certificate {2}",
                CertificateDataOps.FormatPublicKeyToken(certificate.Key, true, true),
                Utility.FormatWrapOrNull(CertificateDataOps.FormatKeyPairs(
                keyPairs, true)), DebugOnlyOps.FormatCertificate(certificate));

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the license certificate data from the specified file (or
        /// embedded resource) and imports it into a certificate object.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the operation.  This is optional.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when reading the data.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// The file name (or resource name) to import.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while importing.  This is optional.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading data.  This is
        /// optional.
        /// </param>
        /// <param name="traceOnError">
        /// Non-zero to emit trace output when an error occurs.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow a remote URI to be used.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to allow an embedded resource signed with any public
        /// key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero if the operation is for this assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate against the schema.
        /// </param>
        /// <param name="certificate">
        /// Receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Import( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            IPluginData pluginData,       /* in: OPTIONAL */
            Encoding encoding,            /* in */
            ILogClientData logClientData, /* in: OPTIONAL */
            string fileName,              /* in */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            int? timeout,                 /* in: OPTIONAL */
            bool traceOnError,            /* in */
            bool allowRemoteUri,          /* in */
            bool anyResourcePublicKey,    /* in */
            bool isForThisAssembly,       /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            string text = null;
            bool encrypted = false;

            if (CertificateSharedOps.IsAssemblyFileName(fileName) ||
                CertificateSharedOps.IsEncryptedFileName(fileName))
            {
                bool useResource = true;

                object data = CertificateSharedOps.GetDataFromFile(
                    interpreter, encoding, fileName, timeout,
                    allowRemoteUri, anyResourcePublicKey, false,
                    ref useResource, ref result);

                if (data == null)
                {
#if DEBUG || FORCE_TRACE
                    if (traceOnError)
                    {
                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "No data from file {0} to import: {1} (1)",
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(result)),
                            typeof(CertificateVerifyOps).Name,
                            TracePriority.Highest, 0);
                    }
#endif

                    return ReturnCode.Error;
                }

                if (useResource)
                {
                    if (encoding == null)
                    {
                        result = "invalid encoding for license data";

#if DEBUG || FORCE_TRACE
                        if (traceOnError)
                        {
                            /* NO RESULT */
                            TraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "No data from file {0} to import: {1} (2)",
                                Utility.FormatWrapOrNull(fileName),
                                Utility.FormatWrapOrNull(result)),
                                typeof(CertificateVerifyOps).Name,
                                TracePriority.Highest, 0);
                        }
#endif

                        return ReturnCode.Error;
                    }

                    byte[] bytes = data as byte[];

                    bytes = CertificateSharedOps.GetEmbeddedBytes(fileName,
                        bytes, CertificateSharedOps.ResourceNameFromFileName(
                        fileName), anyResourcePublicKey, isForThisAssembly,
                        ref result);

                    if (bytes == null)
                    {
#if DEBUG || FORCE_TRACE
                        if (traceOnError)
                        {
                            /* NO RESULT */
                            TraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "No data from file {0} to import: {1} (3)",
                                Utility.FormatWrapOrNull(fileName),
                                Utility.FormatWrapOrNull(result)),
                                typeof(CertificateVerifyOps).Name,
                                TracePriority.Highest, 0);
                        }
#endif

                        return ReturnCode.Error;
                    }

                    text = encoding.GetString(bytes);
                }
                else
                {
                    text = data as string;

                    if (text == null)
                    {
                        result = "license data does not contain text";

#if DEBUG || FORCE_TRACE
                        if (traceOnError)
                        {
                            /* NO RESULT */
                            TraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "No data from file {0} to import: {1} (4)",
                                Utility.FormatWrapOrNull(fileName),
                                Utility.FormatWrapOrNull(result)),
                                typeof(CertificateVerifyOps).Name,
                                TracePriority.Highest, 0);
                        }
#endif

                        return ReturnCode.Error;
                    }
                }

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                if (!useResource ||
                    CertificateDataOps.HasEncryptedDataHeader(text))
                {
                    encrypted = true;
                }
#endif
            }

            return Import(
                interpreter, pluginData, encoding, logClientData,
                fileName, text, cultureInfo, timeout, encrypted,
                traceOnError, allowRemoteUri, anyResourcePublicKey,
                isForThisAssembly, validate, ref certificate,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports the supplied license certificate text into a certificate
        /// object, decrypting it first when it is encrypted.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the operation.  This is optional.
        /// </param>
        /// <param name="encoding">
        /// The encoding used while importing.  This is optional.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// The file name associated with the text.
        /// </param>
        /// <param name="text">
        /// The license certificate text to import.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while importing.  This is optional.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used while decrypting.  This is
        /// optional.
        /// </param>
        /// <param name="encrypted">
        /// Non-zero if the supplied text is encrypted.
        /// </param>
        /// <param name="traceOnError">
        /// Non-zero to emit trace output when an error occurs.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow a remote URI to be used.  This is not used.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to allow an embedded resource signed with any public
        /// key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero if the operation is for this assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate against the schema.
        /// </param>
        /// <param name="certificate">
        /// Receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode Import( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            IPluginData pluginData,       /* in: OPTIONAL */
            Encoding encoding,            /* in: OPTIONAL */
            ILogClientData logClientData, /* in: OPTIONAL */
            string fileName,              /* in */
            string text,                  /* in */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            int? timeout,                 /* in: OPTIONAL */
            bool encrypted,               /* in */
            bool traceOnError,            /* in */
            bool allowRemoteUri,          /* in: NOT USED */
            bool anyResourcePublicKey,    /* in */
            bool isForThisAssembly,       /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (encrypted)
            {
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                byte[] newData = null;

                if (CryptographyOps.ObtainParametersAndDecrypt(
                        interpreter, pluginData, encoding,
                        fileName, text, cultureInfo, timeout,
                        traceOnError || (logClientData != null),
                        ref newData, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (CertificateXmlOps.Import(
                        fileName, newData, validate, ref certificate,
                        ref result) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    if (traceOnError)
                    {
                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Could not import encrypted certificate: {0}",
                            Utility.FormatWrapOrNull(result)),
                            typeof(CertificateVerifyOps).Name,
                            TracePriority.Highest, 0);
                    }
#endif

                    return ReturnCode.Error;
                }
#else
                result = "encrypted license certificates unsupported";
                return ReturnCode.Error;
#endif
            }
            else
            {
                if (CertificateXmlOps.Import(fileName,
                        anyResourcePublicKey, isForThisAssembly, validate,
                        ref certificate, ref result) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    if (traceOnError)
                    {
                        /* NO RESULT */
                        TraceOps.MaybeLogAndDebugTrace(
                            logClientData, String.Format(
                            "Could not import certificate: {0}",
                            Utility.FormatWrapOrNull(result)),
                            typeof(CertificateVerifyOps).Name,
                            TracePriority.Highest, 0);
                    }
#endif

                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified list of file names as a single string, with
        /// one file name per line.
        /// </summary>
        /// <param name="fileNames">
        /// The file names to format.
        /// </param>
        /// <returns>
        /// The formatted string, or null if the list was null.
        /// </returns>
        private static string FileNamesToString( /* CORE */
            StringList fileNames /* in */
            )
        {
            if (fileNames == null)
                return null;

            int count = fileNames.Count;

            if (count == 0)
                return String.Empty;

            if (count == 1)
                return fileNames[0];

            return fileNames.ToString(
                Environment.NewLine, null, false, false);
        }
#endif

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a human-readable description of the package being
        /// verified, based on the plugin, assembly, and interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the package.  This is optional.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the package.  This is optional.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name associated with the package.  This is optional.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the package.  This is optional.
        /// </param>
        /// <returns>
        /// The package description, or null if none could be built.
        /// </returns>
        private static string GetPackageName( /* CORE */
            Interpreter interpreter,   /* in: OPTIONAL */
            Assembly assembly,         /* in: EXEMPT, OPTIONAL With assemblyName AND plugin only. */
            AssemblyName assemblyName, /* in: EXEMPT, OPTIONAL With assembly only. */
            IPluginData pluginData     /* in: OPTIONAL With assembly only. */
            )
        {
            bool useFullPackageName = Configuration.DoesVariableExist(
                Constants.UseFullPackageNameEnvVarName);

            StringBuilder builder = null;
            bool havePlugin = false;

            if (pluginData != null)
            {
                CertificateDataOps.AppendTo(
                    ref builder, "within", false);

                if (CertificateSharedOps.IsCrossAppDomain(
                        interpreter, pluginData))
                {
                    CertificateDataOps.AppendTo(
                        ref builder, "isolated plugin",
                        false);
                }
                else
                {
                    CertificateDataOps.AppendTo(
                        ref builder, "local plugin",
                        false);
                }

                CertificateDataOps.AppendTo(ref builder,
                    CertificateDataOps.FormatPluginName(
                        pluginData), false);

                havePlugin = true;
            }

            if (!havePlugin || useFullPackageName)
            {
                if (assembly != null)
                {
                    CertificateDataOps.AppendTo(
                        ref builder, "within", true);

                    CertificateDataOps.AppendTo(
                        ref builder, "loaded assembly", false);

                    CertificateDataOps.AppendTo(ref builder,
                        CertificateDataOps.FormatAssembly(
                            assembly, havePlugin &&
                            !useFullPackageName), false);
                }
                else if (assemblyName != null)
                {
                    CertificateDataOps.AppendTo(
                        ref builder, "within", true);

                    CertificateDataOps.AppendTo(
                        ref builder, "named assembly", false);

                    CertificateDataOps.AppendTo(ref builder,
                        CertificateDataOps.FormatAssemblyName(
                            assemblyName), false);
                }
            }

            CertificateDataOps.AppendTo(ref builder,
                String.Format("for interpreter {0}",
                CertificateDataOps.FormatInterpreter(
                    interpreter, false, false)), false);

            return (builder != null) ? builder.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts a GUID identifier embedded in the specified entity name
        /// using the supplied regular expression.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="entityName">
        /// The entity name that contains an embedded identifier.
        /// </param>
        /// <param name="regEx">
        /// The regular expression used to locate the identifier.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while parsing the identifier.  This is optional.
        /// </param>
        /// <param name="minimumLength">
        /// The minimum allowed length of the entity name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The extracted identifier, or null if one could not be parsed.
        /// </returns>
        private static Guid? GetIdFromEntityName( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            string entityName,       /* in */
            Regex regEx,             /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            int minimumLength,       /* in */
            ref Result error         /* out */
            )
        {
            string newEntityName = entityName;

            if (newEntityName == null)
            {
                error = "invalid entity name";
                return null;
            }

            newEntityName = newEntityName.Trim();

            int length = newEntityName.Length;

            if ((minimumLength > 0) && (length < minimumLength))
            {
                error = "entity name too short for identifier";
                return null;
            }

            if (regEx == null)
            {
                error = "missing pattern for identifier";
                return null;
            }

            string idString;
            Match match = regEx.Match(newEntityName);

            if ((match != null) && match.Success)
            {
                try
                {
                    idString = match.Groups[1].Value; /* throw */
                }
                catch (Exception e)
                {
                    error = e;
                    return null;
                }
            }
            else
            {
                error = "entity name has no embedded identifier";
                return null;
            }

            Guid id = Guid.Empty;

            if (Value.GetGuid(idString,
                    cultureInfo, ref id, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return id;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the process identifier embedded in the
        /// certificate, if any, matches the current process.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose entity name supplies the identifier.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while parsing the identifier.  This is optional.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the identifier matches (or none
        /// is present); otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CheckForProcess( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            ICertificate certificate,     /* in */
            ILogClientData logClientData, /* in: OPTIONAL */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            ref Result error              /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            Guid? wantId;
            Result localError = null;

            wantId = GetIdFromEntityName(interpreter,
                certificate.EntityName, Constants.ProcessRegEx,
                cultureInfo, Constants.MinimumProcessLength,
                ref localError);

            if (wantId == null)
            {
#if DEBUG || FORCE_TRACE
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Process not present: {0}",
                    Utility.FormatWrapOrNull(localError)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.Low, 0);
#endif

                //
                // NOTE: Technically, an embedded process identifier
                //       is optional; therefore, this cannot fail for
                //       a missing one.
                //
                return ReturnCode.Ok;
            }

            Guid? haveId = CertificateSharedOps.TryExtractProcessId(
                interpreter, cultureInfo, Constants.ProcessPluginFlags,
                ref error);

            if (haveId == null)
                return ReturnCode.Error;

            if (!((Guid)haveId).Equals((Guid)wantId))
            {
                error = String.Format(
                    "process identifier mismatch, have {0}, want {1}",
                    Utility.FormatWrapOrNull(haveId),
                    Utility.FormatWrapOrNull(wantId));

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the required license certificate referenced by the
        /// certificate, if any, is currently present.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose entity name supplies the requirement.
        /// </param>
        /// <param name="logClientData">
        /// The logging client data used for trace output.  This is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while parsing the identifier.  This is optional.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the requirement is satisfied (or
        /// none is present); otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CheckForRequirement( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            ICertificate certificate,     /* in */
            ILogClientData logClientData, /* in: OPTIONAL */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            ref Result error              /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            Guid? haveId;
            Result localError = null;

            haveId = GetIdFromEntityName(interpreter,
                certificate.EntityName, Constants.RequirementRegEx,
                cultureInfo, Constants.MinimumRequirementLength,
                ref localError);

            if (haveId == null)
            {
#if DEBUG || FORCE_TRACE
                /* NO RESULT */
                TraceOps.MaybeLogAndDebugTrace(
                    logClientData, String.Format(
                    "Requirement not present: {0}",
                    Utility.FormatWrapOrNull(localError)),
                    typeof(CertificateVerifyOps).Name,
                    TracePriority.Low, 0);
#endif

                //
                // NOTE: Technically, an embedded requirement identifier
                //       is optional; therefore, this cannot fail for a
                //       missing one.
                //
                return ReturnCode.Ok;
            }

            if (!CLS.HaveCertificate((Guid)haveId))
            {
                error = String.Format(
                    "referenced license certificate {0} was not found",
                    Utility.FormatWrapOrNull(haveId));

                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Locates, imports, verifies, and (when necessary) renews the
        /// license certificate for the specified assembly, plugin, or
        /// interpreter, applying the configured execution policy.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the operation.  This is optional.
        /// </param>
        /// <param name="assembly">
        /// The assembly being verified.  This is optional.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name being verified.  This is optional.
        /// </param>
        /// <param name="plugin">
        /// The plugin being verified.  This is optional.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.  This is optional.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when computing a keyed hash.
        /// </param>
        /// <param name="encoding">
        /// The encoding used while importing and hashing.  This is optional.
        /// </param>
        /// <param name="keyPairs">
        /// Extra key pairs to use.  This is optional.
        /// </param>
        /// <param name="features">
        /// The required feature flags.  This is optional.
        /// </param>
        /// <param name="restrictions">
        /// The required restriction flags.  This is optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy to use.  This is optional.
        /// </param>
        /// <param name="keyName">
        /// The name of the key to use.  This is optional.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to use.  This is optional.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used for network operations.  This
        /// is optional.
        /// </param>
        /// <param name="force">
        /// Non-zero to force checking even when it could be skipped.
        /// </param>
        /// <param name="embedded">
        /// Non-zero to consider a certificate embedded in the assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate against the schema.
        /// </param>
        /// <param name="fileNameCallback">
        /// The callback used to select the certificate file name.  This is
        /// optional.
        /// </param>
        /// <param name="renewCallback">
        /// The callback used to renew the certificate.  This is optional.
        /// </param>
        /// <param name="anyClientData">
        /// The client data carried through the operation.  This is optional.
        /// </param>
        /// <param name="fileName">
        /// On input, an optional candidate file name; on output, receives
        /// the file name of the verified certificate.
        /// </param>
        /// <param name="certificate">
        /// Receives the verified certificate.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error; upon
        /// success, receives the operation status.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode LoadAndProcess( /* CORE */
            Interpreter interpreter,                   /* in: OPTIONAL */
            Assembly assembly,                         /* in: OK, OPTIONAL With assemblyName AND plugin only. */
            AssemblyName assemblyName,                 /* in: OK, OPTIONAL With assembly only. */
            IPlugin plugin,                            /* in: OPTIONAL With assembly only. */
            string hashAlgorithmName,                  /* in: OPTIONAL */
            byte[] hashKey,                            /* in: OPTIONAL */
            Encoding encoding,                         /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,            /* in: OPTIONAL */
            string features,                           /* in: OPTIONAL */
            string restrictions,                       /* in: OPTIONAL */
            ExecutionPolicy? policy,                   /* in: OPTIONAL */
            string keyName,                            /* in: OPTIONAL */
            string keyRingName,                        /* in: OPTIONAL */
            int? timeout,                              /* in: OPTIONAL */
            bool force,                                /* in */
            bool embedded,                             /* in */
            bool validate,                             /* in */
            ElementSelectionCallback fileNameCallback, /* in: OPTIONAL */
            RenewCallback renewCallback,               /* in: OPTIONAL */
            IAnyClientData anyClientData,              /* in: OPTIONAL */
            ref string fileName,                       /* in, out */
            ref ICertificate certificate,              /* out */
            ref Result result                          /* out */
            )
        {
            CultureInfo cultureInfo;
            bool disposed;

            DataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                result = "interpreter is disposed";
                return ReturnCode.Error;
            }

            CLS.BeginPending();

            try
            {
                Utility.PushActiveInterpreter(interpreter);

                try
                {
                    ILogClientData logClientData = null;

                    try
                    {
                        if (Configuration.DoesVariableExist(
                                Constants.ForceLogLicenseEnvVarName))
                        {
                            logClientData = new ScriptLogClientData(
                                interpreter, plugin, null, PolicyType.License,
                                policy);
                        }

                        //
                        // HACK: Fallback to using name from the specified assembly,
                        //       if necessary.
                        //
                        if ((assemblyName == null) && (assembly != null))
                            assemblyName = assembly.GetName(); /* FALLBACK */

                        try
                        {
                            Utility.PushActiveInterpreter(
                                interpreter, logClientData);

                            ExecutionPolicy? tracePolicy = policy;
                            bool enableTracing = false;
                            bool wasEnabled = false;
                            TracePriority? savedBasePriority = null;
                            TracePriority? savedPriorities1 = null;
                            TracePriority? savedPriorities2 = null;
                            ICertificate localCertificate = null; /* REUSED */

                            try
                            {
                                TraceOps.MaybeChangeExecutionPolicy(
                                    interpreter, Constants.LicenseExecutionPolicyEnvVarName,
                                    Constants.EnablePolicyTracingLimitMask.ToString(),
                                    cultureInfo, ref tracePolicy);

                                enableTracing = Utility.HasFlags(
                                    tracePolicy, ExecutionPolicy.EnableTracing, true);

                                TraceOps.MaybeEnableOrDisableTextWriter(
                                    interpreter, cultureInfo, tracePolicy, true,
                                    ref wasEnabled, ref savedBasePriority,
                                    ref savedPriorities1, ref savedPriorities2);

                                if (!wasEnabled)
                                {
                                    if (enableTracing ||
                                        TraceOps.ShouldForceEnableForPolicy())
                                    {
                                        TraceOps.AdjustPrioritiesAndLimits(
                                            interpreter, cultureInfo, tracePolicy,
                                            true, ref savedBasePriority,
                                            ref savedPriorities1, ref savedPriorities2);
                                    }
                                }

                                TracePriority defaultPriority = TracePriority.Default;

                                TraceOps.MaybeAdjustPriority(ref defaultPriority);

                                TracePriority priority; /* REUSED */

                                string packageName = GetPackageName(
                                    interpreter, assembly, assemblyName, plugin);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                string skipReason = null;

                                if (!force && KRS.CanSkipLicenseCertificateChecks(ref skipReason))
                                {
                                    fileName = null;
                                    certificate = null;

#if DEBUG || FORCE_TRACE
                                    priority = defaultPriority;

                                    Utility.AdjustTracePriority(ref priority, -1);

                                    /* NO RESULT */
                                    TraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                        "Package {0} certificate checking skipped #1 because {1}.",
                                        CertificateDataOps.FormatPackageName(packageName),
                                        !String.IsNullOrEmpty(skipReason) ?
                                            skipReason : Constants.UnknownSkipReason),
                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                    return ReturnCode.Ok;
                                }
#endif

                                //
                                // NOTE: Figure out the execution policy to use for the license
                                //       certificate checking.  If an explicit execution policy
                                //       was specified by the caller, use that; otherwise, use
                                //       the one configured for the AppDomain.
                                //
                                PolicyType policyType = PolicyType.License;
                                ExecutionPolicy localPolicy;

                                ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
                                priority = defaultPriority;

                                Utility.AdjustTracePriority(ref priority, -2);

                                /* NO RESULT */
                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Starting {0} policy is {1} with a trace policy of {2}.",
                                    Utility.FormatWrapOrNull(policyType),
                                    Utility.FormatWrapOrNull(policy),
                                    Utility.FormatWrapOrNull(tracePolicy)),
                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                ///////////////////////////////////////////////////////////////////

                                ExecutionPolicy defaultPolicy;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                defaultPolicy = CertificatePolicyOps.GetPolicy(policyType);
#else
                                defaultPolicy = Constants.DefaultLicenseExecutionPolicy;
#endif

                                if (policy != null)
                                    localPolicy = (ExecutionPolicy)policy;
                                else
                                    localPolicy = defaultPolicy;

                                ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
                                priority = defaultPriority;

                                Utility.AdjustTracePriority(ref priority, -2);

                                /* NO RESULT */
                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Final {0} policy is {1} with a trace policy of {2}.",
                                    Utility.FormatWrapOrNull(policyType),
                                    Utility.FormatWrapOrNull(localPolicy),
                                    Utility.FormatWrapOrNull(tracePolicy)),
                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                ///////////////////////////////////////////////////////////////////

                                NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                                    policyType);

                                ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
                                priority = defaultPriority;

                                Utility.AdjustTracePriority(ref priority, -2);

                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Starting {0} network flags are {1}",
                                    Utility.FormatWrapOrNull(policyType),
                                    Utility.FormatWrapOrNull(networkFlags)),
                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                ///////////////////////////////////////////////////////////////////

                                //
                                // HACK: These local variables may only be used when compiled
                                //       with XML support; however, in order to keep the code
                                //       simple, they are always included in the final trace
                                //       messages.
                                //
                                Result localResult; /* REUSED */
                                IEnumerable<IKeyPair> localKeyPairs = null;

                                ///////////////////////////////////////////////////////////////////

                                bool skipThisAssembly = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.SkipThisAssembly, true);

                                bool isForThisAssembly = !skipThisAssembly ?
                                    MatchThisAssembly(assembly, assemblyName, plugin) :
                                    false;

                                //
                                // HACK: Sometimes, we want to pretend like the specified
                                //       assembly is part of Harpy itself (e.g. Badge, etc).
                                //       Fortunately, they will share a public key token.
                                //
                                bool isForThisPublicKeyToken =
                                    AssemblyOps.MatchPublicKeyToken(assemblyName);

                                bool isForThisPlugin = isForThisAssembly ||
                                    isForThisPublicKeyToken;

                                ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                localResult = null;

                                if (CertificatePolicyOps.CheckNetworkFlags(
                                        policyType, interpreter, plugin, ref networkFlags,
                                        ref localResult) != ReturnCode.Ok)
                                {
                                    result = localResult;
                                    goto error;
                                }
#endif

                                ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
                                priority = defaultPriority;

                                Utility.AdjustTracePriority(ref priority, -2);

                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Final {0} network flags are {1}",
                                    Utility.FormatWrapOrNull(policyType),
                                    Utility.FormatWrapOrNull(networkFlags)),
                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
                                if (localPolicy != defaultPolicy)
                                {
                                    string someOrAll;
                                    ExecutionPolicy missingPolicy = defaultPolicy;

                                    missingPolicy &= ~localPolicy;

                                    if (missingPolicy != ExecutionPolicy.None)
                                    {
                                        if (Utility.HasFlags(
                                                missingPolicy, defaultPolicy, true))
                                        {
                                            someOrAll = "all";
                                        }
                                        else
                                        {
                                            someOrAll = "some";
                                        }

                                        priority = defaultPriority;

                                        Utility.AdjustTracePriority(ref priority, 0);

                                        /* NO RESULT */
                                        TraceOps.MaybeLogAndDebugTrace(
                                            logClientData, String.Format(
                                            "Missing {0} default {1} policy of {2}.",
                                            someOrAll,
                                            Utility.FormatWrapOrNull(policyType),
                                            Utility.FormatWrapOrNull(missingPolicy)),
                                            typeof(CertificateVerifyOps).Name, priority, 0);
                                    }

                                    ExecutionPolicy extraPolicy = localPolicy;

                                    extraPolicy &= ~defaultPolicy;

                                    if (extraPolicy != ExecutionPolicy.None)
                                    {
                                        if (Utility.HasFlags(
                                                extraPolicy, localPolicy, true))
                                        {
                                            someOrAll = "all";
                                        }
                                        else
                                        {
                                            someOrAll = "some";
                                        }

                                        priority = defaultPriority;

                                        Utility.AdjustTracePriority(ref priority, 0);

                                        /* NO RESULT */
                                        TraceOps.MaybeLogAndDebugTrace(
                                            logClientData, String.Format(
                                            "Extra {0} default {1} policy of {2}.",
                                            someOrAll,
                                            Utility.FormatWrapOrNull(policyType),
                                            Utility.FormatWrapOrNull(extraPolicy)),
                                            typeof(CertificateVerifyOps).Name, priority, 0);
                                    }
                                }
#endif

                                ///////////////////////////////////////////////////////////////////

                                //
                                // NOTE: Grab the license certificate for Harpy itself.
                                //       In some circumstances, this will be null (e.g.
                                //       when this method is invoked "early-bound" via
                                //       the ILicenseManager interface).  In that case,
                                //       the Harpy *plugin* is not loaded first (i.e.
                                //       before validating and verifying a third-party
                                //       license certificate).  Also, in that case, no
                                //       Eagle interpreter context is used.
                                //
                                string assemblyCertificateFileName = CLS.GetFileName();
                                ICertificate assemblyCertificate = CLS.GetCertificate();

                                if (!force && isForThisAssembly && (assemblyCertificate != null))
                                {
                                    fileName = assemblyCertificateFileName;
                                    certificate = assemblyCertificate;

#if DEBUG || FORCE_TRACE
                                    priority = defaultPriority;

                                    Utility.AdjustTracePriority(ref priority, -1);

                                    /* NO RESULT */
                                    TraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                        "Package {0} certificate checking skipped #2.",
                                        CertificateDataOps.FormatPackageName(packageName)),
                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                    return ReturnCode.Ok;
                                }

                                ///////////////////////////////////////////////////////////////////

                                if (!force && isForThisAssembly &&
                                    CLS.CanSkip(LicenseType.Assembly))
                                {
                                    fileName = null;
                                    certificate = null;

#if DEBUG || FORCE_TRACE
                                    priority = defaultPriority;

                                    Utility.AdjustTracePriority(ref priority, -1);

                                    /* NO RESULT */
                                    TraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                        "Package {0} certificate checking skipped #3.",
                                        CertificateDataOps.FormatPackageName(packageName)),
                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                    return ReturnCode.Ok;
                                }

                                ///////////////////////////////////////////////////////////////////

                                if (Configuration.DoesVariableExist(
                                        Constants.ForceSdkModeEnvVarName))
                                {
                                    if (!CertificateSdkMode.IsEnabled())
                                    {
                                        CertificateSdkMode.Enable();

#if DEBUG || FORCE_TRACE
                                        priority = defaultPriority;

                                        Utility.AdjustTracePriority(ref priority, 1);

                                        /* NO RESULT */
                                        TraceOps.MaybeLogAndDebugTrace(
                                            logClientData, String.Format(
                                            "Forcibly enabled SDK mode for package {0}.",
                                            CertificateDataOps.FormatPackageName(packageName)),
                                            typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                    }
                                }

                                ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                if (!isForThisAssembly)
                                {
                                    //
                                    // HACK: If the plugin has no directory available -OR- it
                                    //       is the same as the directory for the manager, do
                                    //       not load any configuration files for it.
                                    //
                                    string baseDirectory = CertificatePathOps.GetDirectory(
                                        plugin);

                                    if ((baseDirectory != null) &&
                                        !AssemblyOps.MatchDirectory(baseDirectory))
                                    {
                                        IPlugin assemblyPlugin = null; /* e.g. "Licensing.*" */

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPlugin(
                                            interpreter, ref assemblyPlugin);

                                        localResult = null;

                                        if (Configuration.MaybeLoadFor(interpreter,
                                                assembly, assemblyName, plugin,
                                                assemblyPlugin as IConfiguration,
                                                baseDirectory, anyClientData,
                                                ConfigurationPhase.Verify, policyType,
                                                policy, keyName, keyRingName, timeout,
                                                false, false, ref localResult) == ReturnCode.Ok)
                                        {
#if DEBUG || FORCE_TRACE
                                            priority = defaultPriority;

                                            Utility.AdjustTracePriority(ref priority, -1);

                                            /* NO RESULT */
                                            TraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Configuration loader success for package {0}: {1}",
                                                CertificateDataOps.FormatPackageName(packageName),
                                                Utility.FormatWrapOrNull(true, false, localResult)),
                                                typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                        }
                                        else
                                        {
#if DEBUG || FORCE_TRACE
                                            priority = defaultPriority;

                                            Utility.AdjustTracePriority(ref priority, 1);

                                            /* NO RESULT */
                                            TraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Configuration loader failure for package {0}: {1}",
                                                CertificateDataOps.FormatPackageName(packageName),
                                                Utility.FormatWrapOrNull(true, false, localResult)),
                                                typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                            result = localResult;
                                            goto error;
                                        }
                                    }
                                }
#endif

                                ///////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION
                                //
                                // NOTE: This is the (eventual) certificate file name to import
                                //       and process.  It is no longer emitted in trace messages;
                                //       therefore, it is only included in the build when it will
                                //       actually be used.
                                //
                                string localFileName = null;

                                //
                                // HACK: Fallback to using the default encoding (UTF-8).
                                //
                                if (encoding == null)
                                    encoding = CertificateDataOps.GetDefaultEncoding();

                                ///////////////////////////////////////////////////////////////////

                                bool explicitOnly = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.ExplicitOnly, true);

                                bool preferEmbedded = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.PreferEmbedded, true);

                                bool checkPublicKeyToken = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.CheckPublicKeyToken, true);

                                bool enforceKeyGroup = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.EnforceKeyGroup, true);

                                bool enforceKeyUsage = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.EnforceKeyUsage, true);

                                bool checkRevocation = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.CheckRevocation, true);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                bool checkDomains = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.CheckDomains, true);
#endif

                                bool allowRemoteUri = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AllowRemoteUri, true);

                                bool looksLikeXml = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.LooksLikeXml, true);

                                bool preValidateXml = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.PreValidateXml, true);

                                bool autoAcquire = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AutoAcquire, true);

                                bool cacheAcquire = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.CacheAcquire, true);

                                //
                                // NOTE: Get the key pairs available for processing the license
                                //       certificate, including the key pair that the specified
                                //       assembly was signed with.  We will use one of these key
                                //       pairs to verify the certificate file for this package.
                                //       If the specified assembly was not signed, then we may
                                //       have a problem.  Currently, there is no way to actually
                                //       load a trusted key ring prior to loading the Harpy
                                //       plugin itself; however, it should be possible for other
                                //       plugins.
                                //
                                int keyPairCount = 0;
                                IKeyPair keyPair = null;

                                ///////////////////////////////////////////////////////////////////

                                //
                                // HACK: If the caller specified a list of extra key pairs, make
                                //       use of them to extract the first one.  For now, only one
                                //       extra key pair may be used.  If more than one is present
                                //       an error will be returned.
                                //
                                if (keyPairs != null)
                                {
                                    localResult = null;

                                    if (CertificateKeyPairOps.GetFirst(
                                            null, keyPairs, ref keyPairCount, ref keyPair,
                                            ref localResult) == ReturnCode.Ok)
                                    {
                                        if (keyPairCount > 1)
                                        {
                                            result = "only one extra key pair may be specified";
                                            goto error;
                                        }
                                        else
                                        {
#if DEBUG || FORCE_TRACE
                                            priority = defaultPriority;

                                            Utility.AdjustTracePriority(ref priority, -1);

                                            /* NO RESULT */
                                            TraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Using extra key pair {0} from caller...",
                                                Utility.FormatWrapOrNull(keyPair)),
                                                typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                        }
                                    }
                                    else
                                    {
                                        result = localResult;
                                        goto error;
                                    }
                                }

                                ///////////////////////////////////////////////////////////////////

                                bool maybeNoFileSearch = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.MaybeNoFileSearch, true);

                                bool anyResourcePublicKey = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AnyResourcePublicKey, true);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                //
                                // NOTE: These key pairs are only used locally within this
                                //       method and are NOT used by evaluate scripts -OR-
                                //       load any other key pairs.
                                //
                                // NOTE: *WARNING* This "small" block of code contained within
                                //       the CERTIFICATE_PLUGIN && CERTIFICATE_POLICY block is
                                //       actually quite complex.  It causes the key ring -AND-
                                //       policy subsystems to be used *before* the plugin is
                                //       fully loaded, which leads to all sorts of (now solved)
                                //       chicken-and-egg problems, including calls back into
                                //       this method (i.e. in this AppDomain or another one).
                                //       Since this (currently) requires an interpreter, it can
                                //       be bypassed by passing null for the interpreter.
                                //
                                bool allowAssemblyPublicKey = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AllowAssemblyPublicKey, true);

                                bool allowEmbeddedPublicKey = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AllowEmbeddedPublicKey, true);

                                bool allowRingPublicKey = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AllowRingPublicKey, true);

                                bool allowAnyPublicKey = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.AllowAnyPublicKey, true);

                                bool ignoreKeyRingError = Utility.HasFlags(
                                    localPolicy, ExecutionPolicy.IgnoreKeyRingError, true);

                                string localKeyRingName = CertificateKeyRingOps.GetName(
                                    keyRingName, policyType);

                                localResult = null;

                                if (((interpreter != null) &&
                                    (MaybeLoadKeyRingsAndThenGetKeyPairsPublicOnly( /* OK */
                                        interpreter, assembly, assemblyName, plugin, keyPair,
                                        logClientData, cultureInfo, localPolicy, keyName,
                                        fileName, localKeyRingName, policyType, defaultPriority,
                                        allowAssemblyPublicKey, allowEmbeddedPublicKey,
                                        allowRingPublicKey, allowAnyPublicKey, enforceKeyUsage,
                                        ignoreKeyRingError, ref localKeyPairs,
                                        ref localResult) == ReturnCode.Ok)) ||
                                    ((interpreter == null) &&
                                    (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                                        assembly, assemblyName, ref localKeyPairs,
                                        ref localResult) == ReturnCode.Ok)))
#else
                                localResult = null;

                                if (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                                        assembly, assemblyName, ref localKeyPairs,
                                        ref localResult) == ReturnCode.Ok)
#endif
                                {
#if DEBUG || FORCE_TRACE
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                    priority = defaultPriority;

                                    Utility.AdjustTracePriority(ref priority, 2);

                                    DebugOnlyOps.DumpKeyPairs(
                                        interpreter, "LoadAndProcess", null,
                                        localKeyPairs, typeof(CertificateVerifyOps).Name,
                                        policyType, priority);
#endif
#endif

                                    StringList fileNames = null;

                                    localResult = null;

                                    if ((maybeNoFileSearch && HaveFileName(
                                            fileNames, fileName, null, false)) ||
                                        (CertificatePathOps.GetFileNames(
                                            interpreter, assembly, plugin, anyClientData,
                                            localPolicy, BootstrapType.License, false,
                                            true, Configuration.DoesVariableExist(
                                                Constants.HarpyAggressiveCacheEnvVarName),
                                            isForThisPlugin, ref fileNames,
                                            ref localResult) == ReturnCode.Ok))
                                    {
                                        //
                                        // HACK: At this point, there may be no file name list if
                                        //       the "no file search" flag was set.  Make sure we
                                        //       have a file name list now.
                                        //
                                        if (fileNames == null)
                                            fileNames = new StringList();

                                        //
                                        // HACK: If the caller specified a file name -AND- set the
                                        //       appropriate flag, clear out any other candidates.
                                        //
                                        if (explicitOnly && (fileName != null))
                                            fileNames.Clear();

                                        //
                                        // NOTE: This is the index where subsequent file names will
                                        //       be inserted into the list.  It will be incremented
                                        //       when necessary.
                                        //
                                        int fileNameOffset = 0;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                        //
                                        // HACK: Add the special certificate file name used only
                                        //       by the key ring subsystem for this process.  This
                                        //       is always done, even when the explicit-only flag
                                        //       is set, because it is a vital, internal file name
                                        //       transfer mechanism.
                                        //
                                        string keyRingFileName = CertificateKeyRingOps.GetFileName();

                                        if (keyRingFileName != null)
                                            fileNames.Insert(fileNameOffset++, keyRingFileName);
#endif

                                        //
                                        // NOTE: If a file name has been provided by the caller,
                                        //       use it.
                                        //
                                        if (fileName != null)
                                            fileNames.Insert(fileNameOffset++, fileName);

                                        //
                                        // NOTE: If the embedded flag is set, look for a license
                                        //       certificate embedded in the assembly specified
                                        //       by the caller.  If one is available, it will be
                                        //       temporarily written to disk and the local file
                                        //       name will be set to point to it.
                                        //
                                        string temporaryFileName = null;
                                        string resourceName = null;

                                        if (embedded && (!maybeNoFileSearch || !HaveFileName(
                                                fileNames, fileName, null, true)))
                                        {
                                            //
                                            // NOTE: Attempt to grab an appropriate resource stream
                                            //       from the plugin and/or assembly specified by
                                            //       the caller.
                                            //
                                            Stream stream;

                                            localResult = null;

                                            stream = CertificateSharedOps.GetStream(
                                                interpreter, assembly, plugin,
                                                anyClientData, BootstrapType.License,
                                                localPolicy, isForThisPlugin,
                                                ref resourceName, ref localResult);

                                            if (stream != null)
                                            {
                                                //
                                                // NOTE: Build a cryptographically random temporary
                                                //       file name.
                                                //
                                                temporaryFileName = Utility.GetTempFileName(
                                                    Constants.TemporaryLicensePrefix);

                                                //
                                                // NOTE: Try to write out the license certificate
                                                //       resource stream to the temporary file.
                                                //       Generally, this should not fail.  If it
                                                //       does, complain loudly.
                                                //
                                                localResult = null;

                                                if (WriteStreamToFile(
                                                        stream, temporaryFileName,
                                                        ref localResult) == ReturnCode.Ok)
                                                {
                                                    //
                                                    // NOTE: Always add the embedded file name
                                                    //       at the end of the candidate list
                                                    //       unless the 'prefer embedded' flag
                                                    //       is set.  In that case, insert it
                                                    //       at the start of the list, right
                                                    //       after the other high-priority
                                                    //       entries.
                                                    //
                                                    if (preferEmbedded)
                                                    {
                                                        fileNames.Insert(
                                                            fileNameOffset++, temporaryFileName);
                                                    }
                                                    else
                                                    {
                                                        fileNames.Add(temporaryFileName);
                                                    }
                                                }
                                                else
                                                {
                                                    //
                                                    // NOTE: Since the temporary file was NOT
                                                    //       successfully written by us (and
                                                    //       possibly does not even exist),
                                                    //       null out the temporary file name
                                                    //       to prevent this method from later
                                                    //       trying to delete it.
                                                    //
                                                    if (!File.Exists(temporaryFileName))
                                                        temporaryFileName = null;

#if DEBUG || FORCE_TRACE
                                                    //
                                                    // NOTE: This should basically never happen;
                                                    //       emit a trace message.  This used to
                                                    //       complain; however, that is overkill
                                                    //       for non-debug builds.
                                                    //
                                                    priority = defaultPriority;

                                                    Utility.AdjustTracePriority(ref priority, 1);

                                                    /* NO RESULT */
                                                    TraceOps.MaybeLogAndDebugTrace(
                                                        logClientData, String.Format(
                                                        "Failed to write certificate to temporary file {0}: {1}",
                                                        Utility.FormatWrapOrNull(temporaryFileName),
                                                        Utility.FormatWrapOrNull(true, false, localResult)),
                                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                }
                                            }
                                            else
                                            {
                                                //
                                                // NOTE: This should happen rarely because it
                                                //       requires the "embedded" parameter to
                                                //       be true; emit a trace message.  This
                                                //       used to complain; however, that is
                                                //       overkill for non-debug builds.
                                                //
#if DEBUG || FORCE_TRACE
                                                priority = defaultPriority;

                                                Utility.AdjustTracePriority(ref priority, -2);

                                                /* NO RESULT */
                                                TraceOps.MaybeLogAndDebugTrace(
                                                    logClientData, String.Format(
                                                    "No embedded certificate was found via assembly {0} or plugin {1}: {2}",
                                                    Utility.FormatWrapOrNull(assembly),
                                                    Utility.FormatWrapOrNull(plugin),
                                                    Utility.FormatWrapOrNull(true, false, localResult)),
                                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                            }
                                        }

                                        try
                                        {
                                            //
                                            // NOTE: Check each candidate license certificate file name
                                            //       for the first one that "looks valid".  That may or
                                            //       may not involve reading and/or validating any file
                                            //       contents.  The exact semantics now depend on which
                                            //       ElementSelectionCallback is used.  The default one
                                            //       will read the file (or remote URI) and attempt to
                                            //       make sure it can actually be imported.  This will
                                            //       include validating it against the associated XSD
                                            //       schema.  Yes, this is more expensive than checking
                                            //       only if the file exists; however, since there are
                                            //       [potentially] multiple candidates (i.e. that are
                                            //       always listed in "priority" order) -AND- selecting
                                            //       incorrectly *WILL* cause a plugin load failure, it
                                            //       is quite important to verify that a candidate has
                                            //       at least a plausible chance of passing the entire
                                            //       loading process before selecting it.  This may be
                                            //       changed in the future if/when this method ends up
                                            //       allowing for more than one try when attempting to
                                            //       load and process a license certificate.  In that
                                            //       case, upon retry, all previously tried candidate
                                            //       license certificate file names would be exluced
                                            //       from the list (obviously?).
                                            //
                                            // HACK: Use the file name selection callback specified by
                                            //       our caller, if any.  However, since a file name
                                            //       MUST be selected in order to continue, always
                                            //       fallback to using the default file name selection
                                            //       semantics if a callback is not specified.  If the
                                            //       callback specified by the caller ends up throwing
                                            //       an exception, the entire certificate verification
                                            //       process will fail (i.e. which is by design).
                                            //
                                            if (fileNameCallback == null)
                                                fileNameCallback = GetFirstValidFileName;

                                            VerifyClientData fileNameCallbackClientData = null;
                                            bool maybeAllowRemoteUri = allowRemoteUri;
                                            bool traceOnError;
                                            bool traceOnFound;
                                            bool traceOnNotFound;

                                            if (Configuration.DoesVariableExist(
                                                    Constants.NoTraceOnErrorEnvVarName))
                                            {
                                                traceOnError = false;
                                                traceOnFound = false;
                                                traceOnNotFound = false;
                                            }
                                            else if (Configuration.DoesVariableExist(
                                                    Constants.TraceOnErrorEnvVarName))
                                            {
                                                traceOnError = true;
                                                traceOnFound = true;
                                                traceOnNotFound = true;
                                            }
                                            else
                                            {
                                                traceOnError = Constants.DefaultTraceOnError;
                                                traceOnFound = Constants.DefaultTraceOnFound;
                                                traceOnNotFound = Constants.DefaultTraceOnNotFound;
                                            }

                                        fileNameViaCallback:

                                            fileNameCallbackClientData = new VerifyClientData(
                                                interpreter, plugin, encoding, logClientData,
                                                cultureInfo, timeout, GetFileNameFlags(
                                                maybeAllowRemoteUri, looksLikeXml,
                                                preValidateXml, embedded, traceOnError,
                                                traceOnFound, traceOnNotFound,
                                                anyResourcePublicKey, isForThisPlugin),
                                                false);

                                            localResult = null;

                                            try
                                            {
                                                localFileName = fileNameCallback( /* throw */
                                                    fileNames, fileNameCallbackClientData);
                                            }
                                            catch (Exception e)
                                            {
                                                localResult = e;
                                            }

                                            //
                                            // NOTE: When attempting to locate a license certificate file
                                            //       for this plugin (Harpy), attempt to request one from
                                            //       the official licensing server when the auto-acquire
                                            //       flag is enabled.  By default, the auto-acquire flag
                                            //       is enabled.
                                            //
                                            Result initialFileNameResult = null;
                                            Result createInterpreterResult = null;
                                            Result tryRequestResult = null;

                                            if (!Configuration.DoesVariableExist(
                                                    Constants.NoAutoAcquireEnvVarName) && autoAcquire &&
                                                isForThisAssembly && (assemblyCertificate == null))
                                            {
                                                if (!CheckFileName(localFileName,
                                                        fileNameCallbackClientData, allowRemoteUri,
                                                        ref initialFileNameResult)) /* EXEMPT */
                                                {
                                                    Interpreter autoAcquireInterpreter = null;

                                                    try
                                                    {
                                                        if (cacheAcquire && (interpreter != null))
                                                        {
                                                            //
                                                            // HACK: Use the existing interpreter purely
                                                            //       for speed.
                                                            //
                                                            autoAcquireInterpreter = interpreter;
                                                        }
                                                        else
                                                        {
                                                            //
                                                            // NOTE: Use a fresh interpreter to prevent
                                                            //       any pre-existing interpreter state
                                                            //       from possibly impacting the license
                                                            //       certificate request.
                                                            //
                                                            autoAcquireInterpreter = Interpreter.Create(
                                                                ref createInterpreterResult);
                                                        }

                                                        if ((autoAcquireInterpreter != null) &&
                                                            TryRequest(autoAcquireInterpreter,
                                                                logClientData, ref fileNames,
                                                                ref tryRequestResult) == ReturnCode.Ok)
                                                        {
                                                            //
                                                            // NOTE: When re-checking for the first valid
                                                            //       license certificate file, skip trying
                                                            //       any remote URIs (again?).
                                                            //
                                                            if (maybeAllowRemoteUri)
                                                                maybeAllowRemoteUri = false;

                                                            //
                                                            // NOTE: Since there should (now) be a saved
                                                            //       license certificate file (somewhere),
                                                            //       try again at locating a valid license
                                                            //       certificate file.
                                                            //
                                                            goto fileNameViaCallback;
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        if (!Object.ReferenceEquals(
                                                                autoAcquireInterpreter, interpreter))
                                                        {
                                                            /* IGNORED */
                                                            Utility.TryDisposeObjectOrComplain<Interpreter>(
                                                                interpreter, ref autoAcquireInterpreter);

                                                            autoAcquireInterpreter = null;
                                                        }
                                                    }
                                                }
                                            }

                                            //
                                            // NOTE: The file must exist.  Also, no point in even calling
                                            //       the import routine (which also checks if the file
                                            //       exists) if the file does not exist.  If we eventually
                                            //       added an "interactive" mode to this certificate file
                                            //       checking, this is where it would go (i.e. here is
                                            //       where we could prompt the user for the location of
                                            //       the certificate file or allow them to download and/or
                                            //       purchase one, if applicable).
                                            //
                                            Result finalFileNameResult = null;

                                            if (CheckFileName(localFileName,
                                                    fileNameCallbackClientData, allowRemoteUri,
                                                    ref finalFileNameResult)) /* EXEMPT */
                                            {
                                                //
                                                // NOTE: Show which certificate file is the actual one we
                                                //       are checking.
                                                //
#if DEBUG || FORCE_TRACE
                                                priority = defaultPriority;

                                                Utility.AdjustTracePriority(ref priority, -2);

                                                /* NO RESULT */
                                                TraceOps.MaybeLogAndDebugTrace(
                                                    logClientData, String.Format(
                                                    "Checking package {0} certificate file {1}...",
                                                    CertificateDataOps.FormatPackageName(packageName),
                                                    Utility.FormatWrapOrNull(localFileName)),
                                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                                //
                                                // NOTE: Using the clientData from the file name selection
                                                //       callback, attempt to determine if the certificate
                                                //       file was already validated against the XML schema.
                                                //
                                                bool wasValidated = fileNameCallbackClientData.WasValidated;

                                                //
                                                // NOTE: Attempt to import the selected certificate file
                                                //       into a certificate object.  If this fails then
                                                //       the certificate file is most likely corrupted,
                                                //       malformed, or otherwise invalid.
                                                //
                                                localCertificate = null;
                                                localResult = null;

                                                if (Import(
                                                        interpreter, plugin, encoding, logClientData,
                                                        localFileName, cultureInfo, timeout, traceOnError,
                                                        allowRemoteUri, anyResourcePublicKey,
                                                        isForThisPlugin, validate && !wasValidated,
                                                        ref localCertificate,
                                                        ref localResult) == ReturnCode.Ok)
                                                {
#if DEBUG || EXTRA_DIAGNOSTICS
                                                    //
                                                    // NOTE: Perform quick (and non-binding) sanity
                                                    //       check on the certificate to see if the
                                                    //       needed key pair is present in the list
                                                    //       of key pairs to be used.
                                                    //
                                                    Result keyPairError = null;

                                                    if (CheckForKeyPairPresent(
                                                            localCertificate, localKeyPairs,
                                                            ref keyPairError) != ReturnCode.Ok)
                                                    {
#if DEBUG || FORCE_TRACE
                                                        priority = defaultPriority;

                                                        Utility.AdjustTracePriority(ref priority, 2);

                                                        /* NO RESULT */
                                                        TraceOps.MaybeLogAndDebugTrace(
                                                            logClientData, String.Format(
                                                            "Package {0} certificate {1} may not work: {2}",
                                                            CertificateDataOps.FormatPackageName(packageName),
                                                            CertificateSharedOps.ToString(localCertificate),
                                                            Utility.FormatWrapOrNull(keyPairError)),
                                                            typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                    }
#endif

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                    bool wasReimported = false;

                                                reimported:
#endif

                                                    //
                                                    // NOTE: Initially, no renewal has been performed.
                                                    //       This flag will be set upon renewal being
                                                    //       completed successfully -AND- before the
                                                    //       license certificate is checked again by
                                                    //       jumping to the "retry" label.
                                                    //
                                                    bool wasRenewed = false;

                                                    //
                                                    // NOTE: Initially, there is no primary key pair
                                                    //       that was used to process the certificate
                                                    //       and/or returned from renewal processing.
                                                    //       This can ONLY be changed via certificate
                                                    //       renewal.  In that case, other key pairs
                                                    //       may NOT be used for processing.
                                                    //
                                                    IKeyPair localKeyPair = null;

                                                    //
                                                    // BUGFIX: Since the license certificate and/or the
                                                    //         selected key pair can change during the
                                                    //         renewal process, reverify everything that
                                                    //         is not already verified during the renewal
                                                    //         process itself, including the key group,
                                                    //         revocation status, and promotional status.
                                                    //
                                                retry:

                                                    //
                                                    // NOTE: Figure out the hash algorithm to use when
                                                    //       verifying the certificate.
                                                    //
                                                    string localHashAlgorithmName =
                                                        CertificateSharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, localKeyPairs, localCertificate,
                                                            HashAlgorithmType.Legacy);

                                                    //
                                                    // NOTE: Attempt to verify the RSA signature on all the
                                                    //       certificate data.  These key pairs are used to
                                                    //       verify the imported license certificate.
                                                    //
                                                    localResult = null;

                                                    if (((localKeyPair != null) && (Process(
                                                            localHashAlgorithmName, hashKey, localCertificate,
                                                            null, encoding, new IKeyPair[] { localKeyPair },
                                                            checkPublicKeyToken, checkRevocation,
                                                            ref localKeyPair, ref localResult) == ReturnCode.Ok)) ||
                                                        ((localKeyPair == null) && (Process(
                                                            localHashAlgorithmName, hashKey, localCertificate,
                                                            null, encoding, localKeyPairs,
                                                            checkPublicKeyToken, checkRevocation,
                                                            ref localKeyPair, ref localResult) == ReturnCode.Ok)))
                                                    {
                                                        long flagsKey = Utility.DefaultAttributeFlagsKey();

                                                        localResult = null;

                                                        if (!Configuration.DoesVariableExist(
                                                                Constants.AsynchronousLicensingEnvVarName) ||
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
                                                            CertificateGlobalState.HaveExtraFeatures(
                                                                Features.AsynchronousLicensingOrAll, false) ||
#endif
                                                            ((localCertificate != null) &&
                                                            (CertificateSharedOps.MatchFlags(
                                                                localCertificate, FlagType.Feature, flagsKey,
                                                                Features.AsynchronousLicensingOrAll,
                                                                null, false, false, true,
                                                                ref localResult) == ReturnCode.Ok)))
                                                        {
                                                            localResult = null;

                                                            if (CheckForRequirement(interpreter,
                                                                    localCertificate, logClientData, cultureInfo,
                                                                    ref localResult) == ReturnCode.Ok)
                                                            {
                                                                localResult = null;

                                                                if (CheckForProcess(interpreter,
                                                                        localCertificate, logClientData, cultureInfo,
                                                                        ref localResult) == ReturnCode.Ok)
                                                                {
                                                                    //
                                                                    // NOTE: If this license certificate has the restriction
                                                                    //       of "fully trusted key", then the key pair used
                                                                    //       to verify it must chain up to a fully trusted
                                                                    //       (root) key.  Generally, this means the key pair
                                                                    //       must have originated in the key ring loader.
                                                                    //
                                                                    localResult = null;

                                                                    if ((CertificateSharedOps.MatchFlags(
                                                                            localCertificate, FlagType.Restriction, flagsKey,
                                                                            null, Restrictions.FullyTrustedKey, false, false,
                                                                            true, ref localResult) == ReturnCode.Ok) ||
                                                                        ((localKeyPair != null) && localKeyPair.IsApproved()))
                                                                    {
                                                                        //
                                                                        // HACK: When the global "force network" flag is set for
                                                                        //       license checking, all revocation and expiration
                                                                        //       checks will require network access.
                                                                        //
                                                                        if (CLS.GetForceNetwork())
                                                                            networkFlags |= NetworkFlags.ForceMask;

                                                                        //
                                                                        // HACK: Maybe invoke the fail-safe checking, which will
                                                                        //       perform an asynchronous forced remote check to
                                                                        //       determine if the certificate -OR- its signing
                                                                        //       key pair has been actively revoked.
                                                                        //
                                                                        CertificateRevocationOps.MaybePerformFailSafeChecks( /* OK */
                                                                            interpreter, assembly, plugin, localHashAlgorithmName,
                                                                            hashKey, encoding, localKeyPairs, localCertificate,
                                                                            localKeyPair, cultureInfo, Utility.GetUtcNow(), timeout,
                                                                            networkFlags);

                                                                        //
                                                                        // NOTE: Make sure the certificate has not been revoked
                                                                        //       by its associated authority.  These key pairs
                                                                        //       are used to verify revocation lists downloaded
                                                                        //       from the server.
                                                                        //
                                                                        localResult = null;

                                                                        if (!checkRevocation ||
                                                                            (CertificateRevocationOps.IsRevoked( /* OK */
                                                                                interpreter, assembly, plugin,
                                                                                localHashAlgorithmName, hashKey, encoding,
                                                                                localKeyPairs, localCertificate, cultureInfo,
                                                                                timeout, networkFlags, ref localResult) == ReturnCode.Ok))
                                                                        {
                                                                            //
                                                                            // NOTE: Make sure that key pair that was used to
                                                                            //       process the license is present in the group
                                                                            //       that are trusted for that purpose.
                                                                            //
                                                                            localResult = null;

                                                                            if (!enforceKeyGroup || (MatchKeyGroup( /* OK */
                                                                                    localKeyPair, assembly, assemblyName,
                                                                                    ref localResult) == ReturnCode.Ok))
                                                                            {
                                                                                //
                                                                                // NOTE: Enforce the "promotional" restriction flag, if
                                                                                //       present.
                                                                                //
                                                                                // BUGFIX: This was breaking the "SDK only" model.  It
                                                                                //         assumed that the Harpy plugin will always be
                                                                                //         loaded before this method is called for any
                                                                                //         third-party assembly.
                                                                                //
                                                                                // NOTE: This compound "if" block checks that one of the
                                                                                //       following is true:
                                                                                //
                                                                                //       1. The imported license certificate is null
                                                                                //          (this should be impossible).
                                                                                //
                                                                                //       2. The key used to sign the license certificate
                                                                                //          is "well-known", e.g. is "0x8bf43b4749e46a0b"
                                                                                //          (Harpy) or "0x5f8230f3e7b9b317" (Demo).
                                                                                //
                                                                                //       3. The key used to sign the license certificate
                                                                                //          is on the trusted (license) key ring for the
                                                                                //          specified interpreter -AND- the "promotional"
                                                                                //          feature flag (or all?) is enabled for the
                                                                                //          license certificate.
                                                                                //
                                                                                //       4. We are being called without a plugin context
                                                                                //          -AND- without a pre-existing Harpy plugin
                                                                                //          license certificate.
                                                                                //
                                                                                //       5. The license certificate for the Harpy plugin
                                                                                //          does not have the "promotional" restriction
                                                                                //          flag enabled.
                                                                                //
                                                                                // TODO: This code is somewhat unclear and may contain
                                                                                //       checks that are no longer required.  Cleanup
                                                                                //       and rationalization would be a good idea.
                                                                                //
                                                                                string wellKnownReason = null;
                                                                                Result wellKnownResult = null;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                Result trustedResult = null;
                                                                                Result featureResult = null;
#endif

                                                                                Result restrictionResult = null;

                                                                                if ((localCertificate == null) || /* TODO: Impossible? */
                                                                                    CertificateSharedOps.IsWellKnownPublicKeyToken(
                                                                                        localCertificate.Key, ref wellKnownReason,
                                                                                        ref wellKnownResult) ||
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                    (CertificatePolicyOps.IsTrustedPublicKeyToken(
                                                                                        interpreter, localKeyRingName, localCertificate.Key,
                                                                                        ref trustedResult) &&
                                                                                    (CertificateSharedOps.MatchFlags(
                                                                                        localCertificate, FlagType.Feature, flagsKey,
                                                                                        Features.PromotionalOrAll, null, false, false, true,
                                                                                        ref featureResult) == ReturnCode.Ok)) ||
#endif
                                                                                    ((plugin == null) && (assemblyCertificate == null)) ||
                                                                                    (CertificateSharedOps.MatchFlags(
                                                                                        assemblyCertificate, FlagType.Restriction, flagsKey,
                                                                                        null, Restrictions.Promotional, false, false, true,
                                                                                        ref restrictionResult) == ReturnCode.Ok))
                                                                                {
                                                                                    if (!String.IsNullOrEmpty(wellKnownReason))
                                                                                    {
#if DEBUG || FORCE_TRACE
                                                                                        priority = defaultPriority;

                                                                                        Utility.AdjustTracePriority(ref priority, 1);

                                                                                        /* NO RESULT */
                                                                                        TraceOps.MaybeLogAndDebugTrace(
                                                                                            logClientData, String.Format(
                                                                                            "Package {0} certificate {1} key pair {2} is well-known because {3}.",
                                                                                            CertificateDataOps.FormatPackageName(packageName),
                                                                                            CertificateSharedOps.ToString(localCertificate),
                                                                                            CertificateDataOps.FormatPublicKeyToken(
                                                                                                localCertificate.Key, true, true),
                                                                                            wellKnownReason), typeof(CertificateVerifyOps).Name,
                                                                                            priority, 0);
#endif
                                                                                    }

                                                                                    localResult = null;

                                                                                    if (!isForThisAssembly || (assemblyCertificate != null) ||
                                                                                        (MatchProduct(
                                                                                            localCertificate, ref localResult) == ReturnCode.Ok))
                                                                                    {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                        bool reimported = false;

                                                                                        localResult = null;

                                                                                        if ((localCertificate == null) || (MatchMachineId(
                                                                                                interpreter, assembly, plugin, encoding,
                                                                                                logClientData, localFileName, cultureInfo,
                                                                                                timeout, traceOnError, allowRemoteUri,
                                                                                                anyResourcePublicKey, isForThisPlugin,
                                                                                                validate && !wasValidated, wasReimported,
                                                                                                ref localCertificate, ref reimported,
                                                                                                ref localResult) == ReturnCode.Ok))
#endif
                                                                                        {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                            if (reimported)
                                                                                            {
                                                                                                wasReimported = true;
                                                                                                goto reimported;
                                                                                            }
#endif

                                                                                            localResult = null;

                                                                                            if (!enforceKeyUsage || (localCertificate == null) ||
                                                                                                (CertificateSharedOps.CheckKeyUsage(
                                                                                                    localKeyPair, localCertificate.EntityType,
                                                                                                    ref localResult) == ReturnCode.Ok))
                                                                                            {
                                                                                                //
                                                                                                // NOTE: Attempt to match the certificate vendor to the
                                                                                                //       Authenticode signature on the assembly itself.
                                                                                                //
                                                                                                localResult = null;

                                                                                                if ((assembly == null) ||
                                                                                                    (CertificateSharedOps.MatchSubject(
                                                                                                        assembly, localCertificate, localPolicy,
                                                                                                        ref localResult) == ReturnCode.Ok))
                                                                                                {
                                                                                                    //
                                                                                                    // NOTE: Next, make sure the entity type is valid for
                                                                                                    //       this kind of certificate.
                                                                                                    //
                                                                                                    localResult = null;

                                                                                                    if (CertificateSharedOps.MatchEntityType(
                                                                                                            localCertificate, EntityType.None,
                                                                                                            EntityType.NonLicenseDataMask, false, false,
                                                                                                            ref localResult) == ReturnCode.Ok)
                                                                                                    {
#if FOR_TEST_USE_ONLY
                                                                                                        localResult = null;

                                                                                                        if (CertificateSharedOps.MatchFlags(
                                                                                                                localCertificate, FlagType.Restriction, flagsKey,
                                                                                                                null, Restrictions.Test, false, false, true,
                                                                                                                ref localResult) != ReturnCode.Ok)
                                                                                                        {
                                                                                                            ResultList errors = new ResultList();

                                                                                                            errors.Add(OperationStatus.ForTestUseOnly);

                                                                                                            if (localResult != null)
                                                                                                                errors.Add(localResult);

                                                                                                            result = errors;
                                                                                                        }
                                                                                                        else
#endif
                                                                                                        {
                                                                                                            //
                                                                                                            // NOTE: Make sure the feature and restriction flags,
                                                                                                            //       if any, are correctly set.
                                                                                                            //
                                                                                                            localResult = null;

                                                                                                            if (CertificateSharedOps.MatchFlags(
                                                                                                                    localCertificate, FlagType.Feature, flagsKey,
                                                                                                                    features, null, false, false, true,
                                                                                                                    ref localResult) == ReturnCode.Ok)
                                                                                                            {
                                                                                                                localResult = null;

                                                                                                                if (CertificateSharedOps.MatchFlags(
                                                                                                                        localCertificate, FlagType.Restriction, flagsKey,
                                                                                                                        null, restrictions, false, false, true,
                                                                                                                        ref localResult) == ReturnCode.Ok)
                                                                                                                {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                    localResult = null;

                                                                                                                    if (!checkDomains || (MatchAnyKeyDomain(
                                                                                                                            localKeyPair, localCertificate, cultureInfo,
                                                                                                                            ref localResult) == ReturnCode.Ok))
#endif
                                                                                                                    {
                                                                                                                        //
                                                                                                                        // NOTE: Make sure that either the certificate has
                                                                                                                        //       unlimited uses or there are at least some
                                                                                                                        //       uses remaining.
                                                                                                                        //
                                                                                                                        bool perMachine =
                                                                                                                            CertificateSharedOps.ShouldUsePerMachine(
                                                                                                                                Constants.QuantityPerMachine);

                                                                                                                        localResult = null;

                                                                                                                        if (wasRenewed ||
                                                                                                                            (CertificateSharedOps.ProcessQuantity(
                                                                                                                                interpreter, plugin, localHashAlgorithmName,
                                                                                                                                hashKey, localCertificate, cultureInfo, null,
                                                                                                                                encoding, null, true, perMachine,
                                                                                                                                ref localResult) == ReturnCode.Ok))
                                                                                                                        {
#if DEBUG && NETWORK && CERTIFICATE_RENEWAL
                                                                                                                            //
                                                                                                                            // NOTE: In the debug build configuration only,
                                                                                                                            //       when the appropriate environment variable
                                                                                                                            //       is set, force usage of the default license
                                                                                                                            //       certificate renewal callback unless the
                                                                                                                            //       caller has specified a non-null value for
                                                                                                                            //       it already.
                                                                                                                            //
                                                                                                                            if ((renewCallback == null) &&
                                                                                                                                Configuration.DoesVariableExist(
                                                                                                                                    Constants.UseDefaultRenewCallbackEnvVarName))
                                                                                                                            {
                                                                                                                                renewCallback = CertificateRenewalOps.DefaultRenewCallback;
                                                                                                                            }
#endif

                                                                                                                            //
                                                                                                                            // NOTE: Check for the feature flag that indicates
                                                                                                                            //       that an expired certificate may be renewed;
                                                                                                                            //       without this, an expired certificate will
                                                                                                                            //       simply fail to verify.
                                                                                                                            //
                                                                                                                            bool needsActivation;
                                                                                                                            Result activationResult = null;

                                                                                                                            needsActivation = CertificateSharedOps.NeedsActivation(
                                                                                                                                localCertificate, ref activationResult);

                                                                                                                            if (!needsActivation &&
                                                                                                                                (CertificateSharedOps.MatchFlags(
                                                                                                                                    localCertificate, FlagType.Feature, flagsKey,
                                                                                                                                    Features.RenewalOrAll, null, false, false,
                                                                                                                                    true) != ReturnCode.Ok))
                                                                                                                            {
#if DEBUG || FORCE_TRACE
                                                                                                                                priority = defaultPriority;

                                                                                                                                Utility.AdjustTracePriority(ref priority, -1);

                                                                                                                                /* NO RESULT */
                                                                                                                                TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                    logClientData, String.Format(
                                                                                                                                    "Package {0} certificate {1} not eligible for renewal.",
                                                                                                                                    CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                    CertificateSharedOps.ToString(localCertificate)),
                                                                                                                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                                                                                                                renewCallback = null;
                                                                                                                            }

                                                                                                                            //
                                                                                                                            // NOTE: Next, check to be sure that the certificate has
                                                                                                                            //       not expired (unless it was __just__ renewed).
                                                                                                                            //       This assumes that the renewal server will *NOT*
                                                                                                                            //       send back a certificate that is (still?) expired.
                                                                                                                            //       And actually, since the default renewal callback
                                                                                                                            //       does check the expiration on renewed certificates,
                                                                                                                            //       that assumption only applies to custom renewal
                                                                                                                            //       callbacks.  Bypassing the check here is important
                                                                                                                            //       to prevent (potential) endless looping.
                                                                                                                            //
                                                                                                                            // NOTE: The key pair used for this expiration check will
                                                                                                                            //       only be used when checking the network time via
                                                                                                                            //       HTTPS.  This implies the remote time server will
                                                                                                                            //       sign the resulting time with that key pair.  No
                                                                                                                            //       other key pair will be accepted.  This will work
                                                                                                                            //       fine for Harpy itself when contacting an official
                                                                                                                            //       remote time server that is using the correct key
                                                                                                                            //       pair (i.e. not the demo key pair); however, it is
                                                                                                                            //       quite unlikely to work for other plugins because
                                                                                                                            //       the remote time servers are not configured on a
                                                                                                                            //       per-plugin basis.  Therefore, using HTTPS remote
                                                                                                                            //       time servers should not be used for certificates
                                                                                                                            //       that are not signed with the Harpy key pair.
                                                                                                                            //
                                                                                                                            DateTime? installed = null;

                                                                                                                            if (plugin == null)
                                                                                                                            {
                                                                                                                                /* NO RESULT */
                                                                                                                                GetInstalledFromClientData(
                                                                                                                                    interpreter, anyClientData, logClientData,
                                                                                                                                    ref installed);
                                                                                                                            }

                                                                                                                            bool canRenew = true;

                                                                                                                            localResult = null;

                                                                                                                            if (!needsActivation &&
                                                                                                                                (CertificateSharedOps.IsExpired(
                                                                                                                                    interpreter, assembly, plugin,
                                                                                                                                    localCertificate, localKeyPairs,
                                                                                                                                    localKeyPair, cultureInfo,
                                                                                                                                    installed, timeout, policyType,
                                                                                                                                    (wasRenewed ?
                                                                                                                                        NetworkFlags.ViaRenewal :
                                                                                                                                        NetworkFlags.None) | networkFlags,
                                                                                                                                    ref canRenew,
                                                                                                                                    ref localResult) == ReturnCode.Ok))
                                                                                                                            {
                                                                                                                                //
                                                                                                                                // NOTE: Success, the certificate was imported
                                                                                                                                //       and verified.
                                                                                                                                //
                                                                                                                                string newFileName;

                                                                                                                                if (!wasRenewed && (temporaryFileName != null))
                                                                                                                                    newFileName = resourceName;
                                                                                                                                else
                                                                                                                                    newFileName = localFileName;

                                                                                                                                fileName = newFileName;
                                                                                                                                certificate = localCertificate;

                                                                                                                                result = wasRenewed ? OperationStatus.RenewedOk :
                                                                                                                                    OperationStatus.VerifiedOk;

                                                                                                                                bool wasAdded = false;

                                                                                                                                if (!force && isForThisAssembly && (assemblyCertificate == null)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                                    //
                                                                                                                                    // BUGFIX: Do not permit any license certificate that
                                                                                                                                    //         may have been used (simply) to load a key
                                                                                                                                    //         ring to be persisted as the overall plugin
                                                                                                                                    //         license certificate.
                                                                                                                                    //
                                                                                                                                        && !KRS.IsLicensePending()
#endif
                                                                                                                                    )
                                                                                                                                {
                                                                                                                                    /* NO RESULT */
                                                                                                                                    CLS.SetFileName(newFileName);

                                                                                                                                    /* NO RESULT */
                                                                                                                                    CLS.SetCertificate(localCertificate);

                                                                                                                                    wasAdded = true;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                                    //
                                                                                                                                    // NOTE: The license certificate, which has now been
                                                                                                                                    //       verified, is for this assembly (Harpy).  If
                                                                                                                                    //       necessary, enable policy tracing now.
                                                                                                                                    //
                                                                                                                                    MaybeEnablePolicyTracing(localCertificate);
#endif

#if DEBUG || FORCE_TRACE
                                                                                                                                    priority = defaultPriority;

                                                                                                                                    Utility.AdjustTracePriority(ref priority, -1);

                                                                                                                                    /* NO RESULT */
                                                                                                                                    TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                        logClientData, String.Format(
                                                                                                                                        "Core package {0} certificate {1} cached.",
                                                                                                                                        CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                        CertificateSharedOps.ToString(localCertificate)),
                                                                                                                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                                                                                                }

                                                                                                                                if (!wasAdded && (localCertificate != null))
                                                                                                                                {
                                                                                                                                    Result addResult = null;

                                                                                                                                    if (CLS.AddCertificate(
                                                                                                                                            localCertificate.Id, localCertificate,
                                                                                                                                            true, ref addResult)) /* IMPOSSIBLE? */
                                                                                                                                    {
#if DEBUG || FORCE_TRACE
                                                                                                                                        priority = defaultPriority;

                                                                                                                                        Utility.AdjustTracePriority(ref priority, -1);

                                                                                                                                        /* NO RESULT */
                                                                                                                                        TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                            logClientData, String.Format(
                                                                                                                                            "Package {0} certificate {1} cached.",
                                                                                                                                            CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                            CertificateSharedOps.ToString(localCertificate)),
                                                                                                                                            typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                                                                                                    }
                                                                                                                                    else
                                                                                                                                    {
#if DEBUG || FORCE_TRACE
                                                                                                                                        priority = defaultPriority;

                                                                                                                                        Utility.AdjustTracePriority(ref priority, 1);

                                                                                                                                        /* NO RESULT */
                                                                                                                                        TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                            logClientData, String.Format(
                                                                                                                                            "Could not cache package {0} certificate {1}: {2}",
                                                                                                                                            CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                            CertificateSharedOps.ToString(localCertificate),
                                                                                                                                            Utility.FormatWrapOrNull(true, false, addResult)),
                                                                                                                                            typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                                                                                                    }
                                                                                                                                }

                                                                                                                                localResult = null;

                                                                                                                                if ((anyClientData != null) &&
                                                                                                                                    (CertificateSharedOps.MatchFlags(
                                                                                                                                        localCertificate, FlagType.Feature, flagsKey,
                                                                                                                                        Features.SkipAuthorizationOrAll, null, false,
                                                                                                                                        false, true, ref localResult) == ReturnCode.Ok) &&
                                                                                                                                    !anyClientData.HasAny(
                                                                                                                                        Constants.SkipAuthorizationDataName))
                                                                                                                                {
                                                                                                                                    /* IGNORED */
                                                                                                                                    anyClientData.TrySetAny(
                                                                                                                                        Constants.SkipAuthorizationDataName, true);
                                                                                                                                }

#if DEBUG || FORCE_TRACE
                                                                                                                                priority = defaultPriority;

                                                                                                                                Utility.AdjustTracePriority(ref priority, -1);

                                                                                                                                /* NO RESULT */
                                                                                                                                TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                    logClientData, String.Format(
                                                                                                                                    "Package {0} certificate {1} {2} success.",
                                                                                                                                    CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                    CertificateSharedOps.ToString(localCertificate),
                                                                                                                                    wasRenewed ? "checking (with renewal)" : "checking"),
                                                                                                                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                                                                                                                return ReturnCode.Ok;
                                                                                                                            }
                                                                                                                            else if (canRenew && !wasRenewed && (renewCallback != null))
                                                                                                                            {
                                                                                                                                byte[] oldPublicKeyToken = Certificate.MaybeCopyKey(
                                                                                                                                    localCertificate);

                                                                                                                                //
                                                                                                                                // HACK: The local key pairs are passed to the renewal callback
                                                                                                                                //       so that it can verify returned (renewed?) certificates
                                                                                                                                //       when the server does not provide a renewal key ring.
                                                                                                                                //
                                                                                                                                Result renewResult = null;

                                                                                                                                if (renewCallback(
                                                                                                                                        interpreter, assembly, assemblyName, plugin,
                                                                                                                                        localHashAlgorithmName, hashKey, null,
                                                                                                                                        encoding, localKeyPairs, anyClientData,
                                                                                                                                        features, restrictions, localPolicy,
                                                                                                                                        policyType, keyName, keyRingName, timeout,
                                                                                                                                        embedded, validate, ref localFileName,
                                                                                                                                        ref localCertificate,
                                                                                                                                        ref renewResult) == ReturnCode.Ok)
                                                                                                                                {
                                                                                                                                    byte[] newPublicKeyToken = Certificate.MaybeCopyKey(
                                                                                                                                        localCertificate);

                                                                                                                                    if (CertificateDataOps.MatchPublicKeyToken(
                                                                                                                                            newPublicKeyToken, oldPublicKeyToken))
                                                                                                                                    {
                                                                                                                                        wasRenewed = true;
                                                                                                                                        goto retry;
                                                                                                                                    }
                                                                                                                                    else
                                                                                                                                    {
                                                                                                                                        //
                                                                                                                                        // NOTE: If the new public key token does not match
                                                                                                                                        //       the old one, make sure the new public key
                                                                                                                                        //       token is present in the currently loaded
                                                                                                                                        //       (and valid) list.  These key pairs are used
                                                                                                                                        //       to locate the (new?) key pair used to sign
                                                                                                                                        //       the renewed certificate.  When not found, a
                                                                                                                                        //       match must be found on the trusted key ring
                                                                                                                                        //       for the interpreter, if applicable, or the
                                                                                                                                        //       operation will fail.
                                                                                                                                        //
                                                                                                                                        localKeyPair = CertificateSharedOps.GetKeyPairByPublicKeyToken(
                                                                                                                                            localKeyPairs, newPublicKeyToken);

                                                                                                                                        if (localKeyPair != null)
                                                                                                                                        {
                                                                                                                                            wasRenewed = true;
                                                                                                                                            goto retry;
                                                                                                                                        }
                                                                                                                                        else
                                                                                                                                        {
                                                                                                                                            //
                                                                                                                                            // NOTE: Next, see if the trusted key ring for
                                                                                                                                            //       the interpreter has been updated with
                                                                                                                                            //       the new key pair.  This requires the
                                                                                                                                            //       associated execution policy flag to be
                                                                                                                                            //       enabled.
                                                                                                                                            //
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                                            Result keyRingError = null;

                                                                                                                                            if (allowRingPublicKey)
                                                                                                                                            {
                                                                                                                                                localKeyPair = CertificateKeyRingOps.GetKeyPair(
                                                                                                                                                    interpreter, localKeyRingName, policyType,
                                                                                                                                                    newPublicKeyToken, ref keyRingError);

                                                                                                                                                if (localKeyPair != null)
                                                                                                                                                {
                                                                                                                                                    wasRenewed = true;
                                                                                                                                                    goto retry;
                                                                                                                                                }
                                                                                                                                            }
#endif

                                                                                                                                            //
                                                                                                                                            // NOTE: Return the "certificate expired" error
                                                                                                                                            //       along with the renewal error(s).
                                                                                                                                            //
                                                                                                                                            result = new ResultList(new Result[] {
                                                                                                                                                activationResult, localResult, renewResult,
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                                                keyRingError,
#endif
                                                                                                                                                "renewal public key token is not present"
                                                                                                                                            });

#if DEBUG || FORCE_TRACE
                                                                                                                                            priority = defaultPriority;

                                                                                                                                            Utility.AdjustTracePriority(ref priority, 1);

                                                                                                                                            /* NO RESULT */
                                                                                                                                            TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                                logClientData, String.Format(
                                                                                                                                                "Package {0} certificate {1} renewal public key token {2} is not present.",
                                                                                                                                                CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                                CertificateSharedOps.ToString(localCertificate),
                                                                                                                                                CertificateDataOps.FormatPublicKeyToken(
                                                                                                                                                    newPublicKeyToken, true, true)),
                                                                                                                                                typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                                                                                                        }
                                                                                                                                    }
                                                                                                                                }
                                                                                                                                else
                                                                                                                                {
                                                                                                                                    //
                                                                                                                                    // NOTE: Return the "certificate expired" error
                                                                                                                                    //       along with the renewal error.
                                                                                                                                    //
                                                                                                                                    result = new ResultList(new Result[] {
                                                                                                                                        activationResult, localResult, renewResult
                                                                                                                                    });

#if DEBUG || FORCE_TRACE
                                                                                                                                    priority = defaultPriority;

                                                                                                                                    Utility.AdjustTracePriority(ref priority, 1);

                                                                                                                                    /* NO RESULT */
                                                                                                                                    TraceOps.MaybeLogAndDebugTrace(
                                                                                                                                        logClientData, String.Format(
                                                                                                                                        "Package {0} certificate {1} renewal failure: {2}",
                                                                                                                                        CertificateDataOps.FormatPackageName(packageName),
                                                                                                                                        CertificateSharedOps.ToString(localCertificate),
                                                                                                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                                                                                                        typeof(CertificateVerifyOps).Name, priority, 0);
#endif
                                                                                                                                }
                                                                                                                            }
                                                                                                                            else
                                                                                                                            {
                                                                                                                                result = new ResultList(new Result[] {
                                                                                                                                    activationResult, localResult
                                                                                                                                });
                                                                                                                            }
                                                                                                                        }
                                                                                                                        else
                                                                                                                        {
                                                                                                                            result = localResult;
                                                                                                                        }
                                                                                                                    }
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                                                    else
                                                                                                                    {
                                                                                                                        result = localResult;
                                                                                                                    }
#endif
                                                                                                                }
                                                                                                                else
                                                                                                                {
                                                                                                                    result = localResult;
                                                                                                                }
                                                                                                            }
                                                                                                            else
                                                                                                            {
                                                                                                                result = localResult;
                                                                                                            }
                                                                                                        }
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        result = localResult;
                                                                                                    }
                                                                                                }
                                                                                                else
                                                                                                {
                                                                                                    result = localResult;
                                                                                                }
                                                                                            }
                                                                                            else
                                                                                            {
                                                                                                result = localResult;
                                                                                            }
                                                                                        }
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                        else
                                                                                        {
                                                                                            result = localResult;
                                                                                        }
#endif
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        result = localResult;
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    localResult = new ResultList(new Result[] {
                                                                                        wellKnownResult,
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                                                        trustedResult, featureResult,
#endif
                                                                                        restrictionResult
                                                                                    });

                                                                                    result = String.Format(
                                                                                        "promotional restriction failure: {0}",
                                                                                        Utility.FormatWrapOrNull(true, false, localResult));
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                result = localResult;
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            result = localResult;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        result = localResult;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    result = localResult;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                result = localResult;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            result = localResult;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        result = localResult;
                                                    }
                                                }
                                                else
                                                {
                                                    result = localResult;
                                                }
                                            }
                                            else
                                            {
                                                localResult = new ResultList(
                                                    ResultFlags.CompactListMask, new Result[] {
                                                    localResult, initialFileNameResult,
                                                    createInterpreterResult, tryRequestResult,
                                                    finalFileNameResult
                                                });

                                                string productName = null;

                                                if (plugin != null)
                                                {
                                                    productName = String.Format("plugin {0}",
                                                        Utility.FormatWrapOrNull(plugin));
                                                }
                                                else
                                                {
                                                    productName = String.Format("product {0}",
                                                        Utility.FormatWrapOrNull(packageName));
                                                }

                                                result = String.Format(
                                                    "cannot find a suitable package certificate file for {3} in {0}{1}{0}: {2}",
                                                    Environment.NewLine, Utility.FormatWrapOrNull(FileNamesToString(fileNames)),
                                                    Utility.FormatWrapOrNull(localResult), productName);
                                            }
                                        }
                                        finally
                                        {
                                            try
                                            {
                                                //
                                                // NOTE: Attempt to delete the temporary file
                                                //       that we created to hold the embedded
                                                //       license certificate data.
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
                                                priority = defaultPriority;

                                                Utility.AdjustTracePriority(ref priority, 1);

                                                /* NO RESULT */
                                                TraceOps.MaybeLogAndDebugTrace(logClientData,
                                                    e, typeof(CertificateVerifyOps).Name,
                                                    priority, 0);
#endif
                                            }
                                        }
                                    }
                                    else
                                    {
                                        result = localResult;
                                    }
                                }
                                else
                                {
                                    result = localResult;
                                }
#else
                                result = "not implemented";
#endif

#if (XML && SERIALIZATION) || (CERTIFICATE_PLUGIN && CERTIFICATE_POLICY)
                            error:
#endif

                                //
                                // HACK: This is actually not the "beginning" of anything;
                                //       however, this (wrapper) method being called does
                                //       the correct call into the core library in order
                                //       to increment the process environment variable.
                                //
                                if (isForThisPlugin)
                                {
                                    CertificateProcessOps.BeginPending(
                                        Constants.LicenseFailureCountEnvVarName);
                                }

#if DEBUG || FORCE_TRACE
                                priority = defaultPriority;

                                Utility.AdjustTracePriority(ref priority, 1);

                                /* NO RESULT */
                                TraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Package {0} certificate {1} checking failure with key pairs {2}: {3}",
                                    CertificateDataOps.FormatPackageName(packageName),
                                    CertificateSharedOps.ToString(localCertificate),
                                    Utility.FormatWrapOrNull(
                                        CertificateDataOps.FormatKeyPairs(localKeyPairs, true)),
                                    Utility.FormatWrapOrNull(true, false, result)),
                                    typeof(CertificateVerifyOps).Name, priority, 0);
#endif

                                return ReturnCode.Error;
                            }
                            finally
                            {
                                if (!wasEnabled)
                                {
                                    if (enableTracing ||
                                        TraceOps.ShouldForceEnableForPolicy())
                                    {
                                        TraceOps.AdjustPrioritiesAndLimits(
                                            interpreter, cultureInfo, tracePolicy,
                                            false, ref savedBasePriority,
                                            ref savedPriorities1, ref savedPriorities2);
                                    }
                                }

                                TraceOps.MaybeEnableOrDisableTextWriter(
                                    interpreter, cultureInfo, tracePolicy, false,
                                    ref wasEnabled, ref savedBasePriority,
                                    ref savedPriorities1, ref savedPriorities2);
                            }
                        }
                        finally
                        {
                            Utility.PopActiveInterpreter();
                        }
                    }
                    finally
                    {
                        if (logClientData != null)
                        {
                            logClientData.Dispose();
                            logClientData = null;
                        }
                    }
                }
                finally
                {
                    Utility.PopActiveInterpreter();
                }
            }
            finally
            {
                CLS.EndPending();
            }
        }
        #endregion
    }
}
