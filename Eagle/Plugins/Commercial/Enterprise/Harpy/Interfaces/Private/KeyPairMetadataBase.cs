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
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Defines the metadata associated with a key pair, including its
    /// intended usage, expiration, the set of domains and groups it applies
    /// to, and the methods used to query and approve that metadata.
    /// </summary>
    [ObjectId("34c10343-4b51-4b26-bb34-95f77d430cdf")]
    internal interface IKeyPairMetadataBase /* CORE */
    {
        #region Metadata Properties
        /// <summary>
        /// Gets or sets the intended usage of the key pair.
        /// </summary>
        string KeyUsage { get; set; }
        /// <summary>
        /// Gets or sets the date and time when the key pair expires, or null
        /// if the key pair does not expire.
        /// </summary>
        DateTime? KeyExpiration { get; set; }
        /// <summary>
        /// Gets or sets the list of domains to which the key pair applies.
        /// </summary>
        IList<string> KeyDomains { get; set; }
        /// <summary>
        /// Gets or sets the list of groups to which the key pair belongs.
        /// </summary>
        IList<byte[]> KeyGroups { get; set; }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Ring Loader & Policy Implementation Usage
        #region Key Domain Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the key pair has any associated domains.
        /// </summary>
        /// <returns>
        /// Non-zero if the key pair has at least one associated domain.
        /// </returns>
        bool HasAnyKeyDomain();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified domain to the set of domains associated with the
        /// key pair.
        /// </summary>
        /// <param name="keyDomain">
        /// The domain to add to the key pair.
        /// </param>
        void AddKeyDomain(string keyDomain);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified <see cref="Uri" /> matches any of
        /// the domains associated with the key pair.
        /// </summary>
        /// <param name="uri">
        /// The <see cref="Uri" /> to match against the associated domains.
        /// </param>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo" /> to use when comparing domains.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about why the match could not be
        /// performed.
        /// </param>
        /// <returns>
        /// Non-zero if the specified <see cref="Uri" /> matches any associated
        /// domain.
        /// </returns>
        bool MatchAnyKeyDomain(
            Uri uri,
            CultureInfo cultureInfo,
            ref Result error
        );
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Group Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adds the specified group to the set of groups associated with the
        /// key pair.
        /// </summary>
        /// <param name="keyGroup">
        /// The group to add to the key pair.
        /// </param>
        void AddKeyGroup(byte[] keyGroup);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified group is associated with the key
        /// pair.
        /// </summary>
        /// <param name="keyGroup">
        /// The group to check for association with the key pair.
        /// </param>
        /// <returns>
        /// Non-zero if the specified group is associated with the key pair.
        /// </returns>
        bool HaveKeyGroup(byte[] keyGroup);
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Key Approval Methods
        /// <summary>
        /// Determines whether the key pair has been approved.
        /// </summary>
        /// <returns>
        /// Non-zero if the key pair has been approved.
        /// </returns>
        bool IsApproved();
        /// <summary>
        /// Marks the key pair as approved.
        /// </summary>
        /// <returns>
        /// Non-zero if the key pair was successfully marked as approved.
        /// </returns>
        bool MarkApproved();
        /// <summary>
        /// Marks the key pair as disapproved.
        /// </summary>
        /// <returns>
        /// Non-zero if the key pair was successfully marked as disapproved.
        /// </returns>
        bool MarkDisapproved();
        #endregion
        #endregion
    }
}
