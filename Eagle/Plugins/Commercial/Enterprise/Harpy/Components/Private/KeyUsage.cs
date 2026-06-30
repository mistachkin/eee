/*
 * KeyUsage.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Defines the set of key usage flags used by the licensing subsystem
    /// to indicate what a given key is permitted to sign or do.  Each
    /// individual flag is a single-character string, and the composite
    /// flags combine several individual flags together.
    /// </summary>
    [ObjectId("86a409b6-6cec-45ba-8b85-06c1f7e19c00")]
    internal static class KeyUsage
    {
        ///////////////////////////////////////////////////////////////////////
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        //                                                                   //
        //     When a new flag is used here, update these strings.           //
        //                                                                   //
        //     Do not change any of these values unless you know exactly     //
        //     what they do.                                                 //
        //                                                                   //
        //     Available upper flags: "".                                    //
        //     Available lower flags: "abcdefghijklmnopqrstuvwxyz".          //
        //                                                                   //
        //    *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*    //
        ///////////////////////////////////////////////////////////////////////

        #region Generic Key Usage Flags (Global / Reserved)
        //
        // NOTE: It should be noted that even if the key usage matches for a
        //       particular use case (or certificate), the public key tokens
        //       must also match for verification to succeed.  For example,
        //       a key with the "L" usage may sign a license certificate for
        //       a plugin; however, its public key token must still match the
        //       one embedded within the plugin assembly.  In a _very_ narrow
        //       set of circumstances, this limitation may not apply (e.g. an
        //       official key is used to cross-sign a different plugin, demo,
        //       or promotional license, etc).
        //
        #region Individual Entity Type Flags
        /* CORE */
        /// <summary>
        /// Placeholder key usage flag representing an invalid or missing
        /// value.
        /// </summary>
        public const string Invalid = null;     /* Placeholder, invalid. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Placeholder key usage flag that is not currently used.
        /// </summary>
        public const string None = "N";         /* Placeholder, not used. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign anything.  Use of
        /// this flag is discouraged.
        /// </summary>
        public const string Any = "A";          /* Can sign anything.  Use of
                                                 * this is discouraged. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any license.
        /// </summary>
        public const string License = "L";      /* Can sign any license. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any non-keyring
        /// script.
        /// </summary>
        public const string Script = "S";       /* Can sign any non-keyring
                                                 * script. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign an arbitrary string
        /// as data.
        /// </summary>
        public const string String = "Z";       /* Can sign an arbitrary string
                                                 * as data. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign an arbitrary file
        /// as data.
        /// </summary>
        public const string File = "G";         /* Can sign an arbitrary file
                                                 * as data. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag reserved for signing a support contract.  This
        /// is not yet used.
        /// </summary>
        public const string Contract = "C";     /* This is not yet used.  The
                                                 * future intent is that it
                                                 * will be used to sign a
                                                 * support contract. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any revocation
        /// list.  This was formerly named "Revocation".
        /// </summary>
        public const string List = "V";         /* Was "Revocation".  Can sign
                                                 * any revocation list. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any time server
        /// response.
        /// </summary>
        public const string Time = "Q";         /* Can sign any time server
                                                 * response. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign data used by the
        /// "secrets" subsystem.
        /// </summary>
        public const string Secret = "E";       /* Can sign data used by the
                                                 * "secrets" subsystem. */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Individual Chain-of-Trust Flags
        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any keyring.  Use
        /// of this flag is discouraged.
        /// </summary>
        public const string Root = "R";         /* Can sign any keyring.  Use
                                                 * of this is discouraged. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag used to allow an intermediate key to sign
        /// another keyring without it being signed directly by a root
        /// key.
        /// </summary>
        public const string Delegation = "D";   /* Used to allow intermediate
                                                 * key to sign another keyring
                                                 * without it being signed
                                                 * directly by a root key. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Key usage flag indicating the key can sign any keyring, but
        /// must itself be signed by a root key.  This is the highest
        /// permission level that will be granted to third-party key rings.
        /// </summary>
        public const string Intermediate = "I"; /* Can sign any keyring.  Must
                                                 * be signed by root key.  This
                                                 * is the highest permission
                                                 * level that will be granted
                                                 * to third-party key rings
                                                 * (i.e. should they choose to
                                                 * be anchored to the official
                                                 * root keys). */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Individual Feature Flags
        /* CORE? */
        /// <summary>
        /// Key usage flag allowing a local file even when a list of key
        /// domains is being used.
        /// </summary>
        public const string LocalFile = "F";    /* Allow a local file even
                                                 * when a list of key domains
                                                 * is being used. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE? */
        /// <summary>
        /// Key usage flag allowing a URI using the FTP or HTTP scheme.
        /// Normally, only the HTTPS scheme is allowed.
        /// </summary>
        public const string InsecureUri = "U";  /* Allow a URI using the FTP
                                                 * or HTTP scheme.  Normally,
                                                 * only the HTTPS scheme is
                                                 * allowed. */

        ///////////////////////////////////////////////////////////////////////

        /* CORE? */
        /// <summary>
        /// Key usage flag indicating that signatures made by an "expired"
        /// key should be considered valid as long as they were made before
        /// its expiration timestamp.
        /// </summary>
        public const string ExpireSignature = "B"; /* Consider signatures of
                                                    * an "expired" key to be
                                                    * valid as long as they
                                                    * were made before its
                                                    * expiration timestamp.
                                                    */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Individual Restriction Flags
        //
        // HACK: Keys with this usage can only be used to sign script files
        //       that are actually key ring files.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        // HACK: Any key that is signed by a key with this usage will also
        //       itself end up with this usage.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key to signing only script files
        /// that are actually key ring files.  Any key signed by a key with
        /// this usage will itself also end up with this usage.
        /// </summary>
        public const string KeyRingOnly = "K";

        ///////////////////////////////////////////////////////////////////////

        #region Release Builds Only
        //
        // HACK: Keys with this usage cannot be used with non-debug builds.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key so that it cannot be used
        /// with non-debug builds.
        /// </summary>
        public const string DeveloperOnly = "J";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage may only be used when test-mode has
        //       been explicitly enabled, e.g. via a signed configuration
        //       file, etc.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key so that it may only be used
        /// when test-mode has been explicitly enabled.
        /// </summary>
        public const string TestOnly = "T";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage may only be used to sign time-limited
        //       certificates (i.e. they cannot have an infinite duration).
        //       Void where prohibited.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key so that it may only sign
        /// time-limited certificates (i.e. they cannot have an infinite
        /// duration).
        /// </summary>
        public const string LimitedTimeOnly = "O";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage are allowed to participate in online
        //       certificate renewal, even if they are only allowed to sign
        //       time-limited certificates.  Void where prohibited.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag allowing the key to participate in online
        /// certificate renewal, even when it is only allowed to sign
        /// time-limited certificates.
        /// </summary>
        public const string RelaxedLimitedTimeOnly = "P";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage are allowed to verify certificates
        //       for a fixed amount of time (e.g. one year) even when an
        //       underlying certificate has an unlimited duration which
        //       would be invalidated through the use of a limited-time
        //       only restriction (see above).  In this case, there will
        //       be an automatic conversion of duration from unlimited
        //       to limited (e.g. using a compile-time constant).  Void
        //       where prohibited.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag allowing the key to verify certificates for a
        /// fixed amount of time even when an underlying certificate has an
        /// unlimited duration, by automatically converting that duration
        /// from unlimited to limited.
        /// </summary>
        public const string ConvertToLimitedTime = "W";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage may only be used if the client is able
        //       to access the remote key revocation server and verify that
        //       the key has not been revoked.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key so that it may only be used
        /// if the client can reach the remote key revocation server and
        /// verify that the key has not been revoked.
        /// </summary>
        public const string OnlineOnly = "X";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage may be used if the client is unable to
        //       access the remote key revocation server; otherwise, such a
        //       failure would be fatal to the operation involving the key.
        //
        // NOTE: *NAMING* The "Only" suffix is used here for consistency with
        //       the very closely associated "OnlineOnly" key usage flag.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag allowing the key to be used even if the client
        /// is unable to reach the remote key revocation server; otherwise,
        /// such a failure would be fatal to the operation involving it.
        /// </summary>
        public const string RelaxedOnlineOnly = "Y";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage will cause all other restrictions to be
        //       passed on to any key signed by them.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag causing all other restrictions to be passed on
        /// to any key signed by this key.
        /// </summary>
        public const string InheritOnly = "M";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Keys with this usage may only be used to sign scripts *IF*
        //       the current (Harpy) license certificate has an "Id" value
        //       that exactly matches their "Id" value.
        //
        // NOTE: *NAMING* The "Only" suffix is used here in order to denote
        //       a restriction, since key usages included both features and
        //       restrictions in the same class.
        //
        /* CORE */
        /// <summary>
        /// Key usage flag restricting the key so that it may only sign
        /// scripts if the current (Harpy) license certificate has an "Id"
        /// value that exactly matches the key's "Id" value.
        /// </summary>
        public const string LicenseeOnly = "H";
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Composite Entity Type Flags (Key Ring Loader)
        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="License" />,
        /// <see cref="Contract" />, <see cref="Time" />, and
        /// <see cref="Secret" />.
        /// </summary>
        public const string Signature = License + Contract + Time + Secret;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="License" />,
        /// <see cref="Contract" />, <see cref="Time" />, and
        /// <see cref="Secret" />.
        /// </summary>
        public const string Assembly = License + Contract + Time + Secret;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="License" />,
        /// <see cref="Contract" />, <see cref="Time" />, and
        /// <see cref="Secret" />.
        /// </summary>
        public const string Auxiliary = License + Contract + Time + Secret;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" />,
        /// <see cref="Script" />, <see cref="String" />,
        /// <see cref="File" />, <see cref="List" />, <see cref="Time" />,
        /// and <see cref="Secret" />.
        /// </summary>
        public const string Embedded =
            Root + Script + String + File + List + Time + Secret;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Composite Entity Type Flags (Key Pair Checking)
        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" /> and
        /// <see cref="Script" />.
        /// </summary>
        public const string Source = Root + Script;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" />,
        /// <see cref="Script" />, and <see cref="File" />.
        /// </summary>
        public const string ReadData = Root + Script + File;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" /> and
        /// <see cref="Intermediate" />.
        /// </summary>
        public const string KeyRing = Root + Intermediate;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" /> and
        /// <see cref="List" />.
        /// </summary>
        public const string RemoteList = Root + List;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" /> and
        /// <see cref="Time" />.
        /// </summary>
        public const string RemoteTime = Root + Time;

        ///////////////////////////////////////////////////////////////////////

        /* CORE */
        /// <summary>
        /// Composite key usage combining <see cref="Root" /> and
        /// <see cref="Secret" />.
        /// </summary>
        public const string RemoteSecret = Root + Secret;
        #endregion
        #endregion
    }
}
