/*
 * KeyPair.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides a concrete implementation of a licensing key pair, combining
    /// the inherited key pair metadata with the public key blob fields used
    /// to identify and serialize the key.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("91d22c0a-3c6b-4b31-a336-eaf6b5f82beb")]
    internal class KeyPair : KeyPairMetadata, IKeyPair
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="KeyPair" /> class.
        /// </summary>
        public KeyPair()
            : base()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IKeyPair Members
        #region From PvkKeyBlob
        /// <summary>
        /// The salt bytes associated with the PVK key blob.
        /// </summary>
        private byte[] salt;
        /// <summary>
        /// Gets or sets the salt bytes associated with the PVK key blob.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public byte[] Salt
        {
            get { return salt; }
            set { salt = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region From [RD]SAPUBLICKEY
        /// <summary>
        /// The magic number identifying the public key blob type.
        /// </summary>
        private uint magic;
        /// <summary>
        /// Gets or sets the magic number identifying the public key blob
        /// type.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint Magic
        {
            get { return magic; }
            set { magic = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The size of the key, in bits.
        /// </summary>
        private uint bitLength;
        /// <summary>
        /// Gets or sets the size of the key, in bits.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public uint BitLength
        {
            get { return bitLength; }
            set { bitLength = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adds the name and public key token for this key pair to the
        /// specified list.
        /// </summary>
        /// <param name="list">
        /// The list to which the key pair information is added.
        /// </param>
        public void AddToList(
            ref IList<byte[]> list /* in, out */
            )
        {
            CertificateDataOps.AddKeyPairToList(
                this.Name, this.PublicKeyToken, ref list);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the name and public key token for this key pair to the
        /// specified list.
        /// </summary>
        /// <param name="list">
        /// The list to which the key pair information is added.
        /// </param>
        public void AddToList(
            ref IStringList list /* in, out */
            )
        {
            CertificateDataOps.AddKeyPairToList(
                this.Name, this.PublicKeyToken, ref list);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a list of name/value pairs describing the properties of
        /// this key pair.
        /// </summary>
        /// <returns>
        /// A list of name/value pairs describing this key pair.
        /// </returns>
        public IStringList ToList() /* CORE? */
        {
            StringPairList list = new StringPairList();

            ///////////////////////////////////////////////////////////////////

            list.Add("Kind", this.Kind.ToString());
            list.Add("Id", CertificateDataOps.FormatId(this.Id));

            ///////////////////////////////////////////////////////////////////

            string name = this.Name;

            if (name != null)
                list.Add("Name", name);

            ///////////////////////////////////////////////////////////////////

            string group = this.Group;

            if (group != null)
                list.Add("Group", group);

            ///////////////////////////////////////////////////////////////////

            string description = this.Description;

            if (description != null)
                list.Add("Description", description);

            ///////////////////////////////////////////////////////////////////

            byte[] publicKeyToken = this.PublicKeyToken;

            if (publicKeyToken != null)
            {
                list.Add("PublicKeyToken",
                    CertificateDataOps.FormatPublicKeyToken(
                        publicKeyToken, false, false));
            }

            ///////////////////////////////////////////////////////////////////

            string keyUsage = this.KeyUsage;

            if (keyUsage != null)
                list.Add("KeyUsage", keyUsage);

            ///////////////////////////////////////////////////////////////////

            DateTime? keyExpiration = this.KeyExpiration;

            if (keyExpiration != null)
            {
                list.Add("KeyExpiration",
                    CertificateDataOps.FormatTimeStamp(
                        (DateTime)keyExpiration));
            }

            ///////////////////////////////////////////////////////////////////

            StringList keyDomains = ListKeyDomains();

            if (keyDomains != null)
                list.Add("KeyDomains", keyDomains.ToString());

            ///////////////////////////////////////////////////////////////////

            StringList keyGroups = ListKeyGroups();

            if (keyGroups != null)
                list.Add("KeyGroups", keyGroups.ToString());

            ///////////////////////////////////////////////////////////////////

            string fileName = this.FileName;

            if (fileName != null)
                list.Add("FileName", fileName);

            ///////////////////////////////////////////////////////////////////

            IKeyPair parent = this.Parent as IKeyPair;

            if (parent != null)
            {
                IStringList localList = null;

                parent.AddToList(ref localList);

                list.Add("Parent", localList.ToString());
            }

            ///////////////////////////////////////////////////////////////////

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Creates a list representing this key pair followed by each of its
        /// parent key pairs, forming the chain of trust.
        /// </summary>
        /// <returns>
        /// A list containing this key pair and all of its ancestors.
        /// </returns>
        public IStringList Chain()
        {
            StringPairList list = new StringPairList();

            list.Add(ToString(), MakeName());

            IKeyPairMetadata parent = Parent;

            while (parent != null)
            {
                list.Add(parent.ToString(), parent.MakeName());
                parent = parent.Parent;
            }

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a detailed list of name/value pairs describing this key
        /// pair, including its metadata, key usage, and key blob fields.
        /// </summary>
        /// <returns>
        /// A list of name/value pairs describing this key pair in detail.
        /// </returns>
        public virtual IStringList Dump()
        {
            StringPairList list = ToList() as StringPairList;

            if (list != null)
            {
                StringPairList localList = new StringPairList();

                ///////////////////////////////////////////////////////////////

                #region IKeyPairMetadata Members
                localList.Add("KeyPairType", KeyPairType.ToString());
                localList.Add("KeyFileFormat", KeyFileFormat.ToString());
                localList.Add("HavePublicKey", HavePublicKey.ToString());
                localList.Add("HavePrivateKey", HavePrivateKey.ToString());

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    if (list.Count > 0)
                    {
                        list.Insert(0, null);
                        list.Insert(0, "IKeyPairMetadata Members");
                    }
                    else
                    {
                        list.Add("IKeyPairMetadata Members");
                        list.Add((IPair<string>)null);
                    }

                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region Key Usage
                CertificateKeyPairOps.KeyUsageToList(
                    this.KeyUsage, Utility.DefaultAttributeFlagsKey(),
                    ref localList);

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("Key Usage");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region KeyPairMetadataBase Members
                bool approved = IsApproved();

                if (approved)
                    localList.Add("Approved", approved.ToString());

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("KeyPairMetadataBase Members");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From PvkKeyBlob
                byte[] salt = this.Salt;

                if (salt != null)
                {
                    localList.Add("Salt", Convert.ToBase64String(
                        salt, Base64FormattingOptions.InsertLineBreaks));
                }

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From PvkKeyBlob");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion

                ///////////////////////////////////////////////////////////////

                #region From [RD]SAPUBLICKEY
                localList.Add("Magic", Magic.ToString());
                localList.Add("BitLength", BitLength.ToString());

                ///////////////////////////////////////////////////////////////

                if (localList.Count > 0)
                {
                    list.Add((IPair<string>)null);
                    list.Add("From PUBLICKEY");
                    list.Add((IPair<string>)null);
                    list.Add(localList);

                    localList.Clear();
                }
                #endregion
            }

            return list;
        }
#endif
#endif
        #endregion
    }
}
