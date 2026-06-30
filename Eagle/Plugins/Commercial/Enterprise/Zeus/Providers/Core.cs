/*
 * Core.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Zeus.Providers
{
    /// <summary>
    /// Provides the core implementation of an RFC 2898 (PBKDF2) data
    /// provider.  It holds the key-derivation parameters (password, salt,
    /// iteration count, hash algorithm name, and signature), tracks which of
    /// them have been explicitly set, and supplies any that are missing when
    /// <see cref="GetData" /> is called.  It serves as the base class for the
    /// more specialized providers (such as the script-based and remote
    /// providers).
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a4164d4d-1824-4cc0-b17a-997f85205b5d")]
    public class Core : Default, IRfc2898Data
    {
        #region Protected Constructors
        /// <summary>
        /// Constructs a new, empty <see cref="Core" /> provider instance.
        /// This constructor is intended for use by derived classes.
        /// </summary>
        protected Core()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Internal Constructors
        /// <summary>
        /// Constructs a new <see cref="Core" /> provider instance associated
        /// with the specified interpreter and caller data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter this provider is associated with.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        internal Core(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
        {
            this.interpreter = interpreter;
            this.clientData = clientData;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898Data Members
        /// <summary>
        /// The backing field for the <see cref="Password" /> property.
        /// </summary>
        private string password;

        /// <summary>
        /// Gets or sets the password used as input material for RFC 2898 key
        /// derivation.  Reading is restricted to this class; assigning a
        /// value also records that the password has been explicitly set.
        /// </summary>
        public virtual string Password
        {
            private get { return password; }
            set { password = value; passwordSet = true; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="PasswordSet" /> property.
        /// </summary>
        private bool passwordSet;

        /// <summary>
        /// Gets a value indicating whether the password has been explicitly
        /// set.
        /// </summary>
        public bool PasswordSet
        {
            get { return passwordSet; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Salt" /> property.
        /// </summary>
        private string salt;

        /// <summary>
        /// Gets or sets the salt used for RFC 2898 key derivation.  Reading
        /// is restricted to this class; assigning a value also records that
        /// the salt has been explicitly set.
        /// </summary>
        public virtual string Salt
        {
            private get { return salt; }
            set { salt = value; saltSet = true; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="SaltSet" /> property.
        /// </summary>
        private bool saltSet;

        /// <summary>
        /// Gets a value indicating whether the salt has been explicitly set.
        /// </summary>
        public bool SaltSet
        {
            get { return saltSet; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="IterationCount" /> property.
        /// </summary>
        private int iterationCount;

        /// <summary>
        /// Gets or sets the iteration count used for RFC 2898 key derivation.
        /// Reading is restricted to this class; assigning a value also
        /// records that the iteration count has been explicitly set.
        /// </summary>
        public virtual int IterationCount
        {
            private get { return iterationCount; }
            set { iterationCount = value; iterationCountSet = true; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="IterationCountSet" />
        /// property.
        /// </summary>
        private bool iterationCountSet;

        /// <summary>
        /// Gets a value indicating whether the iteration count has been
        /// explicitly set.
        /// </summary>
        public bool IterationCountSet
        {
            get { return iterationCountSet; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HashAlgorithmName" />
        /// property.
        /// </summary>
        private string hashAlgorithmName;

        /// <summary>
        /// Gets or sets the name of the hash algorithm used for RFC 2898 key
        /// derivation.  Reading is restricted to this class; assigning a
        /// value also records that the hash algorithm name has been
        /// explicitly set.
        /// </summary>
        public virtual string HashAlgorithmName
        {
            private get { return hashAlgorithmName; }
            set { hashAlgorithmName = value; hashAlgorithmNameSet = true; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HashAlgorithmNameSet" />
        /// property.
        /// </summary>
        private bool hashAlgorithmNameSet;

        /// <summary>
        /// Gets a value indicating whether the hash algorithm name has been
        /// explicitly set.
        /// </summary>
        public bool HashAlgorithmNameSet
        {
            get { return hashAlgorithmNameSet; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Signature" /> property.
        /// </summary>
        private string signature;

        /// <summary>
        /// Gets or sets the signature associated with the derived key data.
        /// Reading is restricted to this class; assigning a value also
        /// records that the signature has been explicitly set.
        /// </summary>
        public virtual string Signature
        {
            private get { return signature; }
            set { signature = value; signatureSet = true; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="SignatureSet" /> property.
        /// </summary>
        private bool signatureSet;

        /// <summary>
        /// Gets a value indicating whether the signature has been explicitly
        /// set.
        /// </summary>
        public bool SignatureSet
        {
            get { return signatureSet; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetClientData / ISetClientData Members
        /// <summary>
        /// The backing field for the <see cref="ClientData" /> property.
        /// </summary>
        private IClientData clientData;

        /// <summary>
        /// Gets or sets the extra data associated with this provider by the
        /// caller, if any.
        /// </summary>
        public override IClientData ClientData
        {
            get { return clientData; }
            set { clientData = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter / ISetInterpreter Members
        /// <summary>
        /// The backing field for the <see cref="Interpreter" /> property.
        /// </summary>
        private Interpreter interpreter;

        /// <summary>
        /// Gets or sets the interpreter this provider is associated with.
        /// </summary>
        public override Interpreter Interpreter
        {
            get { return interpreter; }
            set { interpreter = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898DataProvider Members
        //
        // BUGBUG: The use of a plain string here instead of something like
        //         the SecureString class is due to the requirements of the
        //         Rfc2898DeriveBytes class.
        //
        /// <summary>
        /// Supplies the RFC 2898 key-derivation parameters.  For each
        /// parameter that has been explicitly set on this provider but is
        /// missing (null, empty, or non-positive) in the corresponding
        /// reference argument, the stored value is copied into that argument;
        /// values already present are left unchanged.
        /// </summary>
        /// <param name="fileName">
        /// An optional file name; not used by this implementation.
        /// </param>
        /// <param name="encodingName">
        /// An optional encoding name; not used by this implementation.
        /// </param>
        /// <param name="password">
        /// On input, the caller-supplied password, if any; on output,
        /// receives this provider's password when one was set and none was
        /// supplied.
        /// </param>
        /// <param name="salt">
        /// On input, the caller-supplied salt, if any; on output, receives
        /// this provider's salt when one was set and none was supplied.
        /// </param>
        /// <param name="iterationCount">
        /// On input, the caller-supplied iteration count, if any; on output,
        /// receives this provider's iteration count when one was set and a
        /// non-positive value was supplied.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, the caller-supplied hash algorithm name, if any; on
        /// output, receives this provider's hash algorithm name when one was
        /// set and none was supplied.
        /// </param>
        /// <param name="signature">
        /// On input, the caller-supplied signature, if any; on output,
        /// receives this provider's signature when one was set and none was
        /// supplied.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode GetData(
            string fileName,              /* in: OPTIONAL, NOT USED */
            string encodingName,          /* in: OPTIONAL, NOT USED */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (this.PasswordSet && String.IsNullOrEmpty(password))
                password = this.Password;

            if (this.SaltSet && String.IsNullOrEmpty(salt))
                salt = this.Salt;

            if (this.IterationCountSet && (iterationCount <= 0))
                iterationCount = this.IterationCount;

            if (this.HashAlgorithmNameSet &&
                String.IsNullOrEmpty(hashAlgorithmName))
            {
                hashAlgorithmName = this.HashAlgorithmName;
            }

            if (this.SignatureSet && String.IsNullOrEmpty(signature))
                signature = this.Signature;

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }
        #endregion
    }
}
