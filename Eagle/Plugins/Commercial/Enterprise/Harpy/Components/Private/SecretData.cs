/*
 * SecretData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using DataOps = Licensing.Components.Private.CertificateDataOps;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides a concrete container for secret data together with the
    /// cryptographic parameters and identity metadata needed to process
    /// it (e.g. hashing, key derivation, encryption, and signing).
    /// </summary>
    [ObjectId("b15b1d18-9912-4072-9dd1-468505c424ec")]
    internal sealed class SecretData :
            CryptographyData, IIdentifier, IHaveEncoding, ISecretData
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the mutable state of
        /// this instance.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs an empty instance of the <see cref="SecretData" />
        /// class.
        /// </summary>
        public SecretData()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Creates a new <see cref="ISecretData" /> instance from the
        /// specified textual representation, populating its identity,
        /// encoding, key-derivation, cryptography, and secret data from the
        /// parsed name/value pairs.
        /// </summary>
        /// <param name="text">
        /// The textual representation containing the name/value pairs used
        /// to populate the resulting instance.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when interpreting culture-sensitive values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The newly created <see cref="ISecretData" /> instance, or null if
        /// it could not be created.
        /// </returns>
        public static ISecretData Create(
            string text,             /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            StringDictionary dictionary = StringDictionary.FromString(
                text, false, false, ref error);

            if (dictionary == null)
                return null;

            SecretData secretData = new SecretData();

            if (SecretOps.SetData(dictionary,
                    cultureInfo, secretData as IIdentifier,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (SecretOps.SetData(dictionary,
                    cultureInfo, secretData as IHaveEncoding,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (SecretOps.SetData(dictionary,
                    cultureInfo, secretData as IRfc2898Data,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (SecretOps.SetData(dictionary,
                    cultureInfo, secretData as ICryptographyData,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (SecretOps.SetData(dictionary,
                    cultureInfo, secretData as ISecretData,
                    ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return secretData;
        }
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Resets all identity, encoding, and secret data fields of this
        /// instance to their default values.
        /// </summary>
        private void ResetData()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                name = null;
                kind = IdentifierKind.None;
                id = Guid.Empty;
                group = null;
                description = null;

                ///////////////////////////////////////////////////////////////

                clientData = null;

                ///////////////////////////////////////////////////////////////

                encoding = null;

                ///////////////////////////////////////////////////////////////

                flags = SecretDataFlags.None;
                input = null;
                auxiliary = null;
                output = null;
                signature = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the input bytes to a Base64-encoded string when the
        /// <see cref="SecretDataFlags.InputBase64" /> flag is set.
        /// </summary>
        private void MaybeMutateInput()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((InputBytes != null) && Utility.HasFlags(
                        flags, SecretDataFlags.InputBase64, true))
                {
                    InputString = Convert.ToBase64String(
                        InputBytes.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the auxiliary bytes to a Base64-encoded string when the
        /// <see cref="SecretDataFlags.AuxiliaryBase64" /> flag is set.
        /// </summary>
        private void MaybeMutateAuxiliary()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((AuxiliaryBytes != null) && Utility.HasFlags(
                        flags, SecretDataFlags.AuxiliaryBase64, true))
                {
                    AuxiliaryString = Convert.ToBase64String(
                        AuxiliaryBytes.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the output bytes to a Base64-encoded string when the
        /// <see cref="SecretDataFlags.OutputBase64" /> flag is set.
        /// </summary>
        private void MaybeMutateOutput()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((OutputBytes != null) && Utility.HasFlags(
                        flags, SecretDataFlags.OutputBase64, true))
                {
                    OutputString = Convert.ToBase64String(
                        OutputBytes.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the signature bytes to a Base64-encoded string when the
        /// <see cref="SecretDataFlags.SignatureBase64" /> flag is set.
        /// </summary>
        private void MaybeMutateSignature()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((SignatureBytes != null) && Utility.HasFlags(
                        flags, SecretDataFlags.SignatureBase64, true))
                {
                    SignatureString = Convert.ToBase64String(
                        SignatureBytes.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks);
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Properties
        /// <summary>
        /// Gets the secret operation that is currently selected via the
        /// operation bits of <see cref="Flags" />.
        /// </summary>
        private SecretDataFlags Operation
        {
            get
            {
                lock (syncRoot)
                {
                    return flags & SecretDataFlags.OperationMask;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string representation of this instance, including its
        /// identity, encoding, key-derivation, cryptography, and secret data
        /// values.
        /// </summary>
        /// <returns>
        /// A string representation of this instance.
        /// </returns>
        public override string ToString()
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                StringPairList list = new StringPairList();

                ///////////////////////////////////////////////////////////////
                // ******************** IIdentifierName ******************** //
                ///////////////////////////////////////////////////////////////

                if (name != null)
                    list.Add("name", name);

                ///////////////////////////////////////////////////////////////
                // ******************** IIdentifierBase ******************** //
                ///////////////////////////////////////////////////////////////

                if (kind != IdentifierKind.None)
                    list.Add("kind", kind.ToString());

                if (!id.Equals(Guid.Empty))
                    list.Add("id", id.ToString());

                ///////////////////////////////////////////////////////////////
                // ********************** IIdentifier ********************** //
                ///////////////////////////////////////////////////////////////

                if (group != null)
                    list.Add("group", group);

                if (description != null)
                    list.Add("description", description);

                ///////////////////////////////////////////////////////////////
                // ********************* IHaveEncoding ********************* //
                ///////////////////////////////////////////////////////////////

                if (encoding != null)
                    list.Add("encodingName", encoding.WebName);

                ///////////////////////////////////////////////////////////////
                // ********************* IRfc2898Data ********************** //
                ///////////////////////////////////////////////////////////////

                string password;
                string salt;
                int iterationCount;
                string hashAlgorithmName;
                string signature;
                Result error = null; /* NOT USED */

                /* IGNORED */
                SecretOps.ExtractData(
                    this, false, out password, out salt, out iterationCount,
                    out hashAlgorithmName, out signature, ref error);

                if (password != null)
                {
                    list.Add("password", String.Format(
                        "<string:{0}>", password.Length));
                }

                if (salt != null)
                {
                    list.Add("salt", String.Format(
                        "<string:{0}>", salt.Length));
                }

                if (iterationCount != 0)
                    list.Add("iterationCount", iterationCount.ToString());

                if (hashAlgorithmName != null)
                    list.Add("hashAlgorithmName", hashAlgorithmName);

                if (signature != null)
                    list.Add("signature", signature);

                ///////////////////////////////////////////////////////////////
                // ******************* ICryptographyData ******************* //
                ///////////////////////////////////////////////////////////////

                string symmetricAlgorithmName = base.SymmetricAlgorithmName;

                if (symmetricAlgorithmName != null)
                {
                    list.Add("symmetricAlgorithmName",
                        symmetricAlgorithmName);
                }

                CipherMode cipherMode = base.CipherMode;

                if (cipherMode != (CipherMode)0)
                    list.Add("cipherMode", cipherMode.ToString());

                PaddingMode paddingMode = base.PaddingMode;

                if (paddingMode != (PaddingMode)0)
                    list.Add("paddingMode", paddingMode.ToString());

                ByteList iv = base.Iv;

                if (iv != null)
                {
                    list.Add("iv", Convert.ToBase64String(iv.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks));
                }

                ByteList key = base.Key;

                if (key != null)
                {
                    list.Add("key", Convert.ToBase64String(key.ToArray(),
                        Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////
                // ********************** ISecretData ********************** //
                ///////////////////////////////////////////////////////////////

                if (flags != SecretDataFlags.None)
                    list.Add("flags", flags.ToString());

                ///////////////////////////////////////////////////////////////

                ByteList bytes; /* REUSED */

                if (input != null)
                {
                    bytes = input as ByteList;

                    if (bytes != null)
                    {
                        list.Add("input", String.Format(
                            "<byteList:{0}>", bytes.Count));
                    }
                    else
                    {
                        list.Add("input",
                            input.GetType().ToString());
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (auxiliary != null)
                {
                    bytes = auxiliary as ByteList;

                    if (bytes != null)
                    {
                        list.Add("auxiliary", String.Format(
                            "<byteList:{0}>", bytes.Count));
                    }
                    else
                    {
                        IKeyPair keyPair = auxiliary as IKeyPair;

                        if (keyPair != null)
                        {
                            byte[] publicKeyToken = keyPair.PublicKeyToken;

                            if ((publicKeyToken != null) &&
                                (publicKeyToken.Length > 0))
                            {
                                list.Add("auxiliary",
                                    String.Format("<keyPair:{0}>",
                                        DataOps.FormatPublicKeyToken(
                                            publicKeyToken, true,
                                            true))); /* DIAGNOSTICS */
                            }
                            else
                            {
                                list.Add("auxiliary", "<keyPair>");
                            }
                        }
                        else
                        {
                            list.Add("auxiliary",
                                auxiliary.GetType().ToString());
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (output != null)
                {
                    bytes = output as ByteList;

                    if (bytes != null)
                    {
                        list.Add("output", String.Format(
                            "<byteList:{0}>", bytes.Count));
                    }
                    else
                    {
                        list.Add("output",
                            output.GetType().ToString());
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (signature != null)
                {
                    bytes = this.signature as ByteList;

                    if (bytes != null)
                    {
                        list.Add("signature", String.Format(
                            "<byteList:{0}>", bytes.Count));
                    }
                    else
                    {
                        list.Add("signature",
                            signature.GetType().ToString());
                    }
                }

                ///////////////////////////////////////////////////////////////

                return list.ToString();
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Members
        /// <summary>
        /// The name associated with this instance.
        /// </summary>
        private string name;
        /// <summary>
        /// Gets or sets the name associated with this instance.
        /// </summary>
        public string Name
        {
            get { CheckDisposed(); lock (syncRoot) { return name; } }
            set { CheckDisposed(); lock (syncRoot) { name = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierBase Members
        /// <summary>
        /// The kind of identifier associated with this instance.
        /// </summary>
        private IdentifierKind kind;
        /// <summary>
        /// Gets or sets the kind of identifier associated with this
        /// instance.
        /// </summary>
        public IdentifierKind Kind
        {
            get { CheckDisposed(); lock (syncRoot) { return kind; } }
            set { CheckDisposed(); lock (syncRoot) { kind = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The unique identifier associated with this instance.
        /// </summary>
        private Guid id;
        /// <summary>
        /// Gets or sets the unique identifier associated with this instance.
        /// </summary>
        public Guid Id
        {
            get { CheckDisposed(); lock (syncRoot) { return id; } }
            set { CheckDisposed(); lock (syncRoot) { id = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetClientData / ISetClientData Members
        /// <summary>
        /// The opaque, caller-defined data associated with this instance.
        /// </summary>
        private IClientData clientData;
        /// <summary>
        /// Gets or sets the opaque, caller-defined data associated with this
        /// instance.
        /// </summary>
        public IClientData ClientData
        {
            get { CheckDisposed(); lock (syncRoot) { return clientData; } }
            set { CheckDisposed(); lock (syncRoot) { clientData = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifier Members
        /// <summary>
        /// The group associated with this instance.
        /// </summary>
        private string group;
        /// <summary>
        /// Gets or sets the group associated with this instance.
        /// </summary>
        public string Group
        {
            get { CheckDisposed(); lock (syncRoot) { return group; } }
            set { CheckDisposed(); lock (syncRoot) { group = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The description associated with this instance.
        /// </summary>
        private string description;
        /// <summary>
        /// Gets or sets the description associated with this instance.
        /// </summary>
        public string Description
        {
            get { CheckDisposed(); lock (syncRoot) { return description; } }
            set { CheckDisposed(); lock (syncRoot) { description = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveEncoding Members
        /// <summary>
        /// The text encoding associated with this instance.
        /// </summary>
        private Encoding encoding;
        /// <summary>
        /// Gets or sets the text encoding associated with this instance.
        /// </summary>
        public Encoding Encoding
        {
            get { CheckDisposed(); lock (syncRoot) { return encoding; } }
            set { CheckDisposed(); lock (syncRoot) { encoding = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ISynchronizeBase Members
        /// <summary>
        /// Gets the object used to synchronize access to this instance.
        /// </summary>
        public object SyncRoot
        {
            get { CheckDisposed(); return syncRoot; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ISecretData Members
        /// <summary>
        /// The flags that control the behavior and selected operation of
        /// this instance.
        /// </summary>
        private SecretDataFlags flags;
        /// <summary>
        /// Gets or sets the flags that control the behavior and selected
        /// operation of this instance.
        /// </summary>
        public SecretDataFlags Flags
        {
            get { CheckDisposed(); lock (syncRoot) { return flags; } }
            set { CheckDisposed(); lock (syncRoot) { flags = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The input data to be processed by this instance.
        /// </summary>
        private object input;
        /// <summary>
        /// Gets a value indicating whether input data is present.
        /// </summary>
        public bool HaveInput
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return input != null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The auxiliary data used when processing this instance (e.g. a key
        /// pair).
        /// </summary>
        private object auxiliary;
        /// <summary>
        /// Gets a value indicating whether auxiliary data is present.
        /// </summary>
        public bool HaveAuxiliary
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return auxiliary != null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The output data produced by processing this instance.
        /// </summary>
        private object output;
        /// <summary>
        /// Gets a value indicating whether output data is present.
        /// </summary>
        public bool HaveOutput
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return output != null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The signature data associated with this instance.
        /// </summary>
        private object signature;
        /// <summary>
        /// Gets a value indicating whether signature data is present.
        /// </summary>
        public bool HaveSignature
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return signature != null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the input data to be processed by this instance.
        /// </summary>
        public object Input
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return input;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    input = value;

                    MaybeMutateInput();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the input data interpreted as a string.
        /// </summary>
        public string InputString
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return input as string;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    input = value;

                    MaybeMutateInput();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the input data interpreted as a list of bytes.
        /// </summary>
        public ByteList InputBytes
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return input as ByteList;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    input = value;

                    MaybeMutateInput();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the auxiliary data interpreted as a string.
        /// </summary>
        public string AuxiliaryString
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return auxiliary as string;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    auxiliary = value;

                    MaybeMutateAuxiliary();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the auxiliary data interpreted as a list of bytes.
        /// </summary>
        public ByteList AuxiliaryBytes
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return auxiliary as ByteList;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    auxiliary = value;

                    MaybeMutateAuxiliary();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the output data interpreted as a string.
        /// </summary>
        public string OutputString
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return output as string;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    output = value;

                    MaybeMutateOutput();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the output data interpreted as a list of bytes.
        /// </summary>
        public ByteList OutputBytes
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return output as ByteList;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    output = value;

                    MaybeMutateOutput();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the signature data interpreted as a string.
        /// </summary>
        public string SignatureString
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return signature as string;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    signature = value;

                    MaybeMutateSignature();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the signature data interpreted as a list of bytes.
        /// </summary>
        public ByteList SignatureBytes
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return signature as ByteList;
                }
            }
            set
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    signature = value;

                    MaybeMutateSignature();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the secret operation currently selected by the operation
        /// bits of <see cref="Flags" />, using the input, auxiliary, output,
        /// and signature data of this instance.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a value
        /// indicating the type of failure.
        /// </returns>
        public ReturnCode Process(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                switch (Operation)
                {
                    case SecretDataFlags.None:
                        {
                            return ReturnCode.Ok;
                        }
                    case SecretDataFlags.Nop:
                        {
                            return SecretDataOps.Nop(
                                this, ref error);
                        }
                    case SecretDataFlags.Salt:
                        {
                            return SecretDataOps.Salt(
                                this, ref error);
                        }
                    case SecretDataFlags.Hash:
                        {
                            return SecretDataOps.Hash(
                                this, GetHashAlgorithmName(),
                                ref error);
                        }
                    case SecretDataFlags.Derive:
                        {
                            return SecretDataOps.Derive(
                                this, this, this, null,
                                ref error);
                        }
                    case SecretDataFlags.Encrypt:
                        {
                            return SecretDataOps.EncryptOrDecrypt(
                                this, this, this, this, true,
                                ref error);
                        }
                    case SecretDataFlags.Decrypt:
                        {
                            return SecretDataOps.EncryptOrDecrypt(
                                this, this, this, this, false,
                                ref error);
                        }
                    case SecretDataFlags.Sign:
                        {
                            return SecretDataOps.Sign(
                                auxiliary as IKeyPair, this,
                                GetHashAlgorithmName(),
                                ref error);
                        }
                    case SecretDataFlags.Verify:
                        {
                            return SecretDataOps.Verify(
                                auxiliary as IKeyPair, this,
                                GetHashAlgorithmName(),
                                ref error);
                        }
                    default:
                        {
                            error = String.Format(
                                "unsupported secret operation {0}",
                                Utility.FormatWrapOrNull(Operation));

                            return ReturnCode.Error;
                        }
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has already been disposed and the engine
        /// is configured to throw in that case.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, null))
                throw new ObjectDisposedException(typeof(SecretData).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called to dispose of managed and
        /// unmanaged resources; otherwise, only unmanaged resources are
        /// released.
        /// </param>
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        ResetData();
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
}
