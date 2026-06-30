/*
 * KeyFile.cs --
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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
using MagicPair =
    System.Collections.Generic.KeyValuePair<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.MagicCallback>;

using MagicDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.MagicCallback>;
#endif

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
using FileNamePair =
    System.Collections.Generic.KeyValuePair<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.FileNameCallback>;

using FileNameDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.FileNameCallback>;
#endif

using ReadBlobDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.ReadBlobCallback>;

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
using WriteBlobDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.WriteBlobCallback>;

using FormatDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        Licensing.Components.Private.KeyFileFormat>;

using TypeDictionary =
    System.Collections.Generic.Dictionary<
        Licensing.Components.Private.KeyPairType,
        System.Type>;
#endif

namespace Licensing.Components.Private
{
    #region Low-Level Auto-Detection Support Delegates
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
    /// <summary>
    /// Represents a callback used to detect whether a block of bytes contains
    /// the "magic" signature for a particular key pair type, optionally
    /// reporting the detected key file format.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to be examined for the magic signature.
    /// </param>
    /// <param name="startIndex">
    /// The offset within <paramref name="bytes" /> at which to begin
    /// looking for the magic signature.
    /// </param>
    /// <param name="format">
    /// Upon success, receives the detected key file format.
    /// </param>
    /// <returns>
    /// Non-zero if the magic signature was detected; otherwise, zero.
    /// </returns>
    [ObjectId("4df2e7d7-30f8-47ed-9a93-b46a4feed6e0")]
    internal delegate bool MagicCallback(
        byte[] bytes,             /* in */
        int startIndex,           /* in */
        ref KeyFileFormat? format /* in, out */
    );
#endif

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
    /// <summary>
    /// Represents a callback used to detect a key pair type based on the name
    /// of a file, optionally reporting the detected key file format.
    /// </summary>
    /// <param name="fileName">
    /// The name of the file to be examined.
    /// </param>
    /// <param name="format">
    /// Upon success, receives the detected key file format.
    /// </param>
    /// <returns>
    /// Non-zero if the file name matched this key pair type; otherwise, zero.
    /// </returns>
    [ObjectId("f65fb521-8e3b-4450-8a28-d7fb2af543c9")]
    internal delegate bool FileNameCallback(
        string fileName,          /* in */
        out KeyFileFormat? format /* out */
    );
#endif
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Low-Level Read/Write Support Delegates
    /// <summary>
    /// Represents a callback used to read a key pair from a binary blob of a
    /// particular key file format.
    /// </summary>
    /// <param name="binaryReader">
    /// The binary reader from which the key data will be read.
    /// </param>
    /// <param name="format">
    /// The key file format of the blob being read.
    /// </param>
    /// <param name="publicKeyToken">
    /// The public key token associated with the key pair, if any.
    /// </param>
    /// <param name="publicKey">
    /// Non-zero if the public key portion should be read.
    /// </param>
    /// <param name="privateKey">
    /// Non-zero if the private key portion should be read.
    /// </param>
    /// <param name="keyPair">
    /// Upon success, receives the key pair that was read.
    /// </param>
    /// <param name="result">
    /// Upon success, receives an optional result; otherwise, receives an
    /// error message.
    /// </param>
    /// <returns>
    /// <see cref="ReturnCode.Ok" /> on success; otherwise,
    /// <see cref="ReturnCode.Error" />.
    /// </returns>
    [ObjectId("674c5818-d6b3-4835-af3e-e35d32657c61")]
    internal delegate ReturnCode ReadBlobCallback(
        BinaryReader binaryReader, /* in */
        KeyFileFormat format,      /* in */
        byte[] publicKeyToken,     /* in */
        bool publicKey,            /* in */
        bool privateKey,           /* in */
        ref IKeyPair keyPair,      /* in, out */
        ref Result result          /* out */
    );

    ///////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
    /// <summary>
    /// Represents a callback used to write a key pair to a binary blob of a
    /// particular key file format.
    /// </summary>
    /// <param name="binaryWriter">
    /// The binary writer to which the key data will be written.
    /// </param>
    /// <param name="format">
    /// The key file format of the blob being written.
    /// </param>
    /// <param name="publicKey">
    /// Non-zero if the public key portion should be written.
    /// </param>
    /// <param name="privateKey">
    /// Non-zero if the private key portion should be written.
    /// </param>
    /// <param name="keyPair">
    /// The key pair to be written.
    /// </param>
    /// <param name="result">
    /// Upon success, receives an optional result; otherwise, receives an
    /// error message.
    /// </param>
    /// <returns>
    /// <see cref="ReturnCode.Ok" /> on success; otherwise,
    /// <see cref="ReturnCode.Error" />.
    /// </returns>
    [ObjectId("60117314-e81b-441e-bb48-f528791486a5")]
    internal delegate ReturnCode WriteBlobCallback(
        BinaryWriter binaryWriter, /* in */
        KeyFileFormat format,      /* in */
        bool publicKey,            /* in */
        bool privateKey,           /* in */
        IKeyPair keyPair,          /* in */
        ref Result result          /* out */
    );
#endif
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region StrongName Structures (Public)
    //
    // NOTE: This struct may be StrongName specific?
    //
    /// <summary>
    /// Represents the public key blob header used by the StrongName
    /// subsystem.
    /// </summary>
    internal struct PublicKeyBlob // sizeof(PublicKeyBlob) == 12, StrongName.h
    {
        /// <summary>
        /// The algorithm identifier used for signing.
        /// </summary>
        public uint signatureAlgorithmId; // algorithm Id for signing
        /// <summary>
        /// The algorithm identifier used for hashing.
        /// </summary>
        public uint hashAlgorithmId;      // algorithm Id for hashing
        /// <summary>
        /// The count of bytes remaining in the blob.
        /// </summary>
        public uint byteCount;            // count of bytes remaining
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region CryptoAPI Structures (Public)
    /// <summary>
    /// Represents the CryptoAPI key blob header as defined by WinCrypt.h.
    /// </summary>
    internal struct BLOBHEADER // sizeof(BLOBHEADER) == 8, WinCrypt.h
    {
        /// <summary>
        /// The type of the key blob.
        /// </summary>
        public byte type;
        /// <summary>
        /// The version of the key blob format.
        /// </summary>
        public byte version;
        /// <summary>
        /// Reserved; must be zero.
        /// </summary>
        public ushort reserved;
        /// <summary>
        /// The algorithm identifier associated with the key.
        /// </summary>
        public uint algorithm;
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides low-level support for reading and writing key pair files in
    /// various formats, including auto-detection of the key pair type.
    /// </summary>
    [ObjectId("048bfb8b-fc7a-4f0e-89e9-75719ab3ab0a")]
    internal static class KeyFile
    {
        #region Public Constants
        /// <summary>
        /// The CryptoAPI blob type value that identifies a public key blob.
        /// </summary>
        public const byte PUBLICKEYBLOB = 6;
        /// <summary>
        /// The CryptoAPI blob type value that identifies a private key blob.
        /// </summary>
        public const byte PRIVATEKEYBLOB = 7;

        /// <summary>
        /// The current CryptoAPI blob version.
        /// </summary>
        public const byte CUR_BLOB_VERSION = 2;

        /// <summary>
        /// The CryptoAPI algorithm class bits identifying a signature
        /// algorithm.
        /// </summary>
        public const uint ALG_CLASS_SIGNATURE = 0x2000;
        /// <summary>
        /// The CryptoAPI algorithm class bits identifying a hash algorithm.
        /// </summary>
        public const uint ALG_CLASS_HASH = 0x8000;
        /// <summary>
        /// The CryptoAPI algorithm class bits identifying a key exchange
        /// algorithm.
        /// </summary>
        public const uint ALG_CLASS_KEY_EXCHANGE = 0xA000;

        /// <summary>
        /// The CryptoAPI algorithm type bits indicating any algorithm type.
        /// </summary>
        public const uint ALG_TYPE_ANY = 0;

        /// <summary>
        /// The CryptoAPI algorithm sub-identifier bits for the SHA-1
        /// algorithm.
        /// </summary>
        public const uint ALG_SID_SHA1 = 0x4;

        /// <summary>
        /// The CryptoAPI algorithm identifier for the SHA-1 hash algorithm.
        /// </summary>
        public const uint CALG_SHA1 = ALG_CLASS_HASH | ALG_TYPE_ANY |
                                      ALG_SID_SHA1;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of bits in a whole byte, used as a divisor when
        /// converting between bits and bytes.
        /// </summary>
        public const int WHOLE_BYTE_DIVISOR = 8;
        /// <summary>
        /// The number of bits in a half byte (nibble) pair, used as a divisor
        /// when converting between bits and bytes.
        /// </summary>
        public const int HALF_BYTE_DIVISOR = 16;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The byte offset, within a key blob, at which the CryptoAPI magic
        /// signature is located.
        /// </summary>
        public const int MAGIC_CRYPTOAPI_OFFSET = 8;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the dictionaries of
        //       various key pair type callbacks, below.
        //
        /// <summary>
        /// Used to synchronize access to the dictionaries of key pair type
        /// callbacks.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        //
        // NOTE: This dictionary maps key pair types to their associated
        //       key pair type (magic) matching callbacks.
        //
        /// <summary>
        /// Maps key pair types to their associated magic matching callbacks.
        /// </summary>
        private static MagicDictionary magicCallbacks;
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This dictionary maps key pair types to their associated
        //       key pair type (file name) matching callbacks.
        //
        /// <summary>
        /// Maps key pair types to their associated file name matching
        /// callbacks.
        /// </summary>
        private static FileNameDictionary fileNameCallbacks;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This dictionary maps key pair types to their associated
        //       key pair type (blob) reading callbacks.
        //
        /// <summary>
        /// Maps key pair types to their associated blob reading callbacks.
        /// </summary>
        private static ReadBlobDictionary readBlobCallbacks;

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        //
        // NOTE: This dictionary maps key pair types to their associated
        //       key pair type (blob) writing callbacks.
        //
        /// <summary>
        /// Maps key pair types to their associated blob writing callbacks.
        /// </summary>
        private static WriteBlobDictionary writeBlobCallbacks;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This dictionary maps key pair types to their associated
        //       key file formats.
        //
        /// <summary>
        /// Maps key pair types to their associated key file formats.
        /// </summary>
        private static FormatDictionary formats;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This dictionary maps key pair types to their associated
        //       system types.
        //
        /// <summary>
        /// Maps key pair types to their associated system types.
        /// </summary>
        private static TypeDictionary types;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region PVK Constants (Private)
        /// <summary>
        /// The magic value found at the start of a PVK file header.
        /// </summary>
        private const uint PVK_MAGIC = 0xB0B5F11E; // PVK header magic

        /// <summary>
        /// The required value of the reserved field in a PVK file header.
        /// </summary>
        private const uint PVK_RESERVED = 0;

        /// <summary>
        /// The PVK key type value indicating a key exchange key.
        /// </summary>
        private const uint PVK_TYPE_KEYX = 1; // implies algorithm type CALG_XXX_KEYX
        /// <summary>
        /// The PVK key type value indicating a signing key.
        /// </summary>
        private const uint PVK_TYPE_SIGN = 2; // implies algorithm type CALG_XXX_SIGN

        /// <summary>
        /// The PVK encrypted flag value indicating an unencrypted private key.
        /// </summary>
        private const uint PVK_ENCRYPTED_NO = 0;
        /// <summary>
        /// The PVK encrypted flag value indicating an encrypted private key.
        /// </summary>
        private const uint PVK_ENCRYPTED_YES = 1;

        /// <summary>
        /// The PVK salt length value indicating that no salt is present.
        /// </summary>
        private const uint PVK_SALT_NO = 0;
        /// <summary>
        /// The PVK salt length, in bytes, used when the private key is
        /// encrypted.
        /// </summary>
        private const uint PVK_SALT_YES = 16;

        /// <summary>
        /// The number of key bytes used for "weak" (40-bit) PVK encryption.
        /// </summary>
        private const int PVK_WEAK_KEY_LENGTH = 5;
        /// <summary>
        /// The number of key bytes used for "strong" (128-bit) PVK
        /// encryption.
        /// </summary>
        private const int PVK_STRONG_KEY_LENGTH = 16;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region PVK Structures (Private)
        //
        // NOTE: This struct is PVK file specific.
        //
        /// <summary>
        /// Represents the header of a PVK (private key) file.
        /// </summary>
        private struct PvkHeader // sizeof(PvkHeader) == 24
        {
            /// <summary>
            /// The magic value; always 0xB0B5F11E.
            /// </summary>
            public uint magic;      // always the value 0xB0B5F11E
            /// <summary>
            /// Reserved; must be zero.
            /// </summary>
            public uint reserved;   // reserved, must be zero
            /// <summary>
            /// The key type; 1 for an exchange key, 2 for a signing key (must
            /// match the BLOBHEADER).
            /// </summary>
            public uint type;       // 1 = exchange key, 2 = signing key (must match BLOBHEADER).
            /// <summary>
            /// Non-zero if the private key is encrypted.
            /// </summary>
            public uint encrypted;  // non-zero for encrypted private key.
            /// <summary>
            /// The length of the salt; 0x10 when encrypted, zero otherwise.
            /// </summary>
            public uint saltLength; // length of salt, 0x10 for encrypted, zero otherwise.
            /// <summary>
            /// The length of the key data.
            /// </summary>
            public uint keyLength;  // length of key data.
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This struct is PVK file specific.
        //
        /// <summary>
        /// Represents the contents of a PVK (private key) file, including its
        /// header and optional salt.
        /// </summary>
        private struct PvkKeyBlob // sizeof(PvkKeyBlob) >= 44
        {
            /// <summary>
            /// The PVK file header.
            /// </summary>
            public PvkHeader header;
            /// <summary>
            /// The salt used when the private key is encrypted.
            /// </summary>
            public byte[] salt;
            // public BLOBHEADER blobHeader;
            // public [RD]SAPUBLICKEY publicKey;
            // public [RD]SAPRIVATEKEY privateKey;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Low-Level Read/Write Support Methods (Public)
        /// <summary>
        /// Creates a copy of the specified byte array, optionally reversing
        /// the order of the bytes in the copy.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to be copied.
        /// </param>
        /// <param name="reverse">
        /// Non-zero to reverse the order of the bytes in the returned copy.
        /// </param>
        /// <returns>
        /// A new array containing the copied (and possibly reversed) bytes, or
        /// null if <paramref name="bytes" /> is null.
        /// </returns>
        public static byte[] CopyAndMaybeReverse( /* CORE */
            byte[] bytes, /* in */
            bool reverse  /* in */
            )
        {
            if (bytes == null)
                return null;

            int length = bytes.Length;
            byte[] localBytes = new byte[length];

            Array.Copy(bytes, localBytes, length);

            if (reverse)
                Array.Reverse(localBytes);

            return localBytes;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region RC4 Support Methods (Private)
        /// <summary>
        /// Exchanges the values of the two specified bytes.
        /// </summary>
        /// <param name="X">
        /// The first byte; on return, receives the original value of
        /// <paramref name="Y" />.
        /// </param>
        /// <param name="Y">
        /// The second byte; on return, receives the original value of
        /// <paramref name="X" />.
        /// </param>
        private static void Swap( /* CORE */
            ref byte X, /* in, out */
            ref byte Y  /* in, out */
            )
        {
            byte swap = X; X = Y; Y = swap;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Derives an RC4 key from the specified salt and password by hashing
        /// them together using SHA-1.
        /// </summary>
        /// <param name="salt">
        /// The optional salt bytes to be hashed.
        /// </param>
        /// <param name="password">
        /// The optional password to be hashed.
        /// </param>
        /// <param name="key">
        /// Upon success, receives the derived key bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode DeriveKey( /* CORE */
            byte[] salt,     /* in: OPTIONAL */
            string password, /* in: OPTIONAL */
            ref byte[] key,  /* out */
            ref Result error /* out: NOT USED */
            )
        {
            int saltLength = (salt != null) ? salt.Length : 0;

            byte[] passwordBytes = (password != null) ?
                Encoding.ASCII.GetBytes(password) : null;

            int passwordLength = (passwordBytes != null) ?
                passwordBytes.Length : 0;

            using (SHA1 sha1 = SHA1.Create())
            {
                if (salt != null)
                    sha1.TransformBlock(salt, 0, saltLength, salt, 0);

                if (passwordBytes != null)
                    sha1.TransformFinalBlock(passwordBytes, 0, passwordLength);
                else
                    sha1.TransformFinalBlock(new byte[0], 0, 0);

                key = CopyAndMaybeReverse(sha1.Hash, false);
                sha1.Clear();

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the specified data in place using the RC4
        /// stream cipher with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key bytes used to drive the RC4 key schedule.
        /// </param>
        /// <param name="data">
        /// The data to be encrypted or decrypted in place.
        /// </param>
        /// <param name="offset">
        /// The offset within <paramref name="data" /> at which to begin
        /// processing.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode RC4( /* CORE */
            byte[] key,      /* in */
            byte[] data,     /* in, out */
            int offset,      /* in */
            ref Result error /* out */
            )
        {
            if (key == null)
            {
                error = "invalid key";
                return ReturnCode.Error;
            }

            if (data == null)
            {
                error = "invalid data";
                return ReturnCode.Error;
            }

            //
            // NOTE: Allocate space for the state array.
            //
            byte[] S = new byte[byte.MaxValue + 1];

            //
            // NOTE: Initialize the state array.
            //
            for (int i = 0; i <= byte.MaxValue; i++)
                S[i] = (byte)i;

            //
            // NOTE: Get the total key length (in bytes).
            //
            int keyLength = key.Length;

            //
            // NOTE: Modify the state array in accordance with the key
            //       schedule.
            //
            for (int i = 0, j = 0; i <= byte.MaxValue; i++)
            {
                j = (j + S[i] + key[i % keyLength]) % (byte.MaxValue + 1);

                Swap(ref S[i], ref S[j]);
            }

            //
            // NOTE: Get the total number of bytes to encrypt or decrypt.
            //
            int dataLength = data.Length - offset;

            //
            // NOTE: Perform the actual encryption or decryption.
            //
            for (int i = 0, j = 0, k = 0; i < dataLength; i++)
            {
                j = (j + 1) % (byte.MaxValue + 1);
                k = (S[j] + k) % (byte.MaxValue + 1);

                Swap(ref S[j], ref S[k]);

                int m = (S[j] + S[k]) % (byte.MaxValue + 1);

                data[offset + i] ^= S[m];
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Generates the specified number of random salt bytes.
        /// </summary>
        /// <param name="length">
        /// The number of salt bytes to generate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// The generated salt bytes, or null if the salt could not be
        /// generated.
        /// </returns>
        private static byte[] GenerateSalt(
            int length,      /* in */
            ref Result error /* out */
            )
        {
            if (length <= 0)
            {
                error = "invalid salt length";
                return null;
            }

            byte[] bytes;

            try
            {
                bytes = new byte[length]; /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }

            if (Utility.GetRandomBytes(
                    null, ref bytes, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return bytes;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region PVK Support Methods (Private)
        /// <summary>
        /// Decrypts an RC4-encrypted blob and reads the resulting key pair
        /// using the specified callback.
        /// </summary>
        /// <param name="callback">
        /// The callback used to read the key pair from the decrypted blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being read.
        /// </param>
        /// <param name="key">
        /// The RC4 key used to decrypt the blob.
        /// </param>
        /// <param name="data">
        /// The encrypted blob data.
        /// </param>
        /// <param name="publicKeyToken">
        /// The optional public key token associated with the key pair.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be read.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be read.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was read.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode ReadRc4Encrypted( /* CORE */
            ReadBlobCallback callback, /* in */
            KeyFileFormat format,      /* in */
            byte[] key,                /* in */
            byte[] data,               /* in, out */
            byte[] publicKeyToken,     /* in: OPTIONAL */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            byte[] localData = CopyAndMaybeReverse(data, false);

            if (RC4(
                    key, localData, Marshal.SizeOf(typeof(BLOBHEADER)),
                    ref result) == ReturnCode.Ok)
            {
                using (BinaryReader binaryReader = new BinaryReader(
                        new MemoryStream(localData)))
                {
                    if (callback(
                            binaryReader, format, publicKeyToken,
                            publicKey, privateKey, ref keyPair,
                            ref result) == ReturnCode.Ok)
                    {
                        return ReturnCode.Ok;
                    }
                }
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Writes a key pair to an in-memory blob using the specified
        /// callback.
        /// </summary>
        /// <param name="callback">
        /// The callback used to write the key pair to the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being written.
        /// </param>
        /// <param name="data">
        /// Upon success, receives the bytes of the written blob.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be written.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to be written.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode WriteBlob(
            WriteBlobCallback callback, /* in */
            KeyFileFormat format,       /* in */
            ref byte[] data,            /* out */
            bool publicKey,             /* in */
            bool privateKey,            /* in */
            IKeyPair keyPair,           /* in */
            ref Result result           /* out */
            )
        {
            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(
                        memoryStream))
                {
                    if (callback(
                            binaryWriter, format, publicKey,
                            privateKey, keyPair,
                            ref result) == ReturnCode.Ok)
                    {
                        data = memoryStream.ToArray();
                        return ReturnCode.Ok;
                    }
                }
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a key pair to an in-memory blob and then encrypts that blob
        /// in place using the RC4 stream cipher.
        /// </summary>
        /// <param name="callback">
        /// The callback used to write the key pair to the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being written.
        /// </param>
        /// <param name="key">
        /// The RC4 key used to encrypt the blob.
        /// </param>
        /// <param name="data">
        /// Upon success, receives the bytes of the encrypted blob.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be written.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to be written.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode WriteRc4Encrypted(
            WriteBlobCallback callback, /* in */
            KeyFileFormat format,       /* in */
            byte[] key,                 /* in */
            ref byte[] data,            /* out */
            bool publicKey,             /* in */
            bool privateKey,            /* in */
            IKeyPair keyPair,           /* in */
            ref Result result           /* out */
            )
        {
            try
            {
                byte[] localData = null;

                if (WriteBlob(
                        callback, format, ref localData,
                        publicKey, privateKey, keyPair,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (RC4(
                        key, localData, Marshal.SizeOf(typeof(BLOBHEADER)),
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                data = localData;
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a key pair from a PVK (private key) file, validating its
        /// header and decrypting its contents when necessary.
        /// </summary>
        /// <param name="callback">
        /// The callback used to read the key pair from the (decrypted) blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being read.
        /// </param>
        /// <param name="binaryReader">
        /// The binary reader from which the PVK data will be read.
        /// </param>
        /// <param name="password">
        /// The optional password used to derive the decryption key.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token associated with the key pair, if any.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be read.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be read.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was read.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode ReadPvk( /* CORE */
            ReadBlobCallback callback, /* in */
            KeyFileFormat format,      /* in */
            BinaryReader binaryReader, /* in */
            string password,           /* in: OPTIONAL */
            byte[] publicKeyToken,     /* in */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            if (binaryReader == null)
            {
                result = "invalid binary reader";
                return ReturnCode.Error;
            }

            try
            {
                PvkKeyBlob pvkKeyBlob;

#if MONO_BUILD
                //
                // HACK: *MONO* The Mono C# compiler gives a warning unless
                //       this field is manually initialized.
                //
                pvkKeyBlob.header = new PvkHeader();
#endif

                //
                // NOTE: Read the magic value from the PVK header.  It must
                //       match the magic value we know about.
                //
                pvkKeyBlob.header.magic = binaryReader.ReadUInt32();

                if (pvkKeyBlob.header.magic != PVK_MAGIC)
                {
                    result = String.Format(
                        "invalid key magic {0}",
                        Utility.FormatWrapOrNull(pvkKeyBlob.header.magic));

                    return ReturnCode.Error;
                }

                //
                // NOTE: We purposely ignore the reserved field.  It must be
                //       zero.
                //
                pvkKeyBlob.header.reserved = binaryReader.ReadUInt32();

                //
                // NOTE: Read the key type from the PVK header.  This value
                //       must match the key algorithm field in the BLOBHEADER
                //       structure.
                //
                pvkKeyBlob.header.type = binaryReader.ReadUInt32();

                if ((pvkKeyBlob.header.type != PVK_TYPE_KEYX) &&
                    (pvkKeyBlob.header.type != PVK_TYPE_SIGN))
                {
                    result = String.Format(
                        "invalid key type {0}",
                        Utility.FormatWrapOrNull(pvkKeyBlob.header.type));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Read the encrypted flag from the PVK header.
                //
                pvkKeyBlob.header.encrypted = binaryReader.ReadUInt32();

                if ((pvkKeyBlob.header.encrypted != PVK_ENCRYPTED_NO) &&
                    (pvkKeyBlob.header.encrypted != PVK_ENCRYPTED_YES))
                {
                    result = String.Format(
                        "invalid key encrypted value {0}",
                        Utility.FormatWrapOrNull(pvkKeyBlob.header.encrypted));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Read the number of salt bytes from the PVK header.
                //
                pvkKeyBlob.header.saltLength = binaryReader.ReadUInt32();

                if ((pvkKeyBlob.header.saltLength != PVK_SALT_NO) &&
                    (pvkKeyBlob.header.saltLength != PVK_SALT_YES))
                {
                    result = String.Format(
                        "invalid key salt length {0}",
                        Utility.FormatWrapOrNull(pvkKeyBlob.header.saltLength));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Read the number of data bytes from the PVK header.
                //
                pvkKeyBlob.header.keyLength = binaryReader.ReadUInt32();

                if (pvkKeyBlob.header.keyLength == 0)
                {
                    result = String.Format(
                        "invalid key length {0}",
                        Utility.FormatWrapOrNull(pvkKeyBlob.header.keyLength));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Is the PVK data encrypted?
                //
                if ((pvkKeyBlob.header.encrypted != PVK_ENCRYPTED_YES) ||
                    (pvkKeyBlob.header.saltLength == 0))
                {
                    //
                    // NOTE: No, so read all the remaining key data using
                    //       our standard routine.
                    //
                    return callback(
                        binaryReader, format, publicKeyToken, publicKey,
                        privateKey, ref keyPair, ref result);
                }

                pvkKeyBlob.salt = binaryReader.ReadBytes(
                    (int)pvkKeyBlob.header.saltLength);

                byte[] key = null;

                if (DeriveKey(
                        pvkKeyBlob.salt, password, ref key,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                int keyLength = key.Length;

                if (keyLength < PVK_STRONG_KEY_LENGTH)
                {
                    result = String.Format(
                        "cannot decrypt strong key, " +
                        "did not derive {0} key bytes",
                        Utility.FormatWrapOrNull(PVK_STRONG_KEY_LENGTH));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Per the spec, truncate the key length to exactly 16
                //       bytes.
                //
                keyLength = PVK_STRONG_KEY_LENGTH;
                Array.Resize<byte>(ref key, keyLength);

                byte[] data = binaryReader.ReadBytes(
                    (int)pvkKeyBlob.header.keyLength);

                //
                // NOTE: First, try decrypting and reading the data using the
                //       "strong" (i.e. 128-bit) encryption.
                //
                Result localResult = null;

                if (ReadRc4Encrypted(
                        callback, format, key, data, publicKeyToken,
                        publicKey, privateKey, ref keyPair,
                        ref localResult) == ReturnCode.Ok)
                {
                    keyPair.Salt = pvkKeyBlob.salt;

                    result = localResult;
                    return ReturnCode.Ok;
                }
                else if (keyLength > PVK_WEAK_KEY_LENGTH)
                {
                    //
                    // NOTE: Zero all key bits past the 40-bits required for
                    //       the "weak" encryption.
                    //
                    Array.Clear(
                        key, PVK_WEAK_KEY_LENGTH,
                        keyLength - PVK_WEAK_KEY_LENGTH);

                    //
                    // NOTE: Failing that, try decrypting and reading the data
                    //       using the "weak" (i.e. 40-bit) encryption.
                    //
                    localResult = null;

                    if (ReadRc4Encrypted(
                            callback, format, key, data, publicKeyToken,
                            publicKey, privateKey, ref keyPair,
                            ref localResult) == ReturnCode.Ok)
                    {
                        keyPair.Salt = pvkKeyBlob.salt;

                        result = localResult;
                        return ReturnCode.Ok;
                    }
                    else
                    {
                        result = "could not decrypt strong or weak key";
                    }
                }
                else
                {
                    result = String.Format(
                        "cannot decrypt weak key, " +
                        "did not derive {0} key bytes",
                        Utility.FormatWrapOrNull(PVK_WEAK_KEY_LENGTH));
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Writes a key pair to a PVK (private key) file, optionally
        /// encrypting its contents using a password-derived key.
        /// </summary>
        /// <param name="callback">
        /// The callback used to write the key pair to the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being written.
        /// </param>
        /// <param name="binaryWriter">
        /// The binary writer to which the PVK data will be written.
        /// </param>
        /// <param name="password">
        /// The optional password used to derive the encryption key.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be written.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to be written.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode WritePvk(
            WriteBlobCallback callback, /* in */
            KeyFileFormat format,       /* in */
            BinaryWriter binaryWriter,  /* in */
            string password,            /* in: OPTIONAL */
            bool publicKey,             /* in */
            bool privateKey,            /* in */
            IKeyPair keyPair,           /* in */
            ref Result result           /* out */
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

            try
            {
                uint saltLength;
                byte[] salt = null;
                byte[] key = null;

                if (password != null)
                {
                    saltLength = PVK_SALT_YES;
                    salt = keyPair.Salt;

                    if (salt == null)
                    {
                        salt = GenerateSalt(
                            (int)saltLength, ref result);

                        if (salt == null)
                            return ReturnCode.Error;
                    }

                    if (DeriveKey(
                            salt, password, ref key,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    int keyLength = key.Length;

                    if (keyLength < PVK_STRONG_KEY_LENGTH)
                    {
                        result = String.Format(
                            "cannot decrypt strong key, " +
                            "did not derive {0} key bytes",
                            Utility.FormatWrapOrNull(PVK_STRONG_KEY_LENGTH));

                        return ReturnCode.Error;
                    }

                    keyLength = PVK_STRONG_KEY_LENGTH;
                    Array.Resize<byte>(ref key, keyLength);
                }
                else
                {
                    saltLength = PVK_SALT_NO;
                }

                uint encrypted;
                byte[] data = null;

                if (key != null)
                {
                    if (WriteRc4Encrypted(
                            callback, format, key, ref data,
                            publicKey, privateKey, keyPair,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    encrypted = PVK_ENCRYPTED_YES;
                }
                else
                {
                    if (WriteBlob(
                            callback, format, ref data,
                            publicKey, privateKey, keyPair,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    encrypted = PVK_ENCRYPTED_NO;
                }

                binaryWriter.Write(PVK_MAGIC);
                binaryWriter.Write(PVK_RESERVED);
                binaryWriter.Write(PVK_TYPE_SIGN);
                binaryWriter.Write(encrypted);
                binaryWriter.Write(saltLength);
                binaryWriter.Write(data.Length);

                if (salt != null)
                    binaryWriter.Write(salt);

                binaryWriter.Write(data);

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

        #region Assembly Support Methods (Public)
#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Gets the original local file name associated with the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose file name is requested.
        /// </param>
        /// <returns>
        /// The original local file name of the assembly, or null if
        /// <paramref name="assembly" /> is null.
        /// </returns>
        public static string GetFileName(
            Assembly assembly /* in */
            )
        {
            if (assembly == null)
                return null;

            return Utility.GetOriginalLocalPath(assembly);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the original local file name associated with the specified
        /// assembly name.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name whose file name is requested.
        /// </param>
        /// <returns>
        /// The original local file name of the assembly, or null if
        /// <paramref name="assemblyName" /> is null.
        /// </returns>
        public static string GetFileName( /* CORE */
            AssemblyName assemblyName /* in */
            )
        {
            if (assemblyName == null)
                return null;

            return Utility.GetOriginalLocalPath(assemblyName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region High-Level Auto-Detection Methods (Public)
        /// <summary>
        /// Gets the base key pair type for the specified key pair type by
        /// masking off any non-base bits.
        /// </summary>
        /// <param name="keyPairType">
        /// The key pair type whose base type is requested.
        /// </param>
        /// <returns>
        /// The base key pair type, or <see cref="KeyPairType.None" /> if
        /// <paramref name="keyPairType" /> is null.
        /// </returns>
        public static KeyPairType GetBasePairType( /* CORE */
            KeyPairType? keyPairType
            )
        {
            if (keyPairType == null)
                return KeyPairType.None;

            return ((KeyPairType)keyPairType) & KeyPairType.BaseMask;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Attempts to get the system type associated with the specified key
        /// pair type.
        /// </summary>
        /// <param name="keyPairType">
        /// The key pair type whose associated system type is requested.
        /// </param>
        /// <param name="type">
        /// Upon success, receives the associated system type; otherwise,
        /// receives null.
        /// </param>
        /// <returns>
        /// Non-zero if the associated system type was found; otherwise, zero.
        /// </returns>
        public static bool TryGetType(
            KeyPairType? keyPairType, /* in */
            out Type type             /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((types != null) && types.TryGetValue(
                        GetBasePairType(keyPairType), out type))
                {
                    return true;
                }
            }

            type = null;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to guess the key pair type based on the specified file
        /// name.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to be examined.
        /// </param>
        /// <returns>
        /// The detected key pair type, or null if it could not be detected.
        /// </returns>
        public static KeyPairType? GuessKeyPairType(
            string fileName /* in */
            )
        {
            KeyPairType? keyPairType;
            KeyFileFormat? format; /* NOT USED */
            Result error = null;

            keyPairType = GuessKeyPairType(
                fileName, out format, ref error);

            if (keyPairType != null)
                return keyPairType;

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GuessKeyPairType: fileName = {0}, error = {1}",
                Utility.FormatWrapOrNull(fileName),
                Utility.FormatWrapOrNull(error)),
                typeof(KeyFile).Name, TracePriority.MediumHigh);
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to guess the key pair type based on the specified file
        /// name, also reporting the detected key file format.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to be examined.
        /// </param>
        /// <param name="format">
        /// Upon success, receives the detected key file format.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// The detected key pair type, or null if it could not be detected.
        /// </returns>
        private static KeyPairType? GuessKeyPairType(
            string fileName,           /* in */
            out KeyFileFormat? format, /* out */
            ref Result error           /* out */
            )
        {
            format = null;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileNameCallbacks == null)
                {
                    error = "file name callbacks not available";
                    return null;
                }

                foreach (FileNamePair pair in fileNameCallbacks) /* O(N) */
                {
                    FileNameCallback callback = pair.Value;

                    if (callback == null)
                        continue;

                    if (callback(fileName, out format))
                        return pair.Key;
                }
            }

            error = "could not detect key pair type from file name";
            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the dictionaries of key pair type callbacks, formats,
        /// and system types used to detect and process key files.
        /// </summary>
        /// <param name="force">
        /// Non-zero to reinitialize the dictionaries even if they have already
        /// been initialized.
        /// </param>
        public static void InitializeKeyPairTypes(
            bool force /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                if (force || (magicCallbacks == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (magicCallbacks == null)
                        magicCallbacks = new MagicDictionary();

                    magicCallbacks[KeyPairType.None] = null;
                    magicCallbacks[KeyPairType.RSA] = RsaKeyFile.MatchMagic;
                    magicCallbacks[KeyPairType.DSA] = DsaKeyFile.MatchMagic;
                }
#endif

                ///////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                if (force || (fileNameCallbacks == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (fileNameCallbacks == null)
                        fileNameCallbacks = new FileNameDictionary();

                    fileNameCallbacks[KeyPairType.None] = null;
                    fileNameCallbacks[KeyPairType.RSA] = RsaKeyFile.MatchFileName;
                    fileNameCallbacks[KeyPairType.DSA] = DsaKeyFile.MatchFileName;
                }
#endif

                ///////////////////////////////////////////////////////////////

                if (force || (readBlobCallbacks == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (readBlobCallbacks == null)
                        readBlobCallbacks = new ReadBlobDictionary();

                    readBlobCallbacks[KeyPairType.None] = null;
                    readBlobCallbacks[KeyPairType.RSA] = RsaKeyFile.ReadBlob;
                    readBlobCallbacks[KeyPairType.DSA] = DsaKeyFile.ReadBlob;
                }

                ///////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                if (force || (writeBlobCallbacks == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (writeBlobCallbacks == null)
                        writeBlobCallbacks = new WriteBlobDictionary();

                    writeBlobCallbacks[KeyPairType.None] = null;
                    writeBlobCallbacks[KeyPairType.RSA] = RsaKeyFile.WriteBlob;
                    writeBlobCallbacks[KeyPairType.DSA] = DsaKeyFile.WriteBlob;
                }

                ///////////////////////////////////////////////////////////////

                if (force || (formats == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (formats == null)
                        formats = new FormatDictionary();

                    formats[KeyPairType.None] = KeyFileFormat.None;
                    formats[KeyPairType.RSA] = KeyFileFormat.RsaStrongName; /* .snk */
                    formats[KeyPairType.DSA] = KeyFileFormat.DsaStrongName; /* .dsasnk */
                }

                ///////////////////////////////////////////////////////////////

                if (force || (types == null))
                {
                    //
                    // TODO: Update this list if additional key pair types
                    //       are added.
                    //
                    if (types == null)
                        types = new TypeDictionary();

                    types[KeyPairType.None] = null;
                    types[KeyPairType.RSA] = typeof(RsaKeyPair);
                    types[KeyPairType.DSA] = typeof(DsaKeyPair);
                }
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Scans the specified bytes for a recognizable key pair type magic
        /// signature, also reporting the detected key file format.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to be scanned.
        /// </param>
        /// <param name="format">
        /// Upon success, receives the detected key file format.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message.
        /// </param>
        /// <returns>
        /// The detected key pair type, or null if it could not be detected.
        /// </returns>
        public static KeyPairType? ScanForKeyPairType(
            byte[] bytes,              /* in */
            out KeyFileFormat? format, /* out */
            ref Result error           /* out */
            )
        {
            format = null;

            if (bytes == null)
            {
                error = "invalid key pair bytes";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (magicCallbacks == null)
                {
                    error = "magic callbacks not available";
                    return null;
                }

                int length = bytes.Length;

                for (int index = 0; index < length; index++) /* O(N) */
                {
                    foreach (MagicPair pair in magicCallbacks) /* O(M) */
                    {
                        MagicCallback callback = pair.Value;

                        if (callback == null)
                            continue;

                        if (callback(bytes, index, ref format))
                            return pair.Key;
                    }
                }
            }

            error = "could not detect key pair type from bytes";
            return null;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region High-Level Open/Save Methods (Public)
        /// <summary>
        /// Gets the blob reading callback for the specified key pair type,
        /// falling back on the type of the specified key pair when no key pair
        /// type is given.
        /// </summary>
        /// <param name="keyPair">
        /// The optional key pair whose type is used when
        /// <paramref name="keyPairType" /> is null.
        /// </param>
        /// <param name="keyPairType">
        /// The key pair type whose reading callback is requested.
        /// </param>
        /// <returns>
        /// The matching blob reading callback, or null if none was found.
        /// </returns>
        public static ReadBlobCallback GetReadCallback( /* CORE */
            IKeyPair keyPair,        /* in: OPTIONAL */
            KeyPairType? keyPairType /* in */
            )
        {
            if (keyPairType != null)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    ReadBlobCallback callback;

                    if ((readBlobCallbacks != null) &&
                        readBlobCallbacks.TryGetValue(
                            GetBasePairType(keyPairType), out callback))
                    {
                        return callback;
                    }
                }
            }
            else if (keyPair != null)
            {
                //
                // NOTE: Next, always fallback on its originally
                //       read format.
                //
                return GetReadCallback(
                    null, keyPair.KeyPairType); /* RECURSIVE */
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetReadCallback: unsupported key pair type {0}",
                Utility.FormatWrapOrNull(keyPairType)),
                typeof(KeyFile).Name, TracePriority.MediumHigh);
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Gets the blob writing callback for the specified key pair type,
        /// falling back on the type of the specified key pair when no key pair
        /// type is given.
        /// </summary>
        /// <param name="keyPair">
        /// The optional key pair whose type is used when
        /// <paramref name="keyPairType" /> is null.
        /// </param>
        /// <param name="keyPairType">
        /// The key pair type whose writing callback is requested.
        /// </param>
        /// <returns>
        /// The matching blob writing callback, or null if none was found.
        /// </returns>
        public static WriteBlobCallback GetWriteCallback( /* CORE */
            IKeyPair keyPair,        /* in: OPTIONAL */
            KeyPairType? keyPairType /* in */
            )
        {
            if (keyPairType != null)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    WriteBlobCallback callback;

                    if ((writeBlobCallbacks != null) &&
                        writeBlobCallbacks.TryGetValue(
                            GetBasePairType(keyPairType), out callback))
                    {
                        return callback;
                    }
                }
            }
            else if (keyPair != null)
            {
                //
                // NOTE: Next, always fallback on its originally
                //       read format.
                //
                return GetWriteCallback(
                    null, keyPair.KeyPairType); /* RECURSIVE */
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetWriteCallback: unsupported key pair type {0}",
                Utility.FormatWrapOrNull(keyPairType)),
                typeof(KeyFile).Name, TracePriority.MediumHigh);
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the effective key file format, preferring an explicit format
        /// override, then the format of the specified key pair, then the
        /// format associated with the specified key pair type.
        /// </summary>
        /// <param name="keyPair">
        /// The optional key pair whose format is used when no override is
        /// given.
        /// </param>
        /// <param name="keyPairType">
        /// The optional key pair type whose associated format is used as a
        /// fallback.
        /// </param>
        /// <param name="format">
        /// The optional explicit key file format override.
        /// </param>
        /// <returns>
        /// The effective key file format, or <see cref="KeyFileFormat.None" />
        /// if none could be determined.
        /// </returns>
        public static KeyFileFormat GetFormat(
            IKeyPair keyPair,         /* in: OPTIONAL */
            KeyPairType? keyPairType, /* in: OPTIONAL */
            KeyFileFormat? format     /* in: OPTIONAL */
            )
        {
            //
            // NOTE: First, check for an explicit override of the
            //       key file format.
            //
            if (format != null)
                return (KeyFileFormat)format;

            //
            // NOTE: Next, check for the key pair itself.  Always
            //       fallback on its originally read format.
            //
            if (keyPair != null)
            {
                return keyPair.KeyFileFormat;
            }
            else if (keyPairType != null)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    KeyFileFormat localFormat;

                    if ((formats != null) && formats.TryGetValue(
                            GetBasePairType(keyPairType), out localFormat))
                    {
                        return localFormat;
                    }
                }
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetFormat: unsupported key pair type {0}",
                Utility.FormatWrapOrNull(keyPairType)),
                typeof(KeyFile).Name, TracePriority.MediumHigh);
#endif

            return KeyFileFormat.None;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens and reads a key pair from the public key of the specified
        /// assembly name.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name whose public key is read.
        /// </param>
        /// <param name="callback">
        /// The callback used to read the key pair from the public key blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being read.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be read.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be read.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was read.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Open( /* CORE */
            AssemblyName assemblyName, /* in */
            ReadBlobCallback callback, /* in */
            KeyFileFormat format,      /* in */
            bool publicKey,            /* in */
            bool privateKey,           /* in: IMPOSSIBLE */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (assemblyName == null)
            {
                result = "invalid assembly name";
                return ReturnCode.Error;
            }

            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            byte[] assemblyPublicKey = assemblyName.GetPublicKey();

            if ((assemblyPublicKey == null) ||
                (assemblyPublicKey.Length <= 0))
            {
                result = "invalid assembly public key";
                return ReturnCode.Error;
            }

            byte[] publicKeyToken = assemblyName.GetPublicKeyToken();

            if ((publicKeyToken == null) ||
                (publicKeyToken.Length <= 0))
            {
                result = "invalid assembly public key token";
                return ReturnCode.Error;
            }

            try
            {
                using (BinaryReader binaryReader = new BinaryReader(
                        new MemoryStream(assemblyPublicKey)))
                {
                    if (callback(
                            binaryReader, format, publicKeyToken,
                            publicKey, privateKey, ref keyPair,
                            ref result) == ReturnCode.Ok)
                    {
                        if (keyPair != null)
                        {
                            keyPair.FileName = GetFileName(
                                assemblyName);
                        }

                        return ReturnCode.Ok;
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens and reads a key pair from the specified stream, optionally
        /// treating it as a PVK (private key) file.
        /// </summary>
        /// <param name="stream">
        /// The stream from which the key pair will be read.
        /// </param>
        /// <param name="callback">
        /// The callback used to read the key pair from the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being read.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the stream contains a PVK (private key) file.
        /// </param>
        /// <param name="password">
        /// The optional password used to decrypt a PVK file.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be read.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be read.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was read.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Open( /* CORE */
            Stream stream,             /* in */
            ReadBlobCallback callback, /* in */
            KeyFileFormat format,      /* in */
            bool pvk,                  /* in */
            string password,           /* in: OPTIONAL */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (stream == null)
            {
                result = "invalid stream";
                return ReturnCode.Error;
            }

            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            try
            {
                using (BinaryReader binaryReader = new BinaryReader(
                        stream))
                {
                    if (pvk)
                    {
                        if (ReadPvk(
                                callback, format, binaryReader, password,
                                null, publicKey, privateKey, ref keyPair,
                                ref result) == ReturnCode.Ok)
                        {
                            return ReturnCode.Ok;
                        }
                    }
                    else
                    {
                        if (callback(
                                binaryReader, format, null, publicKey,
                                privateKey, ref keyPair,
                                ref result) == ReturnCode.Ok)
                        {
                            return ReturnCode.Ok;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Opens and reads a key pair from the file with the specified name,
        /// optionally treating it as a PVK (private key) file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file from which the key pair will be read.
        /// </param>
        /// <param name="callback">
        /// The callback used to read the key pair from the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being read.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the file is a PVK (private key) file.
        /// </param>
        /// <param name="password">
        /// The optional password used to decrypt a PVK file.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be read.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be read.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was read.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Open(
            string fileName,           /* in */
            ReadBlobCallback callback, /* in */
            KeyFileFormat format,      /* in */
            bool pvk,                  /* in */
            string password,           /* in: OPTIONAL */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result result          /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            try
            {
                using (BinaryReader binaryReader = new BinaryReader(
                        File.OpenRead(fileName)))
                {
                    if (pvk)
                    {
                        if (ReadPvk(
                                callback, format, binaryReader, password,
                                null, publicKey, privateKey, ref keyPair,
                                ref result) == ReturnCode.Ok)
                        {
                            if (keyPair != null)
                                keyPair.FileName = fileName;

                            return ReturnCode.Ok;
                        }
                    }
                    else
                    {
                        if (callback(
                                binaryReader, format, null, publicKey,
                                privateKey, ref keyPair,
                                ref result) == ReturnCode.Ok)
                        {
                            if (keyPair != null)
                                keyPair.FileName = fileName;

                            return ReturnCode.Ok;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the specified key pair to the specified stream, optionally
        /// writing it as a PVK (private key) file.
        /// </summary>
        /// <param name="stream">
        /// The stream to which the key pair will be written.
        /// </param>
        /// <param name="callback">
        /// The callback used to write the key pair to the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being written.
        /// </param>
        /// <param name="pvk">
        /// Non-zero to write the key pair as a PVK (private key) file.
        /// </param>
        /// <param name="password">
        /// The optional password used to encrypt a PVK file.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be written.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to be written.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Save(
            Stream stream,              /* in */
            WriteBlobCallback callback, /* in */
            KeyFileFormat format,       /* in */
            bool pvk,                   /* in */
            string password,            /* in: OPTIONAL */
            bool publicKey,             /* in */
            bool privateKey,            /* in */
            IKeyPair keyPair,           /* in */
            ref Result result           /* out */
            )
        {
            if (stream == null)
            {
                result = "invalid stream";
                return ReturnCode.Error;
            }

            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            try
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(
                        stream))
                {
                    if (pvk)
                    {
                        return WritePvk(
                            callback, format, binaryWriter, password,
                            publicKey, privateKey, keyPair, ref result);
                    }
                    else
                    {
                        return callback(
                            binaryWriter, format, publicKey, privateKey,
                            keyPair, ref result);
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the specified key pair to the file with the specified name,
        /// optionally writing it as a PVK (private key) file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to which the key pair will be written.
        /// </param>
        /// <param name="callback">
        /// The callback used to write the key pair to the blob.
        /// </param>
        /// <param name="format">
        /// The key file format of the blob being written.
        /// </param>
        /// <param name="pvk">
        /// Non-zero to write the key pair as a PVK (private key) file.
        /// </param>
        /// <param name="password">
        /// The optional password used to encrypt a PVK file.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero if the public key portion should be written.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero if the private key portion should be written.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to be written.
        /// </param>
        /// <param name="result">
        /// Upon success, receives an optional result; otherwise, receives an
        /// error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Save(
            string fileName,            /* in */
            WriteBlobCallback callback, /* in */
            KeyFileFormat format,       /* in */
            bool pvk,                   /* in */
            string password,            /* in: OPTIONAL */
            bool publicKey,             /* in */
            bool privateKey,            /* in */
            IKeyPair keyPair,           /* in */
            ref Result result           /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                result = "invalid file name";
                return ReturnCode.Error;
            }

            if (callback == null)
            {
                result = "invalid blob callback";
                return ReturnCode.Error;
            }

            try
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(
                        File.OpenWrite(fileName)))
                {
                    if (pvk)
                    {
                        return WritePvk(
                            callback, format, binaryWriter, password,
                            publicKey, privateKey, keyPair, ref result);
                    }
                    else
                    {
                        return callback(
                            binaryWriter, format, publicKey, privateKey,
                            keyPair, ref result);
                    }
                }
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion
    }
}
