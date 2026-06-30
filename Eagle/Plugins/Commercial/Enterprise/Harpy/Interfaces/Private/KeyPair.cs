/*
 * KeyPair.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Represents a cryptographic key pair blob, exposing the salt, magic
    /// number, and bit length parsed from a private key file along with
    /// helpers for emitting the key data into lists or certificate chains.
    /// </summary>
    [ObjectId("39ccc04d-cd76-41fb-abe9-49368a947b90")]
    internal interface IKeyPair : IKeyPairMetadata /* CORE */
    {
        #region From PvkKeyBlob
        /// <summary>
        /// Gets or sets the salt bytes associated with the private key blob.
        /// </summary>
        byte[] Salt { get; set; }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From [RD]SAPUBLICKEY
        /// <summary>
        /// Gets or sets the magic number identifying the public key blob type.
        /// </summary>
        uint Magic { get; set; }
        /// <summary>
        /// Gets or sets the bit length of the key.
        /// </summary>
        uint BitLength { get; set; }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy & Command Usage
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adds the key pair data to the specified list of byte arrays.
        /// </summary>
        /// <param name="list">
        /// The list of byte arrays to which the key pair data is added.
        /// </param>
        void AddToList(ref IList<byte[]> list);
        /// <summary>
        /// Adds the key pair data to the specified list of strings.
        /// </summary>
        /// <param name="list">
        /// The list of strings to which the key pair data is added.
        /// </param>
        void AddToList(ref IStringList list);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the key pair data as a list of strings.
        /// </summary>
        /// <returns>
        /// A list of strings representing the key pair data.
        /// </returns>
        IStringList ToList();

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Returns the certificate chain associated with the key pair as a
        /// list of strings.
        /// </summary>
        /// <returns>
        /// A list of strings representing the certificate chain.
        /// </returns>
        IStringList Chain();
        /// <summary>
        /// Returns a diagnostic dump of the key pair as a list of strings.
        /// </summary>
        /// <returns>
        /// A list of strings containing the dumped key pair data.
        /// </returns>
        IStringList Dump();
#endif
#endif
        #endregion
    }
}
