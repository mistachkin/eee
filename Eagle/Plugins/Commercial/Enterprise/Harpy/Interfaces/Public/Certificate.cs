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
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Licensing.Components.Public;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Represents a license certificate, extending the core certificate data
    /// with operations to derive entropy from and populate the certificate.
    /// </summary>
    [ObjectId("71177b6d-f4d8-4c68-9686-d90b20b0fcb7")]
    public interface ICertificate : ICertificateData /* CORE */
#if XML && SERIALIZATION
        , ICertificateXml
#endif
    {
        /// <summary>
        /// Derives a block of entropy from the data contained in this
        /// certificate, optionally combined with the supplied salt.
        /// </summary>
        /// <param name="salt">
        /// Additional salt bytes to mix into the derived entropy.
        /// </param>
        /// <param name="encoding">
        /// The text encoding used when converting certificate string data to
        /// bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that occurred.
        /// </param>
        /// <returns>
        /// The derived entropy bytes, or null if the entropy could not be
        /// extracted.
        /// </returns>
        byte[] ExtractEntropy(
            byte[] salt,
            Encoding encoding,
            ref Result error
        );

        /// <summary>
        /// Populates this certificate with the supplied values, fully
        /// describing the identity, validity, and licensed entitlements that
        /// the certificate represents.
        /// </summary>
        /// <param name="id">
        /// The unique identifier for this certificate.
        /// </param>
        /// <param name="protocol">
        /// The licensing protocol associated with this certificate.
        /// </param>
        /// <param name="protocolVersion">
        /// The version of the licensing protocol associated with this
        /// certificate.
        /// </param>
        /// <param name="vendor">
        /// The name of the vendor that issued this certificate.
        /// </param>
        /// <param name="origin">
        /// The URI identifying the origin of this certificate.
        /// </param>
        /// <param name="authority">
        /// The URI identifying the authority responsible for this certificate.
        /// </param>
        /// <param name="agreement">
        /// The URI identifying the license agreement for this certificate.
        /// </param>
        /// <param name="support">
        /// The URI identifying the support resource for this certificate.
        /// </param>
        /// <param name="timeStamp">
        /// The point in time when this certificate was created.
        /// </param>
        /// <param name="duration">
        /// The length of time for which this certificate is valid.
        /// </param>
        /// <param name="key">
        /// The cryptographic key associated with this certificate.
        /// </param>
        /// <param name="number">
        /// The numeric identifier associated with this certificate.
        /// </param>
        /// <param name="serialNumber">
        /// The serial number associated with this certificate.
        /// </param>
        /// <param name="hashAlgorithm">
        /// The name of the hash algorithm used by this certificate.
        /// </param>
        /// <param name="signature">
        /// The cryptographic signature that authenticates this certificate.
        /// </param>
        /// <param name="type">
        /// The type designation for this certificate.
        /// </param>
        /// <param name="entityType">
        /// The kind of entity to which this certificate is issued.
        /// </param>
        /// <param name="entityName">
        /// The name of the entity to which this certificate is issued.
        /// </param>
        /// <param name="entityValue">
        /// The value associated with the entity to which this certificate is
        /// issued.
        /// </param>
        /// <param name="extraData">
        /// Any additional data to associate with this certificate.
        /// </param>
        /// <param name="quantity">
        /// The licensed quantity granted by this certificate.
        /// </param>
        /// <param name="product">
        /// The name of the licensed product.
        /// </param>
        /// <param name="version">
        /// The version of the licensed product.
        /// </param>
        /// <param name="features">
        /// The features licensed by this certificate.
        /// </param>
        /// <param name="restrictions">
        /// The restrictions imposed by this certificate.
        /// </param>
        /// <param name="notes">
        /// Any free-form notes associated with this certificate.
        /// </param>
        /// <param name="serverInfo">
        /// Information about the server associated with this certificate.
        /// </param>
        void Pack(
            Guid id,
            ProtocolType protocol,
            Version protocolVersion,
            string vendor,
            Uri origin,
            Uri authority,
            Uri agreement,
            Uri support,
            DateTime timeStamp,
            TimeSpan duration,
            byte[] key,
            ulong number,
            string serialNumber,
            string hashAlgorithm,
            byte[] signature,
            string type,
            EntityType entityType,
            string entityName,
            string entityValue,
            string extraData,
            long quantity,
            string product,
            Version version,
            string features,
            string restrictions,
            string notes,
            string serverInfo
        );
    }
}
