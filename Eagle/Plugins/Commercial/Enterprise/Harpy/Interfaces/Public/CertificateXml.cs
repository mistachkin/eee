/*
 * CertificateXml.cs --
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

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Represents the XML form of a licensing certificate, providing the
    /// ability to combine the individual certificate fields into a single
    /// representation and to extract those fields back out again.
    /// </summary>
    [ObjectId("2f720563-186e-448f-a8c6-ebf314e33a9c")]
    public interface ICertificateXml /* CORE */
    {
        /// <summary>
        /// Combines the individual certificate fields into the XML
        /// representation managed by this object.
        /// </summary>
        /// <param name="protocolVersion">
        /// The protocol version associated with the certificate.
        /// </param>
        /// <param name="origin">
        /// The <see cref="Uri" /> identifying the origin of the certificate.
        /// </param>
        /// <param name="authority">
        /// The <see cref="Uri" /> identifying the authority that issued the
        /// certificate.
        /// </param>
        /// <param name="agreement">
        /// The <see cref="Uri" /> identifying the license agreement
        /// associated with the certificate.
        /// </param>
        /// <param name="support">
        /// The <see cref="Uri" /> identifying the support resources
        /// associated with the certificate.
        /// </param>
        /// <param name="timeStamp">
        /// The date and time when the certificate was created.
        /// </param>
        /// <param name="duration">
        /// The length of time for which the certificate remains valid.
        /// </param>
        /// <param name="key">
        /// The bytes of the key associated with the certificate.
        /// </param>
        /// <param name="number">
        /// The numeric identifier associated with the certificate.
        /// </param>
        /// <param name="signature">
        /// The bytes of the cryptographic signature for the certificate.
        /// </param>
        /// <param name="version">
        /// The version associated with the certificate.
        /// </param>
        void Pack(
            Version protocolVersion,
            Uri origin,
            Uri authority,
            Uri agreement,
            Uri support,
            DateTime timeStamp,
            TimeSpan duration,
            byte[] key,
            ulong number,
            byte[] signature,
            Version version
        );

        /// <summary>
        /// Extracts the individual certificate fields from the XML
        /// representation managed by this object.
        /// </summary>
        /// <param name="protocolVersion">
        /// Upon return, receives the protocol version associated with the
        /// certificate.
        /// </param>
        /// <param name="origin">
        /// Upon return, receives the <see cref="Uri" /> identifying the
        /// origin of the certificate.
        /// </param>
        /// <param name="authority">
        /// Upon return, receives the <see cref="Uri" /> identifying the
        /// authority that issued the certificate.
        /// </param>
        /// <param name="agreement">
        /// Upon return, receives the <see cref="Uri" /> identifying the
        /// license agreement associated with the certificate.
        /// </param>
        /// <param name="support">
        /// Upon return, receives the <see cref="Uri" /> identifying the
        /// support resources associated with the certificate.
        /// </param>
        /// <param name="timeStamp">
        /// Upon return, receives the date and time when the certificate was
        /// created.
        /// </param>
        /// <param name="duration">
        /// Upon return, receives the length of time for which the certificate
        /// remains valid.
        /// </param>
        /// <param name="key">
        /// Upon return, receives the bytes of the key associated with the
        /// certificate.
        /// </param>
        /// <param name="number">
        /// Upon return, receives the numeric identifier associated with the
        /// certificate.
        /// </param>
        /// <param name="signature">
        /// Upon return, receives the bytes of the cryptographic signature for
        /// the certificate.
        /// </param>
        /// <param name="version">
        /// Upon return, receives the version associated with the certificate.
        /// </param>
        void Unpack(
            out Version protocolVersion,
            out Uri origin,
            out Uri authority,
            out Uri agreement,
            out Uri support,
            out DateTime timeStamp,
            out TimeSpan duration,
            out byte[] key,
            out ulong number,
            out byte[] signature,
            out Version version
        );
    }
}
