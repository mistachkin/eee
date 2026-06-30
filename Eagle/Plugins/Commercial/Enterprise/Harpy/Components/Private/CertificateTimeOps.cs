/*
 * CertificateTimeOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !NETWORK
#error "This file cannot be compiled or used properly with network support disabled."
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Components.Private.Delegates;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using _Utility = Eagle._Components.Public.Utility;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using NetworkOps = Licensing.Components.Private.CertificateNetworkOps;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the private helper methods used to query trusted network
    /// time servers and to convert the time values they return for use by
    /// the certificate licensing components.
    /// </summary>
    [ObjectId("0d72ee57-fb0c-4be5-88ba-42f689ad61b4")]
    internal static class CertificateTimeOps
    {
        #region Server Type Constants
#if DEBUG || FORCE_TRACE
        //
        // NOTE: The server was specified manually via the method call.  It
        //       is verified because the actual server was fetched from the
        //       default hard-coded list.
        //
        /// <summary>
        /// The server type label used when the time server was specified
        /// manually and then verified against the default hard-coded list.
        /// </summary>
        /* CORE */
        private const string PrimaryServerType = "primaryHttp";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The server was specified manually via the method call.  It
        //       was not verified.
        //
        /// <summary>
        /// The server type label used when the time server was specified
        /// manually and was not verified.
        /// </summary>
        /* CORE */
        private const string ManualServerType = "manualHttp";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The server was found by querying the current interpreter.  It
        //       was successfully verified against a trusted key pair.
        //
        /// <summary>
        /// The server type label used when the time server was obtained by
        /// querying the current interpreter and verified against a trusted
        /// key pair.
        /// </summary>
        /* CORE */
        private const string InterpreterServerType = "interpreterHttp";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The server was found in the default hard-coded list, which is
        //       baked into the assembly itself.  No further verification was
        //       performed on it because the assembly itself is always signed
        //       with both a (private) strong name key pair and a Authenticode
        //       certificate when deployed for production.
        //
        /// <summary>
        /// The server type label used when the time server was taken from
        /// the default hard-coded list baked into the assembly.
        /// </summary>
        /* CORE */
        private const string DefaultServerType = "defaultHttp";
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Time Server Constants
        //
        // NOTE: These are the minimum number of ticks that must occur
        //       between queries to any of the configured network time
        //       (NTP) servers.
        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The minimum number of ticks that must elapse between queries to
        /// the primary network time server.
        /// </summary>
        /* CORE */
        private const long MinimumPrimaryTicksBetweenQueries =
            TimeSpan.TicksPerSecond * 10; /* 10 seconds */

        /// <summary>
        /// The minimum number of ticks that must elapse between normal
        /// queries to a network time server.
        /// </summary>
        /* CORE */
        private const long MinimumNormalTicksBetweenQueries =
            TimeSpan.TicksPerDay; /* 1 day */

        /// <summary>
        /// The minimum number of ticks that must elapse between queries to
        /// a network time server when querying too fast is treated as an
        /// error.
        /// </summary>
        /* CORE */
        private const long MinimumErrorTicksBetweenQueries =
            TimeSpan.TicksPerMinute; /* 1 minute */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This will be matched against the first element of the
        //       list response from the server.  This is the name of the
        //       query parameter used to pass the cryptographic nonce to
        //       the server.  It is also the name of the response field
        //       containing the cryptographic nonce.
        //
        /// <summary>
        /// The name of the query parameter used to pass the cryptographic
        /// nonce to the server and of the matching field in the response.
        /// </summary>
        /* CORE */
        private static string NonceElementName = "nonce";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These will be matched against the second element of the
        //       list response from the server.  They are possible names
        //       of response fields that contains the time data.
        //
        /// <summary>
        /// One of the possible names of the response field that contains
        /// the time data, expressed as a number of ticks.
        /// </summary>
        /* CORE */
        private const string TicksElementName = "ticks";

        /// <summary>
        /// One of the possible names of the response field that contains
        /// the time data, expressed as a Unix time stamp.
        /// </summary>
        /* CORE */
        private const string TimeStampElementName = "timeStamp";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of HTTP servers that we are allowed to
        //       use.  They use a custom trusted time stamping protocol.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The list of HTTP time servers that may be used, in order of
        /// preference, using the custom trusted time stamping protocol.
        /// </summary>
        /* CORE */
        private static string[] DefaultServers = {
            "https://urn.to/r/get_time_01",
            "https://urn.to/r/get_time_02",
            "https://urn.to/r/get_time_03",
            "https://urn.to/r/get_time_04",
            "https://urn.to/r/get_time_05",
            "https://urn.to/r/get_time_06",
            "https://urn.to/r/get_time_07"
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Since Tcl uses the Unix epoch of midnight on January 1st,
        //       1970, so do we.
        //
        /// <summary>
        /// The Unix epoch (midnight on January 1st, 1970, UTC) used as the
        /// reference point when interpreting time stamp values.
        /// </summary>
        /* CORE */
        private static readonly DateTime UnixEpoch = new DateTime(
            1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // COMPAT: Unix, Tcl.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This "generic" error message is returned from the network
        //       time (HTTP) query method when a more precise cause is not
        //       known.
        //
        /// <summary>
        /// The generic error message format returned from the network time
        /// query method when a more precise cause is not known.
        /// </summary>
        /* CORE */
        private const string UnknownError = "unable to query time via HTTP: {0}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the number of random bytes that should be generated
        //       for the nonce sent in the client request (and signed in the
        //       server response).
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The number of random bytes generated for the nonce sent in the
        /// client request and signed in the server response.
        /// </summary>
        /* CORE */
        private static int SizeOfNonce = 16;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Time Server Data
        //
        // NOTE: This is used to synchronize access to the private static
        //       data used in this module.
        //
        /// <summary>
        /// Used to synchronize access to the private static data used in
        /// this module.
        /// </summary>
        /* CORE */
        private static readonly object syncRoot = new object();

        //
        // NOTE: This is the DateTime, if any, when the last HTTP server was
        //       queried.  If null, no HTTP server has been queried yet.
        //
        /// <summary>
        /// The date and time, if any, when an HTTP time server was last
        /// queried, or null if no server has been queried yet.
        /// </summary>
        /* CORE */
        private static DateTime? lastQuery = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Support Methods
        /// <summary>
        /// Determines whether the specified host name or address refers to
        /// the primary (first) entry in the default time server list.
        /// </summary>
        /// <param name="hostNameOrAddress">
        /// The host name or address to check.  This value is optional; a
        /// null value is treated as not the primary server and an empty
        /// value is treated as the primary server.
        /// </param>
        /// <returns>
        /// Non-zero if the value refers to the primary server; otherwise,
        /// zero.
        /// </returns>
        private static bool IsPrimaryServer( /* CORE */
            string hostNameOrAddress /* in: OPTIONAL */
            )
        {
            if (hostNameOrAddress == null)
                return false;

            if (hostNameOrAddress.Length == 0)
                return true;

            if ((DefaultServers == null) || (DefaultServers.Length == 0))
                return false;

            return DataOps.StringEqualsNoCase(
                hostNameOrAddress, DefaultServers[0]);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the minimum number of ticks that must elapse between queries
        /// to the specified time server.
        /// </summary>
        /// <param name="hostNameOrAddress">
        /// The host name or address of the time server.  This value is
        /// optional.
        /// </param>
        /// <param name="errorOnTooFast">
        /// Non-zero if querying too fast should be treated as an error,
        /// which selects a shorter minimum interval.
        /// </param>
        /// <returns>
        /// The minimum number of ticks that must elapse between queries.
        /// </returns>
        private static long GetMinimumTicksBetweenQueries( /* CORE */
            string hostNameOrAddress, /* in: OPTIONAL */
            bool errorOnTooFast       /* in */
            )
        {
            if (IsPrimaryServer(hostNameOrAddress))
                return MinimumPrimaryTicksBetweenQueries;

            return errorOnTooFast ?
                MinimumErrorTicksBetweenQueries :
                MinimumNormalTicksBetweenQueries;
        }

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
        /// <summary>
        /// Gets the string associated with the specified time string type,
        /// formatting it with the supplied arguments when applicable.
        /// </summary>
        /// <param name="type">
        /// The kind of time-related string to return.
        /// </param>
        /// <param name="args">
        /// The arguments used to format the returned string, when
        /// applicable.
        /// </param>
        /// <returns>
        /// The requested string, or null if the type is not recognized.
        /// </returns>
        /* Licensing.Components.Private.Delegates.GetTimeStringCallback */
        private static string GetString( /* CORE */
            TimeStringType type, /* in */
            params object[] args /* in */
            )
        {
            switch (type)
            {
                case TimeStringType.UnknownError:
                    {
                        return String.Format(UnknownError, args);
                    }
                case TimeStringType.DefaultServer:
                    {
                        return DefaultServerType;
                    }
                case TimeStringType.PrimaryServer:
                    {
                        return PrimaryServerType;
                    }
                case TimeStringType.InterpreterServer:
                    {
                        return InterpreterServerType;
                    }
                case TimeStringType.ManualServer:
                    {
                        return ManualServerType;
                    }
                default:
                    {
                        return null;
                    }
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Time Methods
        /// <summary>
        /// Selects the host name or address of the time server to use,
        /// falling back to the default server list when necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This value is optional.
        /// </param>
        /// <param name="hostNameOrAddress">
        /// On input, the requested host name or address, if any; on output,
        /// the selected host name or address.  This value is optional.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an
        /// appropriate error code.
        /// </returns>
        public static ReturnCode SelectHostNameOrAddress( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            ref string hostNameOrAddress, /* in, out: OPTIONAL */
            ref Result error              /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            string serverType = null;
#endif

            string[] defaultServers = DefaultServers;
            ResultList errors = null;

            if (SharedOps.SelectTimeHostNameOrAddress(
                    interpreter, defaultServers, syncRoot,
#if DEBUG || FORCE_TRACE
                    new GetTimeStringCallback(GetString),
#else
                    null,
#endif
                    ref hostNameOrAddress,
#if DEBUG || FORCE_TRACE
                    ref serverType,
#endif
                    ref errors) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }
            else
            {
                error = errors;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to query the current time from a trusted network time
        /// server, validating the signed response and converting it into a
        /// <see cref="DateTime" /> value.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This value is optional.
        /// </param>
        /// <param name="hostNameOrAddress">
        /// The host name or address of the time server to query.  This
        /// value is optional.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the signed server response.  This
        /// value is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information used when parsing numeric values from the
        /// server response.  This value is optional.
        /// </param>
        /// <param name="now">
        /// The current local time, used for rate limiting and diagnostics.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, to use for the query.
        /// This value is optional.
        /// </param>
        /// <param name="retries">
        /// The number of times to retry the query upon failure.  This value
        /// is optional.
        /// </param>
        /// <param name="forceRefresh">
        /// Non-zero to bypass the minimum interval between queries and force
        /// the query to proceed.
        /// </param>
        /// <param name="errorOnTooFast">
        /// Non-zero if querying too fast should be treated as an error
        /// instead of returning the existing time.
        /// </param>
        /// <param name="mustBeSigned">
        /// Non-zero if the server response is required to be signed.
        /// </param>
        /// <param name="dateTime">
        /// Upon success, receives the time reported by the server.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an
        /// appropriate error code.
        /// </returns>
        public static ReturnCode TryQueryTime( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            string hostNameOrAddress,       /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime now,                   /* in */
            int? timeout,                   /* in: OPTIONAL */
            int? retries,                   /* in: OPTIONAL */
            bool forceRefresh,              /* in */
            bool errorOnTooFast,            /* in */
            bool mustBeSigned,              /* in */
            ref DateTime dateTime,          /* out */
            ref Result error                /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            CertificateTraceOps.NetworkDebugTrace(String.Format(
                "TryQueryTime: interpreter = {0}, " +
                "hostNameOrAddress = {1}, now = {2}, " +
                "timeout = {3}", DataOps.FormatInterpreter(
                    interpreter, true, false),
                _Utility.FormatWrapOrNull(hostNameOrAddress),
                DataOps.FormatTimeStamp(now, true, true),
                _Utility.FormatWrapOrNull(timeout)),
                typeof(CertificateTimeOps).Name,
                TracePriority.Highest | TracePriority.Demand);
#endif

            ///////////////////////////////////////////////////////////////////

            int localRetries = (retries != null) ?
                (int)retries : Constants.NetworkTimeDefaultRetries;

            ReturnCode code;
            ResultList errors = null;

        retry:

#if DEBUG || FORCE_TRACE
            string serverType = null;
#endif

            try
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (lastQuery != null)
                    {
                        TimeSpan difference = now.Subtract(
                            (DateTime)lastQuery);

                        if (difference.Ticks < GetMinimumTicksBetweenQueries(
                                hostNameOrAddress, errorOnTooFast))
                        {
                            if (forceRefresh)
                            {
                                //
                                // NOTE: Do nothing.  Allow time query
                                //       to be processed normally.
                                //
                            }
                            else if (errorOnTooFast)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add("time query is too fast");

                                error = errors;
                                return ReturnCode.Error;
                            }
                            else
                            {
                                //
                                // HACK: Fake it.  Just return what they
                                //       already have.
                                //
                                dateTime = now;
                                return ReturnCode.Ok;
                            }
                        }
                    }
                }

                string[] defaultServers = DefaultServers;
                string localHostNameOrAddress = hostNameOrAddress;

                if (SharedOps.SelectTimeHostNameOrAddress(
                        interpreter, defaultServers, syncRoot,
#if DEBUG || FORCE_TRACE
                        new GetTimeStringCallback(GetString),
#else
                        null,
#endif
                        ref localHostNameOrAddress,
#if DEBUG || FORCE_TRACE
                        ref serverType,
#endif
                        ref errors) != ReturnCode.Ok)
                {
                    error = errors;
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Attempting to use {0} time server host {1}...",
                        _Utility.FormatWrapOrNull(serverType),
                        _Utility.FormatWrapOrNull(localHostNameOrAddress)),
                    typeof(CertificateTimeOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                Uri baseUri = null;
                Result localError = null; /* REUSED */

                code = Value.GetUri(
                    localHostNameOrAddress, UriKind.Absolute, cultureInfo,
                    ref baseUri, ref localError);

                if (code != ReturnCode.Ok)
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 3);

                    return code;
                }

                ///////////////////////////////////////////////////////////////

                byte[] nonceBytes = new byte[SizeOfNonce];

                localError = null;

                code = _Utility.GetRandomBytes(
                    interpreter, ref nonceBytes, ref localError);

                if (code != ReturnCode.Ok)
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 4);

                    return code;
                }

                ///////////////////////////////////////////////////////////////

                //
                // TODO: Create the nonce string value from the random bytes.
                //       In the future, this may need to use a somewhat more
                //       complex algorithm.
                //
                string requestNonce = _Utility.ToHexadecimalString(nonceBytes);

                ///////////////////////////////////////////////////////////////

                Uri uri;

                localError = null;

                uri = _Utility.TryCombineUris(
                    baseUri, String.Format("{0}{1}{2}{3}",
                    Characters.QuestionMark, NonceElementName,
                    Characters.EqualSign, requestNonce), null,
                    UriComponents.AbsoluteUri, UriFormat.Unescaped,
                    UriFlags.NoSeparators, ref localError);

                if (uri == null)
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 5);

                    return ReturnCode.Error;
                }

                ///////////////////////////////////////////////////////////////////

#if TEST
                localError = null;

                code = _Utility.SetWebSecurityProtocol(false, ref localError);

                if (code != ReturnCode.Ok)
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 6);

                    return code;
                }
#endif

                ///////////////////////////////////////////////////////////////

                string hashAlgorithmName = SharedOps.GetHashAlgorithm(
                    null, keyPairs, null, HashAlgorithmType.RemoteUse);

                int? localTimeout = (timeout != null) ?
                    (int)timeout : SharedOps.GetTimeout(interpreter, null);

                StringList list;

                localError = null;

                list = NetworkOps.DownloadList(
                    interpreter, hashAlgorithmName, null, null,
                    keyPairs, uri, EntityType.Time, localTimeout,
                    mustBeSigned, !mustBeSigned, ref localError);

                if (list == null)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Network time check via {0} failed: {1}",
                            _Utility.FormatWrapOrNull(localHostNameOrAddress),
                            _Utility.FormatWrapOrNull(localError)),
                        typeof(CertificateTimeOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 7);

                    return ReturnCode.Error;
                }

                //
                // NOTE: We have now queried the (HTTP) time server.  Make
                //       sure that we do not query it again for at least
                //       X seconds.
                //
                lock (syncRoot)
                {
                    lastQuery = now;
                }

                //
                // NOTE: Now, try to process the time server result.  It must
                //       have at least four elements.  The first must be the
                //       literal string "nonce".  The second must be a string
                //       that exactly matches the cryptographic nonce that we
                //       sent with the request.  The third must be the literal
                //       string "timeStamp" and the fourth must be an integer
                //       number of seconds since the Unix Epoch.
                //
                if ((list == null) || (list.Count < 4))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("malformed time server response");

                    error = errors;
                    return ReturnCode.Error;
                }

                if (!DataOps.StringEquals(list[0], NonceElementName))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("unrecognized time server response (0)");

                    error = errors;
                    return ReturnCode.Error;
                }

                string responseNonce = list[1];

                if (!DataOps.StringEquals(responseNonce, requestNonce))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("unrecognized time server response (1)");

                    error = errors;
                    return ReturnCode.Error;
                }

                if (DataOps.StringEquals(list[2], TicksElementName))
                {
                    long ticks = 0;

                    localError = null;

                    code = Value.GetWideInteger2(
                        list[3], ValueFlags.AnyWideInteger, cultureInfo,
                        ref ticks, ref localError);

                    if (code != ReturnCode.Ok)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add("unrecognized time server response (2)");
                            errors.Add(localError);
                        }

                        if (errors != null)
                            error = errors;
                        else
                            error = String.Format(UnknownError, 8);

                        return code;
                    }

                    //
                    // NOTE: Calculate the final result, based on the returned
                    //       ticks value, and place it in the variable provided
                    //       by the caller.
                    //
                    dateTime = new DateTime(ticks, DateTimeKind.Utc);
                }
                else if (DataOps.StringEquals(list[2], TimeStampElementName))
                {
                    long seconds = 0;

                    localError = null;

                    code = Value.GetWideInteger2(
                        list[3], ValueFlags.AnyWideInteger, cultureInfo,
                        ref seconds, ref localError);

                    if (code != ReturnCode.Ok)
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add("unrecognized time server response (3)");
                            errors.Add(localError);
                        }

                        if (errors != null)
                            error = errors;
                        else
                            error = String.Format(UnknownError, 9);

                        return code;
                    }

                    //
                    // NOTE: Calculate the final result, based on the returned
                    //       seconds value (and the Unix epoch), and place it
                    //       in the variable provided by the caller.
                    //
                    dateTime = UnixEpoch.AddSeconds(seconds);
                }
                else
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("unrecognized time server response (4)");

                    error = errors;
                    return ReturnCode.Error;
                }

                //
                // NOTE: When compiled for the "Debug" build configuration,
                //       show some useful diagnostic messages.
                //
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Local machine has a current time of {0}",
                        DataOps.FormatTimeStamp(now)),
                    typeof(CertificateTimeOps).Name,
                    TracePriority.MediumLow, 0);

                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "The {0} server reported a current time of {1}",
                        _Utility.FormatWrapOrNull(serverType),
                        DataOps.FormatTimeStamp(dateTime)),
                    typeof(CertificateTimeOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                //
                // NOTE: If we get to this point, everything succeeded.
                //
                code = ReturnCode.Ok;
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Failed to query current time from {0} server: {1}",
                        _Utility.FormatWrapOrNull(serverType),
                        _Utility.FormatTraceException(e)),
                    typeof(CertificateTimeOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                if (errors == null)
                    errors = new ResultList();

                errors.Add(e);

                code = ReturnCode.Error;
            }

            //
            // NOTE: If we were unable to successfully query the network time
            //       (HTTP) server, retry the operation if retry handling was
            //       requested by the caller.
            //
            if (code != ReturnCode.Ok)
            {
                if (localRetries-- > 0)
                    goto retry;

                if (errors != null)
                    error = errors;
                else
                    error = String.Format(UnknownError, 10);
            }

            return code;
        }
        #endregion
    }
}
