/*
 * Certificate.cs --
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
using System.Text;

#if XML && SERIALIZATION
using System.Xml.Serialization;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using _Utility = Eagle._Components.Public.Utility;
using DataOps = Licensing.Components.Private.CertificateDataOps;

using CertificateDictionary = System.Collections.Generic.IDictionary<
    string, string>;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Represents a digitally signed licensing certificate, including its
    /// identifying metadata, the licensed entity, and the cryptographic
    /// signature used to verify it.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
#if XML && SERIALIZATION
    [XmlRoot(Namespace = "https://eagle.to/2011/harpy")]
#endif
    [ObjectId("49de0102-bee5-4b86-9451-92b60d2521a8")]
    public sealed class Certificate :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IIdentifier,
        ICertificate,
        ICloneable
    {
        #region Private Identifier Data
        /// <summary>
        /// The client data associated with this certificate, if any.
        /// </summary>
        private IClientData clientData;  /* Client data associated with this
                                          * certificate, if any. */
        /// <summary>
        /// The identifier kind for this certificate; this should always have
        /// the value <see cref="IdentifierKind.Certificate" />.
        /// </summary>
        private IdentifierKind kind;     /* Should always have the value
                                          * "IdentifierKind.Certificate". */
        /// <summary>
        /// The unique identifier for this certificate.
        /// </summary>
        private Guid id;                 /* Unique Id for the certificate. */
        /// <summary>
        /// The optional short description for this certificate, if any.
        /// </summary>
        private string name;             /* Optional short description for
                                          * this certificate, if any. */
        /// <summary>
        /// The optional logical group name for this certificate, if any.
        /// </summary>
        private string group;            /* Optional logical group name for
                                          * this certificate, if any. */
        /// <summary>
        /// The optional long description for this certificate, if any.
        /// </summary>
        private string description;      /* Optional long description for
                                          * this certificate, if any. */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Certificate Data
        /// <summary>
        /// The license compliance checking protocol used by this certificate.
        /// </summary>
        private ProtocolType protocol;   /* What is the license compliance
                                          * checking protocol? */
        /// <summary>
        /// The version of the license compliance checking protocol used by
        /// this certificate.
        /// </summary>
        private Version protocolVersion; /* What version of the protocol is
                                          * this? */
        /// <summary>
        /// The vendor that created this certificate.
        /// </summary>
        private string vendor;           /* Vendor that created this
                                          * certificate. */
        /// <summary>
        /// The URI for the origin of this certificate.
        /// </summary>
        private Uri origin;              /* What is the URI for the origin of
                                          * this certificate? */
        /// <summary>
        /// The URI for the certificate authority.
        /// </summary>
        private Uri authority;           /* What is the URI for the certificate
                                          * authority? */
        /// <summary>
        /// The URI (or content) of the license agreement.
        /// </summary>
        private Uri agreement;           /* What is the URI (or content) of the
                                          * license agreement? */
        /// <summary>
        /// The URI (or content) of the support contract.
        /// </summary>
        private Uri support;             /* What is the URI (or content) of the
                                          * support contract? */
        /// <summary>
        /// The date and time when this certificate was created.
        /// </summary>
        private DateTime timeStamp;      /* When was this certificate created?
                                          */
        /// <summary>
        /// How long this certificate is valid for; a value less than zero
        /// means it is valid forever.
        /// </summary>
        private TimeSpan duration;       /* How long is it good for (less than
                                          * 0 means FOREVER)? */
        /// <summary>
        /// The public key token used to sign this certificate.
        /// </summary>
        private byte[] key;              /* Public key token used to sign this
                                          * certificate.  This is basically a
                                          * byte array that is always 8 bytes
                                          * long.  However, we really want to
                                          * view it as a hex number in the
                                          * XML file; therefore, we use a ulong
                                          * here. */
        /// <summary>
        /// The certificate number (a sequence number that is unique within
        /// the vendor).
        /// </summary>
        private ulong number;            /* Certificate number (seq #, unique
                                          * within vendor). */
        /// <summary>
        /// The certificate serial number, which may be alphanumeric.
        /// </summary>
        private string serialNumber;     /* Certificate serial number (can be
                                          * alphanumeric). */
        /// <summary>
        /// The hash algorithm used when signing the certificate data.
        /// </summary>
        private string hashAlgorithm;    /* EXEMPT: Hash algorithm used when
                                          * signing the certificate data. */
        /// <summary>
        /// The RSA signature for all of the other certificate data.
        /// </summary>
        private byte[] signature;        /* RSA signature for ALL the other
                                          * certificate data (base64). */
        /// <summary>
        /// The type of this certificate.
        /// </summary>
        private string type;             /* What type of certificate? */
        /// <summary>
        /// The type of entity associated with this certificate.
        /// </summary>
        private EntityType entityType;   /* What type of entity? */
        /// <summary>
        /// The name of the entity for this certificate.
        /// </summary>
        private string entityName;       /* Name of the entity for this
                                          * certificate. */
        /// <summary>
        /// The value of the entity for this certificate, if any.
        /// </summary>
        private string entityValue;      /* Value of the entity, if any. */
        /// <summary>
        /// The arbitrary extra data associated with this certificate, if any.
        /// </summary>
        private string extraData;        /* Arbitrary extra data associated
                                          * with this certificate, if any. */
        /// <summary>
        /// How many entities are allowed; a value of -1 means not applicable.
        /// </summary>
        private long quantity;           /* How many "entities" are allowed,
                                          * -1 means N/A. */
        /// <summary>
        /// The product being used or licensed; a value of "All" means any
        /// product.
        /// </summary>
        private string product;          /* What product is being used/licensed
                                          * ("All" means any product)? */
        /// <summary>
        /// The version being used or licensed; a null value means any
        /// version.
        /// </summary>
        private Version version;         /* What version is being used/licensed
                                          * (null means any version)? */
        /// <summary>
        /// Any extra feature flags for this certificate.
        /// </summary>
        private string features;         /* Any extra feature flags for this
                                          * certificate... */
        /// <summary>
        /// Any special restrictions for this certificate, if any.
        /// </summary>
        private string restrictions;     /* Are there any special restrictions
                                          * for this certificate? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extra informational notes for this certificate, if any; this data
        /// is not signed.
        /// </summary>
        private string notes;            /* NOT SIGNED: Extra informational
                                          * notes, if any. */
        /// <summary>
        /// The renewal server information for this certificate; this data is
        /// not signed.
        /// </summary>
        private string serverInfo;       /* NOT SIGNED: Renewal server. */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Certificate" /> class.
        /// </summary>
        public Certificate()
        {
            kind = IdentifierKind.Certificate;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Members
#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Creates a new certificate using the name/value pairs contained in
        /// the specified dictionary.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary containing the certificate field values to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The newly created certificate, or null if it could not be created.
        /// </returns>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        internal static ICertificate CreateFromDictionary(
            CertificateDictionary dictionary, /* in */
            ref Result error                  /* out */
            )
        {
            if (dictionary == null)
            {
                error = "invalid dictionary";
                return null;
            }

            string value; /* REUSED */
            object enumValue; /* REUSED */
            Guid id = Guid.Empty;
            ProtocolType protocol = ProtocolType.None;
            Version protocolVersion = null;
            string vendor = null;
            Uri origin = null;
            Uri authority = null;
            Uri agreement = null;
            Uri support = null;
            DateTime timeStamp = DateTime.MinValue;
            TimeSpan duration = TimeSpan.Zero;
            byte[] key = null;
            ulong number = 0;
            string serialNumber = null;
            string hashAlgorithm = null;
            byte[] signature = null;
            string type = null;
            EntityType entityType = EntityType.None;
            string entityName = null;
            string entityValue = null;
            string extraData = null;
            long quantity = 0;
            string product = null;
            Version version = null;
            string features = null;
            string restrictions = null;
            string notes = null;
            string serverInfo = null;

            if (dictionary.TryGetValue("Id", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseId(
                        value, ref id, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Protocol", out value) &&
                (value != null))
            {
                enumValue = _Utility.TryParseEnum(
                    typeof(ProtocolType), value, true, true,
                    ref error);

                if (enumValue is ProtocolType)
                    protocol = (ProtocolType)enumValue;
                else
                    return null;
            }

            if (dictionary.TryGetValue("ProtocolVersion", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseVersion(
                        value, ref protocolVersion, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Vendor", out value) &&
                (value != null))
            {
                vendor = value;
            }

            if (dictionary.TryGetValue("Origin", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        value, ref origin, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Authority", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        value, ref authority, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Agreement", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        value, ref agreement, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Support", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseAbsoluteUri(
                        value, ref support, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("TimeStamp", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseUniversalTimeStamp(
                        value, ref timeStamp, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Duration", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseDuration(
                        value, ref duration, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Key", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseKey(
                        value, ref key, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Number", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseNumber(
                        value, ref number, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("SerialNumber", out value) &&
                (value != null))
            {
                serialNumber = value;
            }

            if (dictionary.TryGetValue("HashAlgorithm", out value) &&
                (value != null))
            {
                hashAlgorithm = value;
            }

            if (dictionary.TryGetValue("Signature", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseSignature(
                        value, false, ref signature, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Type", out value) &&
                (value != null))
            {
                type = value;
            }

            if (dictionary.TryGetValue("EntityType", out value) &&
                (value != null))
            {
                enumValue = _Utility.TryParseEnum(
                    typeof(EntityType), value, true, true,
                    ref error);

                if (enumValue is EntityType)
                    entityType = (EntityType)enumValue;
                else
                    return null;
            }

            if (dictionary.TryGetValue("EntityName", out value) &&
                (value != null))
            {
                entityName = value;
            }

            if (dictionary.TryGetValue("EntityValue", out value) &&
                (value != null))
            {
                entityValue = value;
            }

            if (dictionary.TryGetValue("ExtraData", out value) &&
                (value != null))
            {
                extraData = value;
            }

            if (dictionary.TryGetValue("Quantity", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseQuantity(
                        value, ref quantity, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Product", out value) &&
                (value != null))
            {
                product = value;
            }

            if (dictionary.TryGetValue("Version", out value) &&
                (value != null))
            {
                if (!DataOps.TryParseVersion(
                        value, ref version, ref error))
                {
                    return null;
                }
            }

            if (dictionary.TryGetValue("Features", out value) &&
                (value != null))
            {
                features = value;
            }

            if (dictionary.TryGetValue("Restrictions", out value) &&
                (value != null))
            {
                restrictions = value;
            }

            if (dictionary.TryGetValue("Notes", out value) &&
                (value != null))
            {
                notes = value;
            }

            if (dictionary.TryGetValue("ServerInfo", out value) &&
                (value != null))
            {
                serverInfo = value;
            }

            ICertificate certificate = new Certificate();

            certificate.Pack(
                id, protocol, protocolVersion, vendor, origin,
                authority, agreement, support, timeStamp, duration,
                key, number, serialNumber, hashAlgorithm, signature,
                type, entityType, entityName, entityValue, extraData,
                quantity, product, version, features, restrictions,
                notes, serverInfo);

            return certificate;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION && CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        //
        // WARNING: For use by the "verify.eagle" script use only.
        //
        /// <summary>
        /// Creates a new certificate by importing it from the specified XML
        /// data.  For use by the "verify.eagle" script only.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file associated with the XML data, if any.  This
        /// parameter is not used.
        /// </param>
        /// <param name="xml">
        /// The XML data to import the certificate from.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the XML data against the schema during the
        /// import operation.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The newly created certificate, or null if it could not be created.
        /// </returns>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        internal static ICertificate CreateFromXml(
            string fileName, /* in: NOT USED */
            string xml,      /* in */
            bool validate,   /* in */
            ref Result error /* out */
            )
        {
            ICertificate certificate = null;
            Result localResult = null;

            if (CertificateXmlOps.Import(
                    fileName, xml, validate, ref certificate,
                    ref localResult) == ReturnCode.Ok)
            {
                return certificate;
            }
            else
            {
                error = localResult;
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Creates a new certificate based on the metadata and signature
        /// contained within the specified script.
        /// </summary>
        /// <param name="script">
        /// The script to create the certificate from.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The newly created certificate, or null if it could not be created.
        /// </returns>
        internal static ICertificate CreateFromScript(
            IScript script,  /* in */
            ref Result error /* out */
            )
        {
            if (script == null)
            {
                error = "invalid script";
                return null;
            }

#if XML
            byte[] key = null;

            if (!DataOps.TryParseKey(
                    script.PublicKeyToken, ref key, ref error))
            {
                return null;
            }

            //
            // NOTE: If the signature is not present, the
            //       resulting certificate has zero chance
            //       of being verified; therefore, we just
            //       return null in that case (i.e. which
            //       will give an embedded certificate a
            //       chance, if present).
            //
            byte[] signature = script.Signature;

            if (signature != null)
            {
                int length = signature.Length;

                if (length == 0)
                {
                    error = "empty script signature";
                    return null;
                }
            }
            else
            {
                error = "invalid script signature";
                return null;
            }
#endif

            ICertificate certificate = new Certificate();

            certificate.Id = script.Id;

#if XML
            certificate.TimeStamp = script.TimeStamp;
#endif

            //
            // HACK: The IScript XML format does not have a
            //       duration field; therefore, all of these
            //       objects are valid forever.  Without this,
            //       all of these certificates would have a
            //       duration value of zero and hence expire
            //       immediately.
            //
            certificate.Duration = Constants.ForeverDuration;

#if XML
            certificate.Key = key;
            certificate.Signature = signature;
#endif

            ///////////////////////////////////////////////////////////////////

            IBundleData bundleData = script.BundleData;
            ScriptSecurityFlags scriptSecurityFlags;

            if (bundleData != null)
            {
                scriptSecurityFlags = bundleData.SecurityFlags;

                if (!_Utility.HasFlags(scriptSecurityFlags,
                        ScriptSecurityFlags.NoVendor, true))
                {
                    certificate.Vendor = bundleData.Vendor;
                }

                if (!_Utility.HasFlags(scriptSecurityFlags,
                        ScriptSecurityFlags.NoHashAlgorithm, true))
                {
                    certificate.HashAlgorithm =
                        bundleData.HashAlgorithmName;
                }
            }
            else
            {
                scriptSecurityFlags = ScriptSecurityFlags.Default;
            }

            ///////////////////////////////////////////////////////////////////

            if (!_Utility.HasFlags(scriptSecurityFlags,
                    ScriptSecurityFlags.NoEntityType, true))
            {
                certificate.EntityType = EntityType.Script;
            }

            ///////////////////////////////////////////////////////////////////

            if (!_Utility.HasFlags(scriptSecurityFlags,
                    ScriptSecurityFlags.NoEntityName, true))
            {
                certificate.EntityName = script.Name;
            }

            if (!_Utility.HasFlags(scriptSecurityFlags,
                    ScriptSecurityFlags.NoEntityValue, true))
            {
                certificate.EntityValue = script.Text;
            }

#if XML
            if (!_Utility.HasFlags(scriptSecurityFlags,
                    ScriptSecurityFlags.NoBlockType, true))
            {
                certificate.ExtraData = script.GetBlockTypeString();
            }
#endif

            return certificate;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts an embedded certificate from the specified script,
        /// replacing the script with one that no longer contains the embedded
        /// certificate data.
        /// </summary>
        /// <param name="validate">
        /// Non-zero to validate the extracted certificate XML data against
        /// the schema.
        /// </param>
        /// <param name="script">
        /// On input, the script to extract the certificate from; on output,
        /// the script with the embedded certificate data removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The extracted certificate, or null if it could not be extracted.
        /// </returns>
        internal static ICertificate ExtractFromScript(
            bool validate,      /* in */
            ref IScript script, /* in, out */
            ref Result error    /* out */
            )
        {
            if (script == null)
            {
                error = "invalid script";
                return null;
            }

#if XML && SERIALIZATION
            string text = script.Text;
            ICertificate certificate = null;

            if (CertificateXmlOps.Extract(
                    script.FileName, validate, ref text,
                    ref certificate, ref error) == ReturnCode.Ok)
            {
                certificate.EntityValue = text;

                IScript localScript = Script.Create(
                    script, ref error); /* throw */

                if (localScript != null)
                {
                    script = localScript;
                    return certificate;
                }
            }

            return null;
#else
            error = "not implemented";
            return null;
#endif
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Prepares the specified certificate to be signed using the
        /// specified key pair, optionally resetting its unique identifier,
        /// time stamp, and public key token.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to be used when signing the
        /// certificate, if any.
        /// </param>
        /// <param name="certificate">
        /// The certificate to prepare for signing.
        /// </param>
        /// <param name="keyPair">
        /// The key pair that will be used to sign the certificate.
        /// </param>
        /// <param name="setId">
        /// On input, non-zero to assign a new unique identifier to the
        /// certificate; on output, non-zero if this step was performed.
        /// </param>
        /// <param name="setTimeStamp">
        /// On input, non-zero to reset the time stamp of the certificate; on
        /// output, non-zero if this step was performed.
        /// </param>
        /// <param name="setKey">
        /// On input, non-zero to reset the public key token of the
        /// certificate; on output, non-zero if this step was performed.
        /// </param>
        internal static void PrepareToSign(
            string hashAlgorithmName, /* in: OPTIONAL */
            ICertificate certificate, /* in, out */
            IKeyPair keyPair,         /* in */
            ref bool setId,           /* in, out */
            ref bool setTimeStamp,    /* in, out */
            ref bool setKey           /* in, out */
            )
        {
            //
            // NOTE: If the certificate is invalid, be sure to reset flags
            //       provided by the caller.
            //
            if (certificate == null)
            {
                setId = false;
                setTimeStamp = false;
                setKey = false;

                return;
            }

            //
            // NOTE: When using a non-default hash algorithm, make sure it
            //       is set within the certificate itself.
            //
            if (!CommandOps.IsLegacyHashAlgorithm(
                    hashAlgorithmName))
            {
                certificate.HashAlgorithm = hashAlgorithmName;
            }

            //
            // NOTE: Create a new unique Id for this certificate.
            //
            if (setId)
                certificate.Id = DataOps.GetNewId(false);

            //
            // NOTE: Reset the time stamp for the certificate to right now.
            //
            if (setTimeStamp)
                certificate.TimeStamp = DataOps.GetTimeStamp();

            //
            // NOTE: Reset the public key token for this certificate to the
            //       one we are using to sign it.
            //
            if (setKey)
            {
                if (keyPair != null)
                {
                    certificate.Key = keyPair.PublicKeyToken;
                }
                else
                {
                    setKey = false;
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if ISOLATED_PLUGINS && CERTIFICATE_PLUGIN
        /// <summary>
        /// Converts this certificate into a dictionary of name/value pairs.
        /// </summary>
        /// <returns>
        /// The dictionary of name/value pairs representing this certificate.
        /// </returns>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        internal StringDictionary ToDictionary()
        {
            StringDictionary result = new StringDictionary();

            result.Add("Kind", kind.ToString());
            result.Add("Id", DataOps.FormatId(id));
            result.Add("Name", name);
            result.Add("Group", group);
            result.Add("Description", description);

            result.Add("Protocol", protocol.ToString());

            result.Add("ProtocolVersion", (protocolVersion != null) ?
                protocolVersion.ToString() : null);

            result.Add("Vendor", vendor);

            result.Add("Origin", (origin != null) ?
                origin.ToString() : null);

            result.Add("Authority", (authority != null) ?
                authority.ToString() : null);

            result.Add("Agreement", (agreement != null) ?
                agreement.ToString() : null);

            result.Add("Support", (support != null) ?
                support.ToString() : null);

            result.Add("TimeStamp",
                DataOps.FormatTimeStamp(timeStamp));

            result.Add("Duration", duration.ToString());

            result.Add("Key",
                DataOps.FormatPublicKeyToken(key, false, false));

            result.Add("Number",
                DataOps.FormatHexadecimal(number));

            result.Add("SerialNumber", serialNumber);
            result.Add("HashAlgorithm", hashAlgorithm);

            result.Add("Signature", (signature != null) ?
                Convert.ToBase64String(signature,
                    Base64FormattingOptions.InsertLineBreaks) : null);

            result.Add("Type", type);
            result.Add("EntityType", entityType.ToString());
            result.Add("EntityName", entityName);
            result.Add("EntityValue", entityValue);
            result.Add("ExtraData", extraData);
            result.Add("Quantity", quantity.ToString());
            result.Add("Product", product);

            result.Add("Version", (version != null) ?
                version.ToString() : null);

            result.Add("Features", features);
            result.Add("Restrictions", restrictions);
            result.Add("Notes", notes);
            result.Add("ServerInfo", serverInfo);

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified certificate into a dictionary of name/value
        /// pairs.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to convert.
        /// </param>
        /// <returns>
        /// The dictionary of name/value pairs representing the certificate,
        /// or null if it could not be converted.
        /// </returns>
        internal static StringDictionary ToDictionary(
            ICertificate certificate /* in */
            )
        {
            if (certificate == null)
                return null;

            Certificate localCertificate =
                certificate as Certificate; /* EXEMPT */

            if (localCertificate == null)
                return null;

            return localCertificate.ToDictionary();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified certificate into a string containing its
        /// name/value pairs.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to convert.
        /// </param>
        /// <returns>
        /// The string representation of the certificate name/value pairs, or
        /// null if it could not be converted.
        /// </returns>
        internal static string ToDictionaryString(
            ICertificate certificate /* in */
            )
        {
            if (certificate == null)
                return null;

            Certificate localCertificate =
                certificate as Certificate; /* EXEMPT */

            if (localCertificate == null)
                return null;

            StringDictionary dictionary = localCertificate.ToDictionary();

            if (dictionary == null)
                return null;

            return dictionary.KeysAndValuesToString(null, false);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //    Changing this method WILL break ALL existing certificates.     //
        //  Do not change this method unless you know exactly what it does.  //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Adds the selected data from the specified certificate to the
        /// specified list of bytes, which is typically used to compute a hash
        /// of the certificate.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose data should be added.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert string-based certificate fields into
        /// bytes.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The flags that control which certificate fields are included.
        /// </param>
        /// <param name="list">
        /// The list of bytes to add the selected certificate data to; it is
        /// created if necessary.
        /// </param>
        internal static void AddToHash( /* CORE */
            ICertificate certificate,                  /* in: OPTIONAL */
            Encoding encoding,                         /* in: OPTIONAL */
            CertificateHashFlags certificateHashFlags, /* in */
            ref ByteList list                          /* in, out */
            )
        {
            if (certificate == null)
                return;

            if (CertificateSharedOps.HasFlags(certificateHashFlags,
                    CertificateHashFlags.Full, false))
            {
                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Protocol, true))
                {
                    ProtocolType protocol = certificate.Protocol;

                    if (protocol != ProtocolType.None)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(
                            BitConverter.GetBytes((int)protocol));
                    }
                }

                if (encoding != null)
                {
                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.ProtocolVersion, true))
                    {
                        Version protocolVersion = certificate.ProtocolVersion;

                        if (protocolVersion != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                protocolVersion.ToString()));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Vendor, true))
                    {
                        string vendor = certificate.Vendor;

                        if (!String.IsNullOrEmpty(vendor))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                vendor));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Origin, true))
                    {
                        Uri origin = certificate.Origin;

                        if (origin != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                origin.ToString()));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Authority, true))
                    {
                        Uri authority = certificate.Authority;

                        if (authority != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                authority.ToString()));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Agreement, true))
                    {
                        Uri agreement = certificate.Agreement;

                        if (agreement != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                agreement.ToString()));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Support, true))
                    {
                        Uri support = certificate.Support;

                        if (support != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                support.ToString()));
                        }
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Id, true))
                {
                    //
                    // NOTE: This is from the "IScript.Id" property.
                    //
                    Guid id = certificate.Id;

                    if (!id.Equals(Guid.Empty))
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(id.ToByteArray());
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.TimeStamp, true))
                {
                    //
                    // NOTE: This is from the "IScript.TimeStamp" property.
                    //
                    DateTime timeStamp = certificate.TimeStamp;

                    if (timeStamp != DateTime.MinValue)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(BitConverter.GetBytes(
                            timeStamp.Ticks));
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Duration, true))
                {
                    //
                    // NOTE: For an "IScript", this is typically set to
                    //       the value of "Constants.DurationForever".
                    //
                    TimeSpan duration = certificate.Duration;

                    if (duration != TimeSpan.Zero)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(BitConverter.GetBytes(
                            duration.Ticks));
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Key, true))
                {
                    //
                    // NOTE: This is from the "IScript.PublicKeyToken"
                    //       property.
                    //
                    byte[] key = certificate.Key;

                    if (key != null)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(key);
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Number, true))
                {
                    ulong number = certificate.Number;

                    if (number != 0)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(BitConverter.GetBytes(
                            number));
                    }
                }

                if (encoding != null)
                {
                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.SerialNumber, true))
                    {
                        string serialNumber = certificate.SerialNumber;

                        if (!String.IsNullOrEmpty(serialNumber))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                serialNumber));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.HashAlgorithm, true))
                    {
                        //
                        // BUGBUG: When this property is set, this assumes
                        //         that the caller specified the same hash
                        //         algorithm to perform the actual hashing
                        //         (below).
                        //
                        string hashAlgorithm = certificate.HashAlgorithm;

                        if (!String.IsNullOrEmpty(hashAlgorithm))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                hashAlgorithm));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Type, true))
                    {
                        string type = certificate.Type;

                        if (!String.IsNullOrEmpty(type))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                type));
                        }
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.EntityType, true))
                {
                    EntityType entityType = certificate.EntityType;

                    if (entityType != EntityType.None)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(BitConverter.GetBytes(
                            (int)entityType));
                    }
                }

                if (encoding != null)
                {
                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.EntityName, true))
                    {
                        string entityName = certificate.EntityName;

                        if (!String.IsNullOrEmpty(entityName))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(entityName));
                        }
                    }

                    //
                    // NOTE: When the ScriptCertificatePolicy needs to
                    //       verify an embedded script certificate, this
                    //       flag must be set in order to consider the
                    //       EntityValue property (i.e. which contains
                    //       the original script text) as well.
                    //
                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.EntityValue, true))
                    {
                        string entityValue = certificate.EntityValue;

                        if (!String.IsNullOrEmpty(entityValue))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                entityValue));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.ExtraData, true))
                    {
                        string extraData = certificate.ExtraData;

                        if (!String.IsNullOrEmpty(extraData))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                extraData));
                        }
                    }
                }

                if (CertificateSharedOps.HasFlags(certificateHashFlags,
                        CertificateHashFlags.Quantity, true))
                {
                    long quantity = certificate.Quantity;

                    if (quantity != 0 /* NO QUANTITY */)
                    {
                        if (list == null)
                            list = new ByteList();

                        list.AddRange(BitConverter.GetBytes(
                            quantity));
                    }
                }

                if (encoding != null)
                {
                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Product, true))
                    {
                        string product = certificate.Product;

                        if (!String.IsNullOrEmpty(product))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                product));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Version, true))
                    {
                        Version version = certificate.Version;

                        if (version != null)
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                version.ToString()));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Features, true))
                    {
                        string features = certificate.Features;

                        if (!String.IsNullOrEmpty(features))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                features));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Restrictions, true))
                    {
                        string restrictions = certificate.Restrictions;

                        if (!String.IsNullOrEmpty(restrictions))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                restrictions));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.Notes, true))
                    {
                        string notes = certificate.Notes;

                        if (!String.IsNullOrEmpty(notes))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                notes));
                        }
                    }

                    if (CertificateSharedOps.HasFlags(certificateHashFlags,
                            CertificateHashFlags.ServerInfo, true))
                    {
                        string serverInfo = certificate.ServerInfo;

                        if (!String.IsNullOrEmpty(serverInfo))
                        {
                            if (list == null)
                                list = new ByteList();

                            list.AddRange(encoding.GetBytes(
                                serverInfo));
                        }
                    }
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// Determines whether the specified certificate is allowed to be
        /// embedded.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to check.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate is allowed to be embedded.
        /// </returns>
        internal static bool CanBeEmbedded(
            ICertificate certificate /* in */
            )
        {
            bool embedded = false;

            return CanBeEmbedded(certificate, ref embedded);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate is allowed to be
        /// embedded, also indicating whether it currently has embedded data.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to check.
        /// </param>
        /// <param name="embedded">
        /// Upon return, non-zero if the certificate currently has embedded
        /// entity data.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate is allowed to be embedded.
        /// </returns>
        internal static bool CanBeEmbedded(
            ICertificate certificate, /* in */
            ref bool embedded         /* out */
            )
        {
            return CanBeEmbedded(certificate, null, ref embedded);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate is allowed to be
        /// embedded, also indicating whether it (or the specified entity
        /// value) contains embedded data.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to check.
        /// </param>
        /// <param name="entityValue">
        /// The candidate entity value to consider as embedded data, if any.
        /// </param>
        /// <param name="embedded">
        /// Upon return, non-zero if the certificate (or the specified entity
        /// value) contains embedded entity data.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate is allowed to be embedded.
        /// </returns>
        private static bool CanBeEmbedded(
            ICertificate certificate, /* in */
            string entityValue,       /* in */
            ref bool embedded         /* out */
            )
        {
            if (certificate == null)
                return false;

            if (!CertificateSharedOps.HasFlags(
                    certificate.EntityType,
                    EntityType.EmbeddedDataMask,
                    false))
            {
                return false;
            }

            embedded = ((entityValue != null) ||
                (certificate.EntityValue != null));

            return true;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adjusts the specified certificate hash flags to include the
        /// authority field when the specified certificate has an authority.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to examine.
        /// </param>
        /// <param name="certificateHashFlags">
        /// On input, the certificate hash flags to adjust; on output, the
        /// adjusted certificate hash flags.
        /// </param>
        internal static void MaybeAdjustHashFlagsForAuthority(
            ICertificate certificate,                      /* in */
            ref CertificateHashFlags? certificateHashFlags /* out */
            )
        {
            if ((certificate == null) || (certificateHashFlags == null))
                return;

            CertificateHashFlags localCertificateHashFlags =
                (CertificateHashFlags)certificateHashFlags;

            if (certificate.Authority != null)
                localCertificateHashFlags |= CertificateHashFlags.Authority;

            certificateHashFlags = localCertificateHashFlags;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if XML && NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// Adjusts the specified certificate and certificate hash flags to
        /// account for embedded entity data when applicable.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to examine and possibly modify.
        /// </param>
        /// <param name="entityValue">
        /// The candidate entity value to use as embedded data, if any.
        /// </param>
        /// <param name="certificateHashFlags">
        /// On input, the certificate hash flags to adjust; on output, the
        /// adjusted certificate hash flags.
        /// </param>
        internal static void MaybeAdjustForEmbedded(
            ICertificate certificate,                      /* in */
            string entityValue,                            /* in */
            ref CertificateHashFlags? certificateHashFlags /* out */
            )
        {
            bool embedded = false;

            if (CanBeEmbedded(
                    certificate, entityValue, ref embedded) && embedded)
            {
                if ((certificate != null) &&
                    (certificate.EntityValue == null))
                {
                    certificate.EntityValue = entityValue;
                }

                certificateHashFlags = CertificateHashFlags.Embedded;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a copy of the public key token from the specified
        /// certificate, if it has one.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to copy the public key token from.
        /// </param>
        /// <returns>
        /// A copy of the public key token, or null if there is none.
        /// </returns>
        internal static byte[] MaybeCopyKey(
            ICertificate certificate /* in */
            )
        {
            if (certificate == null)
                return null;

            byte[] key = certificate.Key;

            if (key == null)
                return null;

            byte[] newKey = new byte[key.Length];
            Array.Copy(key, newKey, key.Length);

            return newKey;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Members
        /// <summary>
        /// Gets or sets the name (a short description) for this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierBase Members
        /// <summary>
        /// Gets or sets the identifier kind for this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public IdentifierKind Kind
        {
            get { return kind; }
            set { kind = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetClientData / ISetClientData Members
        /// <summary>
        /// Gets or sets the client data associated with this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public IClientData ClientData
        {
            get { return clientData; }
            set { clientData = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifier Members
        /// <summary>
        /// Gets or sets the optional logical group name for this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public string Group
        {
            get { return group; }
            set { group = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the optional long description for this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public string Description
        {
            get { return description; }
            set { description = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICertificateData Members
        /// <summary>
        /// Gets or sets the license compliance checking protocol used by this
        /// certificate.
        /// </summary>
        public ProtocolType Protocol
        {
            get { return protocol; }
            set { protocol = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the version of the license compliance checking
        /// protocol used by this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Version ProtocolVersion
        {
            get { return protocolVersion; }
            set { protocolVersion = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the vendor that created this certificate.
        /// </summary>
        public string Vendor
        {
            get { return vendor; }
            set { vendor = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the URI for the origin of this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Uri Origin
        {
            get { return origin; }
            set { origin = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the URI for the certificate authority.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Uri Authority
        {
            get { return authority; }
            set { authority = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the URI (or content) of the license agreement.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Uri Agreement
        {
            get { return agreement; }
            set { agreement = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the URI (or content) of the support contract.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Uri Support
        {
            get { return support; }
            set { support = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierBase Members
        //
        // HACK: This does not belong here.  It should be among the
        //       IIdentifierBase members instead; however, that can
        //       cause serialization of this class to put things in
        //       a different order; therefore, it must be here.
        //
        /// <summary>
        /// Gets or sets the unique identifier for this certificate.
        /// </summary>
        public Guid Id
        {
            get { return id; }
            set { id = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the date and time when this certificate was created.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public DateTime TimeStamp
        {
            get { return timeStamp; }
            set { timeStamp = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets how long this certificate is valid for.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public TimeSpan Duration
        {
            get { return duration; }
            set { duration = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the public key token used to sign this certificate.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public byte[] Key
        {
            get { return key; }
            set { key = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate number, which is unique within the
        /// vendor.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public ulong Number
        {
            get { return number; }
            set { number = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate serial number, which may be
        /// alphanumeric.
        /// </summary>
        public string SerialNumber
        {
            get { return serialNumber; }
            set { serialNumber = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the hash algorithm used when signing the certificate
        /// data.
        /// </summary>
        public string HashAlgorithm /* EXEMPT */
        {
            get { return hashAlgorithm; }
            set { hashAlgorithm = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the RSA signature for all of the other certificate
        /// data.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public byte[] Signature
        {
            get { return signature; }
            set { signature = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of this certificate.
        /// </summary>
        public string Type
        {
            get { return type; }
            set { type = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the type of entity associated with this certificate.
        /// </summary>
        public EntityType EntityType
        {
            get { return entityType; }
            set { entityType = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the name of the entity for this certificate.
        /// </summary>
        public string EntityName
        {
            get { return entityName; }
            set { entityName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the value of the entity for this certificate, if any.
        /// </summary>
        public string EntityValue
        {
            get { return entityValue; }
            set { entityValue = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the arbitrary extra data associated with this
        /// certificate, if any.
        /// </summary>
        public string ExtraData
        {
            get { return extraData; }
            set { extraData = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets how many entities are allowed for this certificate.
        /// </summary>
        public long Quantity
        {
            get { return quantity; }
            set { quantity = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the product being used or licensed.
        /// </summary>
        public string Product
        {
            get { return product; }
            set { product = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the version being used or licensed.
        /// </summary>
#if XML && SERIALIZATION
        [XmlIgnore()]
#endif
        public Version Version
        {
            get { return version; }
            set { version = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets any extra feature flags for this certificate.
        /// </summary>
        public string Features
        {
            get { return features; }
            set { features = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets any special restrictions for this certificate.
        /// </summary>
        public string Restrictions
        {
            get { return restrictions; }
            set { restrictions = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the extra informational notes for this certificate,
        /// if any.
        /// </summary>
        public string Notes
        {
            get { return notes; }
            set { notes = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renewal server information for this certificate.
        /// </summary>
        public string ServerInfo
        {
            get { return serverInfo; }
            set { serverInfo = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICertificateXml Members
#if XML
        /// <summary>
        /// Stores the specified subset of certificate field values into this
        /// certificate.
        /// </summary>
        /// <param name="protocolVersion">
        /// The version of the license compliance checking protocol.
        /// </param>
        /// <param name="origin">
        /// The URI for the origin of the certificate.
        /// </param>
        /// <param name="authority">
        /// The URI for the certificate authority.
        /// </param>
        /// <param name="agreement">
        /// The URI (or content) of the license agreement.
        /// </param>
        /// <param name="support">
        /// The URI (or content) of the support contract.
        /// </param>
        /// <param name="timeStamp">
        /// The date and time when the certificate was created.
        /// </param>
        /// <param name="duration">
        /// How long the certificate is valid for.
        /// </param>
        /// <param name="key">
        /// The public key token used to sign the certificate.
        /// </param>
        /// <param name="number">
        /// The certificate number.
        /// </param>
        /// <param name="signature">
        /// The RSA signature for the certificate data.
        /// </param>
        /// <param name="version">
        /// The version being used or licensed.
        /// </param>
        public void Pack( /* CORE */
            Version protocolVersion, /* in */
            Uri origin,              /* in */
            Uri authority,           /* in */
            Uri agreement,           /* in */
            Uri support,             /* in */
            DateTime timeStamp,      /* in */
            TimeSpan duration,       /* in */
            byte[] key,              /* in */
            ulong number,            /* in */
            byte[] signature,        /* in */
            Version version          /* in */
            )
        {
            this.protocolVersion = protocolVersion;
            this.origin = origin;
            this.authority = authority;
            this.agreement = agreement;
            this.support = support;
            this.timeStamp = timeStamp;
            this.duration = duration;
            this.key = key;
            this.number = number;
            this.signature = signature;
            this.version = version;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a subset of the field values from this certificate.
        /// </summary>
        /// <param name="protocolVersion">
        /// Upon return, the version of the license compliance checking
        /// protocol.
        /// </param>
        /// <param name="origin">
        /// Upon return, the URI for the origin of the certificate.
        /// </param>
        /// <param name="authority">
        /// Upon return, the URI for the certificate authority.
        /// </param>
        /// <param name="agreement">
        /// Upon return, the URI (or content) of the license agreement.
        /// </param>
        /// <param name="support">
        /// Upon return, the URI (or content) of the support contract.
        /// </param>
        /// <param name="timeStamp">
        /// Upon return, the date and time when the certificate was created.
        /// </param>
        /// <param name="duration">
        /// Upon return, how long the certificate is valid for.
        /// </param>
        /// <param name="key">
        /// Upon return, the public key token used to sign the certificate.
        /// </param>
        /// <param name="number">
        /// Upon return, the certificate number.
        /// </param>
        /// <param name="signature">
        /// Upon return, the RSA signature for the certificate data.
        /// </param>
        /// <param name="version">
        /// Upon return, the version being used or licensed.
        /// </param>
        public void Unpack( /* CORE */
            out Version protocolVersion, /* out */
            out Uri origin,              /* out */
            out Uri authority,           /* out */
            out Uri agreement,           /* out */
            out Uri support,             /* out */
            out DateTime timeStamp,      /* out */
            out TimeSpan duration,       /* out */
            out byte[] key,              /* out */
            out ulong number,            /* out */
            out byte[] signature,        /* out */
            out Version version          /* out */
            )
        {
            protocolVersion = this.protocolVersion;
            origin = this.origin;
            authority = this.authority;
            agreement = this.agreement;
            support = this.support;
            timeStamp = this.timeStamp;
            duration = this.duration;
            key = this.key;
            number = this.number;
            signature = this.signature;
            version = this.version;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICertificate Members
        /// <summary>
        /// Extracts a sequence of bytes suitable for use as entropy, derived
        /// from the unique identifier, number, and serial number of this
        /// certificate.
        /// </summary>
        /// <param name="salt">
        /// Optional salt bytes to prepend to the extracted entropy.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert string-based fields into bytes; the
        /// default encoding is used when this is null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The extracted entropy bytes, or null if they could not be
        /// extracted.
        /// </returns>
        public byte[] ExtractEntropy( /* CORE */
            byte[] salt,       /* in: OPTIONAL */
            Encoding encoding, /* in: OPTIONAL */
            ref Result error   /* out */
            )
        {
            if (id.Equals(Guid.Empty))
            {
                error = "missing unique identifier";
                return null;
            }

            if (number == 0)
            {
                error = "missing number";
                return null;
            }

            if (String.IsNullOrEmpty(serialNumber))
            {
                error = "missing serial number";
                return null;
            }

            if (encoding == null)
                encoding = Constants.DefaultEncoding;

            if (encoding == null)
            {
                error = "invalid default encoding";
                return null;
            }

            ByteList bytes = new ByteList();

            if (salt != null)
                bytes.AddRange(salt);

            bytes.AddRange(id.ToByteArray());
            bytes.AddRange(BitConverter.GetBytes(number));
            bytes.AddRange(encoding.GetBytes(serialNumber));

            return bytes.ToArray();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified complete set of certificate field values into
        /// this certificate.
        /// </summary>
        /// <param name="id">
        /// The unique identifier for the certificate.
        /// </param>
        /// <param name="protocol">
        /// The license compliance checking protocol.
        /// </param>
        /// <param name="protocolVersion">
        /// The version of the license compliance checking protocol.
        /// </param>
        /// <param name="vendor">
        /// The vendor that created the certificate.
        /// </param>
        /// <param name="origin">
        /// The URI for the origin of the certificate.
        /// </param>
        /// <param name="authority">
        /// The URI for the certificate authority.
        /// </param>
        /// <param name="agreement">
        /// The URI (or content) of the license agreement.
        /// </param>
        /// <param name="support">
        /// The URI (or content) of the support contract.
        /// </param>
        /// <param name="timeStamp">
        /// The date and time when the certificate was created.
        /// </param>
        /// <param name="duration">
        /// How long the certificate is valid for.
        /// </param>
        /// <param name="key">
        /// The public key token used to sign the certificate.
        /// </param>
        /// <param name="number">
        /// The certificate number.
        /// </param>
        /// <param name="serialNumber">
        /// The certificate serial number.
        /// </param>
        /// <param name="hashAlgorithm">
        /// The hash algorithm used when signing the certificate data.
        /// </param>
        /// <param name="signature">
        /// The RSA signature for the certificate data.
        /// </param>
        /// <param name="type">
        /// The type of the certificate.
        /// </param>
        /// <param name="entityType">
        /// The type of entity associated with the certificate.
        /// </param>
        /// <param name="entityName">
        /// The name of the entity for the certificate.
        /// </param>
        /// <param name="entityValue">
        /// The value of the entity for the certificate, if any.
        /// </param>
        /// <param name="extraData">
        /// The arbitrary extra data associated with the certificate, if any.
        /// </param>
        /// <param name="quantity">
        /// How many entities are allowed.
        /// </param>
        /// <param name="product">
        /// The product being used or licensed.
        /// </param>
        /// <param name="version">
        /// The version being used or licensed.
        /// </param>
        /// <param name="features">
        /// Any extra feature flags for the certificate.
        /// </param>
        /// <param name="restrictions">
        /// Any special restrictions for the certificate.
        /// </param>
        /// <param name="notes">
        /// The extra informational notes for the certificate, if any.
        /// </param>
        /// <param name="serverInfo">
        /// The renewal server information for the certificate.
        /// </param>
        public void Pack( /* CORE */
            Guid id,                 /* in */
            ProtocolType protocol,   /* in */
            Version protocolVersion, /* in */
            string vendor,           /* in */
            Uri origin,              /* in */
            Uri authority,           /* in */
            Uri agreement,           /* in */
            Uri support,             /* in */
            DateTime timeStamp,      /* in */
            TimeSpan duration,       /* in */
            byte[] key,              /* in */
            ulong number,            /* in */
            string serialNumber,     /* in */
            string hashAlgorithm,    /* in */
            byte[] signature,        /* in */
            string type,             /* in */
            EntityType entityType,   /* in */
            string entityName,       /* in */
            string entityValue,      /* in */
            string extraData,        /* in */
            long quantity,           /* in */
            string product,          /* in */
            Version version,         /* in */
            string features,         /* in */
            string restrictions,     /* in */
            string notes,            /* in */
            string serverInfo        /* in */
            )
        {
            this.id = id;
            this.protocol = protocol;
            this.protocolVersion = protocolVersion;
            this.vendor = vendor;
            this.origin = origin;
            this.authority = authority;
            this.agreement = agreement;
            this.support = support;
            this.timeStamp = timeStamp;
            this.duration = duration;
            this.key = key;
            this.number = number;
            this.serialNumber = serialNumber;
            this.hashAlgorithm = hashAlgorithm;
            this.signature = signature;
            this.type = type;
            this.entityType = entityType;
            this.entityName = entityName;
            this.entityValue = entityValue;
            this.extraData = extraData;
            this.quantity = quantity;
            this.product = product;
            this.version = version;
            this.features = features;
            this.restrictions = restrictions;
            this.notes = notes;
            this.serverInfo = serverInfo;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string representation of this certificate.
        /// </summary>
        /// <returns>
        /// The string representation of this certificate.
        /// </returns>
        public override string ToString()
        {
#if ISOLATED_PLUGINS && CERTIFICATE_PLUGIN
            StringDictionary dictionary = ToDictionary();

            return (dictionary != null) ?
                dictionary.KeysAndValuesToString(null, false) :
                String.Empty;
#else
            return CertificateSharedOps.ToString(this);
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICloneable Members
        /// <summary>
        /// Creates a shallow copy of this certificate.
        /// </summary>
        /// <returns>
        /// The newly created copy of this certificate.
        /// </returns>
        public object Clone()
        {
            return MemberwiseClone();
        }
        #endregion
    }
}
