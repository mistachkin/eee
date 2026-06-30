/*
 * DsaKeyFile.cs --
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

#if NET_40
using System.Numerics;
#endif

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;

#if !NET_STANDARD_20
using DSAProvider = System.Security.Cryptography.DSACryptoServiceProvider;
#else
using DSAProvider = System.Security.Cryptography.DSA;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides low-level routines for reading, writing, and detecting DSA
    /// key blobs in the CryptoAPI and StrongName key file formats.
    /// </summary>
    [ObjectId("9746de4f-89af-4e06-abf8-6d4bbb0b3231")]
    internal static class DsaKeyFile
    {
        #region Private Constants
        /// <summary>
        /// The CryptoAPI algorithm class bits that identify a signature
        /// algorithm.
        /// </summary>
        private const uint ALG_CLASS_SIGNATURE = 0x2000;

        /// <summary>
        /// The CryptoAPI algorithm type bits that identify the DSS
        /// algorithm.
        /// </summary>
        private const uint ALG_TYPE_DSS = 0x200;

        /// <summary>
        /// The magic value that identifies a DSA public key blob.
        /// </summary>
        private const uint DSS1 = 0x31535344; // "DSS1" magic, PUBLICKEYBLOB

        /// <summary>
        /// The magic value that identifies a DSA private key blob.
        /// </summary>
        private const uint DSS2 = 0x32535344; // "DSS2" magic, PRIVATEKEYBLOB
        // private const uint DSS3 = 0x33535344; // "DSS3" magic, PUBLICKEYBLOB
        // private const uint DSS4 = 0x34535344; // "DSS4" magic, PRIVATEKEYBLOB

        /// <summary>
        /// The CryptoAPI algorithm identifier for the DSS signature
        /// algorithm.
        /// </summary>
        private const uint CALG_DSS_SIGN = ALG_CLASS_SIGNATURE | ALG_TYPE_DSS;

        /// <summary>
        /// The length, in bytes, of the DSA Q parameter.
        /// </summary>
        private const int Q_LENGTH = 20;

        /// <summary>
        /// The length, in bytes, of the DSA private key X parameter.
        /// </summary>
        private const int X_LENGTH = 20;

        /// <summary>
        /// The length, in bytes, of the DSA seed value.
        /// </summary>
        private const int SEED_LENGTH = 20;

        ///////////////////////////////////////////////////////////////////////

#if NET_40
        /// <summary>
        /// The bit mask used to detect whether the high bit of a byte is
        /// set.
        /// </summary>
        private const byte HighBit = 0x80;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
#if DEBUG
        //
        // HACK: Enable this bit to dump the DSA key parameters when read
        //       from a blob.
        //
        /// <summary>
        /// When non-zero, the DSA key parameters are dumped to the trace
        /// log when they are read from a blob.
        /// </summary>
        private static bool DumpForRead = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Enable this bit to dump the DSA key parameters when verify
        //       method is about to be used.
        //
        /// <summary>
        /// When non-zero, the DSA key parameters are dumped to the trace
        /// log when the verify method is about to be used.
        /// </summary>
        private static bool DumpForVerify = false;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // HACK: Enable this bit to dump the DSA key parameters when sign
        //       method is about to be used.
        //
        /// <summary>
        /// When non-zero, the DSA key parameters are dumped to the trace
        /// log when the sign method is about to be used.
        /// </summary>
        private static bool DumpForSign = false;
#endif
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region CryptoAPI Structures (Private)
        /// <summary>
        /// Represents the CryptoAPI DSSSEED structure that holds the counter
        /// and seed values used during DSA key generation.
        /// </summary>
        private struct DSSSEED // sizeof(DSSSEED) == 24
        {
            /// <summary>
            /// The counter value used during DSA key generation.
            /// </summary>
            public uint counter;

            /// <summary>
            /// The seed bytes used during DSA key generation.
            /// </summary>
            public byte[] seed; /* 20 */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents the CryptoAPI DSSPUBKEY structure that holds the DSA
        /// public key parameters.
        /// </summary>
        private struct DSSPUBLICKEY // sizeof(DSSPUBKEY) > 8, WinCrypt.h
        {
            /// <summary>
            /// Represents the fixed-size leading portion of the
            /// <see cref="DSSPUBLICKEY" /> structure.
            /// </summary>
            public struct FIXED // sizeof(DSSPUBLICKEY.FIXED) == 8
            {
                /// <summary>
                /// The magic value that identifies the type of the key
                /// blob.
                /// </summary>
                public uint magic;

                /// <summary>
                /// The length, in bits, of the DSA key.
                /// </summary>
                public uint bitLength;
            }

            /// <summary>
            /// The fixed-size header fields of this structure.
            /// </summary>
            public FIXED @fixed;

            /// <summary>
            /// The DSA prime modulus P parameter.
            /// </summary>
            public byte[] P;

            /// <summary>
            /// The DSA prime divisor Q parameter.
            /// </summary>
            public byte[] Q; /* 20 */

            /// <summary>
            /// The DSA generator G parameter.
            /// </summary>
            public byte[] G;

            /// <summary>
            /// The DSA public key Y parameter.
            /// </summary>
            public byte[] Y;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents the private key portion of a DSA key blob.
        /// </summary>
        private struct DSSPRIVATEKEY
        {
            /// <summary>
            /// The DSA private key X parameter.
            /// </summary>
            public byte[] X;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Diagnostic Methods (Private)
#if DEBUG
        /// <summary>
        /// Writes the specified DSA key parameters to the trace log for
        /// diagnostic purposes.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method, used to label the trace output.
        /// May be null.
        /// </param>
        /// <param name="parameters">
        /// The DSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the trace output.
        /// </param>
        private static void DumpParameters( /* CORE */
            string methodName,        /* in */
            DSAParameters parameters, /* in */
            TracePriority priority    /* in */
            )
        {
            string localMethodName;

            if (methodName != null)
                localMethodName = methodName;
            else
                localMethodName = "DumpParameters";

            CertificateTraceOps.DebugTrace(String.Format(
                "{0}: P = {1}, Q = {2}, G = {3}, Y = {4}, " +
                "X = {5}, J = {6}, Counter = {7}, Seed = {8}",
                localMethodName,
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.P)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Q)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.G)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Y)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.X)),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.J)),
                Utility.FormatWrapOrNull(parameters.Counter),
                Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatHexadecimal(parameters.Seed))),
                typeof(DsaKeyFile).Name, priority);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Read Methods (Private)
        /// <summary>
        /// Combines the public, private, and seed key blob structures into a
        /// single set of DSA key parameters, optionally reversing the byte
        /// order of each component.
        /// </summary>
        /// <param name="dsaPublicKey">
        /// The public key structure containing the P, Q, G, and Y values.
        /// </param>
        /// <param name="dsaPrivateKey">
        /// The private key structure containing the X value.
        /// </param>
        /// <param name="dsaSeed">
        /// The seed structure containing the counter and seed values.
        /// </param>
        /// <param name="reverse">
        /// Non-zero to reverse the byte order of each copied parameter.
        /// </param>
        /// <returns>
        /// The populated DSA key parameters.
        /// </returns>
        private static DSAParameters GetParameters( /* CORE */
            DSSPUBLICKEY dsaPublicKey,   /* in */
            DSSPRIVATEKEY dsaPrivateKey, /* in */
            DSSSEED dsaSeed,             /* in */
            bool reverse                 /* in */
            )
        {
            DSAParameters parameters = new DSAParameters();

            parameters.P = KeyFile.CopyAndMaybeReverse(
                dsaPublicKey.P, reverse);

            parameters.Q = KeyFile.CopyAndMaybeReverse(
                dsaPublicKey.Q, reverse);

            parameters.G = KeyFile.CopyAndMaybeReverse(
                dsaPublicKey.G, reverse);

            parameters.Y = KeyFile.CopyAndMaybeReverse(
                dsaPublicKey.Y, reverse);

            parameters.X = KeyFile.CopyAndMaybeReverse(
                dsaPrivateKey.X, reverse);

            parameters.Counter = unchecked((int)dsaSeed.counter);
            parameters.Seed = dsaSeed.seed;

            return parameters;
        }

        ///////////////////////////////////////////////////////////////////////

#if NET_40
        /// <summary>
        /// Appends a zero byte to the specified value when its high bit is
        /// set and the byte order is being reversed, ensuring the value is
        /// treated as positive.
        /// </summary>
        /// <param name="bytes">
        /// The value to inspect and possibly fix up.  May be null.
        /// </param>
        /// <param name="reverse">
        /// Non-zero if the byte order is being reversed; otherwise, the
        /// value is returned unchanged.
        /// </param>
        /// <returns>
        /// The original value, or a new value with an extra zero byte
        /// appended when the high bit was set.
        /// </returns>
        private static byte[] MaybeFixupNegative( /* CORE */
            byte[] bytes, /* in */
            bool reverse  /* in */
            )
        {
            if (!reverse)
                return bytes;

            if (bytes == null)
                return bytes;

            int length = bytes.Length;

            if (length == 0)
                return bytes;

            byte lastByte = bytes[length - 1];

            if ((lastByte & HighBit) == 0)
                return bytes;

            byte[] newBytes = new byte[length + 1]; // NOTE: Zeros.

            Array.Copy(bytes, newBytes, length); // NOTE: Last still zero.

            return newBytes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the DSA public key Y value from the P, G, and X key
        /// parameters using big integer modular exponentiation.
        /// </summary>
        /// <param name="parameters">
        /// The DSA key parameters containing the P, G, and X values.
        /// </param>
        /// <param name="reverse">
        /// Non-zero to reverse the byte order of the parameters before use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The computed public key bytes, or null if the public key could
        /// not be computed.
        /// </returns>
        private static byte[] GetPublicKeyViaBigInteger( /* CORE */
            DSAParameters parameters, /* in */
            bool reverse,             /* in */
            ref Result error          /* out */
            )
        {
            byte[] p = MaybeFixupNegative(KeyFile.CopyAndMaybeReverse(
                parameters.P, reverse), reverse);

            if (p == null)
            {
                error = "missing DSA parameter P for public key";
                return null;
            }

            byte[] g = MaybeFixupNegative(KeyFile.CopyAndMaybeReverse(
                parameters.G, reverse), reverse);

            if (g == null)
            {
                error = "missing DSA parameter G for public key";
                return null;
            }

            byte[] x = MaybeFixupNegative(KeyFile.CopyAndMaybeReverse(
                parameters.X, reverse), reverse);

            if (x == null)
            {
                error = "missing DSA parameter X for private key";
                return null;
            }

            try
            {
                BigInteger P = new BigInteger(p);
                BigInteger G = new BigInteger(g);
                BigInteger X = new BigInteger(x);
                BigInteger Y = BigInteger.ModPow(G, X, P);

                byte[] y = Y.ToByteArray();

                if (y == null)
                {
                    error = "public key was not available";
                    return null;
                }

                return y;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the DSA public key Y value by importing the specified
        /// parameters into a DSA provider and exporting the public key.
        /// </summary>
        /// <param name="parameters">
        /// The DSA key parameters to import into the provider.
        /// </param>
        /// <param name="reverse">
        /// Non-zero to reverse the byte order of the exported public key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The exported public key bytes, or null if the public key could
        /// not be obtained.
        /// </returns>
        private static byte[] GetPublicKeyViaProvider( /* CORE */
            DSAParameters parameters, /* in */
            bool reverse,             /* in */
            ref Result error          /* out */
            )
        {
            using (DSA dsa = CertificateSharedOps.CreateDsaProvider(
                    ref error))
            {
                if (dsa != null)
                {
                    dsa.ImportParameters(parameters); /* throw */
                    parameters = dsa.ExportParameters(false); /* throw */

                    byte[] y = KeyFile.CopyAndMaybeReverse(
                        parameters.Y, reverse);

                    if (y == null)
                    {
                        error = "public key was not available";
                        return null;
                    }

                    return y;
                }
                else
                {
                    return null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the DSA public key Y value, selecting the big integer or
        /// provider based implementation as appropriate for the current
        /// runtime.
        /// </summary>
        /// <param name="parameters">
        /// The DSA key parameters from which to derive the public key.
        /// </param>
        /// <param name="reverse">
        /// Non-zero to reverse the byte order of the resulting public key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The computed public key bytes, or null if the public key could
        /// not be obtained.
        /// </returns>
        private static byte[] GetPublicKey( /* CORE */
            DSAParameters parameters, /* in */
            bool reverse,             /* in */
            ref Result error          /* out */
            )
        {
#if DEBUG
            if (DumpForRead)
            {
                DumpParameters(
                    "GetPublicKey", parameters, TracePriority.Highest);
            }
#endif

#if NET_40
            if (Utility.IsDotNetCore())
            {
                return GetPublicKeyViaBigInteger(
                    parameters, reverse, ref error);
            }
            else
#endif
            {
                return GetPublicKeyViaProvider(
                    parameters, reverse, ref error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates a .NET Framework compatible public key token from the
        /// specified DSA key pair by hashing its public key blob fields.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair from which to generate the public key token.
        /// </param>
        /// <returns>
        /// The generated public key token, or null if the token could not be
        /// generated.
        /// </returns>
        private static byte[] MakePublicKeyToken( /* CORE */
            DsaKeyPair keyPair /* in */
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
            if ((keyPair == null) || (keyPair.Y == null))
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
            // NOTE: Add the fields from the DSSPUBLICKEY struct.
            //
            list.AddRange(BitConverter.GetBytes(
                DSS1)); /* HACK: Always public. */

            list.AddRange(BitConverter.GetBytes(keyPair.BitLength));

            //
            // NOTE: Add the public key modulus.
            //
            bool reverse = BitConverter.IsLittleEndian;

            list.AddRange(new ByteList(keyPair.Y, reverse));

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
                        typeof(DsaKeyFile).Name, TracePriority.MediumLow);
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
        /// Dumps the specified DSA key parameters to the trace log prior to a
        /// verify operation when the corresponding diagnostic flag is
        /// enabled.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method, used to label the trace output.
        /// </param>
        /// <param name="parameters">
        /// The DSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the trace output.
        /// </param>
        public static void MaybeDumpVerifyParameters( /* CORE */
            string methodName,        /* in */
            DSAParameters parameters, /* in */
            TracePriority priority    /* in */
            )
        {
            if (DumpForVerify)
                DumpParameters(methodName, parameters, priority);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Dumps the specified DSA key parameters to the trace log prior to a
        /// sign operation when the corresponding diagnostic flag is enabled.
        /// </summary>
        /// <param name="methodName">
        /// The name of the calling method, used to label the trace output.
        /// </param>
        /// <param name="parameters">
        /// The DSA key parameters to dump.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use when emitting the trace output.
        /// </param>
        public static void MaybeDumpSignParameters(
            string methodName,        /* in */
            DSAParameters parameters, /* in */
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
        /// Reads a DSA key blob from the specified binary reader and
        /// populates a key pair with the public and/or private key data.
        /// </summary>
        /// <param name="binaryReader">
        /// The binary reader from which to read the key blob.
        /// </param>
        /// <param name="format">
        /// The key file format describing how the blob is laid out.
        /// </param>
        /// <param name="publicKeyToken">
        /// An optional public key token to associate with the key pair.  If
        /// null, a token is calculated from the public key.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to read and populate the public key portion of the key
        /// pair.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to read and populate the private key portion of the key
        /// pair.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the populated key pair.
        /// </param>
        /// <param name="result">
        /// Upon success, receives status information; upon failure, receives
        /// information about the error.
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
                    //       this value should always be CALG_DSS_SIGN.  It
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

                    publicKeyBlob.signatureAlgorithmId = CALG_DSS_SIGN;
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
                if (blobHeader.algorithm != CALG_DSS_SIGN)
                {
                    result = String.Format(
                        "unsupported key algorithm {0}",
                        Utility.FormatWrapOrNull(blobHeader.algorithm));

                    return ReturnCode.Error;
                }

                DSSPUBLICKEY dsaPublicKey = new DSSPUBLICKEY();
                DSSPRIVATEKEY dsaPrivateKey;

#if MONO_BUILD
                //
                // HACK: *MONO* The Mono C# compiler gives a warning unless
                //       this field is manually initialized.
                //
                dsaPublicKey.@fixed = new DSSPUBLICKEY.FIXED();
#endif

                //
                // NOTE: Read the "magic", this value must be DSS1 or DSS2.
                //
                dsaPublicKey.@fixed.magic = binaryReader.ReadUInt32();

                //
                // NOTE: Make sure the "magic" is DSS1 or DSS2 and that it
                //       matches what we expected to find based on the blob
                //       header type.
                //
                if (((blobHeader.type != KeyFile.PUBLICKEYBLOB) ||
                        (dsaPublicKey.@fixed.magic != DSS1)) &&
                    ((blobHeader.type != KeyFile.PRIVATEKEYBLOB) ||
                        (dsaPublicKey.@fixed.magic != DSS2)))
                {
                    result = String.Format(
                        "invalid key magic or mismatch between " +
                        "key type {0} and key magic {1}",
                        Utility.FormatWrapOrNull(blobHeader.type),
                        Utility.FormatWrapOrNull(dsaPublicKey.@fixed.magic));

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
                // NOTE: Ok, now what is the byte length?
                //
                int byteLength = (int)(bitLength /
                    KeyFile.WHOLE_BYTE_DIVISOR);

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
                        (uint)Marshal.SizeOf(typeof(DSSPUBLICKEY.FIXED)) +
                        (uint)byteLength;
                }

                //
                // NOTE: Read the remaining public and private key fields.
                //
                dsaPublicKey.@fixed.bitLength = bitLength;
                dsaPublicKey.P = binaryReader.ReadBytes(byteLength);
                dsaPublicKey.Q = binaryReader.ReadBytes(Q_LENGTH);
                dsaPublicKey.G = binaryReader.ReadBytes(byteLength);
                dsaPrivateKey.X = null;

                //
                // NOTE: Is this a private key blob?
                //
                if (dsaPublicKey.@fixed.magic == DSS2)
                {
                    //
                    // NOTE: Read the private key field now.
                    //
                    dsaPrivateKey.X = binaryReader.ReadBytes(X_LENGTH);
                }
                else
                {
                    //
                    // NOTE: Read the public key field now.
                    //
                    dsaPublicKey.Y = binaryReader.ReadBytes(byteLength);
                }

                DSSSEED dsaSeed;

                dsaSeed.counter = binaryReader.ReadUInt32();
                dsaSeed.seed = binaryReader.ReadBytes(SEED_LENGTH);

                //
                // NOTE: Is this a private key blob?  If so, we now
                // need to synthesize a public key for it.
                //
                bool reverse = BitConverter.IsLittleEndian;

                if (dsaPublicKey.@fixed.magic == DSS2)
                {
                    //
                    // HACK: The public key is absolutely required;
                    //       therefore, always attempt to obtain it
                    //       using all the key data we just read.
                    //
                    dsaPublicKey.Y = GetPublicKey(GetParameters(
                        dsaPublicKey, dsaPrivateKey, dsaSeed, reverse),
                        reverse, ref result);

                    if (dsaPublicKey.Y == null)
                        return ReturnCode.Error;
                }

                //
                // NOTE: Create new object to hold the key information.
                //
                DsaKeyPair localKeyPair = new DsaKeyPair();

                //
                // NOTE: Mark key pair with the correct file metadata.
                //
                localKeyPair.KeyPairType = KeyPairType.DSA;
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
                // NOTE: Copy DSSSEED fields.
                //
                localKeyPair.Counter = dsaSeed.counter;
                localKeyPair.Seed = dsaSeed.seed;

                //
                // NOTE: If requested and available, copy over private
                //       key info.
                //
                if (privateKey &&
                    (blobHeader.type == KeyFile.PRIVATEKEYBLOB) &&
                    (dsaPublicKey.@fixed.magic == DSS2))
                {
                    localKeyPair.X = KeyFile.CopyAndMaybeReverse(
                        dsaPrivateKey.X, reverse);

                    localKeyPair.HavePrivateKey = true;
                }

                //
                // NOTE: If requested, copy over public key info.
                //
                if (publicKey)
                {
                    localKeyPair.Magic = dsaPublicKey.@fixed.magic;
                    localKeyPair.BitLength = dsaPublicKey.@fixed.bitLength;

                    //
                    // NOTE: Read the public key parameters.
                    //
                    localKeyPair.P = KeyFile.CopyAndMaybeReverse(
                        dsaPublicKey.P, reverse);

                    localKeyPair.Q = KeyFile.CopyAndMaybeReverse(
                        dsaPublicKey.Q, reverse);

                    localKeyPair.G = KeyFile.CopyAndMaybeReverse(
                        dsaPublicKey.G, reverse);

                    localKeyPair.Y = KeyFile.CopyAndMaybeReverse(
                        dsaPublicKey.Y, reverse);

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
        /// Writes the public and/or private key data from the specified key
        /// pair to the given binary writer as a DSA key blob.
        /// </summary>
        /// <param name="binaryWriter">
        /// The binary writer to which the key blob is written.
        /// </param>
        /// <param name="format">
        /// The key file format describing how the blob is laid out.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to write the public key header fields of the key pair.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to write the private key value; otherwise, the public key
        /// value is written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair containing the data to write.
        /// </param>
        /// <param name="result">
        /// Upon success, receives status information; upon failure, receives
        /// information about the error.
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

            DsaKeyPair localKeyPair = keyPair as DsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an DSA key pair";
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

                bool reverse = BitConverter.IsLittleEndian;

                binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                    localKeyPair.P, reverse));

                binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                    localKeyPair.Q, reverse));

                binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                    localKeyPair.G, reverse));

                if (privateKey)
                {
                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.X, reverse));
                }
                else
                {
                    binaryWriter.Write(KeyFile.CopyAndMaybeReverse(
                        localKeyPair.Y, reverse));
                }

                binaryWriter.Write(localKeyPair.Counter);
                binaryWriter.Write(localKeyPair.Seed);

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
        /// Determines whether the specified bytes contain a recognized DSA
        /// key blob magic value at the given offset and, if so, updates the
        /// key file format accordingly.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to inspect.  May be null.
        /// </param>
        /// <param name="startIndex">
        /// The offset within <paramref name="bytes" /> at which to read the
        /// magic value.
        /// </param>
        /// <param name="format">
        /// On input, the current key file format, if any; on output, the
        /// format updated with the detected flags when a match is found.
        /// </param>
        /// <returns>
        /// Non-zero if a recognized magic value was found; otherwise, zero.
        /// </returns>
        public static bool MatchMagic( /* CORE? */
            byte[] bytes,             /* in */
            int startIndex,           /* in */
            ref KeyFileFormat? format /* in, out */
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

            if ((value == DSS1) || (value == DSS2))
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
        /// Determines whether the specified file name has a recognized DSA
        /// key file extension and, if so, reports the corresponding key file
        /// format.
        /// </summary>
        /// <param name="fileName">
        /// The file name whose extension is to be examined.
        /// </param>
        /// <param name="format">
        /// Upon return, receives the detected key file format, or null if the
        /// file name was not recognized.
        /// </param>
        /// <returns>
        /// Non-zero if the file name has a recognized extension; otherwise,
        /// zero.
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
                    fileExtension, FileExtension.DsaStrongNameKey) == 0)
            {
                format = KeyFileFormat.DsaStrongName;
                return true;
            }

            if (Utility.CompareFileNames(
                    fileExtension, FileExtension.DsaPrivateKey) == 0)
            {
                format = KeyFileFormat.DsaPrivateKey;
                return true;
            }

            return false;
        }
#endif
        #endregion
    }
}
