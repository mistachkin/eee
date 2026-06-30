/*
 * DsaKeyPair.cs --
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
using System.Security.Cryptography;
using Eagle._Attributes;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Represents a DSA key pair, including the public and private key
    /// components parsed from a Windows CryptoAPI key blob.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("df3a5a1a-5544-4b5e-b281-b4f624421218")]
    internal sealed class DsaKeyPair : KeyPair
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an empty <see cref="DsaKeyPair" /> instance.
        /// </summary>
        public DsaKeyPair()
            : base()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        #region From PublicKeyBlob
        /// <summary>
        /// The signature algorithm identifier from the public key blob.
        /// </summary>
        private uint signatureAlgorithmId;
        /// <summary>
        /// Gets or sets the signature algorithm identifier from the public
        /// key blob.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint SignatureAlgorithmId
        {
            get { return signatureAlgorithmId; }
            set { signatureAlgorithmId = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The hash algorithm identifier from the public key blob.
        /// </summary>
        private uint hashAlgorithmId;
        /// <summary>
        /// Gets or sets the hash algorithm identifier from the public key
        /// blob.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint HashAlgorithmId
        {
            get { return hashAlgorithmId; }
            set { hashAlgorithmId = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The size, in bytes, of the key from the public key blob.
        /// </summary>
        private uint byteCount;
        /// <summary>
        /// Gets or sets the size, in bytes, of the key from the public key
        /// blob.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint ByteCount
        {
            get { return byteCount; }
            set { byteCount = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From BLOBHEADER
        /// <summary>
        /// The key blob type from the BLOBHEADER structure.
        /// </summary>
        private byte type;
        /// <summary>
        /// Gets or sets the key blob type from the BLOBHEADER structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte Type
        {
            get { return type; }
            set { type = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key blob version from the BLOBHEADER structure.
        /// </summary>
        private byte version;
        /// <summary>
        /// Gets or sets the key blob version from the BLOBHEADER structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte Version
        {
            get { return version; }
            set { version = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The reserved field from the BLOBHEADER structure.
        /// </summary>
        private ushort reserved;
        /// <summary>
        /// Gets or sets the reserved field from the BLOBHEADER structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public ushort Reserved
        {
            get { return reserved; }
            set { reserved = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The algorithm identifier from the BLOBHEADER structure.
        /// </summary>
        private uint algorithm;
        /// <summary>
        /// Gets or sets the algorithm identifier from the BLOBHEADER
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint Algorithm
        {
            get { return algorithm; }
            set { algorithm = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From DSSPUBLICKEY
        /// <summary>
        /// The DSA prime modulus P from the DSSPUBLICKEY structure.
        /// </summary>
        private byte[] p;
        /// <summary>
        /// Gets or sets the DSA prime modulus P from the DSSPUBLICKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] P
        {
            get { return p; }
            set { p = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The DSA prime divisor Q from the DSSPUBLICKEY structure.
        /// </summary>
        private byte[] q;
        /// <summary>
        /// Gets or sets the DSA prime divisor Q from the DSSPUBLICKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] Q
        {
            get { return q; }
            set { q = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The DSA generator G from the DSSPUBLICKEY structure.
        /// </summary>
        private byte[] g;
        /// <summary>
        /// Gets or sets the DSA generator G from the DSSPUBLICKEY structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] G
        {
            get { return g; }
            set { g = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The DSA public key value Y from the DSSPUBLICKEY structure.
        /// </summary>
        private byte[] y;
        /// <summary>
        /// Gets or sets the DSA public key value Y from the DSSPUBLICKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] Y
        {
            get { return y; }
            set { y = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From DSSPRIVATEKEY
        /// <summary>
        /// The DSA private key value X from the DSSPRIVATEKEY structure.
        /// </summary>
        private byte[] x;
        /// <summary>
        /// Gets or sets the DSA private key value X from the DSSPRIVATEKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] X
        {
            get { return x; }
            set { x = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From DSSSEED
        /// <summary>
        /// The DSA seed counter from the DSSSEED structure.
        /// </summary>
        private uint counter;
        /// <summary>
        /// Gets or sets the DSA seed counter from the DSSSEED structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint Counter
        {
            get { return counter; }
            set { counter = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The DSA seed value from the DSSSEED structure.
        /// </summary>
        private byte[] seed;
        /// <summary>
        /// Gets or sets the DSA seed value from the DSSSEED structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] Seed
        {
            get { return seed; }
            set { seed = value; }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Builds the <see cref="DSAParameters" /> value containing the
        /// components shared by both the public and private parameters
        /// (i.e. P, Q, G, the counter, and the seed).
        /// </summary>
        /// <returns>
        /// The shared <see cref="DSAParameters" /> value.
        /// </returns>
        private DSAParameters ToSharedParameters()
        {
            DSAParameters parameters = new DSAParameters();

            parameters.P = this.P;
            parameters.Q = this.Q;
            parameters.G = this.G;
            parameters.Counter = unchecked((int)this.Counter);
            parameters.Seed = this.Seed;

            return parameters;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Builds the <see cref="DSAParameters" /> value containing the
        /// public parameters, which include the public key value Y in
        /// addition to the shared components.
        /// </summary>
        /// <returns>
        /// The public <see cref="DSAParameters" /> value.
        /// </returns>
        public DSAParameters ToPublicParameters()
        {
            DSAParameters parameters = ToSharedParameters();

            parameters.Y = this.Y;

            return parameters;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the <see cref="DSAParameters" /> value containing the
        /// private parameters, which include the private key value X in
        /// addition to the shared components.
        /// </summary>
        /// <param name="publicKey">
        /// Non-zero to also include the public key components (i.e. the
        /// public parameters) in the returned value; otherwise, only the
        /// shared components and the private key value X are included.
        /// </param>
        /// <returns>
        /// The private <see cref="DSAParameters" /> value.
        /// </returns>
        public DSAParameters ToPrivateParameters(
            bool publicKey /* in */
            )
        {
            DSAParameters parameters = publicKey ?
                ToPublicParameters() : ToSharedParameters();

            parameters.X = this.X;

            return parameters;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// Returns a list of name/value pairs describing the components of
        /// this DSA key pair, suitable for diagnostic display.
        /// </summary>
        /// <returns>
        /// An <see cref="IStringList" /> containing the formatted key pair
        /// components.
        /// </returns>
        public override IStringList Dump()
        {
            StringPairList list = base.Dump() as StringPairList;

            if (list != null)
            {
                StringPairList localList = new StringPairList();

                ///////////////////////////////////////////////////////////////

                #region From PublicKeyBlob
                localList.Add("SignatureAlgorithmId",
                    SignatureAlgorithmId.ToString());

                ///////////////////////////////////////////////////////////////

                localList.Add("HashAlgorithmId", HashAlgorithmId.ToString());
                localList.Add("ByteCount", ByteCount.ToString());

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From PublicKeyBlob");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From BLOBHEADER
                localList.Add("Type", Type.ToString());
                localList.Add("Version", Version.ToString());
                localList.Add("Reserved", Reserved.ToString());
                localList.Add("Algorithm", Algorithm.ToString());

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From BLOBHEADER");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From DSSPUBLICKEY
                byte[] p = this.P;

                if (p != null)
                {
                    localList.Add("P", Convert.ToBase64String(
                        p, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] q = this.Q;

                if (q != null)
                {
                    localList.Add("Q", Convert.ToBase64String(
                        q, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] g = this.G;

                if (g != null)
                {
                    localList.Add("G", Convert.ToBase64String(
                        g, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] y = this.Y;

                if (y != null)
                {
                    localList.Add("Y", Convert.ToBase64String(
                        y, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From DSSPUBLICKEY");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From DSSPRIVATEKEY
                byte[] x = this.X;

                if (x != null)
                {
                    localList.Add("X", Convert.ToBase64String(
                        x, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From DSSPRIVATEKEY");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From DSSSEED
                localList.Add("Counter", Counter.ToString());

                ///////////////////////////////////////////////////////////////

                byte[] seed = this.Seed;

                if (seed != null)
                {
                    localList.Add("Seed", Convert.ToBase64String(
                        seed, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From DSSSEED");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion
            }

            return list;
        }
#endif
        #endregion
    }
}
