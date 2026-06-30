/*
 * LicenseCertificateData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Represents the data associated with a license certificate,
    /// including the name of the file containing the certificate and the
    /// certificate itself.
    /// </summary>
    [ObjectId("9921e0ad-c1a8-42fb-a5c5-30124406e34a")]
    public interface ILicenseCertificateData
    {
        /// <summary>
        /// Gets the name of the file containing the license certificate.
        /// </summary>
        string CertificateFileName { get; }
        /// <summary>
        /// Gets the license certificate.
        /// </summary>
        ICertificate Certificate { get; }
    }
}
