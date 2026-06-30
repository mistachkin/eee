/*
 * KeyPairMetadata.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Licensing.Components.Private;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Describes a cryptographic key pair, extending
    /// <see cref="IKeyPairMetadataBase" /> with the key-pair type, the file
    /// format used to persist it, its public key token, and flags indicating
    /// which of the public and private key components are actually available.
    /// Instances act as lightweight descriptors that can be chained to a
    /// <see cref="Parent" /> descriptor and resolved to a canonical name via
    /// <see cref="MakeName" />.
    /// </summary>
    [ObjectId("2918e584-793d-4300-81a1-6a51562bfd03")]
    internal interface IKeyPairMetadata : IKeyPairMetadataBase /* CORE */
    {
        /// <summary>
        /// Gets or sets the kind of cryptographic key pair described by this
        /// metadata, indicating which key-pair algorithm or scheme the
        /// associated key pair uses.
        /// </summary>
        /// <value>
        /// One of the <c>KeyPairType</c> values.
        /// </value>
        KeyPairType KeyPairType { get; set; }

        /// <summary>
        /// Gets or sets the on-disk file format used to persist the key pair
        /// described by this metadata.
        /// </summary>
        /// <value>
        /// One of the <c>KeyFileFormat</c> values identifying how the key pair
        /// is encoded when stored.
        /// </value>
        KeyFileFormat KeyFileFormat { get; set; }

        /// <summary>
        /// Gets or sets the public key token that uniquely identifies the key
        /// pair described by this metadata.
        /// </summary>
        /// <value>
        /// The raw <c>byte</c> array containing the public key token, or null
        /// if no token is associated with the key pair.
        /// </value>
        byte[] PublicKeyToken { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the public key component of
        /// the described key pair is present and available for use.
        /// </summary>
        /// <value>
        /// Non-zero if the public key component is available; otherwise, zero.
        /// </value>
        bool HavePublicKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the private key component of
        /// the described key pair is present and available for use.
        /// </summary>
        /// <value>
        /// Non-zero if the private key component is available; otherwise, zero.
        /// </value>
        bool HavePrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the file name from which the key pair was loaded or to
        /// which it is persisted.
        /// </summary>
        /// <value>
        /// The file name associated with the key pair, or null if the key pair
        /// is not backed by a file.
        /// </value>
        string FileName { get; set; }

        /// <summary>
        /// Gets or sets the parent <see cref="IKeyPairMetadata" /> from which
        /// this metadata is derived, allowing descriptors to be chained so that
        /// related key pairs can be linked together.
        /// </summary>
        /// <value>
        /// The parent metadata descriptor, or null if this metadata has no
        /// parent.
        /// </value>
        IKeyPairMetadata Parent { get; set; }

        /// <summary>
        /// Constructs a canonical name for the key pair from the values carried
        /// by this metadata, suitable for identifying or displaying the key
        /// pair.
        /// </summary>
        /// <returns>
        /// The constructed name for the key pair.
        /// </returns>
        string MakeName();
    }
}
