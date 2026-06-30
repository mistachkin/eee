/*
 * ProtectOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !NATIVE
#error "This file cannot be compiled or used properly with native code disabled."
#endif

#if WINDOWS || !NET_STANDARD_20
using System;
#endif

#if WINDOWS
using System.Runtime.InteropServices;
#endif

using System.Security;
using System.Security.Cryptography;

#if WINDOWS
using System.Text;
#endif

#if !NET_40
using System.Security.Permissions;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;

#if WINDOWS
using Eagle._Constants;
#endif

using UNM = Licensing.Components.Private.ProtectOps.UnsafeNativeMethods;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides operating-system-specific helper methods for protecting,
    /// unprotecting, and symmetrically encrypting byte data using the
    /// available platform data-protection facilities.
    /// </summary>
#if NET_40
    [SecurityCritical()]
#else
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
    [ObjectId("2c090467-5927-4700-bb25-4f069a06a909")]
    internal static class ProtectOps
    {
        ///////////////////////////////////////////////////////////////////////
        // Required Native APIs used via P/Invoke
        ///////////////////////////////////////////////////////////////////////

        #region Unsafe Native Methods Class
        /// <summary>
        /// Contains the unmanaged native API declarations used by the
        /// containing class via P/Invoke.
        /// </summary>
        [SuppressUnmanagedCodeSecurity()]
        [ObjectId("a10d3f10-ded7-4973-b705-20c5300ae0d6")]
        internal static class UnsafeNativeMethods
        {
#if WINDOWS
            #region Windows Local Memory Allocator Constants
            /* CORE? */
            /// <summary>
            /// Local memory allocation flag specifying fixed memory.
            /// </summary>
            internal const uint LMEM_FIXED = 0x0;

            /* CORE? */
            /// <summary>
            /// Local memory allocation flag specifying that the allocated
            /// memory should be initialized to zero.
            /// </summary>
            internal const uint LMEM_ZEROINIT = 0x40;

            /* CORE? */
            /// <summary>
            /// Local memory allocation flag combining fixed allocation with
            /// zero initialization.
            /// </summary>
            internal const uint LPTR = LMEM_FIXED | LMEM_ZEROINIT;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Local Memory Allocator Functions
            /* CORE? */
            /// <summary>
            /// Allocates the specified number of bytes from the local heap.
            /// </summary>
            /// <param name="flags">
            /// The memory allocation attributes for the request.
            /// </param>
            /// <param name="size">
            /// The number of bytes to allocate.
            /// </param>
            /// <returns>
            /// A handle to the newly allocated memory, or zero on failure.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            internal static extern IntPtr LocalAlloc(
                uint flags,  /* in */
                UIntPtr size /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Returns the number of bytes in the specified local memory
            /// object.
            /// </summary>
            /// <param name="hMemory">
            /// A handle to the local memory object.
            /// </param>
            /// <returns>
            /// The size, in bytes, of the local memory object.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            internal static extern uint LocalSize(
                IntPtr hMemory /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Frees the specified local memory object.
            /// </summary>
            /// <param name="hMemory">
            /// A handle to the local memory object to free.
            /// </param>
            /// <returns>
            /// Zero if the object was freed; otherwise, a handle to the
            /// memory object.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            internal static extern IntPtr LocalFree(
                IntPtr hMemory /* in */
            );
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Data Protection Constants
            /* CORE? */
            /// <summary>
            /// Data protection flag that forbids the display of any user
            /// interface during the operation.
            /// </summary>
            internal const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

            /* CORE? */
            /// <summary>
            /// Data protection flag that associates the protected data with
            /// the local machine rather than the current user.
            /// </summary>
            internal const uint CRYPTPROTECT_LOCAL_MACHINE = 0x4;

            /* NOT USED */
            /// <summary>
            /// Data protection flag used to synchronize credentials.  This
            /// value is not used.
            /// </summary>
            internal const uint CRYPTPROTECT_CRED_SYNC = 0x8;

            /* CORE? */
            /// <summary>
            /// Data protection flag that requests an audit on protect and
            /// unprotect operations.
            /// </summary>
            internal const uint CRYPTPROTECT_AUDIT = 0x10;

            /* NOT USED */
            /// <summary>
            /// Data protection flag indicating the protected data cannot be
            /// recovered.  This value is not used.
            /// </summary>
            internal const uint CRYPTPROTECT_NO_RECOVERY = 0x20;

            /* NOT USED */
            /// <summary>
            /// Data protection flag that verifies the protection of the
            /// data.  This value is not used.
            /// </summary>
            internal const uint CRYPTPROTECT_VERIFY_PROTECTION = 0x40;

            /* NOT USED */
            /// <summary>
            /// Data protection flag used to regenerate the local machine
            /// credentials.  This value is not used.
            /// </summary>
            internal const uint CRYPTPROTECT_CRED_REGENERATE = 0x80;

            /* NOT USED */
            /// <summary>
            /// Data protection flag for system-level protection on Windows
            /// CE.  This value is not used.
            /// </summary>
            internal const uint CRYPTPROTECT_SYSTEM = 0x20000000; /* WinCE */
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Data Protection Structures
            /* CORE? */
            /// <summary>
            /// Represents a block of unmanaged data consisting of a length
            /// and a pointer to the associated bytes.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            [ObjectId("bc93df9e-5d89-496c-9c27-266a393c8a6b")]
            internal struct DATA_BLOB
            {
                /// <summary>
                /// The number of bytes referenced by <see cref="pbData" />.
                /// </summary>
                public uint cbData;
                /// <summary>
                /// A pointer to the unmanaged data bytes.
                /// </summary>
                public IntPtr pbData;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Data Protection Methods
            /* CORE? */
            /// <summary>
            /// Performs encryption on the specified data using the Windows
            /// data protection API.
            /// </summary>
            /// <param name="dataIn">
            /// The data to be encrypted.
            /// </param>
            /// <param name="pDataDescription">
            /// A pointer to a human-readable description of the data.
            /// </param>
            /// <param name="optionalEntropy">
            /// Optional additional entropy used during encryption.
            /// </param>
            /// <param name="pReserved">
            /// Reserved; must be a null pointer.
            /// </param>
            /// <param name="pPromptStruct">
            /// A pointer to a structure describing where and when prompts
            /// are to be displayed, or a null pointer.
            /// </param>
            /// <param name="flags">
            /// Flags controlling the encryption operation.
            /// </param>
            /// <param name="dataOut">
            /// On success, receives the encrypted data.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Crypt32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptProtectData(
                ref DATA_BLOB dataIn,          /* in, out */
                IntPtr pDataDescription,       /* in */
                ref DATA_BLOB optionalEntropy, /* in, out */
                IntPtr pReserved,              /* in */
                IntPtr pPromptStruct,          /* in */
                uint flags,                    /* in */
                ref DATA_BLOB dataOut          /* in, out */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Performs decryption on the specified data using the Windows
            /// data protection API.
            /// </summary>
            /// <param name="dataIn">
            /// The data to be decrypted.
            /// </param>
            /// <param name="pDataDescription">
            /// On success, receives a pointer to a human-readable
            /// description of the data.
            /// </param>
            /// <param name="optionalEntropy">
            /// Optional additional entropy used during decryption.
            /// </param>
            /// <param name="pReserved">
            /// Reserved; must be a null pointer.
            /// </param>
            /// <param name="pPromptStruct">
            /// A pointer to a structure describing where and when prompts
            /// are to be displayed, or a null pointer.
            /// </param>
            /// <param name="flags">
            /// Flags controlling the decryption operation.
            /// </param>
            /// <param name="dataOut">
            /// On success, receives the decrypted data.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Crypt32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptUnprotectData(
                ref DATA_BLOB dataIn,          /* in, out */
                ref IntPtr pDataDescription,   /* in, out */
                ref DATA_BLOB optionalEntropy, /* in, out */
                IntPtr pReserved,              /* in */
                IntPtr pPromptStruct,          /* in */
                uint flags,                    /* in */
                ref DATA_BLOB dataOut          /* in, out */
            );
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows CryptoAPI Methods Constants
            /* CORE? */
            /// <summary>
            /// The name of the Microsoft Enhanced Cryptographic Provider.
            /// </summary>
            internal const string MS_ENHANCED_PROV =
                "Microsoft Enhanced Cryptographic Provider v1.0";

            /* CORE? */
            /// <summary>
            /// The cryptographic provider type identifier for a full RSA
            /// provider.
            /// </summary>
            internal const uint PROV_RSA_FULL = 1;

            /* CORE? */
            /// <summary>
            /// Flag indicating that a cryptographic context is being
            /// acquired for verification only, without access to private
            /// keys.
            /// </summary>
            internal const uint CRYPT_VERIFYCONTEXT = 0xF0000000;

            /* CORE? */
            /// <summary>
            /// Algorithm identifier class bits for data encryption
            /// algorithms.
            /// </summary>
            private const uint ALG_CLASS_DATA_ENCRYPT = (3 << 13);

            /* CORE? */
            /// <summary>
            /// Algorithm identifier class bits for hash algorithms.
            /// </summary>
            private const uint ALG_CLASS_HASH = (4 << 13);

            /* CORE? */
            /// <summary>
            /// Algorithm identifier type bits indicating any algorithm
            /// type.
            /// </summary>
            private const uint ALG_TYPE_ANY = 0;

            /* CORE? */
            /// <summary>
            /// Algorithm identifier type bits for stream cipher algorithms.
            /// </summary>
            private const uint ALG_TYPE_STREAM = (4 << 9);

            /* CORE? */
            /// <summary>
            /// Algorithm identifier sub-identifier bits for the RC4
            /// algorithm.
            /// </summary>
            private const uint ALG_SID_RC4 = 1;

            /* CORE? */
            /// <summary>
            /// Algorithm identifier sub-identifier bits for the SHA-1
            /// algorithm.
            /// </summary>
            private const uint ALG_SID_SHA1 = 4;

            /* CORE? */
            /// <summary>
            /// The composite algorithm identifier for the SHA-1 hash
            /// algorithm.
            /// </summary>
            internal const uint CALG_SHA1 =
                ALG_CLASS_HASH | ALG_TYPE_ANY | ALG_SID_SHA1;

            /* CORE? */
            /// <summary>
            /// The composite algorithm identifier for the RC4 stream
            /// cipher.
            /// </summary>
            internal const uint CALG_RC4 =
                ALG_CLASS_DATA_ENCRYPT | ALG_TYPE_STREAM | ALG_SID_RC4;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows CryptoAPI Methods
            /* CORE? */
            /// <summary>
            /// Acquires a handle to a key container within a cryptographic
            /// service provider.
            /// </summary>
            /// <param name="hProvider">
            /// On success, receives a handle to the cryptographic service
            /// provider.
            /// </param>
            /// <param name="container">
            /// The name of the key container, or a null reference.
            /// </param>
            /// <param name="provider">
            /// The name of the cryptographic service provider.
            /// </param>
            /// <param name="providerType">
            /// The type of provider to acquire.
            /// </param>
            /// <param name="flags">
            /// Flags controlling how the context is acquired.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                CharSet = CharSet.Auto, BestFitMapping = false,
                ThrowOnUnmappableChar = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptAcquireContext(
                ref IntPtr hProvider, /* in, out */
                string container,     /* in */
                string provider,      /* in */
                uint providerType,    /* in */
                uint flags            /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Releases a handle to a cryptographic service provider and key
            /// container.
            /// </summary>
            /// <param name="hProvider">
            /// A handle to the cryptographic service provider to release.
            /// </param>
            /// <param name="flags">
            /// Reserved; must be zero.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptReleaseContext(
                IntPtr hProvider, /* in */
                uint flags        /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Creates a hash object for the specified hash algorithm.
            /// </summary>
            /// <param name="hProvider">
            /// A handle to the cryptographic service provider.
            /// </param>
            /// <param name="algorithmId">
            /// The identifier of the hash algorithm to use.
            /// </param>
            /// <param name="hKey">
            /// A handle to a key for keyed hash algorithms, or a null
            /// handle.
            /// </param>
            /// <param name="flags">
            /// Reserved; must be zero.
            /// </param>
            /// <param name="hHash">
            /// On success, receives a handle to the new hash object.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptCreateHash(
                IntPtr hProvider, /* in */
                uint algorithmId, /* in */
                IntPtr hKey,      /* in */
                uint flags,       /* in */
                ref IntPtr hHash  /* in, out */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Adds data to the specified hash object.
            /// </summary>
            /// <param name="hHash">
            /// A handle to the hash object.
            /// </param>
            /// <param name="pData">
            /// A pointer to the data to add to the hash.
            /// </param>
            /// <param name="dataLength">
            /// The number of bytes of data to add to the hash.
            /// </param>
            /// <param name="flags">
            /// Flags controlling the hash operation.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptHashData(
                IntPtr hHash,    /* in */
                IntPtr pData,    /* in */
                uint dataLength, /* in */
                uint flags       /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Destroys the specified hash object.
            /// </summary>
            /// <param name="hHash">
            /// A handle to the hash object to destroy.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptDestroyHash(
                IntPtr hHash /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Derives a cryptographic key from the data in a hash object.
            /// </summary>
            /// <param name="hProvider">
            /// A handle to the cryptographic service provider.
            /// </param>
            /// <param name="algorithmId">
            /// The identifier of the algorithm for which the key is
            /// derived.
            /// </param>
            /// <param name="hHash">
            /// A handle to the hash object from which the key is derived.
            /// </param>
            /// <param name="flags">
            /// Flags controlling key generation.
            /// </param>
            /// <param name="hKey">
            /// On success, receives a handle to the derived key.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptDeriveKey(
                IntPtr hProvider, /* in */
                uint algorithmId, /* in */
                IntPtr hHash,     /* in */
                uint flags,       /* in */
                ref IntPtr hKey   /* in, out */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Releases the handle to the specified cryptographic key.
            /// </summary>
            /// <param name="hKey">
            /// A handle to the key to destroy.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptDestroyKey(
                IntPtr hKey /* in */
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Encrypts data in place using the specified cryptographic key.
            /// </summary>
            /// <param name="hKey">
            /// A handle to the key to use for encryption.
            /// </param>
            /// <param name="hHash">
            /// A handle to a hash object to update with the data, or a null
            /// handle.
            /// </param>
            /// <param name="final">
            /// Non-zero if this is the last block of data to encrypt.
            /// </param>
            /// <param name="flags">
            /// Reserved; must be zero.
            /// </param>
            /// <param name="pData">
            /// A pointer to the buffer holding the data to encrypt; on
            /// success, receives the encrypted data.
            /// </param>
            /// <param name="dataLength">
            /// On input, the number of bytes to encrypt; on output, the
            /// number of encrypted bytes.
            /// </param>
            /// <param name="bufferLength">
            /// The total size, in bytes, of the data buffer.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptEncrypt(
                IntPtr hKey,
                IntPtr hHash,
                [MarshalAs(UnmanagedType.Bool)]
                bool final,
                uint flags,
                IntPtr pData,
                ref uint dataLength,
                uint bufferLength
            );

            ///////////////////////////////////////////////////////////////////

            /* CORE? */
            /// <summary>
            /// Decrypts data in place using the specified cryptographic key.
            /// </summary>
            /// <param name="hKey">
            /// A handle to the key to use for decryption.
            /// </param>
            /// <param name="hHash">
            /// A handle to a hash object to update with the data, or a null
            /// handle.
            /// </param>
            /// <param name="final">
            /// Non-zero if this is the last block of data to decrypt.
            /// </param>
            /// <param name="flags">
            /// Reserved; must be zero.
            /// </param>
            /// <param name="pData">
            /// A pointer to the buffer holding the data to decrypt; on
            /// success, receives the decrypted data.
            /// </param>
            /// <param name="dataLength">
            /// On input, the number of bytes to decrypt; on output, the
            /// number of decrypted bytes.
            /// </param>
            /// <returns>
            /// Non-zero if the operation succeeded; otherwise, zero.
            /// </returns>
            [DllImport(DllName.AdvApi32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptDecrypt(
                IntPtr hKey,
                IntPtr hHash,
                [MarshalAs(UnmanagedType.Bool)]
                bool final,
                uint flags,
                IntPtr pData,
                ref uint dataLength
            );
            #endregion
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
#if WINDOWS
        /* CORE? */
        /// <summary>
        /// The maximum number of bytes to probe when scanning unmanaged
        /// memory for a string.
        /// </summary>
        private static int DataLimit = 0x40000000;

        ///////////////////////////////////////////////////////////////////////

        /* CORE? */
        /// <summary>
        /// The placeholder description used when no data description is
        /// supplied.
        /// </summary>
        private const string UnknownDescription = "<unknown>";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be an encoding that always results in an even
        //       number of bytes for an arbitrary string (i.e. but not
        //       necessarily two per character).
        //
        /* CORE? */
        /// <summary>
        /// The text encoding used when converting strings to and from
        /// unmanaged memory.
        /// </summary>
        private static readonly Encoding UnicodeEncoding = Encoding.Unicode;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Native String Helper Methods
#if WINDOWS
        //
        // WARNING: The length value returned from this method is in bytes,
        //          not characters.
        //
        /// <summary>
        /// Scans unmanaged memory for the terminating null character of a
        /// Unicode string and returns its length in bytes.
        /// </summary>
        /// <param name="pMemory">
        /// A pointer to the unmanaged memory to scan.
        /// </param>
        /// <param name="limit">
        /// The maximum number of bytes to scan.
        /// </param>
        /// <returns>
        /// The length, in bytes, of the Unicode string up to but not
        /// including the terminating null character.
        /// </returns>
        private static int ProbeForUnicodeLength( /* CORE? */
            IntPtr pMemory, /* in */
            int limit       /* in */
            )
        {
            int length = 0;

            if ((pMemory != IntPtr.Zero) &&
                (limit >= 0) && (limit % sizeof(short) == 0))
            {
                do
                {
                    if (Marshal.ReadInt16(pMemory, length) == 0)
                        break;

                    if (length >= limit)
                        break;

                    length += sizeof(short);
                } while (true);
            }

            return length;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a Unicode string from the specified unmanaged memory.
        /// </summary>
        /// <param name="pMemory">
        /// A pointer to the unmanaged memory containing the string.
        /// </param>
        /// <returns>
        /// The decoded string, or null if the data could not be read.
        /// </returns>
        private static string GetString( /* CORE? */
            IntPtr pMemory /* in */
            )
        {
            Encoding encoding = UnicodeEncoding;

            if (encoding == null)
                return null;

            byte[] bytes = GetData(
                pMemory, ProbeForUnicodeLength(pMemory, DataLimit));

            if (bytes == null)
                return null;

            return encoding.GetString(bytes);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Native Array Helper Methods
#if WINDOWS
        /// <summary>
        /// Copies the bytes referenced by the specified data block into a
        /// managed array.
        /// </summary>
        /// <param name="data">
        /// The data block describing the unmanaged bytes to copy.
        /// </param>
        /// <returns>
        /// A managed array containing the copied bytes, or null if there
        /// was no data.
        /// </returns>
        private static byte[] GetData( /* CORE? */
            UNM.DATA_BLOB data /* in */
            )
        {
            return GetData(data.pbData, (int)data.cbData);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the specified number of bytes from unmanaged memory into
        /// a managed array.
        /// </summary>
        /// <param name="pMemory">
        /// A pointer to the unmanaged memory to copy from.
        /// </param>
        /// <param name="length">
        /// The number of bytes to copy.
        /// </param>
        /// <returns>
        /// A managed array containing the copied bytes, or null if
        /// <paramref name="pMemory" /> is null.
        /// </returns>
        private static byte[] GetData( /* CORE? */
            IntPtr pMemory, /* in */
            int length      /* in */
            )
        {
            if (pMemory == IntPtr.Zero)
                return null;

            byte[] bytes = new byte[length];

            Marshal.Copy(pMemory, bytes, 0, length);

            return bytes;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Native Memory Allocator Helper Methods
#if WINDOWS
        /// <summary>
        /// Allocates unmanaged memory and copies the specified string into
        /// it as null-terminated Unicode bytes.
        /// </summary>
        /// <param name="value">
        /// The string to copy into unmanaged memory.
        /// </param>
        /// <returns>
        /// A pointer to the newly allocated unmanaged memory, or zero on
        /// failure.
        /// </returns>
        private static IntPtr AllocateData( /* CORE? */
            string value /* in */
            )
        {
            if (value == null)
                return IntPtr.Zero;

            Encoding encoding = UnicodeEncoding;

            if (encoding == null)
                return IntPtr.Zero;

            return AllocateData(encoding.GetBytes(value + Characters.Null));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allocates unmanaged memory and copies the specified bytes into
        /// it.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to copy into unmanaged memory.
        /// </param>
        /// <returns>
        /// A pointer to the newly allocated unmanaged memory, or zero on
        /// failure.
        /// </returns>
        private static IntPtr AllocateData( /* CORE? */
            byte[] bytes /* in */
            )
        {
            if (bytes == null)
                return IntPtr.Zero;

            bool success = false;
            IntPtr pMemory = IntPtr.Zero;

            try
            {
                pMemory = UNM.LocalAlloc(
                    UNM.LPTR, new UIntPtr((uint)bytes.Length));

                Marshal.Copy(bytes, 0, pMemory, bytes.Length);

                success = true;
            }
            catch
            {
                success = false;
            }
            finally
            {
                if (!success && (pMemory != IntPtr.Zero))
                {
                    UNM.LocalFree(pMemory);
                    pMemory = IntPtr.Zero;
                }
            }

            return pMemory;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Zeroes and frees the specified block of unmanaged memory.
        /// </summary>
        /// <param name="pMemory">
        /// A pointer to the unmanaged memory to free.
        /// </param>
        /// <returns>
        /// Non-zero if the memory was successfully freed; otherwise, zero.
        /// </returns>
        private static bool FreeData( /* CORE? */
            IntPtr pMemory /* in */
            )
        {
            if (pMemory == IntPtr.Zero)
                return true;

            try
            {
                ReturnCode code;
                Result error = null;

                code = Utility.ZeroMemory(
                    pMemory, UNM.LocalSize(pMemory), ref error);

#if DEBUG || FORCE_TRACE
                if (code != ReturnCode.Ok)
                {
                    CertificateTraceOps.DebugTrace(String.Format(
                        "FreeData: {0}", Utility.FormatWrapOrNull(
                        error)), typeof(ProtectOps).Name,
                        TracePriority.Highest);
                }
#endif

                if (UNM.LocalFree(pMemory) == IntPtr.Zero)
                {
                    return true;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    int lastError = Marshal.GetLastWin32Error();

                    error = String.Format(
                        "LocalFree({1}) failed with error {0}: {2}",
                        lastError, pMemory,
                        Utility.GetErrorMessage(lastError));

                    CertificateTraceOps.DebugTrace(String.Format(
                        "FreeData: {0}", Utility.FormatWrapOrNull(
                        error)), typeof(ProtectOps).Name,
                        TracePriority.Highest);
#endif
                }
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(ProtectOps).Name,
                    TracePriority.Highest);
#endif
            }

            return false;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Platform Abstraction Methods
#if !NET_STANDARD_20
        /// <summary>
        /// Determines the data protection scope to use based on the
        /// specified per-machine preference.
        /// </summary>
        /// <param name="perMachine">
        /// Non-null to force a per-machine or per-user scope; null to
        /// determine the scope from the current configuration.
        /// </param>
        /// <returns>
        /// The data protection scope to use.
        /// </returns>
        public static DataProtectionScope GetScope( /* CORE */
            bool? perMachine /* in: OPTIONAL */
            )
        {
            if (perMachine != null)
            {
                return (bool)perMachine ?
                    DataProtectionScope.LocalMachine :
                    DataProtectionScope.CurrentUser;
            }

            if (CertificateSharedOps.ShouldUsePerMachine(perMachine))
                return DataProtectionScope.LocalMachine;
            else
                return DataProtectionScope.CurrentUser;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts the specified bytes using the platform data protection
        /// facilities available on the current operating system.
        /// </summary>
        /// <param name="entropy">
        /// Optional additional entropy used during encryption.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to protect the data for the local machine instead of
        /// the current user.
        /// </param>
        /// <param name="audit">
        /// Non-zero to request an audit of the operation.
        /// </param>
        /// <param name="errorOnUnsupported">
        /// Non-zero to return an error when data protection is not
        /// supported on the current operating system.
        /// </param>
        /// <param name="description">
        /// A human-readable description to associate with the protected
        /// data.
        /// </param>
        /// <param name="bytes">
        /// On input, the data to protect; on success, receives the
        /// protected data.
        /// </param>
        /// <param name="error">
        /// On failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode ProtectData( /* CORE */
            byte[] entropy,          /* in */
            bool perMachine,         /* in */
            bool audit,              /* in */
            bool errorOnUnsupported, /* in */
            string description,      /* in */
            ref byte[] bytes,        /* in, out */
            ref Result error         /* out */
            )
        {
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
            {
                if (bytes == null)
                {
                    error = "invalid user data";
                    return ReturnCode.Error;
                }

                UNM.DATA_BLOB dataIn = new UNM.DATA_BLOB();
                IntPtr pDataDescription = IntPtr.Zero;
                UNM.DATA_BLOB optionalEntropy = new UNM.DATA_BLOB();
                UNM.DATA_BLOB dataOut = new UNM.DATA_BLOB();

                try
                {
                    dataIn.cbData = (uint)bytes.Length;
                    dataIn.pbData = AllocateData(bytes);

                    if (dataIn.pbData == IntPtr.Zero)
                    {
                        error = "out of memory";
                        return ReturnCode.Error;
                    }

                    if (description == null)
                        description = UnknownDescription;

                    pDataDescription = AllocateData(description);

                    if (pDataDescription == IntPtr.Zero)
                    {
                        error = "out of memory";
                        return ReturnCode.Error;
                    }

                    if (entropy != null)
                    {
                        optionalEntropy.cbData = (uint)entropy.Length;
                        optionalEntropy.pbData = AllocateData(entropy);

                        if (optionalEntropy.pbData == IntPtr.Zero)
                        {
                            error = "out of memory";
                            return ReturnCode.Error;
                        }
                    }

                    uint flags = UNM.CRYPTPROTECT_UI_FORBIDDEN;

                    if (perMachine)
                        flags |= UNM.CRYPTPROTECT_LOCAL_MACHINE;

                    if (audit)
                        flags |= UNM.CRYPTPROTECT_AUDIT;

                    if (UNM.CryptProtectData(
                            ref dataIn, pDataDescription,
                            ref optionalEntropy, IntPtr.Zero,
                            IntPtr.Zero, flags, ref dataOut))
                    {
                        bytes = GetData(dataOut);

                        return ReturnCode.Ok;
                    }

                    int lastError = Marshal.GetLastWin32Error();

                    error = String.Format(
                        "CryptProtectData() failed with error {0}: {1}",
                        lastError, Utility.GetErrorMessage(lastError));

                    return ReturnCode.Error;
                }
                catch (Exception e)
                {
                    error = e;
                    return ReturnCode.Error;
                }
                finally
                {
                    if (dataOut.pbData != IntPtr.Zero)
                    {
                        FreeData(dataOut.pbData);
                        dataOut.pbData = IntPtr.Zero;
                    }

                    if (optionalEntropy.pbData != IntPtr.Zero)
                    {
                        FreeData(optionalEntropy.pbData);
                        optionalEntropy.pbData = IntPtr.Zero;
                    }

                    if (pDataDescription != IntPtr.Zero)
                    {
                        FreeData(pDataDescription);
                        pDataDescription = IntPtr.Zero;
                    }

                    if (dataIn.pbData != IntPtr.Zero)
                    {
                        FreeData(dataIn.pbData);
                        dataIn.pbData = IntPtr.Zero;
                    }
                }
            }
#endif

#if !NET_STANDARD_20
            if (Utility.IsMono())
            {
                try
                {
                    bytes = ProtectedData.Protect(
                        bytes, entropy, GetScope(
                        perMachine)); /* throw */

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    error = e;
                    return ReturnCode.Error;
                }
            }
#endif

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            if (Utility.IsDotNetCore())
            {
                if (CryptographyOps.EncryptOrDecrypt(null,
                        Convert.ToBase64String(entropy), entropy,
                        0, null, Constants.DefaultCipherMode,
                        Constants.DefaultPaddingMode, bytes, true,
                        ref bytes, ref error) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
#endif

            if (errorOnUnsupported)
            {
                error = "not supported on this operating system";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrypts the specified bytes using the platform data protection
        /// facilities available on the current operating system.
        /// </summary>
        /// <param name="entropy">
        /// Optional additional entropy used during decryption.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to unprotect data that was protected for the local
        /// machine instead of the current user.
        /// </param>
        /// <param name="audit">
        /// Non-zero to request an audit of the operation.
        /// </param>
        /// <param name="errorOnUnsupported">
        /// Non-zero to return an error when data protection is not
        /// supported on the current operating system.
        /// </param>
        /// <param name="description">
        /// On success, receives the description associated with the
        /// protected data.
        /// </param>
        /// <param name="bytes">
        /// On input, the data to unprotect; on success, receives the
        /// unprotected data.
        /// </param>
        /// <param name="error">
        /// On failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode UnprotectData( /* CORE */
            byte[] entropy,          /* in */
            bool perMachine,         /* in */
            bool audit,              /* in */
            bool errorOnUnsupported, /* in */
            ref string description,  /* out */
            ref byte[] bytes,        /* in, out */
            ref Result error         /* out */
            )
        {
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
            {
                if (bytes == null)
                {
                    error = "invalid user data";
                    return ReturnCode.Error;
                }

                UNM.DATA_BLOB dataIn = new UNM.DATA_BLOB();
                IntPtr pDataDescription = IntPtr.Zero;
                UNM.DATA_BLOB optionalEntropy = new UNM.DATA_BLOB();
                UNM.DATA_BLOB dataOut = new UNM.DATA_BLOB();

                try
                {
                    dataIn.cbData = (uint)bytes.Length;
                    dataIn.pbData = AllocateData(bytes);

                    if (dataIn.pbData == IntPtr.Zero)
                    {
                        error = "out of memory";
                        return ReturnCode.Error;
                    }

                    if (entropy != null)
                    {
                        optionalEntropy.cbData = (uint)entropy.Length;
                        optionalEntropy.pbData = AllocateData(entropy);

                        if (optionalEntropy.pbData == IntPtr.Zero)
                        {
                            error = "out of memory";
                            return ReturnCode.Error;
                        }
                    }

                    uint flags = UNM.CRYPTPROTECT_UI_FORBIDDEN;

                    if (perMachine)
                        flags |= UNM.CRYPTPROTECT_LOCAL_MACHINE;

                    if (audit)
                        flags |= UNM.CRYPTPROTECT_AUDIT;

                    if (UNM.CryptUnprotectData(
                            ref dataIn, ref pDataDescription,
                            ref optionalEntropy, IntPtr.Zero,
                            IntPtr.Zero, flags, ref dataOut))
                    {
                        description = GetString(pDataDescription);
                        bytes = GetData(dataOut);

                        return ReturnCode.Ok;
                    }

                    int lastError = Marshal.GetLastWin32Error();

                    error = String.Format(
                        "CryptUnprotectData() failed with error {0}: {1}",
                        lastError, Utility.GetErrorMessage(lastError));

                    return ReturnCode.Error;
                }
                catch (Exception e)
                {
                    error = e;
                    return ReturnCode.Error;
                }
                finally
                {
                    if (dataOut.pbData != IntPtr.Zero)
                    {
                        FreeData(dataOut.pbData);
                        dataOut.pbData = IntPtr.Zero;
                    }

                    if (optionalEntropy.pbData != IntPtr.Zero)
                    {
                        FreeData(optionalEntropy.pbData);
                        optionalEntropy.pbData = IntPtr.Zero;
                    }

                    if (pDataDescription != IntPtr.Zero)
                    {
                        FreeData(pDataDescription);
                        pDataDescription = IntPtr.Zero;
                    }

                    if (dataIn.pbData != IntPtr.Zero)
                    {
                        FreeData(dataIn.pbData);
                        dataIn.pbData = IntPtr.Zero;
                    }
                }
            }
#endif

#if !NET_STANDARD_20
            if (Utility.IsMono())
            {
                try
                {
                    bytes = ProtectedData.Unprotect(
                        bytes, entropy, GetScope(
                        perMachine)); /* throw */

                    return ReturnCode.Ok;
                }
                catch (Exception e)
                {
                    error = e;
                    return ReturnCode.Error;
                }
            }
#endif

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            if (Utility.IsDotNetCore())
            {
                if (CryptographyOps.EncryptOrDecrypt(null,
                        Convert.ToBase64String(entropy), entropy,
                        0, null, Constants.DefaultCipherMode,
                        Constants.DefaultPaddingMode, bytes, false,
                        ref bytes, ref error) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
#endif

            if (errorOnUnsupported)
            {
                error = "not supported on this operating system";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts or decrypts the specified data in place using the RC4
        /// stream cipher with a key derived from the supplied key bytes.
        /// </summary>
        /// <param name="key">
        /// The key bytes from which the RC4 key is derived.
        /// </param>
        /// <param name="encrypt">
        /// Non-zero to encrypt the data; zero to decrypt it.
        /// </param>
        /// <param name="data">
        /// On input, the data to transform; on success, receives the
        /// transformed data.
        /// </param>
        /// <param name="error">
        /// On failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode Rc4EncryptOrDecrypt( /* CORE */
            byte[] key,      /* in */
            bool encrypt,    /* in */
            ref byte[] data, /* in, out */
            ref Result error /* out */
            )
        {
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
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

                int lastError; /* REUSED */
                IntPtr hProvider = IntPtr.Zero;
                IntPtr hHash = IntPtr.Zero;
                IntPtr pKey = IntPtr.Zero;
                IntPtr hKey = IntPtr.Zero;
                IntPtr pData = IntPtr.Zero;

                try
                {
                    if (!UNM.CryptAcquireContext(
                            ref hProvider, null, UNM.MS_ENHANCED_PROV,
                            UNM.PROV_RSA_FULL, UNM.CRYPT_VERIFYCONTEXT))
                    {
                        lastError = Marshal.GetLastWin32Error();

                        error = String.Format(
                            "CryptAcquireContext() failed with error {0}: {1}",
                            lastError, Utility.GetErrorMessage(lastError));

                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    if (!UNM.CryptCreateHash(
                            hProvider, UNM.CALG_SHA1, IntPtr.Zero, 0,
                            ref hHash))
                    {
                        lastError = Marshal.GetLastWin32Error();

                        error = String.Format(
                            "CryptCreateHash() failed with error {0}: {1}",
                            lastError, Utility.GetErrorMessage(lastError));

                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    pKey = AllocateData(key);

                    if (pKey == IntPtr.Zero)
                    {
                        error = "out of memory";
                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    uint keyLength = (uint)key.Length;

                    if (!UNM.CryptHashData(hHash, pKey, keyLength, 0))
                    {
                        lastError = Marshal.GetLastWin32Error();

                        error = String.Format(
                            "CryptHashData() failed with error {0}: {1}",
                            lastError, Utility.GetErrorMessage(lastError));

                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    if (!UNM.CryptDeriveKey(
                            hProvider, UNM.CALG_RC4, hHash, 0, ref hKey))
                    {
                        lastError = Marshal.GetLastWin32Error();

                        error = String.Format(
                            "CryptDeriveKey() failed with error {0}: {1}",
                            lastError, Utility.GetErrorMessage(lastError));

                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    pData = AllocateData(data);

                    if (pData == IntPtr.Zero)
                    {
                        error = "out of memory";
                        return ReturnCode.Error;
                    }

                    ///////////////////////////////////////////////////////////

                    uint oldDataLength = (uint)data.Length;
                    uint newDataLength = oldDataLength;

                    if (encrypt)
                    {
                        if (!UNM.CryptEncrypt(
                                hKey, IntPtr.Zero, true, 0, pData,
                                ref newDataLength, oldDataLength))
                        {
                            lastError = Marshal.GetLastWin32Error();

                            error = String.Format(
                                "CryptEncrypt() failed with error {0}: {1}",
                                lastError, Utility.GetErrorMessage(lastError));

                            return ReturnCode.Error;
                        }
                    }
                    else
                    {
                        if (!UNM.CryptDecrypt(
                                hKey, IntPtr.Zero, true, 0, pData,
                                ref newDataLength))
                        {
                            lastError = Marshal.GetLastWin32Error();

                            error = String.Format(
                                "CryptDecrypt() failed with error {0}: {1}",
                                lastError, Utility.GetErrorMessage(lastError));

                            return ReturnCode.Error;
                        }
                    }

                    ///////////////////////////////////////////////////////////

                    data = new byte[newDataLength];
                    Marshal.Copy(pData, data, 0, (int)newDataLength);

                    return ReturnCode.Ok;
                }
                finally
                {
                    if (pData != IntPtr.Zero)
                    {
                        FreeData(pData);
                        pData = IntPtr.Zero;
                    }

                    ///////////////////////////////////////////////////////////

                    if (pKey != IntPtr.Zero)
                    {
                        FreeData(pKey);
                        pKey = IntPtr.Zero;
                    }

                    ///////////////////////////////////////////////////////////

                    Result localError; /* REUSED */

                    ///////////////////////////////////////////////////////////

                    try
                    {
                        if (hKey != IntPtr.Zero)
                        {
                            if (!UNM.CryptDestroyKey(hKey))
                            {
                                lastError = Marshal.GetLastWin32Error();

                                localError = String.Format(
                                    "CryptDestroyKey() failed " +
                                    "with error {0}: {1}", lastError,
                                    Utility.GetErrorMessage(lastError));

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.DebugTrace(
                                    String.Format(
                                        "Rc4EncryptOrDecrypt: {0}",
                                        Utility.FormatWrapOrNull(
                                            localError)),
                                    typeof(ProtectOps).Name,
                                    TracePriority.Highest);
#endif
                            }

                            hKey = IntPtr.Zero;
                        }
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(
                            e, typeof(ProtectOps).Name,
                            TracePriority.Highest);
#endif
                    }

                    ///////////////////////////////////////////////////////////

                    try
                    {
                        if (hHash != IntPtr.Zero)
                        {
                            if (!UNM.CryptDestroyHash(hHash))
                            {
                                lastError = Marshal.GetLastWin32Error();

                                localError = String.Format(
                                    "CryptDestroyHash() failed " +
                                    "with error {0}: {1}", lastError,
                                    Utility.GetErrorMessage(lastError));

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.DebugTrace(
                                    String.Format(
                                        "Rc4EncryptOrDecrypt: {0}",
                                        Utility.FormatWrapOrNull(
                                            localError)),
                                    typeof(ProtectOps).Name,
                                    TracePriority.Highest);
#endif
                            }

                            hHash = IntPtr.Zero;
                        }
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(
                            e, typeof(ProtectOps).Name,
                            TracePriority.Highest);
#endif
                    }

                    ///////////////////////////////////////////////////////////

                    try
                    {
                        if (hProvider != IntPtr.Zero)
                        {
                            if (!UNM.CryptReleaseContext(hProvider, 0))
                            {
                                lastError = Marshal.GetLastWin32Error();

                                localError = String.Format(
                                    "CryptReleaseContext() failed " +
                                    "with error {0}: {1}", lastError,
                                    Utility.GetErrorMessage(lastError));

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.DebugTrace(
                                    String.Format(
                                        "Rc4EncryptOrDecrypt: {0}",
                                        Utility.FormatWrapOrNull(
                                            localError)),
                                    typeof(ProtectOps).Name,
                                    TracePriority.Highest);
#endif
                            }

                            hProvider = IntPtr.Zero;
                        }
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(
                            e, typeof(ProtectOps).Name,
                            TracePriority.Highest);
#endif
                    }
                }
            }
#endif

            error = "not supported on this operating system";
            return ReturnCode.Error;
        }
        #endregion
    }
}
