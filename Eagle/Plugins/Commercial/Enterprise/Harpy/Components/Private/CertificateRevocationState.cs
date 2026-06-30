/*
 * CertificateRevocationState.cs --
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
using Eagle._Components.Public;
using Eagle._Containers.Public;

using RevocationTriplet = Eagle._Components.Public.AnyTriplet<
    System.DateTime?, Eagle._Components.Public.ReturnCode, object>;

using RevocationDictionary = System.Collections.Generic.Dictionary<
    string, Eagle._Components.Public.AnyTriplet<System.DateTime?,
        Eagle._Components.Public.ReturnCode, object>>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-process cache of certificate revocation check
    /// results, recording when each certificate identifier was last checked
    /// against a remote revocation source so that repeated checks can be
    /// throttled.
    /// </summary>
    [ObjectId("0d570af7-fc7c-4829-bc4d-763618afaa3a")]
    internal static class CertificateRevocationState
    {
        #region Private Constants
        //
        // NOTE: This is the maximum number of seconds a given "success"
        //       remote revocation check is valid for.
        //
        /// <summary>
        /// The maximum number of seconds for which a successful remote
        /// revocation check result remains valid before it must be
        /// re-checked.
        /// </summary>
        private const int MaximumOkSeconds = 86400; /* ~1 day */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum number of seconds a given "failure"
        //       remote revocation check is valid for.
        //
        /// <summary>
        /// The maximum number of seconds for which a failed remote revocation
        /// check result remains valid before it must be re-checked.
        /// </summary>
        private const int MaximumErrorSeconds = 60; /* 1 minute */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the data below.
        //
        /// <summary>
        /// The object used to synchronize access to the revocation check
        /// cache.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the mapping between certificate identifiers and the
        //       last time its revocation was checked remotely.
        //
        /// <summary>
        /// Maps each certificate identifier to a triplet recording when its
        /// revocation was last checked remotely, the resulting return code,
        /// and the associated value.
        /// </summary>
        private static readonly RevocationDictionary lastChecks =
            new RevocationDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether enough time has elapsed since the previous
        /// remote revocation check that a new check should be performed.
        /// </summary>
        /// <param name="now">
        /// The current time used as the basis for the elapsed-time
        /// calculation.
        /// </param>
        /// <param name="then">
        /// The time of the previous remote revocation check, or null if no
        /// previous check has been performed.
        /// </param>
        /// <param name="code">
        /// The return code from the previous remote revocation check, used to
        /// select the applicable validity window.
        /// </param>
        /// <returns>
        /// Non-zero if a new remote revocation check should be performed;
        /// otherwise, zero.
        /// </returns>
        private static bool ShouldDownload( /* CORE */
            DateTime now,   /* in */
            DateTime? then, /* in */
            ReturnCode code /* in */
            )
        {
            if (then == null) /* NEVER? */
                return true;

            DateTime localThen = (DateTime)then;

            if (localThen == DateTime.MinValue) /* INVALID? */
                return true;

            if (localThen > now) /* TIME MACHINE? */
                return true;

            TimeSpan elapsed = now.Subtract(localThen);

            int maximumSeconds = (code == ReturnCode.Ok) ?
                MaximumOkSeconds : MaximumErrorSeconds;

            if (elapsed.TotalSeconds >= maximumSeconds)
                return true;

            //
            // NOTE: At this point, we know that the last check
            //       was not very long ago; therefore, skip it.
            //
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a remote revocation check should be performed
        /// for the certificate associated with the specified key, returning
        /// the previously cached result when it is still valid.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check.
        /// </param>
        /// <param name="key">
        /// The key identifying the certificate whose cached revocation result
        /// is being queried.
        /// </param>
        /// <param name="code">
        /// Receives the cached return code when a valid cached result is
        /// available.
        /// </param>
        /// <param name="list">
        /// Receives the cached revocation list when a valid successful cached
        /// result is available.
        /// </param>
        /// <param name="error">
        /// Receives the cached error result when a valid failed cached result
        /// is available.
        /// </param>
        /// <returns>
        /// Non-zero if a new remote revocation check should be performed;
        /// otherwise, zero.
        /// </returns>
        private static bool ShouldDownload( /* CORE */
            Interpreter interpreter, /* in */
            string key,              /* in */
            ref ReturnCode code,     /* out */
            ref StringList list,     /* out */
            ref Result error         /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((lastChecks != null) && (key != null))
                {
                    RevocationTriplet anyTriplet;

                    if (lastChecks.TryGetValue(key, out anyTriplet))
                    {
                        DateTime now = Utility.GetUtcNow();
                        ReturnCode localCode = anyTriplet.Y;

                        if (!ShouldDownload(
                                now, anyTriplet.X, localCode))
                        {
                            object localValue = anyTriplet.Z;

                            if (localCode == ReturnCode.Ok)
                            {
                                if (localValue is StringList)
                                {
                                    list = (StringList)localValue;
                                    code = localCode;

                                    return false;
                                }
                            }
                            else
                            {
                                //
                                // TODO: This does NOT permit a
                                //       null error message to
                                //       be returned.
                                //
                                if (localValue is Result)
                                {
                                    error = (Result)localValue;
                                    code = localCode;

                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            return true; /* HACK: Use "safe" default, i.e. check remote. */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the result of a remote revocation check for the
        /// certificate associated with the specified key so that subsequent
        /// checks can be throttled.
        /// </summary>
        /// <param name="key">
        /// The key identifying the certificate whose revocation result is
        /// being recorded.
        /// </param>
        /// <param name="now">
        /// The time at which the remote revocation check was performed.
        /// </param>
        /// <param name="code">
        /// The return code produced by the remote revocation check.
        /// </param>
        /// <param name="value">
        /// The value produced by the remote revocation check, typically a
        /// revocation list or an error result.
        /// </param>
        private static void WasDownloaded( /* CORE */
            string key,      /* in */
            DateTime now,    /* in */
            ReturnCode code, /* in */
            object value     /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((lastChecks != null) && (key != null))
                {
                    lastChecks[key] = new RevocationTriplet(
                        now, code, value);
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public ICertificate Methods
        /// <summary>
        /// Determines whether a remote revocation check should be performed
        /// for the certificate with the specified identifier, returning the
        /// previously cached result when it is still valid.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check.
        /// </param>
        /// <param name="id">
        /// The unique identifier of the certificate whose revocation status
        /// is being queried.
        /// </param>
        /// <param name="code">
        /// Receives the cached return code when a valid cached result is
        /// available.
        /// </param>
        /// <param name="list">
        /// Receives the cached revocation list when a valid successful cached
        /// result is available.
        /// </param>
        /// <param name="result">
        /// Receives the cached error result when a valid failed cached result
        /// is available.
        /// </param>
        /// <returns>
        /// Non-zero if a new remote revocation check should be performed;
        /// otherwise, zero.
        /// </returns>
        public static bool ShouldDownload( /* CORE */
            Interpreter interpreter, /* in */
            Guid id,                 /* in */
            out ReturnCode code,     /* out */
            out StringList list,     /* out */
            out Result result        /* out */
            )
        {
            code = ReturnCode.Break;
            list = null;
            result = null;

            return ShouldDownload(
                interpreter, CertificateDataOps.FormatId(id), ref code,
                ref list, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the result of a remote revocation check for the
        /// certificate with the specified identifier so that subsequent
        /// checks can be throttled.
        /// </summary>
        /// <param name="id">
        /// The unique identifier of the certificate whose revocation result
        /// is being recorded.
        /// </param>
        /// <param name="code">
        /// The return code produced by the remote revocation check.
        /// </param>
        /// <param name="value">
        /// The value produced by the remote revocation check, typically a
        /// revocation list or an error result.
        /// </param>
        public static void WasDownloaded( /* CORE */
            Guid id,         /* in */
            ReturnCode code, /* in */
            object value     /* in */
            )
        {
            WasDownloaded(
                CertificateDataOps.FormatId(id), Utility.GetUtcNow(),
                code, value);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public IKeyPair Methods
        /// <summary>
        /// Determines whether a remote revocation check should be performed
        /// for the key pair with the specified public key token, returning
        /// the previously cached result when it is still valid.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token identifying the key pair whose revocation
        /// status is being queried.
        /// </param>
        /// <param name="code">
        /// Receives the cached return code when a valid cached result is
        /// available.
        /// </param>
        /// <param name="list">
        /// Receives the cached revocation list when a valid successful cached
        /// result is available.
        /// </param>
        /// <param name="result">
        /// Receives the cached error result when a valid failed cached result
        /// is available.
        /// </param>
        /// <returns>
        /// Non-zero if a new remote revocation check should be performed;
        /// otherwise, zero.
        /// </returns>
        public static bool ShouldDownload( /* CORE */
            Interpreter interpreter, /* in */
            byte[] publicKeyToken,   /* in */
            out ReturnCode code,     /* out */
            out StringList list,     /* out */
            out Result result        /* out */
            )
        {
            code = ReturnCode.Break;
            list = null;
            result = null;

            return ShouldDownload(
                interpreter, CertificateDataOps.FormatPublicKeyToken(
                publicKeyToken, false, false), ref code, ref list,
                ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the result of a remote revocation check for the key pair
        /// with the specified public key token so that subsequent checks can
        /// be throttled.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token identifying the key pair whose revocation
        /// result is being recorded.
        /// </param>
        /// <param name="code">
        /// The return code produced by the remote revocation check.
        /// </param>
        /// <param name="value">
        /// The value produced by the remote revocation check, typically a
        /// revocation list or an error result.
        /// </param>
        public static void WasDownloaded( /* CORE */
            byte[] publicKeyToken, /* in */
            ReturnCode code,       /* in */
            object value           /* in */
            )
        {
            WasDownloaded(CertificateDataOps.FormatPublicKeyToken(
                publicKeyToken, false, false), Utility.GetUtcNow(),
                code, value);
        }
        #endregion
    }
}
