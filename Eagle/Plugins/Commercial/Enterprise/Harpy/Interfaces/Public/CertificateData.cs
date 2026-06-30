/*
 * CertificateData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Licensing.Components.Public;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Defines the complete set of data carried by a licensing certificate.
    /// A certificate binds an issuing vendor and authority to a covered
    /// entity, product, and feature set for a bounded validity period, and
    /// carries the cryptographic material (key, hash algorithm name, and
    /// signature) required to verify its authenticity and integrity. The
    /// properties exposed here constitute the canonical, serializable form
    /// of that data; implementations are expected to round-trip every value
    /// without loss. See <see cref="ProtocolType" /> and
    /// <see cref="EntityType" /> for the enumerated values used by the
    /// <see cref="Protocol" /> and <see cref="EntityType" /> properties.
    /// </summary>
    [ObjectId("7b057186-c1db-4415-bec2-13dfee93ffe6")]
    public interface ICertificateData /* CORE */
    {
        /// <summary>
        /// Gets or sets the licensing protocol family used to interpret and
        /// validate this certificate.
        /// </summary>
        /// <value>
        /// A <see cref="ProtocolType" /> value; <c>Local</c> is the typical
        /// default, while <c>Invalid</c>, <c>Remote</c>, and <c>Secure</c>
        /// must not be used.
        /// </value>
        ProtocolType Protocol { get; set; }
        /// <summary>
        /// Gets or sets the version of the licensing protocol that produced
        /// this certificate, used to govern how its contents are parsed.
        /// </summary>
        /// <value>
        /// A <see cref="System.Version" /> identifying the protocol revision;
        /// may be <c>null</c> when no protocol version has been assigned.
        /// </value>
        Version ProtocolVersion { get; set; }
        /// <summary>
        /// Gets or sets the name of the vendor that issued this certificate.
        /// </summary>
        /// <value>
        /// The issuing vendor name; may be <c>null</c> if unspecified.
        /// </value>
        string Vendor { get; set; }
        /// <summary>
        /// Gets or sets the URI identifying the origin from which this
        /// certificate was obtained.
        /// </summary>
        /// <value>
        /// A <see cref="System.Uri" /> identifying the certificate origin;
        /// may be <c>null</c> if unspecified.
        /// </value>
        Uri Origin { get; set; }
        /// <summary>
        /// Gets or sets the URI of the authority responsible for issuing and
        /// vouching for this certificate.
        /// </summary>
        /// <value>
        /// A <see cref="System.Uri" /> identifying the issuing authority;
        /// may be <c>null</c> if unspecified.
        /// </value>
        Uri Authority { get; set; }
        /// <summary>
        /// Gets or sets the URI of the license agreement governing the use
        /// of this certificate.
        /// </summary>
        /// <value>
        /// A <see cref="System.Uri" /> referencing the license agreement;
        /// may be <c>null</c> if unspecified.
        /// </value>
        Uri Agreement { get; set; }
        /// <summary>
        /// Gets or sets the URI where support for the licensed product may
        /// be obtained.
        /// </summary>
        /// <value>
        /// A <see cref="System.Uri" /> referencing the support resource;
        /// may be <c>null</c> if unspecified.
        /// </value>
        Uri Support { get; set; }
        /// <summary>
        /// Gets or sets the globally unique identifier that distinguishes
        /// this certificate from all others.
        /// </summary>
        /// <value>
        /// A <see cref="System.Guid" /> uniquely identifying the certificate;
        /// <see cref="System.Guid.Empty" /> indicates no identifier.
        /// </value>
        Guid Id { get; set; }
        /// <summary>
        /// Gets or sets the date and time at which this certificate was
        /// created, marking the start of its validity period.
        /// </summary>
        /// <value>
        /// A <see cref="System.DateTime" /> recording when the certificate
        /// was issued.
        /// </value>
        DateTime TimeStamp { get; set; }
        /// <summary>
        /// Gets or sets the length of time, measured from
        /// <see cref="TimeStamp" />, for which this certificate remains
        /// valid.
        /// </summary>
        /// <value>
        /// A <see cref="System.TimeSpan" /> describing the validity window.
        /// </value>
        TimeSpan Duration { get; set; }
        /// <summary>
        /// Gets or sets the cryptographic key material associated with this
        /// certificate.
        /// </summary>
        /// <value>
        /// A byte array holding the key; may be <c>null</c> if no key is
        /// present.
        /// </value>
        byte[] Key { get; set; }
        /// <summary>
        /// Gets or sets the opaque numeric value associated with this
        /// certificate, used by the licensing protocol.
        /// </summary>
        /// <value>
        /// An unsigned 64-bit integer (<c>ulong</c>) value.
        /// </value>
        ulong Number { get; set; }
        /// <summary>
        /// Gets or sets the human-readable serial number assigned to this
        /// certificate.
        /// </summary>
        /// <value>
        /// The serial number; may be <c>null</c> if unspecified.
        /// </value>
        string SerialNumber { get; set; }
        /// <summary>
        /// Gets or sets the name of the hash algorithm used when computing
        /// and verifying the <see cref="Signature" /> of this certificate.
        /// </summary>
        /// <value>
        /// The hash algorithm name (for example, <c>SHA512</c>); may be
        /// <c>null</c> if unspecified.
        /// </value>
        string HashAlgorithm { get; set; } /* EXEMPT */
        /// <summary>
        /// Gets or sets the cryptographic signature that authenticates this
        /// certificate, computed using the <see cref="HashAlgorithm" /> over
        /// the certificate contents.
        /// </summary>
        /// <value>
        /// A byte array holding the signature; may be <c>null</c> if the
        /// certificate has not yet been signed.
        /// </value>
        byte[] Signature { get; set; }
        /// <summary>
        /// Gets or sets the type designation of this certificate.
        /// </summary>
        /// <value>
        /// The certificate type; may be <c>null</c> if unspecified.
        /// </value>
        string Type { get; set; }
        /// <summary>
        /// Gets or sets the kind of entity (for example, an individual,
        /// team, or company) that this certificate licenses.
        /// </summary>
        /// <value>
        /// An <see cref="Licensing.Components.Public.EntityType" /> value
        /// identifying the licensed entity kind.
        /// </value>
        EntityType EntityType { get; set; }
        /// <summary>
        /// Gets or sets the name of the entity that this certificate
        /// licenses, qualifying the <see cref="EntityType" />.
        /// </summary>
        /// <value>
        /// The entity name; may be <c>null</c> if unspecified.
        /// </value>
        string EntityName { get; set; }
        /// <summary>
        /// Gets or sets the value associated with the licensed entity,
        /// qualifying the <see cref="EntityName" />.
        /// </summary>
        /// <value>
        /// The entity value; may be <c>null</c> if unspecified.
        /// </value>
        string EntityValue { get; set; }
        /// <summary>
        /// Gets or sets any additional, application-defined data carried by
        /// this certificate.
        /// </summary>
        /// <value>
        /// The extra data; may be <c>null</c> if none is present.
        /// </value>
        string ExtraData { get; set; }
        /// <summary>
        /// Gets or sets the quantity (for example, the number of seats or
        /// units) authorized by this certificate.
        /// </summary>
        /// <value>
        /// A 64-bit count of authorized units.
        /// </value>
        long Quantity { get; set; }
        /// <summary>
        /// Gets or sets the name of the product covered by this certificate.
        /// </summary>
        /// <value>
        /// The product name; may be <c>null</c> if unspecified.
        /// </value>
        string Product { get; set; }
        /// <summary>
        /// Gets or sets the version of the product covered by this
        /// certificate.
        /// </summary>
        /// <value>
        /// A <see cref="System.Version" /> identifying the covered product
        /// version; may be <c>null</c> if unspecified.
        /// </value>
        Version Version { get; set; }
        /// <summary>
        /// Gets or sets the set of product features enabled by this
        /// certificate.
        /// </summary>
        /// <value>
        /// The enabled feature set; may be <c>null</c> if unspecified.
        /// </value>
        string Features { get; set; }
        /// <summary>
        /// Gets or sets the restrictions imposed on the use of the licensed
        /// product by this certificate.
        /// </summary>
        /// <value>
        /// The applicable restrictions; may be <c>null</c> if none apply.
        /// </value>
        string Restrictions { get; set; }
        /// <summary>
        /// Gets or sets free-form notes associated with this certificate.
        /// </summary>
        /// <value>
        /// The notes; may be <c>null</c> if none are present.
        /// </value>
        string Notes { get; set; }
        /// <summary>
        /// Gets or sets information describing the server associated with
        /// this certificate.
        /// </summary>
        /// <value>
        /// The server information; may be <c>null</c> if unspecified.
        /// </value>
        string ServerInfo { get; set; }
    }
}
