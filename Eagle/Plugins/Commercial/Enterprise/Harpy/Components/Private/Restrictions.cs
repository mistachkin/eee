/*
 * Restrictions.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the set of well-known certificate restriction flag values
    /// used by this plugin.  Each flag is a single-character string that may
    /// appear within a license certificate to enable or constrain specific
    /// licensing behavior.
    /// </summary>
    [ObjectId("5b2af075-c8a2-4743-8822-670448b2ef39")]
    internal static class Restrictions
    {
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     When a new flag is used here, update these strings.           //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //     Available upper flags: "BCDGHIJKMOSVWXY".                     //
        //     Available lower flags: "abcdefghijklmnopqrstuvwxyz".          //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        #region Generic Restriction Flags (Global / Reserved)
        #region Dead Code
#if DEAD_CODE
        //
        // NOTE: This value is not used by this plugin.  However, if it were,
        //       it would simply mean that there are no restrictions on the
        //       associated certificate.  Currently, this would be the same
        //       as using a null value.
        //
        /// <summary>
        /// When present, indicates that there are no restrictions on the
        /// associated certificate.  This value is not currently used by this
        /// plugin.
        /// </summary>
        private static readonly string None = String.Empty; // no restrictions.
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate a certificate
        //       must be activated prior to use.  Currently, this uses the
        //       same underlying process as certificate renewal.  It treats
        //       the certificate as immediately "expired" regardless of the
        //       actual expiration date and requires the remote certificate
        //       renewal server to have an active subscription.
        //
        /// <summary>
        /// Indicates that a certificate must be activated prior to use,
        /// forcing immediate renewal regardless of the actual expiration
        /// date and requiring an active subscription on the remote renewal
        /// server.
        /// </summary>
        /* CORE */
        public const string Activation = "A"; // Force immediate renewal.

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        //
        // NOTE: *POLICY* As a matter of policy, release builds MAY NOT use
        //       any "For engineering use only" license certificates.  This
        //       is currently enforced only on license certificates used for
        //       the Harpy plugin itself; however, third-party plugins and
        //       applications may enforce this flag in the future.
        //
        /// <summary>
        /// Indicates that a certificate is for engineering use only.  As a
        /// matter of policy, release builds may not use these certificates.
        /// </summary>
        public const string Engineering = "E"; // For engineering use only.
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *POLICY* As a matter of policy, license certificates for
        //       the Harpy plugin itself containing this flag will cause
        //       the package certificate verification subsystem to refuse
        //       to verify license certificates associated with non-Harpy
        //       public key tokens.
        //
        /// <summary>
        /// Indicates that a certificate is for promotional use only.  When
        /// present on a certificate for the Harpy plugin itself, it causes
        /// the package certificate verification subsystem to refuse to
        /// verify certificates associated with non-Harpy public key tokens.
        /// </summary>
        /* CORE */
        public const string Promotional = "P"; // For promotional use only.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *POLICY* If this flag is set on a certificate, it will
        //       require revocation checking that uses a remote server.
        //       Without this flag, only the hard-coded revocation list
        //       will be checked.  No revocation checking will be done
        //       for script certificates unless the associated execution
        //       policy is enabled.
        //
        /// <summary>
        /// Indicates that the certificate requires revocation checking using
        /// a remote server.  Without this flag, only the hard-coded
        /// revocation list is checked.
        /// </summary>
        /* CORE */
        public const string Revocation = "R"; // Use remote revocation.

        ///////////////////////////////////////////////////////////////////////

#if FOR_TEST_USE_ONLY
        //
        // NOTE: This has now been implemented.  This flag is used to mark the
        //       data in the certificate as ONLY for testing purposes and that
        //       it should not be deployed nor used for any other non-testing
        //       purpose.
        //
        /// <summary>
        /// Indicates that the data in the certificate is for testing
        /// purposes only and that it should not be deployed nor used for any
        /// other non-testing purpose.
        /// </summary>
        /* CORE */
        public const string Test = "T"; // For test use only.
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This has now been implemented.  It requires keeping track of
        //       the number of total times the certificate has been verified
        //       successfully and comparing that against the quantity declared
        //       within the certificate itself.  Due to its very nature, this
        //       handling must be performed deep within the Harpy certificate
        //       manager itself.
        //
        /// <summary>
        /// Indicates that the certificate may only be verified successfully
        /// a limited number of times, as declared by the quantity within the
        /// certificate itself.
        /// </summary>
        /* CORE */
        public const string LimitedQuantity = "L"; // ${Quantity} uses only.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that remote
        //       network time (NTP/HTTPS) server checks must be used.  This
        //       flag overrides the NoNetworkTime feature.
        //
        /// <summary>
        /// Indicates that remote network time (NTP/HTTPS) server checks must
        /// be used.  This flag overrides the NoNetworkTime feature.
        /// </summary>
        /* CORE */
        public const string ForceNetworkTime = "N"; // Force time checks.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that failures
        //       of the remote network time (NTP/HTTPS) server checks due to
        //       query frequency should result in failures of the entire
        //       operation.  This flag will be ignored if the NoNetworkTime
        //       feature is present.
        //
        /// <summary>
        /// Indicates that failures of the remote network time (NTP/HTTPS)
        /// server checks due to query frequency should cause the entire
        /// operation to fail.  This flag is ignored when the NoNetworkTime
        /// feature is present.
        /// </summary>
        /* CORE */
        public const string StrictNetworkTime = "U"; // Strict time checks.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that the time
        //       server checks should be performed using the HTTP subsystem.
        //       This flag will be ignored if the NoNetworkTime feature is
        //       present.
        //
        /// <summary>
        /// Indicates that the network time server checks should be performed
        /// using the HTTP subsystem.  This flag is ignored when the
        /// NoNetworkTime feature is present.
        /// </summary>
        /* CORE */
        public const string HttpNetworkTime = "Q"; // Force time via HTTPS.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that the time
        //       of the plugin installation should be used as the basis for
        //       figuring out the expiration date (i.e. instead of using the
        //       certificate creation date).  The plugin installation will
        //       be based on the creation and/or modification times of the
        //       directory containing the plugin; however, this may change
        //       in the future.
        //
        /// <summary>
        /// Indicates that the time of the plugin installation should be used
        /// as the basis for figuring out the expiration date, instead of
        /// using the certificate creation date.
        /// </summary>
        /* CORE */
        public const string ExpiredFromInstall = "Z"; // Trial install.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This flag is used by this plugin to indicate that a license
        //       certificate must be signed using a "fully trusted" key pair,
        //       i.e. it cannot be signed using any assembly or embedded key
        //       pair, unless that key pair also chains up to a trusted root
        //       within the Harpy assembly itself.
        //
        /// <summary>
        /// Indicates that a license certificate must be signed using a fully
        /// trusted key pair, i.e. one that chains up to a trusted root within
        /// the Harpy assembly itself.
        /// </summary>
        /* CORE */
        public const string FullyTrustedKey = "F"; // Must chain to root key.
        #endregion
    }
}
