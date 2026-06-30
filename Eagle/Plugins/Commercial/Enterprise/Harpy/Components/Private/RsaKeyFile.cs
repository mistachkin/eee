/*
 * RsaKeyFile.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper methods for reading and writing RSA key blobs
    /// in the various supported key file formats.
    /// </summary>
    [ObjectId("7b2df1b4-ca32-439c-860a-9142ad8926f0")]
    internal static class RsaKeyFile
    {
        #region Private Constants
        /// <summary>
        /// The RSA algorithm type bits used when composing a CryptoAPI
        /// algorithm identifier.
        /// </summary>
        private const uint ALG_TYPE_RSA = 0x400;

        /// <summary>
        /// The CryptoAPI algorithm identifier for an RSA signature key.
        /// </summary>
        private const uint CALG_RSA_SIGN = KeyFile.ALG_CLASS_SIGNATURE |
                                           ALG_TYPE_RSA;

        /// <summary>
        /// The CryptoAPI algorithm identifier for an RSA key exchange key.
        /// </summary>
        private const uint CALG_RSA_KEYX = KeyFile.ALG_CLASS_KEY_EXCHANGE |
                                           ALG_TYPE_RSA;

        /// <summary>
        /// The "RSA1" magic value identifying a public key blob.
        /// </summary>
        private const uint RSA1 = 0x31415352; // "RSA1" magic, PUBLICKEYBLOB

        /// <summary>
        /// The "RSA2" magic value identifying a private key blob.
        /// </summary>
        private const uint RSA2 = 0x32415352; // "RSA2" magic, PRIVATEKEYBLOB
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
#if DEBUG
        //
        // HACK: Enable this bit to dump the RSA key parameters when read
        //       from a blob.
        //
        /// <summary>
        /// Non-zero to dump the RSA key parameters when they are read from a
        /// blob.
        /// </summary>
        private static bool DumpForRead = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Enable this bit to dump the RSA key parameters when verify
        //       method is about to be used.
        //
        /// <summary>
        /// Non-zero to dump the RSA key parameters when the verify method is
        /// about to be used.
        /// </summary>
        private static bool DumpForVerify = false;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // HACK: Enable this bit to dump the RSA key parameters when sign
        //       method is about to be used.
        //
        /// <summary>
        /// Non-zero to dump the RSA key parameters when the sign method is
        /// about to be used.
        /// </summary>
        private static bool DumpForSign = false;
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region CryptoAPI Structures (Private)
        /// <summary>
        /// Represents the layout of a CryptoAPI RSAPUBLICKEY structure (as
        /// used by WinCrypt.h).
        /// </summary>
        private struct RSAPUBLICKEY // sizeof(RSAPUBKEY) > 12, WinCrypt.h
        {
            //
            // HACK: We need to get the size of all the fields in the
            //       RSAPUBLICKEY except the modulus; therefore, we
            //       create a nested struct that contains just those
            //       fields.
            //
            /// <summary>
            /// Represents the fixed-size leading fields of an
            /// <see cref="RSAPUBLICKEY" /> (everything except the modulus).
            /// </summary>
            public struct FIXED // sizeof(RSAPUBLICKEY.FIXED) == 12
            {
                /// <summary>
                /// The key blob "magic" value (e.g. <see cref="RSA1" /> or
                /// <see cref="RSA2" />).
                /// </summary>
                public uint magic;

                /// <summary>
                /// The length, in bits, of the RSA key.
                /// </summary>
                public uint bitLength;

                /// <summary>
                /// The RSA public exponent.
                /// </summary>
                public uint exponent;
            }

            /// <summary>
            /// The fixed-size leading fields of the public key structure.
            /// </summary>
            public FIXED @fixed;

            /// <summary>
            /// The RSA public key modulus.
            /// </summary>
            public byte[] modulus;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents the private key components of a CryptoAPI RSA key blob
        /// (corresponding to <see cref="RSAParameters" />).
        /// </summary>
        private struct RSAPRIVATEKEY // System.Security.Cryptography.RSAParameters
        {
            /// <summary>
            /// The first prime factor of the RSA modulus.
            /// </summary>
            public byte[] P;

            /// <summary>
            /// The second prime factor of the RSA modulus.
            /// </summary>
            public byte[] Q;

            /// <summary>
            /// The first prime exponent (D mod (P - 1)).
            /// </summary>
            public byte[] DP;

            /// <summary>
            /// The second prime exponent (D mod (Q - 1)).
            /// </summary>
            public byte[] DQ;

            /// <summary>
            /// The CRT coefficient (the inverse of Q modulo P).
            /// </summary>
            public byte[] IQ;

            /// <summary>
            /// The RSA private exponent.
            /// </summary>
            public byte[] D;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Diagnostic Methods (Private)
#if DEBUG
        /// <summary>
        /// Writes the components of the specified RSA key parameters to the
        /// trace log for diagnostic purposes.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method to include in the trace output; if
        /// null, a default name is used.
        /// </param>
        /// <param name="parameters">
        /// The RSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the diagnostic output.
        /// </param>
        private static void DumpParameters( /* CORE */
            string methodName,        /* in */
            RSAParameters parameters, /* in */
            TracePriority priority    /* in */
            )
        {
            string localMethodName;

            if (methodName != null)
                localMethodName = methodName;
            else
                localMethodName = "DumpParameters";

            CertificateTraceOps.DebugTrace(String.Format(
                "{0}: Exponent = {1}, Modulus = {2}, P = {3}, " +
                "Q = {4}, DP = {5}, DQ = {6}, InverseQ = {7}, D = {8}",
                localMethodName,
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Exponent)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Modulus)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.P)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Q)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.DP)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.DQ)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.InverseQ)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.D))),
                typeof(RsaKeyFile).Name, priority);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Read Methods (Private)
        /// <summary>
        /// Generates a .NET Framework compatible public key token from the
        /// specified RSA key pair.
        /// </summary>
        /// <param name="keyPair">
        /// The RSA key pair containing the public key information used to
        /// compute the token.
        /// </param>
        /// <returns>
        /// The computed public key token, or null if it could not be
        /// computed.
        /// </returns>
        private static byte[] MakePublicKeyToken( /* CORE */
            RsaKeyPair keyPair /* in */
            )
        {
            //
            // NOTE: The purpose of this function is to generate a .NET
            //       Framework compatible public key token from the
            //       specified key pair.  This has been tested and works
            //       properly with both public-only and public-private
            //       StrongName keys.  There is no built-in or officially
            //       documented way to accomplish this.
            //
            if ((keyPair == null) || (keyPair.Modulus == null))
                return null;

            //
            // NOTE: We use a list here because we are building up the
            //       bytes to hash incrementally from different fields.
            //
            ByteList list = new ByteList();

            //
            // NOTE: Add the fields from the PublicKeyBlob struct.
            //
            list.AddRange(BitConverter.GetBytes(keyPair.SignatureAlgorithmId));
            list.AddRange(BitConverter.GetBytes(keyPair.HashAlgorithmId));
            list.AddRange(BitConverter.GetBytes(keyPair.ByteCount));

            //
            // NOTE: Add the fields from the BLOBHEADER struct.
            //
            list.Add(KeyFile.PUBLICKEYBLOB); /* HACK: Always public. */
            list.Add(keyPair.Version); /* NOTE: One byte. */
            list.AddRange(BitConverter.GetBytes(keyPair.Reserved));
            list.AddRange(BitConverter.GetBytes(keyPair.Algorithm));

            //
            // NOTE: Add the fields from the RSAPUBLICKEY struct.
            //
            list.AddRange(BitConverter.GetBytes(
                RSA1)); /* HACK: Always public. */

            list.AddRange(BitConverter.GetBytes(keyPair.BitLength));
            list.AddRange(BitConverter.GetBytes(keyPair.Exponent));

            //
            // NOTE: Add the public key modulus.
            //
            bool reverse = BitConverter.IsLittleEndian;

            list.AddRange(new ByteList(keyPair.Modulus, reverse));

            //
            // NOTE: Create a default hash algorithm (SHA1).
            //
            Result error = null;

            using (HashAlgorithm hashAlgorithm = Utility.CreateHashAlgorithm(
                    null, ref error))
            {
                if (hashAlgorithm == null)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "MakePublicKeyToken: error = {0}", error),
                        typeof(RsaKeyFile).Name, TracePriority.MediumLow);
#endif

                    return null;
                }

                //
                // NOTE: Hash the contents of the list we created above
                //       (as a byte array).
                //
                list = new ByteList(hashAlgorithm.ComputeHash(
                    list.ToArray()));
            }

            //
            // NOTE: Extract the last sizeof(ulong) bytes from the
            //       hash value.
            //
            list = new ByteList(list.GetRange(
                list.Count - sizeof(ulong), sizeof(ulong)));

            //
            // NOTE: Convert to network byte order (reverse on little
            //       endian).
            //
            if (reverse)
                list.Reverse();

            return list.ToArray();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Diagnostic Methods (Public)
#if DEBUG
        /// <summary>
        /// Dumps the specified RSA key parameters prior to a verify
        /// operation, if dumping for verification is enabled.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method to include in the trace output; if
        /// null, a default name is used.
        /// </param>
        /// <param name="parameters">
        /// The RSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the diagnostic output.
        /// </param>
        public static void MaybeDumpVerifyParameters(
            string methodName,        /* in */
            RSAParameters parameters, /* in */
            TracePriority priority    /* in */
            ) /* CORE */
        {
            if (DumpForVerify)
                DumpParameters(methodName, parameters, priority);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Dumps the specified RSA key parameters prior to a sign operation,
        /// if dumping for signing is enabled.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method to include in the trace output; if
        /// null, a default name is used.
        /// </param>
        /// <param name="parameters">
        /// The RSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the diagnostic output.
        /// </param>
        public static void MaybeDumpSignParameters(
            string methodName,        /* in */
            RSAParameters parameters, /* in */
            TracePriority priority    /* in */
            )
        {
            if (DumpForSign)
                DumpParameters(methodName, parameters, priority);
        }
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Read Methods (Public)
        /// <summary>
        /// Reads an RSA key blob from the specified binary reader, populating
        /// a key pair with the public and/or private key components.
        /// </summary>
        /// <param name="binaryReader">
        /// The binary reader to read the RSA key blob from.
        /// </param>
        /// <param name="format">
        /// The key file format describing how the blob is laid out.
        /// </param>
        /// <param name="publicKeyToken">
        /// An optional public key token to assign to the resulting key pair;
        /// if null, one is computed from the public key.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to read and populate the public key components.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to read and populate the private key components, when
        /// they are available.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair read from the blob.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message; otherwise, receives
        /// operational status information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ReadBlob( /* CORE */
            BinaryReader binaryReader, /* in */
            KeyFileFormat format,      /* in */
            byte[] publicKeyToken,     /* in: OPTIONAL */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (binaryReader == null)
            {
                result = "invalid binary reader";
                return ReturnCode.Error;
            }

            try
            {
                BLOBHEADER blobHeader;
                PublicKeyBlob publicKeyBlob;

                //
                // NOTE: Read the key type from the blob header.
                //
                blobHeader.type = binaryReader.ReadByte();

                //
                // NOTE: If the key type is not correct, retry after skipping
                //       some more bytes...
                //
                // HACK: Must be a public key with an initial PublicKeyBlob
                //       header.  Read those fields now.
                //
                if (CertificateSharedOps.HasFlags(
                        format, KeyFileFormat.StrongName, true) &&
                    (blobHeader.type != KeyFile.PUBLICKEYBLOB) &&
                    (blobHeader.type != KeyFile.PRIVATEKEYBLOB))
                {
                    //
                    // NOTE: Read the signature algorithm Id.  We must build
                    //       this value incrementally from bytes because we
                    //       already read one byte of the value and we cannot
                    //       assume that the stream can be rewound.  For now,
                    //       this value should always be CALG_RSA_SIGN.  It
                    //       should be noted that this code must be portable.
                    //
                    byte[] bytes = new byte[sizeof(uint)];

                    bytes[0] = blobHeader.type;

                    for (int index = 1; index < sizeof(uint); index++)
                        bytes[index] = binaryReader.ReadByte();

                    publicKeyBlob.signatureAlgorithmId =
                        BitConverter.ToUInt32(bytes, 0);

                    //
                    // NOTE: Read the hash algorithm Id.  For now, this should
                    //       always be CALG_SHA1.
                    //
                    publicKeyBlob.hashAlgorithmId = binaryReader.ReadUInt32();

                    //
                    // NOTE: Get the number of bytes remaining after the above
                    //       header fields have been read.
                    //
                    publicKeyBlob.byteCount = binaryReader.ReadUInt32();

                    //
                    // NOTE: Try again to read the key type.
                    //
                    blobHeader.type = binaryReader.ReadByte();
                }
                else
                {
                    //
                    // HACK: Fake it, we need the data to be valid so that we
                    //       can compute the public key token.
                    //
                    format &= ~KeyFileFormat.StrongName;

                    publicKeyBlob.signatureAlgorithmId = CALG_RSA_SIGN;
                    publicKeyBlob.hashAlgorithmId = KeyFile.CALG_SHA1;
                    publicKeyBlob.byteCount = 0;
                }

                //
                // NOTE: It has to be a public or private key blob.
                //
                if ((blobHeader.type != KeyFile.PUBLICKEYBLOB) &&
                    (blobHeader.type != KeyFile.PRIVATEKEYBLOB))
                {
                    result = String.Format(
                        "invalid key type {0}",
                        Utility.FormatWrapOrNull(blobHeader.type));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Read the version.
                //
                blobHeader.version = binaryReader.ReadByte();

                //
                // NOTE: The version must match exactly.
                //
                if (blobHeader.version != KeyFile.CUR_BLOB_VERSION)
                {
                    result = String.Format(
                        "invalid key version {0}",
                        Utility.FormatWrapOrNull(blobHeader.version));

                    return ReturnCode.Error;
                }

                //
                // NOTE: We purposely ignore the reserved field.  It must be
                //       zero.
                //
                blobHeader.reserved = binaryReader.ReadUInt16();

                //
                // NOTE: Read the algorithm and make sure it is supported.
                //
                blobHeader.algorithm = binaryReader.ReadUInt32();

                //
                // NOTE: Make sure the algorithm is supported.
                //
                if ((blobHeader.algorithm != CALG_RSA_SIGN) &&
                    (blobHeader.algorithm != CALG_RSA_KEYX))
                {
                    result = String.Format(
                        "unsupported key algorithm {0}",
                        Utility.FormatWrapOrNull(blobHeader.algorithm));

                    return ReturnCode.Error;
                }

                RSAPUBLICKEY rsaPublicKey;
                RSAPRIVATEKEY rsaPrivateKey;

#if MONO_BUILD
                //
                // HACK: *MONO* The Mono C# compiler gives a warning unless
                //       this field is manually initialized.
                //
                rsaPublicKey.@fixed = new RSAPUBLICKEY.FIXED();
#endif

                //
                // NOTE: Read the "magic", this value must be RSA1 or RSA2.
                //
                rsaPublicKey.@fixed.magic = binaryReader.ReadUInt32();

                //
                // NOTE: Make sure the "magic" is RSA1 or RSA2 and that it
                //       matches what we expected to find based on the blob
                //       header type.
                //
                if (((blobHeader.type != KeyFile.PUBLICKEYBLOB) ||
                        (rsaPublicKey.@fixed.magic != RSA1)) &&
                    ((blobHeader.type != KeyFile.PRIVATEKEYBLOB) ||
                        (rsaPublicKey.@fixed.magic != RSA2)))
                {
                    result = String.Format(
                        "invalid key magic or mismatch between " +
                        "key type {0} and key magic {1}",
                        Utility.FormatWrapOrNull(blobHeader.type),
                        Utility.FormatWrapOrNull(rsaPublicKey.@fixed.magic));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Both public and private key blobs have the public key
                //       data.  Read the key bit length now.
                //
                uint bitLength = binaryReader.ReadUInt32();

                //
                // NOTE: Make sure the key bit length is an even multiple of 8
                //       (i.e. whole bytes).  This limitation applies to both
                //       the Microsoft Base Cryptographic Provider and the
                //       Microsoft Enhanced Cryptographic Provider.
                //
                if ((bitLength % KeyFile.WHOLE_BYTE_DIVISOR) != 0)
                {
                    result = String.Format(
                        "unsupported key length {0} " +
                        "(whole byte key lengths are required)",
                        Utility.FormatWrapOrNull(bitLength));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Ok, now what is the byte length as well as the floor
                //       of half that quantity?
                //
                int byteLength = (int)(bitLength /
                    KeyFile.WHOLE_BYTE_DIVISOR);

                int halfByteLength = (int)(bitLength /
                    KeyFile.HALF_BYTE_DIVISOR);

                //
                // NOTE: If the key bit length is not divisible by 16 then we
                //       need to read an extra byte per item (below).
                //
                // HACK: This is not well documented in MSDN (or apparently
                //       anywhere else).
                //
                if (bitLength % KeyFile.HALF_BYTE_DIVISOR != 0)
                    halfByteLength++;

                //
                // HACK: If necessary, update the byte count in the
                //       PublicKeyBlob.
                //
                if (publicKeyBlob.byteCount == 0)
                {
                    //
                    // NOTE: We need this to calculate the public key
                    //       token.
                    //
                    publicKeyBlob.byteCount =
                        (uint)Marshal.SizeOf(typeof(BLOBHEADER)) +
                        (uint)Marshal.SizeOf(typeof(RSAPUBLICKEY.FIXED)) +
                        (uint)byteLength;
                }

                //
                // NOTE: Read the remaining public key fields.
                //
                rsaPublicKey.@fixed.bitLength = bitLength;
                rsaPublicKey.@fixed.exponent = binaryReader.ReadUInt32();
                rsaPublicKey.modulus = binaryReader.ReadBytes(byteLength);

                //
                // NOTE: Is this a private key blob?
                //
                if (rsaPublicKey.@fixed.magic == RSA2)
                {
                    //
                    // NOTE: Read the private key fields now.
                    //
                    rsaPrivateKey.P = binaryReader.ReadBytes(halfByteLength);
                    rsaPrivateKey.Q = binaryReader.ReadBytes(halfByteLength);
                    rsaPrivateKey.DP = binaryReader.ReadBytes(halfByteLength);
                    rsaPrivateKey.DQ = binaryReader.ReadBytes(halfByteLength);
                    rsaPrivateKey.IQ = binaryReader.ReadBytes(halfByteLength);
                    rsaPrivateKey.D = binaryReader.ReadBytes(byteLength);
                }
                else
                {
                    //
                    // NOTE: No private key fields available.
                    //
                    rsaPrivateKey.P = null;
                    rsaPrivateKey.Q = null;
                    rsaPrivateKey.DP = null;
                    rsaPrivateKey.DQ = null;
                    rsaPrivateKey.IQ = null;
                    rsaPrivateKey.D = null;
                }

                //
                // NOTE: Create new object to hold the key information.
                //
                RsaKeyPair localKeyPair = new RsaKeyPair();

                //
                // NOTE: Mark key pair with the correct file metadata.
                //
                localKeyPair.KeyPairType = KeyPairType.RSA;
                localKeyPair.KeyFileFormat = format;

                //
                // NOTE: Copy PublicKeyBlob fields.
                //
                localKeyPair.SignatureAlgorithmId =
                    publicKeyBlob.signatureAlgorithmId;

                localKeyPair.HashAlgorithmId = publicKeyBlob.hashAlgorithmId;
                localKeyPair.ByteCount = publicKeyBlob.byteCount;

                //
                // NOTE: Copy BLOBHEADER fields.
                //
                localKeyPair.Type = blobHeader.type;
                localKeyPair.Version = blobHeader.version;
                localKeyPair.Reserved = blobHeader.reserved;
                localKeyPair.Algorithm = blobHeader.algorithm;

                //
                // NOTE: If requested, copy over public key info.
                //
                bool reverse = BitConverter.IsLittleEndian;

                if (publicKey)
                {
                    localKeyPair.Magic = rsaPublicKey.@fixed.magic;
                    localKeyPair.BitLength = rsaPublicKey.@fixed.bitLength;
                    localKeyPair.Exponent = rsaPublicKey.@fixed.exponent;

                    //
                    // NOTE: Read the public key modulus.
                    //
                    localKeyPair.Modulus = KeyFile.CopyAndMaybeReverse(
                        rsaPublicKey.modulus, reverse);

                    //
                    // NOTE: Copy the public key token (if supplied)
                    //       -OR- calculate one based on the public
                    //       key.
                    //
                    if (publicKeyToken != null)
                    {
                        localKeyPair.PublicKeyToken = publicKeyToken;
                    }
                    else
                    {
                        localKeyPair.PublicKeyToken = MakePublicKeyToken(
                            localKeyPair);
                    }

                    localKeyPair.HavePublicKey = true;
                }

                //
                // NOTE: If requested and available, copy over private
                //       key info.
                //
                if (privateKey &&
                    (blobHeader.type == KeyFile.PRIVATEKEYBLOB) &&
                    (rsaPublicKey.@fixed.magic == RSA2))
                {
                    localKeyPair.P = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.P, reverse);

                    localKeyPair.Q = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.Q, reverse);

                    localKeyPair.DP = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.DP, reverse);

                    localKeyPair.DQ = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.DQ, reverse);

                    localKeyPair.IQ = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.IQ, reverse);

                    localKeyPair.D = KeyFile.CopyAndMaybeReverse(
                        rsaPrivateKey.D, reverse);

                    localKeyPair.HavePrivateKey = true;
                }

#if DEBUG
                if (DumpForRead)
                {
                    DumpParameters("ReadBlob",
                        localKeyPair.ToPrivateParameters(true),
                        TracePriority.Highest);
                }
#endif

                keyPair = localKeyPair;
                result = OperationStatus.KeyPairOk;

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Write Methods (Public)
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Writes the specified RSA key pair to the supplied binary writer as
        /// a key blob in the requested format.
        /// </summary>
        /// <param name="binaryWriter">
        /// The binary writer to write the RSA key blob to.
        /// </param>
        /// <param name="format">
        /// The key file format describing how the blob should be laid out.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to write the public key components.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to write the private key components.
        /// </param>
        /// <param name="keyPair">
        /// The RSA key pair to write.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message; otherwise, receives
        /// operational status information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode WriteBlob(
            BinaryWriter binaryWriter, /* in */
            KeyFileFormat format,      /* in */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            IKeyPair keyPair,          /* in */
            ref Result result          /* out */
            )
        {
            if (binaryWriter == null)
            {
                result = "invalid binary writer";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            RsaKeyPair localKeyPair = keyPair as RsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an RSA key pair";
                return ReturnCode.Error;
            }

            try
            {
                if (publicKey && CertificateSharedOps.HasFlags(
                        format, KeyFileFormat.StrongName, true))
                {
                    binaryWriter.Write(localKeyPair.SignatureAlgorithmId);
                    binaryWriter.Write(localKeyPair.HashAlgorithmId);
                    binaryWriter.Write(localKeyPair.ByteCount);
                }

                binaryWriter.Write(localKeyPair.Type);
                binaryWriter.Write(localKeyPair.Version);
                binaryWriter.Write(localKeyPair.Reserved);
                binaryWriter.Write(localKeyPair.Algorithm);
                binaryWriter.Write(localKeyPair.Magic);
                binaryWriter.Write(localKeyPair.BitLength);
                binaryWriter.Write(localKeyPair.Exponent);

                bool reverse = BitConverter.IsLittleEndian;

                binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                    localKeyPair.Modulus, reverse));

                if (privateKey)
                {
                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.P, reverse));

                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.Q, reverse));

                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.DP, reverse));

                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.DQ, reverse));

                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.IQ, reverse));

                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.D, reverse));
                }

                result = OperationStatus.KeyPairOk;
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Auto-Detection Methods (Public)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the bytes at the specified offset contain a
        /// recognized RSA key blob "magic" value, updating the key file
        /// format accordingly.
        /// </summary>
        /// <param name="bytes">
        /// The byte array to examine.
        /// </param>
        /// <param name="startIndex">
        /// The offset within <paramref name="bytes" /> at which to look for
        /// the magic value.
        /// </param>
        /// <param name="format">
        /// On input, an optional existing key file format; on output, the
        /// format updated with the detected RSA flags.
        /// </param>
        /// <returns>
        /// Non-zero if a recognized RSA magic value was found; otherwise,
        /// zero.
        /// </returns>
        public static bool MatchMagic( /* CORE? */
            byte[] bytes,             /* in */
            int startIndex,           /* in */
            ref KeyFileFormat? format /* out */
            )
        {
            if (bytes == null)
                return false;

            int length = bytes.Length;

            if (length == 0)
                return false;

            if (startIndex < 0)
                return false;

            if ((startIndex + sizeof(uint)) > length)
                return false;

            uint value = BitConverter.ToUInt32(
                bytes, startIndex); /* throw */

            if ((value == RSA1) || (value == RSA2))
            {
                KeyFileFormat localFormat;

                if (format != null)
                    localFormat = (KeyFileFormat)format;
                else
                    localFormat = KeyFileFormat.None;

                if (startIndex == KeyFile.MAGIC_CRYPTOAPI_OFFSET)
                    localFormat |= KeyFileFormat.CryptoAPI;
                else
                    localFormat |= KeyFileFormat.StrongName;

                format = localFormat;
                return true;
            }

            return false;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region High-Level Auto-Detection Methods (Public)
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Determines the RSA key file format implied by the extension of the
        /// specified file name.
        /// </summary>
        /// <param name="fileName">
        /// The file name whose extension is examined.
        /// </param>
        /// <param name="format">
        /// Upon return, receives the detected key file format, or null if the
        /// extension is not recognized.
        /// </param>
        /// <returns>
        /// Non-zero if the file name extension matched a known RSA key file
        /// format; otherwise, zero.
        /// </returns>
        public static bool MatchFileName(
            string fileName,          /* in */
            out KeyFileFormat? format /* out */
            )
        {
            format = null;

            if (String.IsNullOrEmpty(fileName))
                return false;

            string fileExtension = Path.GetExtension(fileName);

            if (Utility.CompareFileNames(
                    fileExtension, FileExtension.StrongNameKey) == 0)
            {
                format = KeyFileFormat.RsaStrongName;
                return true;
            }

            if (Utility.CompareFileNames(
                    fileExtension, FileExtension.PrivateKey) == 0)
            {
                format = KeyFileFormat.RsaPrivateKey;
                return true;
            }

            return false;
        }
#endif
        #endregion
    }
}
