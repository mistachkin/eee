/*
 * NtpOps.cs --
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
using System.Net;
using System.Net.Sockets;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Licensing.Components.Private.Delegates;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the private helper methods used to query trusted network
    /// time (NTP) servers and to convert the time values they return for
    /// use by the certificate licensing components.
    /// </summary>
    [ObjectId("f80eefc0-4ce2-40f2-a4e9-43bcfaa5b93e")]
    internal static class NtpOps
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
        private const string PrimaryServerType = "primaryNtp";

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
        private const string ManualServerType = "manualNtp";

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
        private const string InterpreterServerType = "interpreterNtp";

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
        private const string DefaultServerType = "defaultNtp";
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Time Protocol Constants
        //
        // NOTE: These are the minimum number of ticks that must occur
        //       between queries to any of the configured network time
        //       (NTP) servers.
        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The minimum number of ticks that must elapse between queries to
        /// the primary network time (NTP) server.
        /// </summary>
        /* CORE */
        private const long MinimumPrimaryTicksBetweenQueries =
            TimeSpan.TicksPerSecond; /* 1 second */

        /// <summary>
        /// The minimum number of ticks that must elapse between normal
        /// queries to a network time (NTP) server.
        /// </summary>
        /* CORE */
        private const long MinimumNormalTicksBetweenQueries =
            TimeSpan.TicksPerHour; /* 1 hour */

        /// <summary>
        /// The minimum number of ticks that must elapse between queries to
        /// a network time (NTP) server when querying too fast is treated as
        /// an error.
        /// </summary>
        /* CORE */
        private const long MinimumErrorTicksBetweenQueries =
            TimeSpan.TicksPerMinute; /* 1 minute */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of NTP servers that we are allowed to
        //       use.  They should NEVER be queried more than once every
        //       4 seconds; however, in reality we SHOULD limit queries
        //       to less than once per minute.
        //
        // NOTE: Apparently, the non-prefixed global NTP pool is a legacy
        //       thing now.  Also, the servers appear to be chronically
        //       overloaded.
        //
        // TODO: Apply for a vendor prefix from the NTP organization.
        //
        /// <summary>
        /// The list of network time (NTP) servers that may be used, in
        /// order of preference.
        /// </summary>
        /* CORE */
        private static readonly string[] DefaultServers = {
            "time.mistachkin.net", // Primary, Frankfurt, Germany (Linode)

            ///////////////////////////////////////////////////////////////////

#if !DEMO && !LIMITED_EDITION
            //
            // NOTE: Do not remove this and do not use it.
            //
            // "pool.ntp.org",        // Global Pool, all servers, multi-location

            ///////////////////////////////////////////////////////////////////

            "0.pool.ntp.org",      // Global Pool, all servers, multi-location
            "1.pool.ntp.org",      // Global Pool, all servers, multi-location
            "2.pool.ntp.org",      // Global Pool, all servers, multi-location
            "3.pool.ntp.org",      // Global Pool, all servers, multi-location

            ///////////////////////////////////////////////////////////////////

            "time.nist.gov",       // NIST, all servers, multi-location

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Do not remove these and do not use them.
            //
            // "time-a.nist.gov",     // NIST, Gaithersburg, Maryland
            // "time-b.nist.gov",     // NIST, Gaithersburg, Maryland
            // "time-c.nist.gov",     // NIST, Gaithersburg, Maryland

            ///////////////////////////////////////////////////////////////////

            "time.windows.com"     // Microsoft, Redmond, Washington
#endif
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the buffer size needed to fit the response from
        //       an NTP server.
        //
        /// <summary>
        /// The buffer size, in bytes, needed to hold the response from an
        /// NTP server.
        /// </summary>
        /* CORE */
        private const int BufferSize = 48;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the only data byte we need when querying an NTP
        //       server.
        //
        //       Leap Indicator (LI) =  00, "no warning"
        //       Version Number (VN) = 011, "version 3"
        //               Mode (Mode) = 011, "client"
        //
        /// <summary>
        /// The only data byte that needs to be set when querying an NTP
        /// server, encoding a leap indicator of "no warning", version
        /// number 3, and client mode.
        /// </summary>
        /* CORE */
        private const byte RequestByte0 = 0x1B;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is used when converting the fractional portion of the
        //       NTP result (232 picosecond units) to picoseconds.  It goes
        //       from units to picoseconds.
        //
        /// <summary>
        /// The number of picoseconds per unit used when converting the
        /// fractional portion of the NTP result (232 picosecond units) to
        /// picoseconds.
        /// </summary>
        /* CORE */
        private const ulong PicosecondsPerUnit = 232;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: There are exactly 1000 picoseconds per nanosecond and exactly
        //       100 nanoseconds per tick.  This is the number used to convert
        //       the fractional portion of the NTP result from picoseconds to
        //       ticks.
        //
        /// <summary>
        /// The number of picoseconds per tick, used when converting the
        /// fractional portion of the NTP result from picoseconds to ticks.
        /// </summary>
        /* CORE */
        private const ulong PicosecondsPerTick = 1000 * 100;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: (From RFC 5905) The prime epoch, or base date of era 0,
        //       is 1 January 1900 UTC, when all bits are zero.  It should
        //       be noted that strictly speaking, UTC did not exist prior
        //       to 1 January 1972, but it is convenient to assume it has
        //       existed for all eternity.
        //
        /// <summary>
        /// The NTP prime epoch (1 January 1900 UTC), used as the reference
        /// point when interpreting the seconds reported by an NTP server.
        /// </summary>
        /* CORE */
        private static readonly DateTime PrimeEpoch = new DateTime(
            1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This "generic" error message is returned from the network
        //       time (NTP) query method when a more precise cause is not
        //       known.
        //
        /// <summary>
        /// The generic error message format returned from the network time
        /// (NTP) query method when a more precise cause is not known.
        /// </summary>
        /* CORE */
        private const string UnknownError = "unable to query time via NTP: {0}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Network Time Protocol Data
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
        // NOTE: This is the DateTime, if any, when the last NTP server was
        //       queried.  If null, no NTP server has been queried yet.
        //
        /// <summary>
        /// The date and time, if any, when an NTP time server was last
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

        /// <summary>
        /// Filters the specified collection of IP addresses, returning only
        /// those that match the requested address family.
        /// </summary>
        /// <param name="addresses">
        /// The IP addresses to filter.  This value may be null, in which
        /// case an empty result is produced.
        /// </param>
        /// <param name="addressFamily">
        /// The address family to require; when this is greater than
        /// <see cref="AddressFamily.Unspecified" />, addresses that do not
        /// match it are excluded.
        /// </param>
        /// <returns>
        /// An array of the IP addresses that matched the requested address
        /// family.
        /// </returns>
        private static IPAddress[] FilterHostAddresses( /* CORE */
            IEnumerable<IPAddress> addresses, /* in */
            AddressFamily addressFamily       /* in */
            )
        {
            List<IPAddress> result = null;

            if (addresses != null)
            {
                result = new List<IPAddress>();

                foreach (IPAddress address in addresses)
                {
                    if (address == null)
                        continue;

                    if ((addressFamily > AddressFamily.Unspecified) &&
                        (address.AddressFamily != addressFamily))
                    {
                        continue;
                    }

                    result.Add(address);
                }
            }

            return result.ToArray();
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

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: This method assumes its "ntpData" parameter is non-null
        //          -AND- has the correct number of bytes for an NTP server
        //          response.
        //
        /// <summary>
        /// Converts the raw bytes of an NTP server response into a
        /// <see cref="DateTime" /> value.
        /// </summary>
        /// <param name="ntpData">
        /// The raw NTP response bytes.  This method assumes the array is
        /// non-null and contains the correct number of bytes for an NTP
        /// server response.
        /// </param>
        /// <param name="dateTime">
        /// Upon return, receives the time represented by the NTP response.
        /// </param>
        private static void ConvertRawData( /* CORE */
            byte[] ntpData,       /* in */
            ref DateTime dateTime /* out */
            )
        {
            //
            // NOTE: The whole number of seconds since the prime epoch.
            //
            ulong seconds = ((ulong)ntpData[40] << 24) | /* BIG-ENDIAN */
                            ((ulong)ntpData[41] << 16) |
                            ((ulong)ntpData[42] << 8) |
                            ((ulong)ntpData[43]);

            //
            // NOTE: The whole number of 232 picosecond units to add.
            //
            ulong fraction = ((ulong)ntpData[44] << 24) | /* BIG-ENDIAN */
                             ((ulong)ntpData[45] << 16) |
                             ((ulong)ntpData[46] << 8) |
                             ((ulong)ntpData[47]);

            //
            // NOTE: Figure out how many DateTime ticks (100 nanosecond
            //       units) are represented by the fractional portion of
            //       the NTP result.
            //
            ulong ticks = ((fraction * PicosecondsPerUnit) /
                PicosecondsPerTick);

            //
            // NOTE: Figure out the final DateTime value, based on the
            //       (double?) number of seconds and the long integer
            //       number of ticks.
            //
            dateTime = PrimeEpoch.AddSeconds(seconds).AddTicks(
                (long)ticks);
        }
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
        /// (NTP) server and convert the response into a
        /// <see cref="DateTime" /> value.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to use, if any.  This value is optional.
        /// </param>
        /// <param name="hostNameOrAddress">
        /// The host name or address of the time server to query.  This
        /// value is optional.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information; this parameter is not currently used.
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
            Interpreter interpreter,  /* in: OPTIONAL */
            string hostNameOrAddress, /* in: OPTIONAL */
            CultureInfo cultureInfo,  /* in: NOT USED */
            DateTime now,             /* in */
            int? timeout,             /* in: OPTIONAL */
            int? retries,             /* in: OPTIONAL */
            bool forceRefresh,        /* in */
            bool errorOnTooFast,      /* in */
            ref DateTime dateTime,    /* out */
            ref Result error          /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            CertificateTraceOps.NetworkDebugTrace(String.Format(
                "TryQueryTime: interpreter = {0}, " +
                "hostNameOrAddress = {1}, now = {2}, " +
                "timeout = {3}", DataOps.FormatInterpreter(
                    interpreter, true, false),
                Utility.FormatWrapOrNull(hostNameOrAddress),
                DataOps.FormatTimeStamp(now, true, true),
                Utility.FormatWrapOrNull(timeout)),
                typeof(NtpOps).Name,
                TracePriority.Highest | TracePriority.Demand);
#endif

            ///////////////////////////////////////////////////////////////////

            ResultList errors = null;

            if (Utility.InOfflineMode())
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("cannot query time in offline mode");

                error = errors;
                return ReturnCode.Error;
            }

            int localRetries = (retries != null) ?
                (int)retries : Constants.NetworkTimeDefaultRetries;

            ReturnCode code; /* REUSED */

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
                                if (hostNameOrAddress == null)
                                {
                                    //
                                    // NOTE: This is not allowed because we
                                    //       do not permit violations of the
                                    //       NTP pool guidelines for refresh
                                    //       rate.
                                    //
                                    if (errors == null)
                                        errors = new ResultList();

                                    errors.Add("forced refresh is not supported");

                                    error = errors;
                                    return ReturnCode.Error;
                                }
                                else
                                {
                                    //
                                    // NOTE: Do nothing.  Allow time query
                                    //       to be processed normally.  The
                                    //       caller specified a manual host
                                    //       name (or IP address).  Assume
                                    //       caller has superior knowledge
                                    //       of the situation.
                                    //
                                }
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
                        Utility.FormatWrapOrNull(serverType),
                        Utility.FormatWrapOrNull(localHostNameOrAddress)),
                    typeof(NtpOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                //
                // HACK: For now, limit ourselves to IPv4 addresses.
                //       Ideally, this should not be necessary -AND-
                //       we should be able to use IPv6 addresses;
                //       however, as of this writing (late 2022), it
                //       appears that using IPv6 addresses can cause
                //       timeouts and other failures.
                //
                IPAddress[] addresses = FilterHostAddresses(
                    Dns.GetHostAddresses(localHostNameOrAddress),
                    AddressFamily.InterNetwork);

                if (addresses == null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("invalid IP address array");

                    error = errors;
                    return ReturnCode.Error;
                }

            reselect:

                object value = null;
                Result localError = null;

                code = Utility.SelectRandomArrayValue(
                    interpreter, addresses, ref value,
                    ref localError);

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

                IPAddress address = value as IPAddress;

                if (address == null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("invalid IP address");

                    error = errors;
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Attempting to use {0} time server address {1}...",
                        Utility.FormatWrapOrNull(serverType),
                        Utility.FormatWrapOrNull(address)),
                    typeof(NtpOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                //
                // TODO: This condition should never be hit because all
                //       IPv6 addresses are now filtered out (above).
                //
                /* REDUNDANT */
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    if (localRetries-- > 0)
                        goto reselect;

                    if (errors != null)
                        error = errors;
                    else
                        error = String.Format(UnknownError, 4);
                }

                //
                // NOTE: Allocate a byte buffer to hold the raw NTP query
                //       and response data.
                //
                byte[] ntpData = new byte[BufferSize];

                //
                // NOTE: Figure out how long the timeout value should be,
                //       if any.
                //
                int? localTimeout = (timeout != null) ?
                    (int)timeout : SharedOps.GetTimeout(interpreter, null);

                //
                // NOTE: Create an Internet UDP datagram socket, attempt to
                //       synchronously connect to it, send the query, and
                //       receive the response.  Finally, cleanup the socket.
                //
                using (Socket socket = new Socket(
                        address.AddressFamily, SocketType.Dgram,
                        ProtocolType.Udp))
                {
                    //
                    // NOTE: Set both the send and receive timeout values
                    //       to the one specified by the caller.
                    //
                    if (localTimeout != null)
                    {
                        socket.SendTimeout = (int)localTimeout;
                        socket.ReceiveTimeout = (int)localTimeout;
                    }

                    //
                    // NOTE: Connect synchronously to the target network
                    //       time (NTP) server.  This may fail if there
                    //       is a firewall in the way or if the Internet
                    //       is unavailable.  In that case, this method
                    //       will simply return overall failure.
                    //
                    socket.Connect(address, Port.NetworkTime);

                    //
                    // NOTE: Set the required fields of the network time
                    //       (NTP) request packet.
                    //
                    ntpData[0] = RequestByte0;

                    //
                    // NOTE: Send the network time (NTP) request over the
                    //       freshly connected socket, synchronously, and
                    //       then wait for a response, also synchronously.
                    //
                    socket.Send(ntpData); socket.Receive(ntpData);

                    //
                    // NOTE: We have now queried the network time (NTP)
                    //       server.  Make sure that we do not query it
                    //       again for at least X seconds.
                    //
                    lock (syncRoot)
                    {
                        lastQuery = now;
                    }
                }

                //
                // NOTE: Perform the calculations necessary to convert the
                //       raw NTP response into a DateTime.
                //
                /* NO RESULT */
                ConvertRawData(ntpData, ref dateTime);

                //
                // NOTE: When compiled for the "Debug" build configuration,
                //       show some useful diagnostic messages.
                //
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Local machine has a current time of {0}",
                        DataOps.FormatTimeStamp(now)),
                    typeof(NtpOps).Name,
                    TracePriority.MediumLow, 0);

                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "The {0} server reported a current time of {1}",
                        Utility.FormatWrapOrNull(serverType),
                        DataOps.FormatTimeStamp(dateTime)),
                    typeof(NtpOps).Name,
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
                        Utility.FormatWrapOrNull(serverType),
                        Utility.FormatTraceException(e)),
                    typeof(NtpOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                if (errors == null)
                    errors = new ResultList();

                errors.Add(e);

                code = ReturnCode.Error;
            }

            //
            // NOTE: If we were unable to successfully query the network time
            //       (NTP) server, retry the operation if retry handling was
            //       requested by the caller.
            //
            if (code != ReturnCode.Ok)
            {
                if (localRetries-- > 0)
                    goto retry;

                if (errors != null)
                    error = errors;
                else
                    error = String.Format(UnknownError, 5);
            }

            return code;
        }
        #endregion
    }
}
