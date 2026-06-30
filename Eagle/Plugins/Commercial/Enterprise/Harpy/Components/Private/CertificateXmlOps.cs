/*
 * CertificateXmlOps.cs --
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
using System.Reflection;
using System.Text;
using System.Xml;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides private helper methods for importing, exporting, and
    /// validating Harpy certificate XML documents.
    /// </summary>
    [ObjectId("d477e4d1-bc62-4948-a7b8-dc0e881692a0")]
    internal static class CertificateXmlOps
    {
        #region Core Support
        /// <summary>
        /// Gets the schema namespace URI, as a string, used for certificate
        /// XML documents.
        /// </summary>
        /// <returns>
        /// The schema namespace URI string, or null if it is unavailable.
        /// </returns>
        private static string GetNamespaceUriString() /* CORE */
        {
            Uri uri = Constants.SchemaNamespaceUri;

            if (uri == null)
                return null;

            return uri.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified text appears to be an encrypted
        /// certificate document.
        /// </summary>
        /// <param name="text">
        /// The text to examine.
        /// </param>
        /// <returns>
        /// Non-zero if the text appears to be an encrypted document.
        /// </returns>
        private static bool LooksLikeEncryptedDocument( /* CORE */
            string text /* in */
            )
        {
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            return DataOps.HasEncryptedDataHeader(text);
#else
            //
            // HACK: Since this build lacks support for the encrypted
            //       document format, just pretend it does not exist.
            //
            return false;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified text appears to be a certificate
        /// document.
        /// </summary>
        /// <param name="text">
        /// The text to examine.
        /// </param>
        /// <param name="encrypted">
        /// Non-zero if the text is expected to be an encrypted document.
        /// </param>
        /// <returns>
        /// Non-zero if the text appears to be a certificate document.
        /// </returns>
        public static bool LooksLikeDocument( /* CORE */
            string text,   /* in */
            bool encrypted /* in */
            )
        {
            bool? maybeEncrypted = encrypted;
            Result error = null;

            return LooksLikeDocument(
                text, false, ref maybeEncrypted, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified bytes appear to be a certificate
        /// document, after decoding them using the specified encoding.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to examine.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the bytes to text.
        /// </param>
        /// <param name="encrypted">
        /// Non-zero if the bytes are expected to be an encrypted document.
        /// </param>
        /// <param name="viaServer">
        /// Non-zero if the bytes were obtained from a (Kapok) server.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the bytes appear to be a certificate document.
        /// </returns>
        public static bool LooksLikeDocument( /* CORE */
            byte[] bytes,             /* in */
            Encoding encoding,        /* in */
            bool encrypted,           /* in */
            bool viaServer,           /* in */
            ref Result error          /* out */
            )
        {
            bool? maybeEncrypted = encrypted;

            return LooksLikeDocument(
                bytes, encoding, viaServer, ref maybeEncrypted,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified bytes appear to be a certificate
        /// document, after decoding them using the specified encoding.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to examine.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the bytes to text.
        /// </param>
        /// <param name="viaServer">
        /// Non-zero if the bytes were obtained from a (Kapok) server.
        /// </param>
        /// <param name="maybeEncrypted">
        /// On input, indicates whether the document is expected to be
        /// encrypted, or null if unknown; on output, indicates whether it
        /// appears to be encrypted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the bytes appear to be a certificate document.
        /// </returns>
        public static bool LooksLikeDocument( /* CORE */
            byte[] bytes,             /* in */
            Encoding encoding,        /* in */
            bool viaServer,           /* in */
            ref bool? maybeEncrypted, /* in, out */
            ref Result error          /* out */
            )
        {
            if (bytes == null)
            {
                error = "invalid bytes";
                return false;
            }

            if (encoding == null)
            {
                error = "invalid encoding";
                return false;
            }

            string text;

            try
            {
                text = encoding.GetString(bytes); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return false;
            }

            if (!LooksLikeDocument(
                    text, viaServer, ref maybeEncrypted,
                    ref error))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified text appears to be a certificate
        /// document, optionally treating non-conforming text from a server as
        /// an error message.
        /// </summary>
        /// <param name="text">
        /// The text to examine.
        /// </param>
        /// <param name="viaServer">
        /// Non-zero if the text was obtained from a (Kapok) server.
        /// </param>
        /// <param name="maybeEncrypted">
        /// On input, indicates whether the document is expected to be
        /// encrypted, or null if unknown; on output, indicates whether it
        /// appears to be encrypted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the text appears to be a certificate document.
        /// </returns>
        public static bool LooksLikeDocument( /* CORE */
            string text,              /* in */
            bool viaServer,           /* in */
            ref bool? maybeEncrypted, /* in, out */
            ref Result error          /* out */
            )
        {
            //
            // HACK: If the text string does not appear to
            //       be a "well-formed" certificate (XML?)
            //       document -AND- the viaServer parameter
            //       is non-zero then assume the text is an
            //       error message from a (Kapok) server and
            //       use it verbatim.
            //
            // TODO: Perhaps make this detection algorithm
            //       more robust in the future.
            //
            if (maybeEncrypted == null)
            {
                if (LooksLikeEncryptedDocument(text))
                {
                    maybeEncrypted = true;
                    return true;
                }
                else
                {
                    maybeEncrypted = false;
                }
            }
            else if ((bool)maybeEncrypted)
            {
                if (LooksLikeEncryptedDocument(text))
                {
                    return true;
                }
                else
                {
                    if (viaServer)
                        error = text;
                    else
                        error = "missing encrypted data header";

                    return false;
                }
            }

            if (!Utility.LooksLikeXmlDocument(text))
            {
                if (viaServer)
                    error = text;
                else
                    error = "missing XML document start";

                return false;
            }

            string uriString = GetNamespaceUriString();

            if (String.IsNullOrEmpty(uriString))
            {
                error = "invalid XML document namespace URI";
                return false;
            }

            if (text.IndexOf(uriString) == Index.Invalid)
            {
                error = "missing XML document namespace URI";
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates an XML namespace manager for the specified document,
        /// populated with the certificate schema namespace.
        /// </summary>
        /// <param name="document">
        /// The XML document for which to create the namespace manager.
        /// </param>
        /// <returns>
        /// The newly created XML namespace manager.
        /// </returns>
        private static XmlNamespaceManager GetNamespaceManager( /* CORE */
            XmlDocument document /* in */
            )
        {
            XmlNamespaceManager namespaceManager =
                new XmlNamespaceManager(document.NameTable);

            string namespaceName = Constants.SchemaNamespaceName;
            string uriString = GetNamespaceUriString();

            if ((namespaceName != null) && (uriString != null))
                namespaceManager.AddNamespace(namespaceName, uriString);

            return namespaceManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates the specified XML document against the certificate
        /// schema.
        /// </summary>
        /// <param name="document">
        /// The XML document to validate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the document is valid; otherwise,
        /// an error code.
        /// </returns>
        private static ReturnCode ValidateDocument( /* CORE */
            XmlDocument document, /* in */
            ref Result error      /* out */
            )
        {
            ReturnCode code = Utility.Validate(
                CertificateAssemblyOps.GetObject(),
                Constants.SchemaResourceName, document, ref error);

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "Validation of certificate {0}, document = {1}, " +
                "outerXml = {2}, code = {3}, error = {4}",
                (code == ReturnCode.Ok) ? "success" : "failure",
                Utility.FormatWrapOrNull(document),
                Utility.FormatWrapOrNull(true, true,
                    (document != null) ? document.OuterXml : null),
                code, Utility.FormatWrapOrNull(true, false, error)),
                typeof(CertificateXmlOps).Name, TracePriority.MediumLow);
#endif

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to select the named property node from the specified
        /// document element.
        /// </summary>
        /// <param name="documentElement">
        /// The XML document element to search.
        /// </param>
        /// <param name="namespaceManager">
        /// The XML namespace manager used to resolve the schema namespace.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property node to select.
        /// </param>
        /// <param name="node">
        /// Upon success, receives the selected node, or null if it was not
        /// found.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero on success.
        /// </returns>
        private static bool TryGetPropertyNode( /* CORE */
            XmlElement documentElement,           /* in */
            XmlNamespaceManager namespaceManager, /* in */
            string propertyName,                  /* in */
            ref XmlNode node,                     /* out */
            ref Result error                      /* out */
            )
        {
            if (documentElement == null)
            {
                error = "invalid xml document";
                return false;
            }

            if (namespaceManager == null)
            {
                error = "invalid xml namespace manager";
                return false;
            }

            if (String.IsNullOrEmpty(propertyName))
            {
                error = "invalid property name";
                return false;
            }

            node = documentElement.SelectSingleNode(
                String.Format("{0}:{1}", Constants.SchemaNamespaceName,
                propertyName), namespaceManager);

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified property node is present and has
        /// non-empty inner text.
        /// </summary>
        /// <param name="node">
        /// The property node to examine.
        /// </param>
        /// <returns>
        /// Non-zero if the node is present and has non-empty inner text.
        /// </returns>
        private static bool HavePropertyNode( /* CORE */
            XmlNode node /* in */
            )
        {
            return (node != null) && !String.IsNullOrEmpty(node.InnerText);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the certificate properties from the specified XML document
        /// and packs them into the specified certificate object.
        /// </summary>
        /// <param name="document">
        /// The XML document to read the certificate properties from.
        /// </param>
        /// <param name="certificateXml">
        /// The certificate object to populate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode FromDocument( /* CORE */
            XmlDocument document,           /* in */
            ICertificateXml certificateXml, /* in */
            ref Result error                /* out */
            )
        {
            if (document == null)
            {
                error = "invalid xml document";
                return ReturnCode.Error;
            }

            if (certificateXml == null)
            {
                error = "invalid certificate xml";
                return ReturnCode.Error;
            }

            XmlElement documentElement = document.DocumentElement;

            if (documentElement == null)
            {
                error = "invalid xml document element";
                return ReturnCode.Error;
            }

            XmlNamespaceManager namespaceManager =
                GetNamespaceManager(document);

            if (namespaceManager == null)
            {
                error = "invalid xml namespace manager";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            XmlNode node = null;
            Version protocolVersion = null;
            Uri origin = null;
            Uri authority = null;
            Uri agreement = null;
            Uri support = null;
            DateTime timeStamp = DateTime.MinValue;
            TimeSpan duration = TimeSpan.Zero;
            byte[] key = null;
            ulong number = 0;
            byte[] signature = null;
            Version version = null;

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "ProtocolVersion",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseVersion(
                        node.InnerText, ref protocolVersion, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Origin",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        node.InnerText, ref origin, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Authority",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        node.InnerText, ref authority, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Agreement",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        node.InnerText, ref agreement, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Support",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        node.InnerText, ref support, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "TimeStamp",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseUniversalTimeStamp(
                        node.InnerText, ref timeStamp, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Duration",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseDuration(
                        node.InnerText, ref duration, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Key",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseKey(
                        node.InnerText, ref key, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Keys",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (node != null)
            {
                error = "support for multiple keys not implemented";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Number",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseNumber(
                        node.InnerText, ref number, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Signature",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseSignature(
                        node.InnerText, false, ref signature, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Signatures",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (node != null)
            {
                error = "support for multiple signatures not implemented";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!TryGetPropertyNode(
                    documentElement, namespaceManager, "Version",
                    ref node, ref error))
            {
                return ReturnCode.Error;
            }

            if (HavePropertyNode(node))
            {
                if (!DataOps.TryParseVersion(
                        node.InnerText, ref version, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            certificateXml.Pack(
                protocolVersion, origin, authority, agreement, support,
                timeStamp, duration, key, number, signature, version);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs a sanity check to confirm that the deserialized object
        /// originates from the same assembly as the specified type.
        /// </summary>
        /// <param name="type">
        /// The expected type, whose assembly is compared against that of the
        /// deserialized object.
        /// </param>
        /// <param name="object">
        /// The deserialized object to check.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the check passes; otherwise, an
        /// error code.
        /// </returns>
        private static ReturnCode DeserializeSanityCheck( /* CORE */
            Type type,       /* in */
            object @object,  /* in */
            ref Result error /* out */
            )
        {
            if (type == null)
            {
                error = String.Format(
                    "invalid type{0}",
                    Constants.SanityCheckSuffix);

                return ReturnCode.Error;
            }

            if (@object == null)
            {
                error = String.Format(
                    "invalid object{0}",
                    Constants.SanityCheckSuffix);

                return ReturnCode.Error;
            }

            try
            {
                Assembly typeAssembly = type.Assembly; /* throw */

                if (typeAssembly == null)
                {
                    error = String.Format(
                        "invalid type assembly{0}",
                        Constants.SanityCheckSuffix);

                    return ReturnCode.Error;
                }

                string typeLocation = typeAssembly.Location; /* throw */

                if (String.IsNullOrEmpty(typeLocation))
                {
                    error = String.Format(
                        "invalid type assembly location{0}",
                        Constants.SanityCheckSuffix);

                    return ReturnCode.Error;
                }

                Type objectType = @object.GetType(); /* throw */

                if (objectType == null)
                {
                    error = String.Format(
                        "invalid object type{0}",
                        Constants.SanityCheckSuffix);

                    return ReturnCode.Error;
                }

                Assembly objectAssembly = objectType.Assembly; /* throw */

                if (objectAssembly == null)
                {
                    error = String.Format(
                        "invalid object assembly{0}",
                        Constants.SanityCheckSuffix);

                    return ReturnCode.Error;
                }

                string objectLocation = objectAssembly.Location; /* throw */

                if (String.IsNullOrEmpty(objectLocation))
                {
                    error = String.Format(
                        "invalid object assembly location{0}",
                        Constants.SanityCheckSuffix);

                    return ReturnCode.Error;
                }

                if (Utility.IsSameFile(typeLocation, objectLocation))
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    error = String.Format(
                        "failed deserialize sanity check: type " +
                        "assembly {0} is different from object " +
                        "assembly {1}", Utility.FormatWrapOrNull(
                        typeLocation), Utility.FormatWrapOrNull(
                        objectLocation));
                }
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    e, typeof(CertificateXmlOps).Name,
                    TracePriority.Highest, 0);
#endif

                error = "exception during deserialize sanity check";
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deserializes a certificate from the specified XML document, then
        /// fixes up the fields that the XML serializer cannot handle.
        /// </summary>
        /// <param name="document">
        /// The XML document to deserialize.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema
        /// before deserializing it.
        /// </param>
        /// <param name="certificateXml">
        /// Upon success, receives the deserialized certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode DeserializeAndFixup( /* CORE */
            XmlDocument document,               /* in */
            bool validate,                      /* in */
            ref ICertificateXml certificateXml, /* out */
            ref Result error                    /* out */
            )
        {
            if (document == null)
            {
                error = "invalid xml document";
                return ReturnCode.Error;
            }

            if (validate && (ValidateDocument(
                    document, ref error) != ReturnCode.Ok))
            {
                return ReturnCode.Error;
            }

            using (XmlNodeReader reader = new XmlNodeReader(document))
            {
                //
                // NOTE: Use the .NET Framework built-in XML serializer to
                //       perform the "first pass" of the deserialization.
                //
                object @object = null;

                if (Utility.Deserialize(
                        typeof(Certificate), reader, ref @object,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                //
                // HACK: An extra sanity-check here.  In some cases, the
                //       call to deserialize the certificate may load an
                //       assembly for Harpy that is different from its
                //       loaded plugin location.  When this happens, the
                //       cast to ICertificateXml (below) will not work.
                //       Instead, completely avoid doing that and return
                //       an appropriate error message instead.
                //
                if (DeserializeSanityCheck(typeof(Certificate), @object,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                //
                // NOTE: Ok, now fix the fields that the XmlSerializer
                //       screws up on.
                //
                certificateXml = @object as ICertificateXml;

                //
                // NOTE: Handle the remaining certificate fields directly
                //       by manually reading and parsing them from the XML
                //       document.
                //
                if (FromDocument(
                        document, certificateXml, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports a certificate from the specified file, which may name a
        /// local file, a remote URI, or a managed assembly with an embedded
        /// certificate resource.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to import the certificate from.
        /// </param>
        /// <param name="anyResourcePublicKey">
        /// Non-zero to permit the embedded resource to be signed with any
        /// public key.
        /// </param>
        /// <param name="isForThisAssembly">
        /// Non-zero if the certificate is expected to belong to this
        /// assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Import( /* LOCAL OR REMOTE */ /* CORE */
            string fileName,              /* in */
            bool anyResourcePublicKey,    /* in */
            bool isForThisAssembly,       /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (!Utility.IsRemoteUri(fileName) && /* EXEMPT */
                !File.Exists(fileName))
            {
                result = String.Format(
                    "could not read {0}: no such file",
                    Utility.FormatWrapOrNull(fileName));

                return ReturnCode.Error;
            }

            if (certificate != null)
            {
                result = "cannot overwrite valid certificate";
                return ReturnCode.Error;
            }

            try
            {
                byte[] bytes; /* REUSED */

                //
                // HACK: If the file is present in the in-memory
                //       file cache, do not (attempt to) read it
                //       from the file system.
                //
                if (CertificateLicenseState.TryGetCachedBinaryFile(
                        fileName, out bytes))
                {
                    return Import(
                        fileName, bytes, validate, ref certificate,
                        ref result);
                }

                //
                // HACK: If this is a managed assembly file name,
                //       extract associated certificate resource
                //       string and import it as a string.
                //
                if (SharedOps.IsAssemblyFileName(fileName))
                {
                    bytes = File.ReadAllBytes(fileName); /* throw */

                    bytes = SharedOps.GetEmbeddedBytes(
                        fileName, bytes, SharedOps.ResourceNameFromFileName(
                        fileName), anyResourcePublicKey, isForThisAssembly,
                        ref result);

                    if (bytes == null)
                        return ReturnCode.Error;

                    return Import(
                        fileName, bytes, validate, ref certificate,
                        ref result);
                }

                XmlDocument document = new XmlDocument();
                document.Load(fileName); /* throw */

                ICertificateXml certificateXml = null;

                if (DeserializeAndFixup(
                        document, validate, ref certificateXml,
                        ref result) == ReturnCode.Ok) /* throw */
                {
                    certificate = certificateXml as ICertificate;
                    result = OperationStatus.ImportedOk;

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Support
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Imports a certificate from a script file obtained through the
        /// interpreter's file system host.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose file system host is used to obtain the file.
        /// </param>
        /// <param name="fileName">
        /// The name of the file to import the certificate from.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags used when obtaining the file.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode ImportFromHost( /* LOCAL EMBEDDED */ /* CORE? */
            Interpreter interpreter,      /* in */
            string fileName,              /* in */
            ScriptFlags scriptFlags,      /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (Utility.IsRemoteUri(fileName)) /* EXEMPT */
            {
                result = "remote uri not supported";
                return ReturnCode.Error;
            }

            if (certificate != null)
            {
                result = "cannot overwrite valid certificate";
                return ReturnCode.Error;
            }

            IClientData clientData = ClientData.Empty;

            //
            // NOTE: This method (without the Library flag) always
            //       ends up calling into the GetData method of the
            //       IFileSystemHost interface, thereby preventing
            //       us from needing to call into the IFileSystemHost
            //       interface directly (along with the associated
            //       error handling gymnastics).
            //
            Result localResult = null;

            if (interpreter.GetScript(
                    fileName, ref scriptFlags, ref clientData,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            string xml;

            if (Utility.HasFlags(scriptFlags, ScriptFlags.File, true))
            {
                string newFileName = localResult;
                string text = null;

                localResult = null;

                if (Engine.ReadScriptFile(
                        interpreter, newFileName,
                        Constants.ImportEngineFlags, ref text,
                        ref localResult) != ReturnCode.Ok)
                {
                    result = localResult;
                    return ReturnCode.Error;
                }

                xml = text;
            }
            else
            {
                xml = localResult;
            }

            return Import(
                fileName, xml, validate, ref certificate, ref result);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Imports a certificate from the specified XML string, discarding
        /// the imported certificate object.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the XML, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="xml">
        /// The certificate XML to import.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Import( /* CORE? */
            string fileName,  /* in: NOT USED */
            string xml,       /* in */
            bool validate,    /* in */
            ref Result result /* out */
            )
        {
            ICertificate certificate = null;

            return Import(
                fileName, xml, validate, ref certificate, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports a certificate from the specified XML string.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the XML, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="xml">
        /// The certificate XML to import.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Import( /* CORE? */
            string fileName,              /* in: NOT USED */
            string xml,                   /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (String.IsNullOrEmpty(xml))
            {
                result = "invalid xml";
                return ReturnCode.Error;
            }

            if (certificate != null)
            {
                result = "cannot overwrite valid certificate";
                return ReturnCode.Error;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xml); /* throw */

                ICertificateXml certificateXml = null;

                if (DeserializeAndFixup(
                        document, validate, ref certificateXml,
                        ref result) == ReturnCode.Ok) /* throw */
                {
                    certificate = certificateXml as ICertificate;
                    result = OperationStatus.ImportedOk;

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts an embedded certificate from the specified text, removing
        /// the signature block from the text.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the text, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="text">
        /// On input, the text containing the embedded certificate; on output,
        /// the text with the signature block removed.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the extracted certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Extract( /* CORE? */
            string fileName,              /* in: NOT USED */
            bool validate,                /* in */
            ref string text,              /* in, out */
            ref ICertificate certificate, /* out */
            ref Result error              /* out */
            )
        {
            return ExtractOrDiscard(
                fileName, false, validate, ref text, ref certificate,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the embedded certificate signature block from the
        /// specified text without importing the certificate.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the text, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="text">
        /// On input, the text containing the embedded certificate; on output,
        /// the text with the signature block removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Discard( /* CORE? */
            string fileName, /* in: NOT USED */
            ref string text, /* in, out */
            ref Result error /* out */
            )
        {
            ICertificate certificate = null;

            return ExtractOrDiscard(
                fileName, false, false, ref text, ref certificate,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Locates the embedded certificate signature block in the specified
        /// text, optionally importing the certificate, and removes the block
        /// from the text.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the text, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="discard">
        /// Non-zero to discard the certificate without importing it.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="text">
        /// On input, the text containing the embedded certificate; on output,
        /// the text with the signature block removed.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the extracted certificate, unless
        /// discarding.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode ExtractOrDiscard( /* CORE? */
            string fileName,              /* in: NOT USED */
            bool discard,                 /* in */
            bool validate,                /* in */
            ref string text,              /* in, out */
            ref ICertificate certificate, /* out */
            ref Result error              /* out */
            )
        {
            if (text == null)
            {
                error = "invalid text";
                return ReturnCode.Error;
            }

            if (text.Length == 0)
            {
                error = "empty text";
                return ReturnCode.Error;
            }

            string beginMagic = Constants.BeginMagic;

            if (beginMagic == null)
            {
                error = "missing begin magic";
                return ReturnCode.Error;
            }

            string endMagic = Constants.EndMagic;

            if (endMagic == null)
            {
                error = "missing end magic";
                return ReturnCode.Error;
            }

            int beginMagicLength = beginMagic.Length;

            int beginIndex = text.LastIndexOf(
                beginMagic, Utility.GetSystemComparisonType(false));

            if (beginIndex == Index.Invalid)
            {
                error = "start of signature block not found";
                return ReturnCode.Error;
            }

            int endIndex = text.IndexOf(
                endMagic, beginIndex + beginMagicLength,
                Utility.GetSystemComparisonType(false));

            if (endIndex == Index.Invalid)
            {
                error = "end of signature block not found";
                return ReturnCode.Error;
            }

            string value = text.Substring(
                beginIndex + beginMagicLength,
                endIndex - beginIndex - beginMagicLength);

            if (Utility.ExtractDataFromComments(
                    ref value, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: The "<?xml?>" tag must be at the very start of the
            //       document; therefore, remove any whitespace.
            //
            value = value.Trim();

            //
            // NOTE: Import the embedded certificate, while ignoring any
            //       pre-existing certificate that may be present in the
            //       variable passed by the caller.  This is not done if
            //       the caller invoked us in "discard only" mode.
            //
            ICertificate localCertificate = null;

            if (!discard)
            {
                Result localResult = null;

                if (Import(
                        fileName, value, validate, ref localCertificate,
                        ref localResult) != ReturnCode.Ok)
                {
                    error = localResult;
                    return ReturnCode.Error;
                }
            }

            //
            // NOTE: Check for the extra "optional" spacing prior to the
            //       embedded certificate.
            //
            string magicSpacing = Constants.MagicSpacing;
            string localText = text.Substring(0, beginIndex);

            if ((magicSpacing != null) && localText.EndsWith(
                    magicSpacing, Utility.GetSystemComparisonType(false)))
            {
                localText = localText.Substring(0,
                    localText.Length - magicSpacing.Length);
            }

            //
            // NOTE: Finally, commit changes to the variables provided by
            //       the caller.
            //
            text = localText;

            if (!discard)
                certificate = localCertificate;

            return ReturnCode.Ok;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Renewal & Encryption Support
        /// <summary>
        /// Imports a certificate from the specified byte array.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the bytes, for diagnostic
        /// purposes.
        /// </param>
        /// <param name="bytes">
        /// The bytes containing the certificate to import.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Import( /* CORE */
            string fileName,              /* in: NOT USED */
            byte[] bytes,                 /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (bytes == null)
            {
                result = "invalid byte array";
                return ReturnCode.Error;
            }

            if (certificate != null)
            {
                result = "cannot overwrite valid certificate";
                return ReturnCode.Error;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                {
                    return Import(
                        stream, validate, ref certificate, ref result);
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Renewal & Commands Support
        /// <summary>
        /// Imports a certificate from the specified stream.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the certificate XML to import.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the imported certificate.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Import(
            Stream stream,                /* in */
            bool validate,                /* in */
            ref ICertificate certificate, /* out */
            ref Result result             /* out */
            )
        {
            if (stream == null)
            {
                result = "invalid stream";
                return ReturnCode.Error;
            }

            if (certificate != null)
            {
                result = "cannot overwrite valid certificate";
                return ReturnCode.Error;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(stream); /* throw */

                ICertificateXml certificateXml = null;

                if (DeserializeAndFixup(
                        document, validate, ref certificateXml,
                        ref result) == ReturnCode.Ok) /* throw */
                {
                    certificate = certificateXml as ICertificate;
                    result = OperationStatus.ImportedOk;

                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Commands Support
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Creates a property element with the specified name and value and
        /// appends it to the specified document element.
        /// </summary>
        /// <param name="document">
        /// The XML document used to create the new element.
        /// </param>
        /// <param name="documentElement">
        /// The XML document element to append the new element to.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property element to create.
        /// </param>
        /// <param name="propertyValue">
        /// The value to assign to the new property element.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero on success.
        /// </returns>
        private static bool TryAddPropertyNode(
            XmlDocument document,       /* in, out */
            XmlElement documentElement, /* in, out */
            string propertyName,        /* in */
            string propertyValue,       /* in */
            ref Result error            /* out */
            )
        {
            if (document == null)
            {
                error = "invalid xml document";
                return false;
            }

            if (documentElement == null)
            {
                error = "invalid xml document element";
                return false;
            }

            if (String.IsNullOrEmpty(propertyName))
            {
                error = "invalid property name";
                return false;
            }

            XmlNode node = document.CreateElement(
                propertyName, GetNamespaceUriString());

            if (node == null)
            {
                error = "could not create xml element";
                return false;
            }

            node.InnerText = propertyValue;
            documentElement.AppendChild(node);

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the "Quantity" property element from the document when the
        /// certificate quantity is zero.
        /// </summary>
        /// <param name="certificateXml">
        /// The certificate whose quantity is examined.
        /// </param>
        /// <param name="document">
        /// The XML document being modified.
        /// </param>
        /// <param name="documentElement">
        /// The XML document element to remove the property from.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero on success.
        /// </returns>
        private static bool MaybeRemoveZeroQuantity(
            ICertificateXml certificateXml, /* in */
            XmlDocument document,           /* in, out */
            XmlElement documentElement,     /* in, out */
            ref Result error                /* out */
            )
        {
            if (certificateXml == null)
            {
                error = "invalid certificate xml";
                return false;
            }

            //
            // HACK: *SPECIAL* Finally, remove any useless elements.  As of
            //       this writing (December 2020), this cast cannot actually
            //       fail; however, in the future, that may not be the case.
            //
            ICertificate certificate = certificateXml as ICertificate;

            if (certificate == null)
            {
                error = "invalid certificate";
                return false;
            }

            if (document == null)
            {
                error = "invalid xml document";
                return false;
            }

            if (documentElement == null)
            {
                error = "invalid xml document element";
                return false;
            }

            XmlNamespaceManager namespaceManager = GetNamespaceManager(
                document);

            if (namespaceManager == null)
            {
                error = "invalid xml namespace manager";
                return false;
            }

            if (certificate.Quantity == 0)
            {
                XmlNode node = null;

                if (!TryGetPropertyNode(
                        documentElement, namespaceManager, "Quantity",
                        ref node, ref error))
                {
                    return false;
                }

                if (node != null) /* OPTIONAL */
                    documentElement.RemoveChild(node);
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the certificate properties that the XML serializer cannot
        /// handle into the specified XML document.
        /// </summary>
        /// <param name="certificateXml">
        /// The certificate whose properties are written.
        /// </param>
        /// <param name="document">
        /// The XML document being populated.
        /// </param>
        /// <param name="settings">
        /// The XML writer settings used to format the property values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode ToDocument(
            ICertificateXml certificateXml, /* in */
            XmlDocument document,           /* in, out */
            XmlWriterSettings settings,     /* in */
            ref Result error                /* out */
            )
        {
            if (certificateXml == null)
            {
                error = "invalid certificate xml";
                return ReturnCode.Error;
            }

            if (document == null)
            {
                error = "invalid xml document";
                return ReturnCode.Error;
            }

            if (settings == null)
            {
                error = "invalid xml writer settings";
                return ReturnCode.Error;
            }

            XmlElement documentElement = document.DocumentElement;

            if (documentElement == null)
            {
                error = "invalid xml document element";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            Version protocolVersion;
            Uri origin;
            Uri authority;
            Uri agreement;
            Uri support;
            DateTime timeStamp;
            TimeSpan duration;
            byte[] key;
            ulong number;
            byte[] signature;
            Version version;

            certificateXml.Unpack(
                out protocolVersion, out origin, out authority,
                out agreement, out support, out timeStamp,
                out duration, out key, out number, out signature,
                out version);

            ///////////////////////////////////////////////////////////////////

            if (protocolVersion != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "ProtocolVersion",
                        protocolVersion.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (origin != null) 
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Origin",
                        origin.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (authority != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Authority",
                        authority.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (agreement != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Agreement",
                        agreement.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (support != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Support",
                        support.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (timeStamp != DateTime.MinValue)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "TimeStamp",
                        DataOps.FormatTimeStamp(timeStamp),
                        ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (duration != TimeSpan.Zero)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Duration",
                        duration.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (key != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Key",
                        DataOps.FormatPublicKeyToken(key, false, true),
                        ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (number != 0)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Number",
                        DataOps.FormatHexadecimal(number),
                        ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (signature != null)
            {
                //
                // NOTE: Create properly formatted signature text.
                //
                string text = Environment.NewLine +
                    Convert.ToBase64String(signature,
                    Base64FormattingOptions.InsertLineBreaks);

                text = text.Replace(
                    Environment.NewLine, Environment.NewLine +
                    settings.IndentChars + settings.IndentChars) +
                    Environment.NewLine + settings.IndentChars;

                if (!TryAddPropertyNode(
                        document, documentElement, "Signature",
                        text, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (version != null)
            {
                if (!TryAddPropertyNode(
                        document, documentElement, "Version",
                        version.ToString(), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!MaybeRemoveZeroQuantity(
                    certificateXml, document, documentElement,
                    ref error))
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Exports the specified certificate as XML to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// The stream to write the certificate XML to.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to write the certificate XML.
        /// </param>
        /// <param name="certificate">
        /// The certificate to export.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema
        /// before writing it.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Export(
            Stream stream,            /* in */
            Encoding encoding,        /* in */
            ICertificate certificate, /* in */
            bool validate,            /* in */
            ref Result result         /* out */
            )
        {
            if (stream == null)
            {
                result = "invalid stream";
                return ReturnCode.Error;
            }

            if (encoding == null)
            {
                result = "invalid encoding";
                return ReturnCode.Error;
            }

            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            try
            {
                using (MemoryStream stream2 = new MemoryStream())
                {
                    using (XmlTextWriter writer = new XmlTextWriter(
                            stream2, encoding))
                    {
                        if (Utility.Serialize(
                                certificate, typeof(Certificate), writer,
                                null, ref result) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        //
                        // NOTE: First, make sure all pending changes are
                        //       flushed.
                        //
                        writer.Flush();

                        //
                        // NOTE: Declare document for the new certificate.
                        //
                        XmlDocument document;

                        //
                        // NOTE: Get a copy of the stream we are using.
                        //
                        using (MemoryStream stream3 = new MemoryStream(
                                stream2.ToArray(), false))
                        {
                            //
                            // NOTE: Close the existing writer.
                            //
                            writer.Close();

                            //
                            // NOTE: Load the newly created stream into a
                            //       DOM.
                            //
                            document = new XmlDocument();
                            document.Load(stream3);
                        }

                        //
                        // NOTE: Create an XML settings object to enable
                        //       us to customize the formatting.
                        //
                        XmlWriterSettings settings = new XmlWriterSettings();

                        //
                        // NOTE: We need to use the specified encoding and we
                        //       want to make sure the file is human-readable.
                        //
                        settings.Encoding = encoding;
                        settings.Indent = true;

                        //
                        // NOTE: Handle the remaining certificate fields
                        //       directly by manually formatting and writing
                        //       them into the XML document.
                        //
                        if (ToDocument(
                                certificate, document, settings,
                                ref result) == ReturnCode.Ok)
                        {
                            if (!validate || (ValidateDocument(
                                    document, ref result) == ReturnCode.Ok))
                            {
                                //
                                // NOTE: Finally, save the file from the
                                //       [modified] DOM.
                                //
                                using (XmlWriter writer2 = XmlWriter.Create(
                                        stream, settings))
                                {
                                    document.WriteTo(writer2);
                                }

                                result = OperationStatus.ExportedOk;
                                return ReturnCode.Ok;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Exports the specified certificate as XML to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to write the certificate XML to.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to write the certificate XML.
        /// </param>
        /// <param name="certificate">
        /// The certificate to export.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema
        /// before writing it.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode Export(
            string fileName,          /* in */
            Encoding encoding,        /* in */
            ICertificate certificate, /* in */
            bool validate,            /* in */
            ref Result result         /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (File.Exists(fileName))
            {
                result = "cannot overwrite existing file";
                return ReturnCode.Error;
            }

            if (encoding == null)
            {
                result = "invalid encoding";
                return ReturnCode.Error;
            }

            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            try
            {
                using (Stream stream = new FileStream(
                        fileName, FileMode.CreateNew, FileAccess.Write))
                {
                    return Export(
                        stream, encoding, certificate, validate,
                        ref result);
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Inserts a warning comment, built from the specified warning
        /// template stream, at the top of the certificate XML file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the certificate XML file to modify.
        /// </param>
        /// <param name="stream">
        /// The stream containing the warning XML template.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to write the modified certificate XML file.
        /// </param>
        /// <param name="warningType">
        /// The warning type substituted into the warning template.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm included in the warning, if any.
        /// </param>
        /// <param name="hashValue">
        /// The hash value included in the warning, if any.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the document against the certificate schema
        /// before writing it.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status message; upon failure, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode AddWarning(
            string fileName,          /* in */
            Stream stream,            /* in */
            Encoding encoding,        /* in */
            string warningType,       /* in */
            string hashAlgorithmName, /* in */
            byte[] hashValue,         /* in */
            bool validate,            /* in */
            ref Result result         /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (!File.Exists(fileName))
            {
                result = String.Format(
                    "could not read {0}: no such file",
                    Utility.FormatWrapOrNull(fileName));

                return ReturnCode.Error;
            }

            if (stream == null)
            {
                result = "invalid stream";
                return ReturnCode.Error;
            }

            if (encoding == null)
            {
                result = "invalid encoding";
                return ReturnCode.Error;
            }

            if (warningType == null)
            {
                result = "invalid warning type";
                return ReturnCode.Error;
            }

            try
            {
                string warningXml;

                using (StreamReader reader = new StreamReader(stream))
                {
                    warningXml = reader.ReadToEnd();
                }

                if (String.IsNullOrEmpty(warningXml))
                {
                    result = "invalid warning xml";
                    return ReturnCode.Error;
                }

                string warningSuffix;

                if ((hashAlgorithmName != null) && (hashValue != null))
                {
                    warningSuffix = String.Format(
                        Constants.WarningSuffixFormat, Environment.NewLine,
                        hashAlgorithmName, DataOps.FormatHexadecimal(
                        hashValue, false));
                }
                else
                {
                    warningSuffix = null;
                }

                warningXml = String.Format(
                    warningXml, warningType, warningSuffix);

                XmlDocument document; /* REUSED */
                XmlNode comment; /* REUSED */

                //
                // NOTE: Phase 1, get the comment node from the XML
                //       warning file.
                //
                document = new XmlDocument();
                document.LoadXml(warningXml);

                comment = document.DocumentElement.SelectSingleNode(
                    "comment()");

                document = null;

                //
                // NOTE: Phase 2, load the certificate XML file,
                //       insert the comment at the top of the file,
                //       and re-save the certificate XML file.
                //
                document = new XmlDocument();
                document.Load(fileName);

                comment = document.CreateComment(comment.InnerText);

                document.InsertBefore(comment, document.DocumentElement);

                if (!validate || (ValidateDocument(
                        document, ref result) == ReturnCode.Ok))
                {
                    //
                    // NOTE: Create an XML settings object to enable
                    //       us to customize the formatting.
                    //
                    XmlWriterSettings settings = new XmlWriterSettings();

                    //
                    // NOTE: We need to use the specified encoding and we
                    //       want to make sure the file is human-readable.
                    //
                    settings.Encoding = encoding;
                    settings.Indent = true;

                    //
                    // NOTE: Finally, save the file from the [modified]
                    //       DOM.
                    //
                    using (XmlWriter writer = XmlWriter.Create(fileName,
                            settings))
                    {
                        document.WriteTo(writer);
                    }

                    //
                    // NOTE: Everything was a success.
                    //
                    result = OperationStatus.WarningOk;
                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion
    }
}
