/*
 * Rfc2898Data.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Threading;
using Eagle._Attributes;

#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
using Eagle._Components.Public;
#endif

using Eagle._Interfaces.Public;

namespace Zeus.Components.Public
{
    /// <summary>
    /// Holds the RFC 2898 (PBKDF2) key-derivation parameters (password, salt,
    /// iteration count, hash algorithm name, and signature) used by the Zeus
    /// plugin, tracking which of them have been explicitly set.  Access to the
    /// stored values is gated by a global enabled flag, so the data can be
    /// effectively disabled at runtime.  When isolated interpreters or plugins
    /// are enabled, it derives from <c>ScriptMarshalByRefObject</c> so
    /// it can cross application domain boundaries.
    /// </summary>
    [ObjectId("e0ef9d9c-0b80-42c0-9518-58e0ab7715ae")]
    public sealed class Rfc2898Data :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IRfc2898Data
    {
        #region Private Static Data
        /// <summary>
        /// The reference count controlling whether this data is enabled; a
        /// value greater than zero means access to the stored parameters is
        /// permitted.
        /// </summary>
        private static int enabledCount = 1;

        /// <summary>
        /// The reference count controlling whether this data is persistent; a
        /// value greater than zero prevents it from being reset.
        /// </summary>
        private static int persistentCount = 1;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new, empty <see cref="Rfc2898Data" /> instance.
        /// </summary>
        public Rfc2898Data()
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new <see cref="Rfc2898Data" /> instance initialized
        /// with the specified key-derivation parameters.  The parameters are
        /// only stored when this data is currently enabled.
        /// </summary>
        /// <param name="password">
        /// The password used as input material for key derivation.
        /// </param>
        /// <param name="salt">
        /// The salt used for key derivation.
        /// </param>
        /// <param name="iterationCount">
        /// The iteration count used for key derivation.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used for key derivation.
        /// </param>
        /// <param name="signature">
        /// The signature associated with the derived key data.
        /// </param>
        public Rfc2898Data(
            string password,          /* in */
            string salt,              /* in */
            int iterationCount,       /* in */
            string hashAlgorithmName, /* in */
            string signature          /* in */
            )
            : this()
        {
            if (IsEnabled())
            {
                this.password = password;
                this.salt = salt;
                this.iterationCount = iterationCount;
                this.hashAlgorithmName = hashAlgorithmName;
                this.signature = signature;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether this RFC 2898 data is currently enabled, in
        /// which case its stored parameters may be read and written.
        /// </summary>
        /// <returns>
        /// Non-zero if the data is enabled; otherwise, zero.
        /// </returns>
        internal static bool IsEnabled()
        {
            return Interlocked.CompareExchange(ref enabledCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this RFC 2898 data is currently persistent, in
        /// which case it cannot be reset on a manager.
        /// </summary>
        /// <returns>
        /// Non-zero if the data is persistent; otherwise, zero.
        /// </returns>
        private static bool IsPersistent()
        {
            return Interlocked.CompareExchange(ref persistentCount, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the RFC 2898 data held by the specified manager, unless the
        /// data is persistent, the manager is invalid, or the manager does
        /// not hold an instance of this class.
        /// </summary>
        /// <param name="manager">
        /// The RFC 2898 data manager whose data may be reset.
        /// </param>
        /// <returns>
        /// Non-zero if the manager's data was reset; otherwise, zero.
        /// </returns>
        internal static bool MaybeReset(
            IRfc2898DataManager manager /* in */
            )
        {
            if (IsPersistent())
                return false;

            if (manager == null)
                return false;

            Rfc2898Data data = manager.Rfc2898Data as Rfc2898Data;

            if (data == null)
                return false;

            manager.Rfc2898Data = null;
            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The backing field for the <see cref="Password" /> property.
        /// </summary>
        private string password;

        /// <summary>
        /// Gets or sets the password used as input material for RFC 2898 key
        /// derivation.  Reads return null and writes are ignored while the
        /// data is disabled; a successful write also records that the password
        /// has been explicitly set.
        /// </summary>
        public string Password
        {
            internal get { return IsEnabled() ? password : null; }
            set
            {
                if (IsEnabled())
                {
                    password = value;
                    passwordSet = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="PasswordSet" /> property.
        /// </summary>
        private bool passwordSet;

        /// <summary>
        /// Gets a value indicating whether the password has been explicitly
        /// set; always false while the data is disabled.
        /// </summary>
        public bool PasswordSet
        {
            get { return IsEnabled() ? passwordSet : false; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Salt" /> property.
        /// </summary>
        private string salt;

        /// <summary>
        /// Gets or sets the salt used for RFC 2898 key derivation.  Reads
        /// return null and writes are ignored while the data is disabled; a
        /// successful write also records that the salt has been explicitly
        /// set.
        /// </summary>
        public string Salt
        {
            internal get { return IsEnabled() ? salt : null; }
            set
            {
                if (IsEnabled())
                {
                    salt = value;
                    saltSet = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="SaltSet" /> property.
        /// </summary>
        private bool saltSet;

        /// <summary>
        /// Gets a value indicating whether the salt has been explicitly set;
        /// always false while the data is disabled.
        /// </summary>
        public bool SaltSet
        {
            get { return IsEnabled() ? saltSet : false; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="IterationCount" /> property.
        /// </summary>
        private int iterationCount;

        /// <summary>
        /// Gets or sets the iteration count used for RFC 2898 key derivation.
        /// Reads return zero and writes are ignored while the data is
        /// disabled; a successful write also records that the iteration count
        /// has been explicitly set.
        /// </summary>
        public int IterationCount
        {
            internal get { return IsEnabled() ? iterationCount : 0; }
            set
            {
                if (IsEnabled())
                {
                    iterationCount = value;
                    iterationCountSet = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="IterationCountSet" />
        /// property.
        /// </summary>
        private bool iterationCountSet;

        /// <summary>
        /// Gets a value indicating whether the iteration count has been
        /// explicitly set; always false while the data is disabled.
        /// </summary>
        public bool IterationCountSet
        {
            get { return IsEnabled() ? iterationCountSet : false; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HashAlgorithmName" />
        /// property.
        /// </summary>
        private string hashAlgorithmName;

        /// <summary>
        /// Gets or sets the name of the hash algorithm used for RFC 2898 key
        /// derivation.  Reads return null and writes are ignored while the
        /// data is disabled; a successful write also records that the hash
        /// algorithm name has been explicitly set.
        /// </summary>
        public string HashAlgorithmName
        {
            internal get { return IsEnabled() ? hashAlgorithmName : null; }
            set
            {
                if (IsEnabled())
                {
                    hashAlgorithmName = value;
                    hashAlgorithmNameSet = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HashAlgorithmNameSet" />
        /// property.
        /// </summary>
        private bool hashAlgorithmNameSet;

        /// <summary>
        /// Gets a value indicating whether the hash algorithm name has been
        /// explicitly set; always false while the data is disabled.
        /// </summary>
        public bool HashAlgorithmNameSet
        {
            get { return IsEnabled() ? hashAlgorithmNameSet : false; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Signature" /> property.
        /// </summary>
        private string signature;

        /// <summary>
        /// Gets or sets the signature associated with the derived key data.
        /// Reads return null and writes are ignored while the data is
        /// disabled; a successful write also records that the signature has
        /// been explicitly set.
        /// </summary>
        public string Signature
        {
            internal get { return IsEnabled() ? signature : null; }
            set
            {
                if (IsEnabled())
                {
                    signature = value;
                    signatureSet = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="SignatureSet" /> property.
        /// </summary>
        private bool signatureSet;

        /// <summary>
        /// Gets a value indicating whether the signature has been explicitly
        /// set; always false while the data is disabled.
        /// </summary>
        public bool SignatureSet
        {
            get { return IsEnabled() ? signatureSet : false; }
        }
        #endregion
    }
}
