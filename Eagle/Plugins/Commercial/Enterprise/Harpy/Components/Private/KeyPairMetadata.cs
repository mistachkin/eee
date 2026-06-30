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

using System;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Interfaces.Private;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides a concrete implementation of the
    /// <see cref="IKeyPairMetadata" /> interface, holding metadata that
    /// describes a public/private key pair, such as its type, file
    /// format, public key token, and associated file name.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("c3b54a04-fcb9-43b7-a424-a34290f489b9")]
    internal class KeyPairMetadata : KeyPairMetadataBase, IKeyPairMetadata
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        , IEquatable<IKeyPairMetadata>
#endif
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="KeyPairMetadata" />
        /// class with all metadata values initialized to their defaults.
        /// </summary>
        public KeyPairMetadata()
            : base(null, null, null, null)
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IKeyPairMetadata Members
        /// <summary>
        /// Stores the type of the key pair represented by this metadata.
        /// </summary>
        private KeyPairType keyPairType;
        /// <summary>
        /// Gets or sets the type of the key pair represented by this
        /// metadata.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public KeyPairType KeyPairType
        {
            get { return keyPairType; }
            set { keyPairType = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the file format used to store the key pair.
        /// </summary>
        private KeyFileFormat keyFileFormat;
        /// <summary>
        /// Gets or sets the file format used to store the key pair.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public KeyFileFormat KeyFileFormat
        {
            get { return keyFileFormat; }
            set { keyFileFormat = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the public key token bytes associated with the key pair.
        /// </summary>
        private byte[] publicKeyToken;
        /// <summary>
        /// Gets or sets the public key token bytes associated with the key
        /// pair.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] PublicKeyToken
        {
            get { return publicKeyToken; }
            set { publicKeyToken = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores a value indicating whether a public key is available.
        /// </summary>
        private bool havePublicKey;
        /// <summary>
        /// Gets or sets a value indicating whether a public key is
        /// available.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public bool HavePublicKey
        {
            get { return havePublicKey; }
            set { havePublicKey = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores a value indicating whether a private key is available.
        /// </summary>
        private bool havePrivateKey;
        /// <summary>
        /// Gets or sets a value indicating whether a private key is
        /// available.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public bool HavePrivateKey
        {
            get { return havePrivateKey; }
            set { havePrivateKey = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the name of the file associated with the key pair.
        /// </summary>
        private string fileName;
        /// <summary>
        /// Gets or sets the name of the file associated with the key pair.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the parent metadata associated with this metadata, if
        /// any.
        /// </summary>
        private IKeyPairMetadata parent;
        /// <summary>
        /// Gets or sets the parent metadata associated with this metadata,
        /// if any.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public IKeyPairMetadata Parent
        {
            get { return parent; }
            set { parent = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a name for this key pair metadata by combining its file
        /// name and key usage.
        /// </summary>
        /// <returns>
        /// Returns the constructed name as a string.
        /// </returns>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public string MakeName()
        {
            return new StringList(FileName, KeyUsage).ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEquatable<IKeyPairMetadata> Members
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the specified <see cref="IKeyPairMetadata" />
        /// is equal to the current instance by comparing the public key
        /// token and the public/private key availability flags.
        /// </summary>
        /// <param name="other">
        /// The other key pair metadata to compare against this instance.
        /// </param>
        /// <returns>
        /// Returns true if the specified metadata is considered equal to
        /// this instance; otherwise, false.
        /// </returns>
#if OBFUSCATION
        //
        // HACK: Workaround for Crypto Obfuscator for .Net, to prevent
        //       errors like "Method 'X' in type 'Y' from assembly 'Z'
        //       does not have an implementation." errors.
        //
        [Obfuscation(Feature = "renaming")]
#endif
        public bool Equals(
            IKeyPairMetadata other /* in */
            )
        {
            if (other == null)
                return false;

            if (!CertificateKeyPairOps.MatchPublicKeyToken(
                    other, publicKeyToken))
            {
                return false;
            }

            if (havePublicKey != other.HavePublicKey)
                return false;

            if (havePrivateKey != other.HavePrivateKey)
                return false;

            return true;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the specified object is equal to the current
        /// instance.
        /// </summary>
        /// <param name="obj">
        /// The object to compare against this instance.
        /// </param>
        /// <returns>
        /// Returns true if the specified object is an equal
        /// <see cref="IKeyPairMetadata" />; otherwise, false.
        /// </returns>
#if OBFUSCATION
        //
        // HACK: Workaround for Crypto Obfuscator for .Net, to prevent
        //       errors like "Method 'X' in type 'Y' from assembly 'Z'
        //       does not have an implementation." errors.
        //
        [Obfuscation(Feature = "renaming")]
#endif
        public override bool Equals(
            object obj /* in */
            )
        {
            return Equals(obj as IKeyPairMetadata);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a hash code for this instance based on its public key
        /// token and key availability flags.
        /// </summary>
        /// <returns>
        /// Returns the computed hash code.
        /// </returns>
        public override int GetHashCode()
        {
            int result = 0;

            byte[] publicKeyToken = this.PublicKeyToken;

            if (publicKeyToken != null)
                result ^= Utility.GetHashCode(publicKeyToken);

            result ^= this.HavePublicKey ? 2 : 0;
            result ^= this.HavePrivateKey ? 1 : 0;

            return result;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a string representation of this instance, formatted from
        /// its public key token.
        /// </summary>
        /// <returns>
        /// Returns the formatted string representation.
        /// </returns>
        public override string ToString()
        {
            return CertificateDataOps.FormatPublicKeyToken(
                publicKeyToken, false, false);
        }
        #endregion
    }
}
