/*
 * RsaKeyPair.cs --
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
    /// Represents an RSA public and private key pair, holding the raw values
    /// parsed from a Microsoft cryptographic key BLOB and exposing them as
    /// <see cref="RSAParameters" /> instances.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("0094ee1d-a69b-4667-9bfe-5d0a2fd13081")]
    internal sealed class RsaKeyPair : KeyPair
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an empty <see cref="RsaKeyPair" /> instance.
        /// </summary>
        public RsaKeyPair()
            : base()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        #region From PublicKeyBlob
        /// <summary>
        /// The backing field for the <see cref="SignatureAlgorithmId" />
        /// property.
        /// </summary>
        private uint signatureAlgorithmId;
        /// <summary>
        /// Gets or sets the signature algorithm identifier obtained from the
        /// public key BLOB.
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
        /// The backing field for the <see cref="HashAlgorithmId" /> property.
        /// </summary>
        private uint hashAlgorithmId;
        /// <summary>
        /// Gets or sets the hash algorithm identifier obtained from the public
        /// key BLOB.
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
        /// The backing field for the <see cref="ByteCount" /> property.
        /// </summary>
        private uint byteCount;
        /// <summary>
        /// Gets or sets the byte count obtained from the public key BLOB.
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
        /// The backing field for the <see cref="Type" /> property.
        /// </summary>
        private byte type;
        /// <summary>
        /// Gets or sets the key BLOB type obtained from the BLOBHEADER.
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
        /// The backing field for the <see cref="Version" /> property.
        /// </summary>
        private byte version;
        /// <summary>
        /// Gets or sets the key BLOB version obtained from the BLOBHEADER.
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
        /// The backing field for the <see cref="Reserved" /> property.
        /// </summary>
        private ushort reserved;
        /// <summary>
        /// Gets or sets the reserved field obtained from the BLOBHEADER.
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
        /// The backing field for the <see cref="Algorithm" /> property.
        /// </summary>
        private uint algorithm;
        /// <summary>
        /// Gets or sets the algorithm identifier obtained from the BLOBHEADER.
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

        #region From RSAPUBLICKEY
        /// <summary>
        /// The backing field for the <see cref="Exponent" /> property.
        /// </summary>
        private uint exponent;
        /// <summary>
        /// Gets or sets the public exponent obtained from the RSAPUBLICKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint Exponent
        {
            get { return exponent; }
            set { exponent = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Modulus" /> property.
        /// </summary>
        private byte[] modulus;
        /// <summary>
        /// Gets or sets the modulus obtained from the RSAPUBLICKEY structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] Modulus
        {
            get { return modulus; }
            set { modulus = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From RSAPRIVATEKEY
        /// <summary>
        /// The backing field for the <see cref="P" /> property.
        /// </summary>
        private byte[] p;
        /// <summary>
        /// Gets or sets the first prime factor obtained from the
        /// RSAPRIVATEKEY structure.
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
        /// The backing field for the <see cref="Q" /> property.
        /// </summary>
        private byte[] q;
        /// <summary>
        /// Gets or sets the second prime factor obtained from the
        /// RSAPRIVATEKEY structure.
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
        /// The backing field for the <see cref="DP" /> property.
        /// </summary>
        private byte[] dp;
        /// <summary>
        /// Gets or sets the exponent d mod (p - 1) obtained from the
        /// RSAPRIVATEKEY structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] DP
        {
            get { return dp; }
            set { dp = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="DQ" /> property.
        /// </summary>
        private byte[] dq;
        /// <summary>
        /// Gets or sets the exponent d mod (q - 1) obtained from the
        /// RSAPRIVATEKEY structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] DQ
        {
            get { return dq; }
            set { dq = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="IQ" /> property.
        /// </summary>
        private byte[] iq;
        /// <summary>
        /// Gets or sets the coefficient (inverse of q mod p) obtained from the
        /// RSAPRIVATEKEY structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] IQ
        {
            get { return iq; }
            set { iq = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="D" /> property.
        /// </summary>
        private byte[] d;
        /// <summary>
        /// Gets or sets the private exponent obtained from the RSAPRIVATEKEY
        /// structure.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] D
        {
            get { return d; }
            set { d = value; }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Creates an empty set of <see cref="RSAParameters" /> shared by both
        /// the public and private parameter views.
        /// </summary>
        /// <returns>
        /// A new, empty <see cref="RSAParameters" /> value.
        /// </returns>
        private RSAParameters ToSharedParameters()
        {
            //
            // HACK: There are no truly "shared" parameters for RSA?
            //
            return new RSAParameters();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Builds the <see cref="RSAParameters" /> containing only the public
        /// key components, namely the exponent and modulus.
        /// </summary>
        /// <returns>
        /// The <see cref="RSAParameters" /> populated with the public key
        /// values.
        /// </returns>
        public RSAParameters ToPublicParameters()
        {
            RSAParameters parameters = ToSharedParameters();

            parameters.Exponent = BitConverter.GetBytes(this.Exponent);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(parameters.Exponent);

            parameters.Modulus = this.Modulus;

            return parameters;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the <see cref="RSAParameters" /> containing the private key
        /// components, optionally including the public key values as well.
        /// </summary>
        /// <param name="publicKey">
        /// Non-zero to also include the public key components (exponent and
        /// modulus) in the returned parameters.
        /// </param>
        /// <returns>
        /// The <see cref="RSAParameters" /> populated with the private key
        /// values.
        /// </returns>
        public RSAParameters ToPrivateParameters(
            bool publicKey /* in */
            )
        {
            RSAParameters parameters = publicKey ?
                ToPublicParameters() : ToSharedParameters();

            parameters.P = this.P;
            parameters.Q = this.Q;
            parameters.DP = this.DP;
            parameters.DQ = this.DQ;
            parameters.InverseQ = this.IQ;
            parameters.D = this.D;

            return parameters;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && PLUGIN_COMMANDS
        /// <summary>
        /// Returns a list of name/value pairs describing the components of
        /// this key pair, suitable for diagnostic display.
        /// </summary>
        /// <returns>
        /// An <see cref="IStringList" /> containing the key pair details, or
        /// null if the base implementation does not provide one.
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

                #region From RSAPUBLICKEY
                localList.Add("Exponent", Exponent.ToString());

                ///////////////////////////////////////////////////////////////

                byte[] modulus = this.Modulus;

                if (modulus != null)
                {
                    localList.Add("Modulus", Convert.ToBase64String(
                        modulus, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From RSAPUBLICKEY");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From RSAPRIVATEKEY
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

                byte[] dp = this.DP;

                if (dp != null)
                {
                    localList.Add("DP", Convert.ToBase64String(
                        dp, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] dq = this.DQ;

                if (dq != null)
                {
                    localList.Add("DQ", Convert.ToBase64String(
                        dq, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] iq = this.IQ;

                if (iq != null)
                {
                    localList.Add("IQ", Convert.ToBase64String(
                        iq, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                byte[] d = this.D;

                if (d != null)
                {
                    localList.Add("D", Convert.ToBase64String(
                        d, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From RSAPRIVATEKEY");
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
