/*
 * CertificateNetworkOps.cs --
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
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using TimeOps = Licensing.Components.Private.CertificateTimeOps;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides private helper methods used by the certificate licensing
    /// subsystem to perform network operations, including querying time
    /// servers, downloading and verifying remote data, and resolving the
    /// authority URI associated with a certificate.
    /// </summary>
    [ObjectId("50b35c63-ebc8-4b64-b1d2-24a1af9d512b")]
    internal static class CertificateNetworkOps
    {
        /// <summary>
        /// Emits a diagnostic trace message describing a failed network
        /// request to the specified <see cref="Uri" />, including the
        /// timeout that was in effect and the associated error, if any.
        /// When <paramref name="uri" /> is null, no trace is emitted.
        /// </summary>
        /// <param name="prefix">
        /// A short label prepended to the trace message that identifies
        /// the call site which detected the failure.
        /// </param>
        /// <param name="uri">
        /// The <see cref="Uri" /> that was the target of the failed
        /// network request.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, that was in effect for the
        /// request, or null if none was specified.
        /// </param>
        /// <param name="error">
        /// The error associated with the failed request.  This parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use when emitting the
        /// trace message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        public static void DebugTraceUriError(
            string prefix,         /* in */
            Uri uri,               /* in */
            int? timeout,          /* in */
            Result error,          /* in: OPTIONAL */
            TracePriority priority /* in */
            )
        {
            if (uri == null)
                return;

            TraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "{0} Failed network request to {1} with timeout {2}: {3}",
                    prefix, Utility.FormatWrapOrNull(uri),
                    Utility.FormatWrapOrNull(timeout),
                    Utility.FormatMaybeNull(
                        DataOps.FirstLine(error))).TrimStart(),
                typeof(CertificateNetworkOps).Name,
                priority, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits a diagnostic trace message describing a failed network
        /// request to the URI represented by the specified string,
        /// including the timeout that was in effect and the associated
        /// error, if any.  When <paramref name="uriString" /> is null,
        /// no trace is emitted.
        /// </summary>
        /// <param name="prefix">
        /// A short label prepended to the trace message that identifies
        /// the call site which detected the failure.
        /// </param>
        /// <param name="uriString">
        /// The string form of the URI that was the target of the failed
        /// network request.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, that was in effect for the
        /// request, or null if none was specified.
        /// </param>
        /// <param name="error">
        /// The error associated with the failed request.  This parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use when emitting the
        /// trace message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        private static void DebugTraceUriError(
            string prefix,         /* in */
            string uriString,      /* in */
            int? timeout,          /* in */
            Result error,          /* in: OPTIONAL */
            TracePriority priority /* in */
            )
        {
            if (uriString == null)
                return;

            TraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "{0} Failed network request to {1} with timeout {2}: {3}",
                    prefix, Utility.FormatWrapOrNull(uriString),
                    Utility.FormatWrapOrNull(timeout),
                    Utility.FormatMaybeNull(
                        DataOps.FirstLine(error))).TrimStart(),
                typeof(CertificateNetworkOps).Name,
                priority, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new <see cref="WebClient" /> instance via the core
        /// library.  Since the interpreter is either null or locked (i.e.
        /// it cannot be used), null is passed for the corresponding core
        /// library parameter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="argument">
        /// An optional argument used when creating the web client.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to associate with the request.
        /// This parameter is optional and may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to apply to the web client, or
        /// null to use the default.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The newly created <see cref="WebClient" /> instance, or null
        /// if it could not be created.
        /// </returns>
        private static WebClient NewWebClient( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            string argument,         /* in: OPTIONAL */
            IClientData clientData,  /* in: OPTIONAL */
            int? timeout,            /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            //
            // HACK: Fallback to just creating a new WebClient
            //       instance via the core library.  Since the
            //       interpreter is either null or locked (i.e.
            //       it cannot be used), just use null for the
            //       corresponding parameter here.
            //
            return Utility.CreateWebClient(
                interpreter, argument, clientData, timeout, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to obtain the list of configured time servers from the
        /// specified interpreter, parsing and verifying each candidate
        /// entry and returning the first valid one.  When a usable entry
        /// cannot be found and <paramref name="errorOnBadServer" /> is
        /// true, <paramref name="badServer" /> is set to indicate a fatal
        /// failure for the caller.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter from which to obtain the configured time server
        /// list.  This parameter is optional and may be null.
        /// </param>
        /// <param name="errorOnBadServer">
        /// Non-zero if the absence of any valid time server entry should
        /// be treated as a fatal error for the caller.
        /// </param>
        /// <param name="badServer">
        /// Set to true when no valid time server entry was found and
        /// <paramref name="errorOnBadServer" /> is non-zero.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while obtaining or parsing the
        /// time server list.  A new list is created if necessary.
        /// </param>
        /// <returns>
        /// An array containing the first valid time server sub-list, or
        /// null if none could be obtained.
        /// </returns>
        public static string[] TryGetTimeServers( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            bool errorOnBadServer,   /* in */
            ref bool badServer,      /* out */
            ref ResultList errors    /* out */
            )
        {
            //
            // HACK: Technically, the interpreter is optional; therefore,
            //       do not signal our caller that its operation should
            //       be considered a failure.
            //
            if (interpreter == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid interpreter");
                return null;
            }

            //
            // HACK: Technically, the NTP server list is optional (e.g. in
            //       fact it is null by default); therefore, do not signal
            //       our caller that its operation should be considered a
            //       failure.
            //
            IEnumerable<string> timeServers = interpreter.TimeServers;

            if (timeServers == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid time server list");
                return null;
            }

            //
            // NOTE: After this point, if the errorOnBadServer parameter is
            //       non-zero, any errors are considered to be fatal to the
            //       operation being performed by our caller.  If there are
            //       one or more bad time server entries -AND- at least one
            //       good time server entry, the errors for the bad server
            //       entries will not be considered fatal.
            //
            StringList list = new StringList(timeServers);

            foreach (string element in list)
            {
                if (element == null)
                    continue;

                StringList subList;
                Result localError = null;

                subList = ParseAndVerifyList(
                    element, EntityType.List, true, ref localError);

                if (subList != null)
                {
                    return subList.ToArray();
                }
                else if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            //
            // NOTE: At this point, we know that no good time server entry
            //       was found.  If errorOnBadServer parameter is non-zero,
            //       the operation being performed by our caller cannot be
            //       completed.
            //
            if (errorOnBadServer)
                badServer = true;

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to query the current time from a remote server,
        /// dispatching to the HTTPS-based time query when
        /// <paramref name="viaHttp" /> is true and to the NTP-based query
        /// otherwise.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="hostNameOrAddress">
        /// The host name or address of the time server to query.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify a signed time response.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information used when formatting or parsing values.
        /// This parameter is optional and may be null.
        /// </param>
        /// <param name="now">
        /// The current local time stamp, used for comparison against the
        /// queried time.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to apply to the request, or null
        /// to use the default.
        /// </param>
        /// <param name="retries">
        /// The number of times to retry the query, or null to use the
        /// default.
        /// </param>
        /// <param name="viaHttp">
        /// Non-zero to query the time using HTTPS; otherwise, NTP is used.
        /// </param>
        /// <param name="forceRefresh">
        /// Non-zero to bypass any cached time value and force a fresh
        /// query.
        /// </param>
        /// <param name="errorOnTooFast">
        /// Non-zero to treat a response that arrives suspiciously quickly
        /// as an error.
        /// </param>
        /// <param name="mustBeSigned">
        /// Non-zero to require that the time response be signed and
        /// verified.  This applies only to the HTTPS query path.
        /// </param>
        /// <param name="dateTime">
        /// Receives the date and time obtained from the remote server.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a value
        /// indicating the reason for failure.
        /// </returns>
        public static ReturnCode TryQueryTime( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            string hostNameOrAddress,       /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime now,                   /* in */
            int? timeout,                   /* in: OPTIONAL */
            int? retries,                   /* in: OPTIONAL */
            bool viaHttp,                   /* in */
            bool forceRefresh,              /* in */
            bool errorOnTooFast,            /* in */
            bool mustBeSigned,              /* in */
            ref DateTime dateTime,          /* out */
            ref Result error                /* out */
            )
        {
            if (viaHttp)
            {
                return TimeOps.TryQueryTime(
                    interpreter, hostNameOrAddress, keyPairs, cultureInfo,
                    now, timeout, retries, forceRefresh, errorOnTooFast,
                    mustBeSigned, ref dateTime, ref error);
            }
            else
            {
                return NtpOps.TryQueryTime(
                    interpreter, hostNameOrAddress, cultureInfo, now,
                    timeout, retries, forceRefresh, errorOnTooFast,
                    ref dateTime, ref error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs network access checks asynchronously by queuing work
        /// items that verify connectivity to the configured NTP and HTTPS
        /// time servers.  The checks are skipped when they have already
        /// been performed recently, and per-process pending flags prevent
        /// the servers from being hammered by multiple AppDomains or
        /// threads.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the checks.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="now">
        /// The current local time stamp, used both for the recent-check
        /// determination and when querying the time servers.
        /// </param>
        /// <param name="defaultAppDomainOnly">
        /// Non-zero to restrict the recent-check determination to the
        /// default AppDomain.
        /// </param>
        public static void AsynchronousAccessChecks( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            DateTime now,             /* in */
            bool defaultAppDomainOnly /* in */
            )
        {
            //
            // HACK: Ideally, these checks should be performed
            //       only once per process -OR- AppDomain.
            //
            if (CertificateNetworkState.WasCheckedRecently(
                    now, defaultAppDomainOnly))
            {
#if DEBUG || FORCE_TRACE
                TraceOps.NetworkDebugTrace(String.Format(
                    "AsynchronousAccessChecks: SKIPPED " +
                    "interpreter = {0}, now = {1}, " +
                    "defaultAppDomainOnly = {2}",
                    DataOps.FormatInterpreter(
                        interpreter, true, false),
                    DataOps.FormatTimeStamp(
                        now, true, true),
                    defaultAppDomainOnly),
                    typeof(CertificateNetworkOps).Name,
                    TracePriority.Highest | TracePriority.Demand);
#endif

                return;
            }
            else
            {
#if DEBUG || FORCE_TRACE
                TraceOps.NetworkDebugTrace(String.Format(
                    "AsynchronousAccessChecks: TRIPPED " +
                    "interpreter = {0}, now = {1}, " +
                    "defaultAppDomainOnly = {2}",
                    DataOps.FormatInterpreter(
                        interpreter, true, false),
                    DataOps.FormatTimeStamp(
                        now, true, true),
                    defaultAppDomainOnly),
                    typeof(CertificateNetworkOps).Name,
                    TracePriority.Highest | TracePriority.Demand);
#endif
            }

            ///////////////////////////////////////////////////////////////////

            ThreadStart callback1 = new ThreadStart(delegate()
            {
                //
                // HACK: These "pending" checks are per-process
                //       so that we do not hammer the selected
                //       NTP server using multiple AppDomains
                //       and/or threads.
                //
                if (CertificateNetworkState.IsNtpPending())
                    return;

                int? timeout = SharedOps.GetTimeout(
                    interpreter, null); /* NEEDED FOR TRACE */

                CertificateNetworkState.BeginNtpPending();

                try
                {
                    string hostNameOrAddress = null;
                    Result error = null;

                    try
                    {
                        hostNameOrAddress =
                            SharedOps.GetTimeHostNameOrAddress(false, true);

                        DateTime dateTime = DateTime.MinValue;

                        if (NtpOps.TryQueryTime(
                                interpreter, hostNameOrAddress, null,
                                DataOps.GetTimeStamp(), timeout, null,
                                true, false, ref dateTime,
                                ref error) == ReturnCode.Ok)
                        {
#if DEBUG || FORCE_TRACE
                            TraceOps.MaybeLogAndDebugTrace(
                                "Success checking NTP server.",
                                typeof(CertificateNetworkOps).Name,
                                TracePriority.Medium, 0);
#endif

                            CertificateNetworkState.SetNtpOk();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

#if DEBUG || FORCE_TRACE
                    DebugTraceUriError(
                        "AsynchronousAccessChecks(1)",
                        hostNameOrAddress, timeout, error,
                        TracePriority.High);
#endif
                }
                finally
                {
                    CertificateNetworkState.EndNtpPending();
                }
            });

            ///////////////////////////////////////////////////////////////////

            ThreadStart callback2 = new ThreadStart(delegate()
            {
                //
                // HACK: These "pending" checks are per-process
                //       so that we do not hammer the selected
                //       HTTPS server using multiple AppDomains
                //       and/or threads.
                //
                if (CertificateNetworkState.IsHttpsPending())
                    return;

                int? timeout = SharedOps.GetTimeout(
                    interpreter, null); /* NEEDED FOR TRACE */

                CertificateNetworkState.BeginHttpsPending();

                try
                {
                    string hostNameOrAddress = null;
                    Result error = null;

                    try
                    {
                        hostNameOrAddress =
                            SharedOps.GetTimeHostNameOrAddress(true, true);

                        DateTime dateTime = DateTime.MinValue;

                        if (TimeOps.TryQueryTime(
                                interpreter, hostNameOrAddress, null,
                                null, DataOps.GetTimeStamp(), timeout,
                                null, true, false, false, ref dateTime,
                                ref error) == ReturnCode.Ok)
                        {
#if DEBUG || FORCE_TRACE
                            TraceOps.MaybeLogAndDebugTrace(
                                "Success checking HTTPS server.",
                                typeof(CertificateNetworkOps).Name,
                                TracePriority.Medium, 0);
#endif

                            CertificateNetworkState.SetHttpsOk();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

#if DEBUG || FORCE_TRACE
                    DebugTraceUriError(
                        "AsynchronousAccessChecks(2)",
                        hostNameOrAddress, timeout, error,
                        TracePriority.High);
#endif
                }
                finally
                {
                    CertificateNetworkState.EndHttpsPending();
                }
            });

            ///////////////////////////////////////////////////////////////////

            int count = 0;

            try
            {
                if (Engine.QueueWorkItem(
                        null, callback1, QueueFlags.Default))
                {
                    count++;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        "Failed to queue check for NTP server.",
                        typeof(CertificateNetworkOps).Name,
                        TracePriority.Highest, 0);
#endif
                }

                ///////////////////////////////////////////////////////////////

                if (Engine.QueueWorkItem(
                        null, callback2, QueueFlags.Default))
                {
                    count++;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        "Failed to queue check for HTTPS server.",
                        typeof(CertificateNetworkOps).Name,
                        TracePriority.Highest, 0);
#endif
                }
            }
            finally
            {
                //
                // HACK: If applicable, set a flag that indicates
                //       the network access checks have (at least)
                //       been performed; at this point, we do not
                //       care if the checks actually succeed, only
                //       that they were actually performed, i.e.
                //       the work items were queued to the thread
                //       pool.
                //
                if (count == 2) /* HACK: Both must succeed. */
                    CertificateNetworkState.SetCheckedRecently(now);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// In the debug build configuration, forces usage of the local
        /// host certificate renewal server when the appropriate
        /// environment variable is set; otherwise, falls back to obtaining
        /// the authority URI from the configured authority base URI.
        /// </summary>
        /// <param name="authority">
        /// Receives the resolved authority <see cref="Uri" />.
        /// </param>
        /// <param name="components">
        /// Receives the <see cref="UriComponents" /> that should be used
        /// with the resolved authority.
        /// </param>
        private static void GetLocalHostOrAuthorityUri( /* CORE */
            ref Uri authority,           /* out */
            ref UriComponents components /* out */
            )
        {
            //
            // NOTE: In the debug build configuration only, when the
            //       appropriate environment variable is set, always
            //       force usage of the local host certificate renewal
            //       server.
            //
            if (Configuration.DoesVariableExist(
                    Constants.UseLocalHostEnvVarName))
            {
                int port = GetServerPort();

                authority = new Uri(String.Format(
                    Constants.DefaultLocalHostUriFormat,
                    (port != Port.Invalid) ?
                        String.Format(":{0}", port) :
                        String.Empty));

                components = Constants.DefaultLocalHostUriComponents;
            }
            else
            {
                //
                // NOTE: Check for the "AuthorityBaseUri" environment
                //       variable.  When set, use it instead of any
                //       authority that may be present within the
                //       certificate and/or the plugin assembly.
                //
                GetAuthorityUri(ref authority, ref components);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Obtains the authority <see cref="Uri" /> from the configured
        /// authority base URI environment variable, if it is set to a
        /// valid absolute URI.  When the variable is unset or invalid,
        /// the supplied values are left unchanged.
        /// </summary>
        /// <param name="authority">
        /// Receives the resolved authority <see cref="Uri" />.
        /// </param>
        /// <param name="components">
        /// Receives the <see cref="UriComponents" /> that should be used
        /// with the resolved authority.
        /// </param>
        private static void GetAuthorityUri( /* CORE */
            ref Uri authority,           /* out */
            ref UriComponents components /* out */
            )
        {
            string value = Configuration.GetVariable(
                Constants.AuthorityBaseUriEnvVarName);

            if (String.IsNullOrEmpty(value))
                return;

            Uri uri;

            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return;

            authority = uri;
            components = Constants.DefaultAuthorityUriComponents;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Obtains the server port from the configured server port
        /// environment variable, parsing it as an integer when present.
        /// </summary>
        /// <returns>
        /// The configured server port, or <see cref="Port.Invalid" /> if
        /// the variable is unset or cannot be parsed.
        /// </returns>
        private static int GetServerPort() /* CORE */
        {
            string value = Configuration.GetVariable(
                Constants.ServerPortEnvVarName);

            if (!String.IsNullOrEmpty(value))
            {
                int result;

                if (int.TryParse(value, out result))
                    return result;
            }

            return Port.Invalid;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Possibly mutates the specified authority <see cref="Uri" /> so
        /// that it is reduced to the requested
        /// <paramref name="components" />, ensuring it contains all the
        /// information required for proper operation.  When
        /// <paramref name="authority" /> is null, no action is taken.
        /// </summary>
        /// <param name="authority">
        /// The authority <see cref="Uri" /> to mutate in place.
        /// </param>
        /// <param name="components">
        /// The <see cref="UriComponents" /> to retain in the resulting
        /// authority URI.
        /// </param>
        private static void MaybeMutateAuthority( /* CORE */
            ref Uri authority,       /* in, out */
            UriComponents components /* in */
            )
        {
            if (authority == null)
                return;

            authority = new Uri(
                authority.GetComponents(components, UriFormat.Unescaped));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to resolve the authority <see cref="Uri" /> and its
        /// associated components from the specified certificate, preferring
        /// the certificate's explicit authority and falling back to the
        /// scheme and server portions of its origin.  In the debug build
        /// configuration, a local host override may take precedence.
        /// </summary>
        /// <param name="certificate">
        /// The certificate from which to obtain the authority.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="authority">
        /// Receives the resolved authority <see cref="Uri" />.
        /// </param>
        /// <param name="components">
        /// Receives the <see cref="UriComponents" /> that should be used
        /// with the resolved authority.
        /// </param>
        /// <returns>
        /// True if a valid authority was resolved; otherwise, false.
        /// </returns>
        private static bool GetAuthorityAndComponents( /* CORE */
            ICertificate certificate,    /* in: OPTIONAL */
            ref Uri authority,           /* out */
            ref UriComponents components /* out */
            )
        {
            Uri localAuthority = null;
            UriComponents localComponents = (UriComponents)0;

            ///////////////////////////////////////////////////////////////////

#if DEBUG || EXTRA_DIAGNOSTICS
            GetLocalHostOrAuthorityUri(
                ref localAuthority, ref localComponents);
#endif

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: If there is no certificate context, nothing much else
            //       that is useful can be done by this method.
            //
            if (certificate == null)
                goto done;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Initially, try to use the actual authority from the
            //       specified certificate as that is what that property
            //       is explicitly intended for.
            //
            if (localAuthority == null)
            {
                localAuthority = certificate.Authority;
                localComponents = Constants.DefaultAuthorityUriComponents;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Fallback on the origin of the certificate.  However,
            //       only use the scheme and server portions.
            //
            if (localAuthority == null)
            {
                localAuthority = certificate.Origin;
                localComponents = Constants.DefaultOriginUriComponents;
            }

            ///////////////////////////////////////////////////////////////////

        done:

            //
            // NOTE: At this point, if there is still no valid authority,
            //       just fail.
            //
            if (localAuthority == null)
                return false;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Possibly mutate the authority URI to be returned so that
            //       it contains all the information (e.g. query) that may be
            //       required for proper operation.
            //
            MaybeMutateAuthority(ref localAuthority, localComponents);

            authority = localAuthority;
            components = localComponents;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the authority <see cref="Uri" /> to use, considering
        /// the specified certificate, assembly, and plugin in turn.  This
        /// overload discards the resolved <see cref="UriComponents" /> and
        /// returns only the authority URI.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to consult for an authority URI.  This parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin to consult for an authority URI.  This parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to consult for an authority URI.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information used when querying the plugin for its
        /// URI.  This parameter is optional and may be null.
        /// </param>
        /// <param name="authority">
        /// Receives the resolved authority <see cref="Uri" />.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a value
        /// indicating the reason for failure.
        /// </returns>
        public static ReturnCode GetAuthorityAndComponents( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            Assembly assembly,        /* in: OPTIONAL */
            IPlugin plugin,           /* in: OPTIONAL */
            ICertificate certificate, /* in: OPTIONAL */
            CultureInfo cultureInfo,  /* in: OPTIONAL */
            ref Uri authority,        /* out */
            ref Result error          /* out */
            )
        {
            UriComponents components = (UriComponents)0; /* NOT USED */

            return GetAuthorityAndComponents(
                interpreter, assembly, plugin, certificate,
                cultureInfo, ref authority, ref components,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the authority <see cref="Uri" /> and its associated
        /// <see cref="UriComponents" /> to use, considering the specified
        /// certificate, assembly, and plugin in distinct phases.  The
        /// certificate is consulted first, followed by an assembly URI
        /// dedicated to the authority name, then the plugin, then a
        /// general assembly URI; failure results when no authority can be
        /// found.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly to consult for an authority URI.  This parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin to consult for an authority URI.  This parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to consult for an authority URI.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information used when querying the plugin for its
        /// URI.  This parameter is optional and may be null.
        /// </param>
        /// <param name="authority">
        /// Receives the resolved authority <see cref="Uri" />.
        /// </param>
        /// <param name="components">
        /// Receives the <see cref="UriComponents" /> that should be used
        /// with the resolved authority.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a value
        /// indicating the reason for failure.
        /// </returns>
        public static ReturnCode GetAuthorityAndComponents( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            Assembly assembly,            /* in: OPTIONAL */
            IPlugin plugin,               /* in: OPTIONAL */
            ICertificate certificate,     /* in: OPTIONAL */
            CultureInfo cultureInfo,      /* in: OPTIONAL */
            ref Uri authority,            /* out */
            ref UriComponents components, /* out */
            ref Result error              /* out */
            )
        {
            //
            // NOTE: *PHASE 0* Attempt to obtain the authority URI (somehow)
            //       from the specified certificate.
            //
            Uri localAuthority = null;
            UriComponents localComponents = (UriComponents)0;

            ///////////////////////////////////////////////////////////////////

            /* IGNORED */
            GetAuthorityAndComponents(
                certificate, ref localAuthority, ref localComponents);

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: What is the short name for the authority URI we want?
            //       Normally, this will be something like "authority".
            //
            string uriName = Constants.AuthorityUriName;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: *PHASE 1* Attempt to obtain the authority URI (somehow)
            //       from the specified assembly, by using its dedicated URI
            //       for that purpose, if any.
            //
            if (localAuthority == null)
            {
                localAuthority = Utility.GetAssemblyUri(assembly, uriName);

                if ((localAuthority == null) &&
                    !CertificateAssemblyOps.MatchObject(assembly))
                {
                    localAuthority = Utility.GetAssemblyUri(
                        CertificateAssemblyOps.GetObject(), uriName);
                }

                localComponents =
                    Constants.DefaultAssemblyAuthorityUriComponents;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: *PHASE 2* Attempt to obtain the authority URI (somehow)
            //       from the specified plugin.
            //
            if ((localAuthority == null) && (plugin != null))
            {
                Uri uri;
                Result uriError = null;

                uri = plugin.GetUri(
                    interpreter, uriName, cultureInfo, ref uriError);

                if (uri != null)
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Plugin {0} has URI {1} for {2} / {3}.",
                            Utility.FormatWrapOrNull(plugin),
                            Utility.FormatWrapOrNull(uri),
                            Utility.FormatWrapOrNull(uriName),
                            Utility.FormatWrapOrNull(cultureInfo)),
                        typeof(CertificateNetworkOps).Name,
                        TracePriority.MediumLow, 0);
#endif

                    localAuthority = uri;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Plugin {0} has no URI for {1} / {2}: {3}",
                            Utility.FormatWrapOrNull(plugin),
                            Utility.FormatWrapOrNull(uriName),
                            Utility.FormatWrapOrNull(cultureInfo),
                            Utility.FormatWrapOrNull(uriError)),
                        typeof(CertificateNetworkOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    localAuthority = plugin.Uri;
                }

                localComponents = Constants.DefaultPluginUriComponents;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: *PHASE 3* Attempt to obtain the authority URI (somehow)
            //       from the specified assembly.
            //
            if (localAuthority == null)
            {
                localAuthority = Utility.GetAssemblyUri(assembly);

                if ((localAuthority == null) &&
                    !CertificateAssemblyOps.MatchObject(assembly))
                {
                    localAuthority = Utility.GetAssemblyUri(
                        CertificateAssemblyOps.GetObject());
                }

                localComponents = Constants.DefaultAssemblyUriComponents;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: *PHASE 4* At this point, if there is still no authority,
            //       this method has failed.
            //
            if (localAuthority == null)
            {
                error = "invalid authority base uri";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Possibly mutate the authority URI to be returned so that
            //       it contains all the information (e.g. query) that may be
            //       required for proper operation.
            //
            MaybeMutateAuthority(ref localAuthority, localComponents);

            authority = localAuthority;
            components = localComponents;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Downloads data from the specified URI, retrying up to the
        /// configured maximum number of times and sleeping between
        /// attempts.  Distinct errors encountered across attempts are
        /// accumulated and reported if no attempt succeeds.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="uriOrString">
        /// The target of the download, expressed as either a
        /// <see cref="Uri" /> or a string.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to apply to each attempt, or null
        /// to use the default.
        /// </param>
        /// <param name="raw">
        /// Non-zero to download the data as raw bytes; otherwise, the data
        /// is downloaded as a string.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the errors that were
        /// encountered.
        /// </param>
        /// <returns>
        /// The downloaded data, as either a byte array or a string, or
        /// null if all attempts failed.
        /// </returns>
        public static object DownloadData( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            object uriOrString,      /* in */
            int? timeout,            /* in */
            bool raw,                /* in */
            ref Result error         /* out */
            )
        {
            int maximumRetries = Utility.GetWebMaximumRetries();
            int retries = 0;
            ResultList errors = null;

            while (true)
            {
                object data;
                Result localError = null;

                data = DownloadDataOnce(
                    interpreter, uriOrString, timeout, raw,
                    ref localError);

                if (data != null)
                {
#if DEBUG || FORCE_TRACE
                    TraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Success after {0} {1} out of {2}.",
                            retries, (retries == 1) ? "retry" :
                            "retries", maximumRetries),
                        typeof(CertificateNetworkOps).Name,
                        (retries > 0) ?
                            TracePriority.MediumHigh :
                            TracePriority.MediumLow, 0);
#endif

                    return data;
                }

                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    if (errors.Find( /* NO DUPLICATES */
                            localError) == Index.Invalid)
                    {
                        errors.Add(localError);
                    }
                }

                if ((maximumRetries <= 0) ||
                    (++retries > maximumRetries))
                {
                    break;
                }

                /* NO RESULT */
                Utility.SleepForWebRetry(interpreter, null, retries);
            }

            if (errors != null)
                error = errors;

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: All use of the WebClient class in this plugin should go
        //          through this method.  If necessary, add more parameters
        //          to make it more flexible.
        //
        /// <summary>
        /// Performs a single download attempt from the specified URI using
        /// a <see cref="WebClient" /> obtained from
        /// <see cref="NewWebClient" />.  Downloads are refused when the
        /// plugin is operating in offline mode, and any exception is
        /// captured and reported through <paramref name="error" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="uriOrString">
        /// The target of the download, expressed as either a
        /// <see cref="Uri" /> or a string.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to apply to the request, or null
        /// to use the default.
        /// </param>
        /// <param name="raw">
        /// Non-zero to download the data as raw bytes; otherwise, the data
        /// is downloaded as a string.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The downloaded data, as either a byte array or a string, or
        /// null if the attempt failed.
        /// </returns>
        private static object DownloadDataOnce( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            object uriOrString,      /* in */
            int? timeout,            /* in */
            bool raw,                /* in */
            ref Result error         /* out */
            )
        {
#if DEBUG || FORCE_TRACE
            TraceOps.NetworkDebugTrace(String.Format(
                "DownloadDataOnce: interpreter = {0}, " +
                "uriOrString = {1}, timeout = {2}, " +
                "raw = {3}", DataOps.FormatInterpreter(
                    interpreter, true, false),
                Utility.FormatWrapOrNull(uriOrString),
                Utility.FormatWrapOrNull(timeout), raw),
                typeof(CertificateNetworkOps).Name,
                TracePriority.Highest | TracePriority.Demand);
#endif

            ///////////////////////////////////////////////////////////////////

            if (Utility.InOfflineMode())
            {
                error = "cannot download data in offline mode";
                return null;
            }

            Uri uri = null;
            string uriString = null;

            if (uriOrString is Uri)
            {
                uri = (Uri)uriOrString;
            }
            else if (uriOrString is string)
            {
                uriString = (string)uriOrString;
            }
            else
            {
                error = "invalid uri";
                return null;
            }

            try
            {
                using (WebClient webClient = NewWebClient(
                        interpreter, null, null, timeout, ref error))
                {
                    if (webClient == null)
                    {
#if DEBUG || FORCE_TRACE
                        TraceOps.MaybeLogAndDebugTrace(
                            String.Format(
                                "Could not create web client for {0} with timeout {1}: {2}",
                                Utility.FormatWrapOrNull(uriOrString),
                                Utility.FormatWrapOrNull(timeout),
                                Utility.FormatWrapOrNull(error)),
                            typeof(CertificateNetworkOps).Name,
                            TracePriority.MediumHigh, 0);
#endif

                        return null;
                    }

#if DEBUG || FORCE_TRACE
                    if (!Configuration.DoesVariableExist(
                            Constants.NetworkTraceEnvVarName))
                    {
                        TraceOps.DebugTrace(String.Format(
                            "DownloadDataOnce: uriOrString = {0}, timeout = {1}",
                            Utility.FormatWrapOrNull(uriOrString),
                            Utility.FormatWrapOrNull(timeout)),
                            typeof(CertificateNetworkOps).Name,
                            TracePriority.Medium);
                    }
#endif

                    if (uri != null)
                    {
                        return raw ? (object)
                            webClient.DownloadData(uri) :
                            webClient.DownloadString(uri);
                    }
                    else
                    {
                        return raw ? (object)
                            webClient.DownloadData(uriString) :
                            webClient.DownloadString(uriString);
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                DebugTraceUriError(
                    "DownloadDataOnce(1)", uri, timeout,
                    e, TracePriority.High);

                DebugTraceUriError(
                    "DownloadDataOnce(2)", uriString, timeout,
                    e, TracePriority.High);

                TraceOps.MaybeLogAndDebugTrace(
                    e, typeof(CertificateNetworkOps).Name,
                    TracePriority.MediumLow, 0);
#endif

                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Downloads a list from the specified URI and then parses and,
        /// optionally, verifies its signature, returning the resulting
        /// list of strings.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context to associate with the request.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used during signature
        /// verification.  This parameter is optional and may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used during keyed hashing.  This parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the downloaded text.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the downloaded list signature.
        /// This parameter is optional and may be null.
        /// </param>
        /// <param name="uri">
        /// The <see cref="Uri" /> from which to download the list.
        /// </param>
        /// <param name="entityType">
        /// The <see cref="EntityType" /> represented by the downloaded
        /// list, used during key usage checks.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to apply to the download, or null
        /// to use the default.
        /// </param>
        /// <param name="mustBeSigned">
        /// Non-zero to require that the downloaded list be signed and
        /// verified.
        /// </param>
        /// <param name="onlyFirstSubList">
        /// Non-zero to return only the first sub-list parsed from the
        /// downloaded data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The parsed list of strings, or null if the download or parsing
        /// failed.
        /// </returns>
        public static StringList DownloadList( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            Uri uri,                        /* in */
            EntityType entityType,          /* in */
            int? timeout,                   /* in: OPTIONAL */
            bool mustBeSigned,              /* in */
            bool onlyFirstSubList,          /* in */
            ref Result error                /* out */
            )
        {
            string text = DownloadData(
                interpreter, uri, timeout, false, ref error) as string;

            if (text == null)
                return null;

            return ParseAndMaybeVerifyList(
                hashAlgorithmName, hashKey, encoding, keyPairs,
                text, entityType, mustBeSigned, onlyFirstSubList,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses and verifies the signature of the specified list text
        /// using the embedded public key pairs and the hash algorithm
        /// appropriate for local or remote use.  When the required
        /// certificate plugin and policy features are not compiled in,
        /// this method reports that it is not implemented.
        /// </summary>
        /// <param name="text">
        /// The list text to parse and verify.
        /// </param>
        /// <param name="entityType">
        /// The <see cref="EntityType" /> represented by the list, used
        /// during key usage checks.
        /// </param>
        /// <param name="remote">
        /// Non-zero if the list originates from a remote source, which
        /// selects the remote-use hash algorithm.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The verified list of strings, or null if parsing or
        /// verification failed.
        /// </returns>
        public static StringList ParseAndVerifyList( /* CORE */
            string text,           /* in */
            EntityType entityType, /* in */
            bool remote,           /* in */
            ref Result error       /* out */
            )
        {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            IEnumerable<IKeyPair> keyPairs = null;

            if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(), null, false,
                    ref keyPairs, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            string hashAlgorithmName = SharedOps.GetHashAlgorithm(
                null, keyPairs, null, remote ? HashAlgorithmType.RemoteUse :
                HashAlgorithmType.LocalUse);

            return ParseAndMaybeVerifyList(
                hashAlgorithmName, null, null, keyPairs, text, entityType,
                true, false, ref error);
#else
            error = "not implemented";
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified list text and, when
        /// <paramref name="mustBeSigned" /> is non-zero, verifies its
        /// detached Base64 signature against the supplied key pairs and
        /// checks the matching key's usage.  Optionally returns only the
        /// first sub-list of the parsed result.  Any exception is captured
        /// and reported through <paramref name="error" />.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used during signature
        /// verification.  This parameter is optional and may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used during keyed hashing.  This parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the list text.  This parameter
        /// is optional and may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the list signature.  This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="text">
        /// The list text to parse and, optionally, verify.
        /// </param>
        /// <param name="entityType">
        /// The <see cref="EntityType" /> represented by the list, used
        /// during key usage checks.
        /// </param>
        /// <param name="mustBeSigned">
        /// Non-zero to require that the list be signed and verified.
        /// </param>
        /// <param name="onlyFirstSubList">
        /// Non-zero to return only the first sub-list parsed from the
        /// list text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The parsed (and possibly verified) list of strings, or null if
        /// parsing or verification failed.
        /// </returns>
        private static StringList ParseAndMaybeVerifyList( /* CORE */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            string text,                    /* in */
            EntityType entityType,          /* in */
            bool mustBeSigned,              /* in */
            bool onlyFirstSubList,          /* in */
            ref Result error                /* out */
            )
        {
            try
            {
                StringList list = null;

                if (Parser.SplitList(
                        null, text, 0, Length.Invalid, true, ref list,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }

                if (list.Count < 1)
                {
                    error = "missing downloaded list data";
                    return null;
                }

                if (!mustBeSigned)
                {
                    //
                    // NOTE: Even though the caller does not want signature
                    //       verification, it may still need only the first
                    //       sub-list from the overall result.
                    //
                    if (onlyFirstSubList)
                        goto splitFirstSubList;
                    else
                        return list;
                }

                if (list.Count < 2)
                {
                    error = "missing downloaded list signature";
                    return null;
                }

                if (!Utility.IsBase64(list[1]))
                {
                    error = "malformed downloaded list signature";
                    return null;
                }

                byte[] signature = Convert.FromBase64String(list[1]);

                IKeyPair localKeyPair = null;
                Result localResult = null;

                if (VerifyString(hashAlgorithmName,
                        hashKey, encoding, keyPairs, list[0], signature,
                        ref localKeyPair, ref localResult) != ReturnCode.Ok)
                {
                    error = localResult;
                    return null;
                }

                if (SharedOps.CheckKeyUsage(
                        localKeyPair, entityType, ref error) != ReturnCode.Ok)
                {
                    return null;
                }

            splitFirstSubList:

                StringList subList = null;

                if (Parser.SplitList(
                        null, list[0], 0, Length.Invalid, true, ref subList,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }

                return subList;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the specified value and verifies the supplied signature
        /// against each candidate key pair in turn, succeeding as soon as
        /// any key pair verifies the signature.  Errors from individual
        /// key pairs are accumulated and reported when no key pair
        /// succeeds.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to hash
        /// <paramref name="value" /> and verify the signature.
        /// </param>
        /// <param name="hashKey">
        /// The key used during keyed hashing.  This parameter is optional
        /// and may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the value.  This parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs against which to verify the signature.
        /// </param>
        /// <param name="value">
        /// The value whose signature is to be verified.  This parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="signature">
        /// The signature bytes to verify against the hashed value.
        /// </param>
        /// <param name="keyPair">
        /// Receives the key pair that successfully verified the signature.
        /// </param>
        /// <param name="result">
        /// Receives the result of the verification on success, or
        /// information about the errors that were encountered on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the signature was verified by
        /// some key pair; otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyString( /* CORE */
            string hashAlgorithmName,       /* in */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in */
            string value,                   /* in: OPTIONAL */
            byte[] signature,               /* in */
            ref IKeyPair keyPair,           /* out */
            ref Result result               /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (SharedOps.HashString(
                    hashAlgorithmName, hashKey, encoding, value,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (SharedOps.VerifyHash(
                        hashBytes, hashAlgorithmName, signature,
                        localKeyPair, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify string and bytes";

            return ReturnCode.Error;
        }
    }
}
