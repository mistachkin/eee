/*
 * KeyPairMetadataBase.cs --
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
using System.Globalization;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using _KeyUsage = Licensing.Components.Private.KeyUsage;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the base implementation of the
    /// <see cref="IKeyPairMetadataBase" /> interface, holding the metadata
    /// associated with a licensing key pair, including its key usage flags,
    /// expiration, allowed domains, and key groups.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("2bfb778d-0aa5-4c14-b390-98c2638a13d0")]
    internal class KeyPairMetadataBase : Identifier, IKeyPairMetadataBase
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the
        /// <see cref="KeyPairMetadataBase" /> class with the specified key
        /// pair metadata.
        /// </summary>
        /// <param name="keyUsage">
        /// The key usage flags that describe how the key pair may be used.
        /// </param>
        /// <param name="keyExpiration">
        /// The optional date and time when the key pair expires, or null if
        /// it does not expire.
        /// </param>
        /// <param name="keyDomains">
        /// The list of domains for which the key pair is valid, or null if
        /// any domain is allowed.
        /// </param>
        /// <param name="keyGroups">
        /// The list of key groups (public key tokens) associated with the
        /// key pair, or null if there are none.
        /// </param>
        public KeyPairMetadataBase(
            string keyUsage,          /* in */
            DateTime? keyExpiration,  /* in */
            IList<string> keyDomains, /* in */
            IList<byte[]> keyGroups   /* in */
            )
            : base(IdentifierKind.KeyPair)
        {
            this.keyUsage = keyUsage;
            this.keyExpiration = keyExpiration;
            this.keyDomains = keyDomains;
            this.keyGroups = keyGroups;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IKeyPairMetadataBase Members
        #region Metadata Properties
        /// <summary>
        /// The key usage flags that describe how the key pair may be used.
        /// </summary>
        private string keyUsage;
        /// <summary>
        /// Gets or sets the key usage flags that describe how the key pair
        /// may be used.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public string KeyUsage /* CORE? */
        {
            get { return keyUsage; }
            set { keyUsage = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The optional date and time when the key pair expires, or null if
        /// it does not expire.
        /// </summary>
        private DateTime? keyExpiration;
        /// <summary>
        /// Gets or sets the optional date and time when the key pair expires.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public DateTime? KeyExpiration /* CORE? */
        {
            get { return keyExpiration; }
            set { keyExpiration = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of domains for which the key pair is valid, or null if
        /// any domain is allowed.
        /// </summary>
        private IList<string> keyDomains;
        /// <summary>
        /// Gets or sets the list of domains for which the key pair is valid.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public IList<string> KeyDomains /* CORE? */
        {
            get { return keyDomains; }
            set { keyDomains = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of key groups (public key tokens) associated with the
        /// key pair, or null if there are none.
        /// </summary>
        private IList<byte[]> keyGroups;
        /// <summary>
        /// Gets or sets the list of key groups (public key tokens) associated
        /// with the key pair.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        public IList<byte[]> KeyGroups /* CORE? */
        {
            get { return keyGroups; }
            set { keyGroups = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Ring Loader & Policy Implementation Usage
        #region Key Domain Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether any key domains are configured for this key
        /// pair.
        /// </summary>
        /// <returns>
        /// Non-zero if at least one key domain is configured; otherwise,
        /// zero.
        /// </returns>
        public bool HasAnyKeyDomain() /* CORE? */
        {
            return (keyDomains != null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified key domain to the list of domains for which
        /// this key pair is valid, creating the list if necessary.
        /// </summary>
        /// <param name="keyDomain">
        /// The key domain to add.
        /// </param>
        public void AddKeyDomain( /* CORE? */
            string keyDomain /* in */
            )
        {
            if (keyDomains == null)
                keyDomains = new List<string>();

            keyDomains.Add(keyDomain);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified <paramref name="uri" /> matches
        /// any of the key domains configured for this key pair, taking the
        /// key usage flags into account.  When no key domains are configured,
        /// all URIs (including local files) are allowed.
        /// </summary>
        /// <param name="uri">
        /// The URI whose host is checked against the configured key domains.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive comparisons.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the
        /// URI did not match any key domain.
        /// </param>
        /// <returns>
        /// Non-zero if the URI matches one of the configured key domains (or
        /// none are configured); otherwise, zero.
        /// </returns>
        public bool MatchAnyKeyDomain( /* CORE? */
            Uri uri,                 /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: If there are no key domains configured, then all are
            //       allowed, including local files.
            //
            if (keyDomains == null)
                return true;

            long flagsKey = Utility.DefaultAttributeFlagsKey();

            //
            // NOTE: Check the key usage flags (for this key) to see if
            //       insecure URIs and/or local files (i.e. and not just
            //       secure remote URIs) are allowed.  By default, only
            //       HTTPS URIs are allowed (i.e. no local files and/or
            //       HTTP/FTP URIs).
            //
            UriFlags flags = UriFlags.SecureOnlyMask;

            if (CertificateSharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, flagsKey,
                    _KeyUsage.InsecureUri, null, true,
                    false, true) == ReturnCode.Ok)
            {
                flags |= UriFlags.InsecureOnlyMask;
            }

            //
            // NOTE: By default, local files are not allowed if this key
            //       pair has key domains configured for it; however,
            //       this behavior can be modified via the "F" key usage
            //       flag -OR- if the list of key domains includes one
            //       -OR- more GUID values (i.e. one of which may match
            //       the current machine GUID).
            //
            bool allowLocalFile = false;

            if (CertificateSharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, flagsKey,
                    _KeyUsage.LocalFile, null, true,
                    false, true) == ReturnCode.Ok)
            {
                flags |= UriFlags.LocalOnlyMask;
                allowLocalFile = true;
            }
            else if (CertificatePolicyOps.HasAnyGuidKeyDomain(
                    keyDomains, cultureInfo))
            {
                flags |= UriFlags.LocalOnlyMask;
            }

            //
            // NOTE: Verify that the URI is for one of the allowed schemes
            //       and extract the host from it.
            //
            string host = null;

            if (!Utility.IsWebUri(uri, ref flags, ref host, ref error))
                return false;

            //
            // NOTE: Check if local files are allowed.  If so, make sure
            //       there is no host and that the URI was indeed for the
            //       file scheme and return true when all checks pass.
            //
            bool noHost = String.IsNullOrEmpty(host);
            bool wasFile = Utility.HasFlags(flags, UriFlags.WasFile, true);

            if (allowLocalFile && noHost && wasFile)
                return true;

            //
            // NOTE: Use the default handling.  Verify the host that was
            //       extracted from the URI glob matches one of the key
            //       domains.
            //
            foreach (string keyDomain in keyDomains)
            {
                //
                // HACK: Even when local files are not allowed, permit
                //       a key domain matching the local machine ID as
                //       long as the host represents a local machine.
                //
                if (noHost && wasFile &&
                    CertificatePolicyOps.MatchKeyDomainToMachineId(
                        null, keyDomain, cultureInfo))
                {
                    return true;
                }

                //
                // HACK: This check assumes that URI host names are NOT
                //       case-sensitive.
                //
                if (Parser.StringMatch(
                        null, host, 0, keyDomain, 0, true))
                {
                    return true;
                }
            }

            if (noHost && wasFile)
            {
                Guid? machineId = CertificatePolicyOps.GetMachineId(
                    null, null, cultureInfo);

                error = String.Format(
                    "local machine {0} did not match any key domains",
                    Utility.FormatWrapOrNull(machineId));
            }
            else
            {
                error = String.Format(
                    "uri host {0} did not match any key domains",
                    Utility.FormatWrapOrNull(host));
            }

            return false;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Group Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adds the specified key group (public key token) to the list of
        /// key groups associated with this key pair, creating the list if
        /// necessary.
        /// </summary>
        /// <param name="keyGroup">
        /// The key group (public key token) to add.
        /// </param>
        public void AddKeyGroup( /* CORE? */
            byte[] keyGroup /* in */
            )
        {
            if (keyGroups == null)
                keyGroups = new List<byte[]>();

            keyGroups.Add(keyGroup);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key group (public key token)
        /// matches any of the key groups associated with this key pair.
        /// </summary>
        /// <param name="keyGroup">
        /// The key group (public key token) to look for.
        /// </param>
        /// <returns>
        /// Non-zero if the key group matches one of the associated key
        /// groups; otherwise, zero.
        /// </returns>
        public bool HaveKeyGroup( /* CORE? */
            byte[] keyGroup /* in */
            )
        {
            if (keyGroup == null)
                return false;

            if (keyGroups == null)
                return false;

            foreach (byte[] localKeyGroup in keyGroups)
            {
                if (localKeyGroup == null)
                    continue;

                if (CertificateDataOps.MatchPublicKeyToken(
                        keyGroup, localKeyGroup))
                {
                    return true;
                }
            }

            return false;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Approval Methods
        /// <summary>
        /// Non-zero if this key pair has been approved.
        /// </summary>
        private bool approved;
        /// <summary>
        /// Determines whether this key pair has been approved.
        /// </summary>
        /// <returns>
        /// Non-zero if this key pair has been approved; otherwise, zero.
        /// </returns>
        public bool IsApproved() /* CORE */
        {
            return approved;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks this key pair as approved.
        /// </summary>
        /// <returns>
        /// Non-zero if the approval state was changed; zero if this key pair
        /// was already approved.
        /// </returns>
        public bool MarkApproved() /* CORE */
        {
            if (approved)
                return false;

            approved = true;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks this key pair as no longer approved.
        /// </summary>
        /// <returns>
        /// Non-zero if the approval state was changed; zero if this key pair
        /// was already not approved.
        /// </returns>
        public bool MarkDisapproved() /* CORE */
        {
            if (!approved)
                return false;

            approved = false;
            return true;
        }
        #endregion
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Builds a list containing the key domains configured for this key
        /// pair.
        /// </summary>
        /// <returns>
        /// A new <see cref="StringList" /> containing the configured key
        /// domains, or null if none are configured.
        /// </returns>
        protected StringList ListKeyDomains() /* CORE? */
        {
            if (keyDomains == null)
                return null;

            StringList list = new StringList();

            foreach (string keyDomain in keyDomains)
            {
                if (keyDomain == null)
                    continue;

                list.Add(keyDomain);
            }

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a list containing the formatted public key tokens for the
        /// key groups associated with this key pair.
        /// </summary>
        /// <returns>
        /// A new <see cref="StringList" /> containing the formatted key
        /// groups, or null if none are configured.
        /// </returns>
        protected StringList ListKeyGroups() /* CORE? */
        {
            if (keyGroups == null)
                return null;

            StringList list = new StringList();

            foreach (byte[] keyGroup in keyGroups)
            {
                if (keyGroup == null)
                    continue;

                list.Add(CertificateDataOps.FormatPublicKeyToken(
                    keyGroup, false, false));
            }

            return list;
        }
#endif
        #endregion
    }
}
