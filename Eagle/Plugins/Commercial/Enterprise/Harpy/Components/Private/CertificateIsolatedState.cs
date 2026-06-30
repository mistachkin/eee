/*
 * CertificateIsolatedState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using This = Licensing.Components.Private.CertificateIsolatedState;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides isolated, thread-safe storage for the certificate error
    /// reporting options used by the isolated licensing plugin.
    /// </summary>
    [ObjectId("213b7ee3-eca4-469d-8ae0-b64cd64940c3")]
    internal static class CertificateIsolatedState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private data in
        //       this class.
        //
        /// <summary>
        /// This is used to synchronize access to the private data in this
        /// class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this is non-zero, error messages may be normalized in
        //       order to make them easier to understand.
        //
        /// <summary>
        /// When this is non-zero, error messages may be normalized in order
        /// to make them easier to understand.
        /// </summary>
        private static bool normalizeErrors = false; /* TODO: Good default? */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this is non-zero, all error messages will include the
        //       public key token associated with the error.
        //
        /// <summary>
        /// When this is non-zero, all error messages will include the public
        /// key token associated with the error.
        /// </summary>
        private static bool includePublicKeyToken = true; /* TODO: Good default? */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Isolated Plugin Support Methods
        /// <summary>
        /// Gets a value indicating whether error messages should be
        /// normalized in order to make them easier to understand.
        /// </summary>
        /// <returns>
        /// Non-zero if error messages should be normalized; otherwise, zero.
        /// </returns>
        public static bool GetNormalizeErrors() /* CORE */
        {
            lock (syncRoot)
            {
                return normalizeErrors;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets a value indicating whether error messages should be
        /// normalized in order to make them easier to understand.
        /// </summary>
        /// <param name="normalizeErrors">
        /// Non-zero if error messages should be normalized; otherwise, zero.
        /// </param>
        public static void SetNormalizeErrors( /* CORE */
            bool normalizeErrors /* in */
            )
        {
            lock (syncRoot)
            {
                This.normalizeErrors = normalizeErrors;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether error messages should include the
        /// public key token associated with the error.
        /// </summary>
        /// <returns>
        /// Non-zero if error messages should include the public key token;
        /// otherwise, zero.
        /// </returns>
        public static bool GetIncludePublicKeyToken() /* CORE */
        {
            lock (syncRoot)
            {
                return includePublicKeyToken;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets a value indicating whether error messages should include the
        /// public key token associated with the error.
        /// </summary>
        /// <param name="includePublicKeyToken">
        /// Non-zero if error messages should include the public key token;
        /// otherwise, zero.
        /// </param>
        public static void SetIncludePublicKeyToken( /* CORE */
            bool includePublicKeyToken /* in */
            )
        {
            lock (syncRoot)
            {
                This.includePublicKeyToken = includePublicKeyToken;
            }
        }
        #endregion
    }
}
