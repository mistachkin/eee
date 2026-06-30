/*
 * CertificateRevocationOps.cs --
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
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the core implementation of certificate and key pair
    /// revocation checking for the licensing subsystem, including local
    /// (embedded) revocation list checks, remote revocation list downloads,
    /// and the optional fail-safe enforcement behavior.
    /// </summary>
    [ObjectId("65714294-a8a0-47ab-998c-f1c192b9e108")]
    internal static class CertificateRevocationOps
    {
#if NETWORK
        /// <summary>
        /// Gets the relative URI used when contacting the remote revocation
        /// server.  An environment variable may override the default, and a
        /// separate environment variable may disable it entirely.
        /// </summary>
        /// <returns>
        /// The configured relative URI, or null if remote revocation has been
        /// disabled via the environment.
        /// </returns>
        public static string GetRelativeUri() /* CORE */
        {
            string value = Configuration.GetVariable(
                Constants.RevocationRelativeUriEnvVarName);

            if (!String.IsNullOrEmpty(value))
                return value;

            if (Configuration.DoesVariableExist(
                    Constants.NoRevocationRelativeUriEnvVarName))
            {
                return null;
            }

            return Constants.DefaultRevocationRelativeUri;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the request portion of a revocation list URI, appending the
        /// query parameter that identifies which revocation list is desired.
        /// </summary>
        /// <param name="relativeUri">
        /// The base relative URI to which the query parameter is appended.
        /// This is assumed to already end with a question mark.
        /// </param>
        /// <param name="type">
        /// The type of revocation list being requested (for example, "key"
        /// or "certificate").
        /// </param>
        /// <returns>
        /// The relative URI with the type query parameter appended, or the
        /// original <paramref name="relativeUri" /> when either argument is
        /// null or empty.
        /// </returns>
        private static string BuildRequest( /* CORE */
            string relativeUri, /* in */
            string type         /* in */
            )
        {
            if (String.IsNullOrEmpty(relativeUri) ||
                String.IsNullOrEmpty(type))
            {
                return relativeUri;
            }

            //
            // HACK: *MAGIC* Use the "type" query parameter to tell the
            //       server which revocation list we are interested in.
            //
            // HACK: This also assumes that the relative URI always ends
            //       with a question mark.
            //
            return String.Format("{0}type={1}", relativeUri,
                (type != null) ? Uri.EscapeUriString(type) : null);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the local (embedded) revocation list with the specified
        /// resource name from the licensing assembly.
        /// </summary>
        /// <param name="name">
        /// A short descriptive name for the kind of revocation list, used
        /// when formatting error messages.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource containing the revocation list.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The revocation list on success; otherwise, null.
        /// </returns>
        private static StringList GetAssemblyList( /* CORE */
            string name,         /* in */
            string resourceName, /* in */
            ref Result error     /* out */
            )
        {
            StringList list = null;
            Result localError = null;

            if (CertificateKeyPairOps.GetAssemblyList(
                    CertificateAssemblyOps.GetObject(), resourceName,
                    ref list, ref localError) == ReturnCode.Ok)
            {
                return list;
            }
            else
            {
                error = String.Format(
                    "could not obtain local {0} revocation list: {1}",
                    Utility.FormatWrapOrNull(name),
                    Utility.FormatWrapOrNull(localError));
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks whether the specified unique identifier appears in the
        /// supplied revocation list as revoked as of the given time stamp.
        /// </summary>
        /// <param name="id">
        /// The unique identifier to look for in the revocation list.
        /// </param>
        /// <param name="dateTime">
        /// The time stamp used to determine whether a revocation entry is
        /// effective; entries whose revocation time stamp is after this value
        /// are ignored.
        /// </param>
        /// <param name="list">
        /// The revocation list to check, where each element describes a
        /// revoked identifier, its revocation time stamp, and an optional
        /// reason.
        /// </param>
        /// <param name="revoked">
        /// Upon return, set to true if the identifier was found to be
        /// revoked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information, including the reason
        /// the identifier is considered revoked.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the identifier is not revoked;
        /// otherwise, an error return code.
        /// </returns>
        private static ReturnCode CheckIdList( /* CORE */
            Guid id,           /* in */
            DateTime dateTime, /* in */
            StringList list,   /* in */
            ref bool revoked,  /* out */
            ref Result error   /* out */
            )
        {
            if (list == null)
            {
                error = "invalid revocation list";
                return ReturnCode.Error;
            }

            foreach (string element in list)
            {
                if (element == null)
                    continue;

                StringList subList = null;
                Result localError = null;

                if (Parser.SplitList(
                        null, element, 0, Length.Invalid, true,
                        ref subList, ref localError) != ReturnCode.Ok)
                {
                    error = String.Format(
                        "bad revocation list element {0}: {1}",
                        Utility.FormatWrapOrNull(element),
                        Utility.FormatWrapOrNull(localError));

                    return ReturnCode.Error;
                }

                if ((subList == null) || (subList.Count < 3))
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed revocation list element {0}, " +
                            "has {1} sub-elements, needs at least 3.",
                            Utility.FormatWrapOrNull(element),
                            (subList == null) ?
                                Count.Invalid : subList.Count),
                        typeof(CertificateRevocationOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    continue;
                }

                Guid revokedId = Guid.Empty;

                localError = null;

                if (!CertificateDataOps.TryParseId(
                        subList[0], ref revokedId, ref localError))
                {
                    error = String.Format(
                        "bad revocation unique identifier {0}: {1}",
                        Utility.FormatWrapOrNull(subList[0]),
                        Utility.FormatWrapOrNull(localError));

                    return ReturnCode.Error;
                }

                //
                // WARNING: If the time stamp field for the revocation
                //          list item is null -OR- an empty string, the
                //          identifier will be invalid as of forever.
                //
                DateTime revokedDateTime = DateTime.MinValue;

                if (!String.IsNullOrEmpty(subList[1]))
                {
                    localError = null;

                    if (!CertificateDataOps.TryParseUniversalTimeStamp(
                            subList[1], ref revokedDateTime,
                            ref localError))
                    {
                        error = String.Format(
                            "bad revocation time stamp {0}: {1}",
                            Utility.FormatWrapOrNull(subList[1]),
                            Utility.FormatWrapOrNull(localError));

                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: At this point, we know the revocation time
                    //       stamp is non-empty and valid.  Now check if
                    //       the caller provided DateTime value occurs
                    //       BEFORE it; if so, it does not matter if the
                    //       identifier is revoked.
                    //
                    if (dateTime < revokedDateTime)
                        continue;
                }

                if (id.Equals(revokedId))
                {
                    string revokedReason = null;

                    if (!String.IsNullOrEmpty(subList[2]))
                        revokedReason = subList[2];

                    error = String.Format(
                        "unique identifier {0} is revoked as of {1}{2}",
                        Utility.FormatWrapOrNull(
                            CertificateDataOps.FormatId(
                                revokedId)),
                        Utility.FormatWrapOrNull(
                            CertificateDataOps.FormatTimeStamp(
                                revokedDateTime, true, true)),
                        (revokedReason != null) ? String.Format(
                            ": {0}", Utility.FormatWrapOrNull(
                                revokedReason)) :
                            String.Empty);

                    revoked = true;
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks whether the public key token of the specified key pair
        /// appears in the supplied revocation list as revoked as of the given
        /// time stamp.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose public key token is checked against the
        /// revocation list.
        /// </param>
        /// <param name="dateTime">
        /// The time stamp used to determine whether a revocation entry is
        /// effective; entries whose revocation time stamp is after this value
        /// are ignored.
        /// </param>
        /// <param name="list">
        /// The revocation list to check, where each element describes a
        /// revoked public key token, its revocation time stamp, and an
        /// optional reason.
        /// </param>
        /// <param name="revoked">
        /// Upon return, set to true if the public key token was found to be
        /// revoked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information, including the reason
        /// the public key token is considered revoked.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the public key token is not
        /// revoked; otherwise, an error return code.
        /// </returns>
        private static ReturnCode CheckPublicKeyTokenList( /* CORE */
            IKeyPair keyPair,  /* in */
            DateTime dateTime, /* in */
            StringList list,   /* in */
            ref bool revoked,  /* out */
            ref Result error   /* out */
            )
        {
            byte[] publicKeyToken = null;

            if (CertificateSharedOps.CheckKeyPair(
                    keyPair, ref publicKeyToken,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (list == null)
            {
                error = "invalid revocation list";
                return ReturnCode.Error;
            }

            foreach (string element in list)
            {
                if (element == null)
                    continue;

                StringList subList = null;
                Result localError = null;

                if (Parser.SplitList(
                        null, element, 0, Length.Invalid, true,
                        ref subList, ref localError) != ReturnCode.Ok)
                {
                    error = String.Format(
                        "bad revocation list element {0}: {1}",
                        Utility.FormatWrapOrNull(element),
                        Utility.FormatWrapOrNull(localError));

                    return ReturnCode.Error;
                }

                if ((subList == null) || (subList.Count < 3))
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Malformed revocation list element {0}, " +
                            "has {1} sub-elements, needs at least 3.",
                            Utility.FormatWrapOrNull(element),
                            (subList == null) ?
                                Count.Invalid : subList.Count),
                        typeof(CertificateRevocationOps).Name,
                        TracePriority.MediumHigh, 0);
#endif

                    continue;
                }

                byte[] revokedPublicKeyToken = null;

                localError = null;

                if (CertificateDataOps.ParsePublicKeyToken(
                        subList[0], ref revokedPublicKeyToken,
                        ref localError) != ReturnCode.Ok)
                {
                    error = String.Format(
                        "bad revocation public key token {0}: {1}",
                        Utility.FormatWrapOrNull(subList[0]),
                        Utility.FormatWrapOrNull(localError));

                    return ReturnCode.Error;
                }

                //
                // WARNING: If the time stamp field for the revocation
                //          list item is null -OR- an empty string, ALL
                //          certificates signed with the public key will
                //          then be considered invalid as of forever.
                //
                DateTime revokedDateTime = DateTime.MinValue;

                if (!String.IsNullOrEmpty(subList[1]))
                {
                    localError = null;

                    if (!CertificateDataOps.TryParseUniversalTimeStamp(
                            subList[1], ref revokedDateTime,
                            ref localError))
                    {
                        error = String.Format(
                            "bad revocation time stamp {0}: {1}",
                            Utility.FormatWrapOrNull(subList[1]),
                            Utility.FormatWrapOrNull(localError));

                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: At this point, we know the revocation time
                    //       stamp is non-empty and valid.  Now check if
                    //       the caller provided DateTime value occurs
                    //       BEFORE it; if so, it does not matter if the
                    //       public key is revoked.
                    //
                    if (dateTime < revokedDateTime)
                        continue;
                }

                if (CertificateDataOps.MatchPublicKeyToken(
                        revokedPublicKeyToken, publicKeyToken))
                {
                    string revokedReason = null;

                    if (!String.IsNullOrEmpty(subList[2]))
                        revokedReason = subList[2];

                    error = String.Format(
                        "public key token {0} is revoked as of {1}{2}",
                        Utility.FormatWrapOrNull(
                            CertificateDataOps.FormatPublicKeyToken(
                                revokedPublicKeyToken, true, true)),
                        Utility.FormatWrapOrNull(
                            CertificateDataOps.FormatTimeStamp(
                                revokedDateTime, true, true)),
                        (revokedReason != null) ? String.Format(
                            ": {0}", Utility.FormatWrapOrNull(
                                revokedReason)) :
                            String.Empty);

                    revoked = true;
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// When fail-safe mode is enabled, performs revocation checks for the
        /// specified key pair and/or certificate so that any revoked entity
        /// trips the fail-safe behavior.  When fail-safe mode is disabled,
        /// this method does nothing beyond optional tracing.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation checks, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the revocation checks, if any.
        /// </param>
        /// <param name="plugin">
        /// The licensing plugin associated with the revocation checks, if
        /// any.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when building request URIs, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The additional key pairs used to verify downloaded data, if any.
        /// </param>
        /// <param name="certificate">
        /// The certificate to check for revocation, if any.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to check for revocation, if any.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use, if any.
        /// </param>
        /// <param name="dateTime">
        /// The time stamp used when evaluating key pair revocation entries.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, used when downloading remote
        /// revocation lists, if any.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags that control how the revocation checks are
        /// performed.
        /// </param>
        public static void MaybePerformFailSafeChecks(
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OK, OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            ICertificate certificate,       /* in: OPTIONAL */
            IKeyPair keyPair,               /* in: OPTIONAL */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime dateTime,              /* in */
            int? timeout,                   /* in: OPTIONAL */
            NetworkFlags networkFlags       /* in */
            )
        {
            if (CertificateFailSafeMode.IsEnabled())
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.NetworkDebugTrace(String.Format(
                    "MaybePerformFailSafeChecks: TRIPPED " +
                    "interpreter = {0}, certificate = {1}, " +
                    "keyPair = {2}, timeout = {3}",
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    DebugOnlyOps.FormatCertificate(certificate),
                    Utility.FormatWrapOrNull(keyPair),
                    Utility.FormatWrapOrNull(timeout)),
                    typeof(CertificateRevocationOps).Name,
                    TracePriority.Highest | TracePriority.Demand);
#endif

                Result result; /* REUSED */

                networkFlags |= NetworkFlags.ForceFailSafeMask;
                networkFlags &= ~NetworkFlags.Strict;

                if (CertificateTestMode.IsEnabled())
                {
                    networkFlags |= NetworkFlags.WhatIf;
                    networkFlags &= ~NetworkFlags.Asynchronous;
                }

                if (!CertificateSharedOps.HasFlags(
                        networkFlags, NetworkFlags.CertificateOnly, true) &&
                    (keyPair != null))
                {
                    result = null;

                    /* ASYNCHRONOUS */
                    /* IGNORED */
                    IsRevoked( /* OK */
                        interpreter, assembly, plugin, hashAlgorithmName,
                        hashKey, encoding, keyPairs, keyPair, cultureInfo,
                        dateTime, timeout, networkFlags, ref result);
                }

                if (!CertificateSharedOps.HasFlags(
                        networkFlags, NetworkFlags.KeyPairOnly, true) &&
                    (certificate != null))
                {
                    result = null;

                    /* ASYNCHRONOUS */
                    /* IGNORED */
                    IsRevoked( /* OK */
                        interpreter, assembly, plugin, hashAlgorithmName,
                        hashKey, encoding, keyPairs, certificate,
                        cultureInfo, timeout, networkFlags, ref result);
                }
            }
            else
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.NetworkDebugTrace(String.Format(
                    "MaybePerformFailSafeChecks: SKIPPED " +
                    "interpreter = {0}, certificate = {1}, " +
                    "keyPair = {2}, timeout = {3}",
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    DebugOnlyOps.FormatCertificate(certificate),
                    Utility.FormatWrapOrNull(keyPair),
                    Utility.FormatWrapOrNull(timeout)),
                    typeof(CertificateRevocationOps).Name,
                    TracePriority.Highest | TracePriority.Demand);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Trips the fail-safe mode and, depending on configuration, either
        /// traces the failure (in what-if mode) or terminates the process via
        /// Environment.FailFast.
        /// </summary>
        /// <param name="whatIf">
        /// When true, the failure is only traced rather than causing the
        /// process to terminate.
        /// </param>
        /// <param name="returnCode">
        /// The return code associated with the failure, if any.
        /// </param>
        /// <param name="result">
        /// The result describing the failure that caused fail-safe mode to be
        /// tripped.
        /// </param>
        private static void MaybeTripFailSafe( /* CORE */
            bool whatIf,            /* in */
            ReturnCode? returnCode, /* in */
            Result result           /* in */
            )
        {
            /* NO RESULT */
            CertificateFailSafeMode.Trip();

            string message = String.Format(
                Constants.TripFailSafeErrorFormat, result);

#if TEST
            Eagle._Tests.Default.TestFailSafeAbortWithTrace(
                returnCode, message, whatIf, true);
#else
            string message2 = String.Format(
                Constants.FallbackFailSafeErrorFormat,
                Utility.FormatWrapOrNull(message));

            if (whatIf)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(message2,
                    typeof(CertificateRevocationOps).Name,
                    TracePriority.FailSafeFatal);
#endif
            }
            else
            {
                Environment.FailFast(message2);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair has been revoked,
        /// optionally performing the check asynchronously and tripping
        /// fail-safe mode when a revoked key pair is detected.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the revocation check, if any.
        /// </param>
        /// <param name="plugin">
        /// The licensing plugin associated with the revocation check, if any.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when building request URIs, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The additional key pairs used to verify downloaded data, if any.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to check for revocation.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use, if any.
        /// </param>
        /// <param name="dateTime">
        /// The time stamp used when evaluating revocation entries.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, used when downloading remote
        /// revocation lists, if any.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags that control how the revocation check is
        /// performed.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, which may be
        /// null when the check was queued asynchronously.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode IsRevoked( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            IKeyPair keyPair,               /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime dateTime,              /* in */
            int? timeout,                   /* in: OPTIONAL */
            NetworkFlags networkFlags,      /* in */
            ref Result result               /* out */
            )
        {
            bool failSafe = CertificateSharedOps.HasFlags(
                networkFlags, NetworkFlags.FailSafe, true);

            bool whatIf = CertificateSharedOps.HasFlags(
                networkFlags, NetworkFlags.WhatIf, true);

            ///////////////////////////////////////////////////////////////////

            ReturnCode code = ReturnCode.Ok;
            Result localResult = null;

            ///////////////////////////////////////////////////////////////////

            ThreadStart callback = new ThreadStart(delegate()
            {
                bool revoked = false;

                try
                {
                    code = IsRevoked(
                        interpreter, assembly, plugin,
                        hashAlgorithmName, hashKey,
                        encoding, keyPairs, keyPair,
                        cultureInfo, dateTime, timeout,
                        networkFlags, ref revoked,
                        ref localResult);
                }
                catch (Exception e)
                {
                    localResult = e;
                    code = ReturnCode.Error;
                }

                if ((code != ReturnCode.Ok) && revoked && failSafe)
                    MaybeTripFailSafe(whatIf, code, localResult);
            });

            ///////////////////////////////////////////////////////////////////

            if (CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.Asynchronous, true))
            {
                if (Engine.QueueWorkItem(
                        interpreter, callback, QueueFlags.Default))
                {
                    result = null;
                    return ReturnCode.Ok;
                }
                else
                {
                    localResult = "cannot queue engine work item (1)";

                    if (failSafe)
                        MaybeTripFailSafe(whatIf, code, localResult);

                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            else
            {
                callback();

                result = localResult;
                return code;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the actual revocation check for the specified key pair,
        /// consulting both the local (embedded) revocation list and, when
        /// permitted, a remote revocation list.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the revocation check, if any.
        /// </param>
        /// <param name="plugin">
        /// The licensing plugin associated with the revocation check, if any.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when building request URIs, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The additional key pairs used to verify downloaded data, if any.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to check for revocation.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use, if any.
        /// </param>
        /// <param name="dateTime">
        /// The time stamp used when evaluating revocation entries.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, used when downloading remote
        /// revocation lists, if any.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags that control how the revocation check is
        /// performed.
        /// </param>
        /// <param name="revoked">
        /// Upon return, set to true if the key pair was found to be revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the key pair is not revoked;
        /// otherwise, an error return code.
        /// </returns>
        private static ReturnCode IsRevoked( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            IKeyPair keyPair,               /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            DateTime dateTime,              /* in */
            int? timeout,                   /* in: OPTIONAL */
            NetworkFlags networkFlags,      /* in */
            ref bool revoked,               /* out */
            ref Result result               /* out */
            )
        {
            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            #region Testing Support (Environment Variable)
            //
            // NOTE: When the appropriate environment variable is set, force
            //       all key pairs to always be treated as revoked (fun).
            //
            if (Configuration.DoesVariableExist(
                    Constants.AlwaysRevokedEnvVarName))
            {
                result = OperationStatus.AlwaysRevoked;

                revoked = true;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            StringList list; /* REUSED */
            Result error; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            #region Local Revocation List Check
            error = null;

            list = GetAssemblyList("key",
                Constants.TrustRootKeyRevocationList, ref error);

            if (list == null)
            {
                result = error;
                return ReturnCode.Error;
            }

            error = null;

            if (CheckPublicKeyTokenList(
                    keyPair, dateTime, list, ref revoked,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            long flagsKey = Utility.DefaultAttributeFlagsKey();
            string keyUsage = keyPair.KeyUsage;

            ///////////////////////////////////////////////////////////////////

            #region Remote Revocation Key Usage Flag Check
            if (!CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.Force, true)
#if DEBUG || EXTRA_DIAGNOSTICS
                || Configuration.DoesVariableExist(
                    Constants.NoNetworkRevocationEnvVarName)
#endif
                )
            {
                if ((keyUsage == null) ||
                    (CertificateSharedOps.MatchFlags(
                        keyUsage, FlagType.KeyUsage, flagsKey,
                        null, KeyUsage.OnlineOnly, false, false,
                        true) == ReturnCode.Ok))
                {
                    result = OperationStatus.NotRevoked;
                    return ReturnCode.Ok;
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            #region Remote Revocation List Check
            byte[] publicKeyToken = keyPair.PublicKeyToken;

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Avoid checking remote revocation lists too frequently.
            //
            ReturnCode localCode;
            StringList localList;
            Result localResult;

            if (!CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.NoCache, true) &&
                !CertificateRevocationState.ShouldDownload(
                    interpreter, publicKeyToken, out localCode,
                    out localList, out localResult))
            {
                if (localCode == ReturnCode.Ok)
                {
                    list = localList;
                    goto skipDownload;
                }
                else
                {
                    result = localResult;
                    return localCode;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Uri authority = null;
            UriComponents components = (UriComponents)0;

            error = null;

            if (CertificateNetworkOps.GetAuthorityAndComponents(
                    interpreter, assembly, plugin, null,
                    cultureInfo, ref authority, ref components,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            string relativeUri = GetRelativeUri();

            if (relativeUri != null)
                components |= Constants.DefaultRelativeUriComponents;

            error = null;

            Uri localUri = Utility.TryCombineUris(
                authority, BuildRequest(relativeUri, "key"), encoding,
                components, UriFormat.Unescaped, UriFlags.None, ref error);

            if (localUri == null)
            {
                result = error;
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            IEnumerable<IKeyPair> localKeyPairs = null;

            ///////////////////////////////////////////////////////////////////

            #region Get Trust Root Public Key (Embedded)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            //
            // NOTE: These key pairs are only used locally within this
            //       method and are NOT used by evaluate scripts -OR-
            //       load any other key pairs.
            //
            IEnumerable<IKeyPair> localKeyPairs1 = null;

            error = null;

            if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                    assembly, null, false, ref localKeyPairs1,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            IEnumerable<IKeyPair> localKeyPairs2 = null;

            error = null;

            if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(),
                    null, false, ref localKeyPairs2,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            localKeyPairs = CertificateKeyPairOps.MergeAll(
                interpreter, localKeyPairs1, localKeyPairs2,
                null, null, null, null, null, PolicyType.Unknown,
                null, false, false, false);

            localKeyPairs = CertificateKeyPairOps.MergeAll(
                interpreter, keyPairs, localKeyPairs, null,
                null, null, null, null, PolicyType.Unknown,
                null, false, false, false);
#else
            localKeyPairs = keyPairs;
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Set HTTPS Security Protocol
#if TEST
            error = null;

            if (Utility.SetWebSecurityProtocol(
                    false, ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            //
            // BUGFIX: We must for use of a hash algorithm here that we know
            //         will be compatible with remote servers.  It does not
            //         matter what the caller specified if the end result is
            //         a failure to verify the signature of the downloaded
            //         data.
            //
            string localHashAlgorithmName =
                CertificateSharedOps.GetHashAlgorithm(hashAlgorithmName,
                    localKeyPairs, null, HashAlgorithmType.RemoteUse);

            list = null;
            error = null;

            try
            {
                list = CertificateNetworkOps.DownloadList(
                    interpreter, localHashAlgorithmName, hashKey, encoding,
                    localKeyPairs, localUri, EntityType.List, timeout, true,
                    false, ref error);
            }
            finally
            {
                if (list != null)
                {
                    CertificateRevocationState.WasDownloaded(
                        publicKeyToken, ReturnCode.Ok, list);
                }
                else
                {
                    CertificateRevocationState.WasDownloaded(
                        publicKeyToken, ReturnCode.Error, error);
                }
            }

            if (list == null)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Network key pair revocation check via {0} failed: {1}",
                        Utility.FormatWrapOrNull(localUri),
                        Utility.FormatWrapOrNull(error)),
                    typeof(CertificateRevocationOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                if (!CertificateSharedOps.HasFlags(
                        networkFlags, NetworkFlags.Strict, true) &&
                    (CertificateSharedOps.MatchFlags(
                        keyUsage, FlagType.KeyUsage, flagsKey,
                        KeyUsage.RelaxedOnlineOnly, null, true,
                        false, true) == ReturnCode.Ok))
                {
                    result = OperationStatus.UnknownRevoked;
                    return ReturnCode.Ok;
                }
                else
                {
                    result = error;
                    return ReturnCode.Error;
                }
            }

        skipDownload:

            error = null;

            if (CheckPublicKeyTokenList(
                    keyPair, dateTime, list, ref revoked,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            result = OperationStatus.NotRevoked;
            return ReturnCode.Ok;
            #endregion
#else
            result = OperationStatus.NotRevoked;
            return ReturnCode.Ok;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified certificate has been revoked,
        /// optionally performing the check asynchronously and tripping
        /// fail-safe mode when a revoked certificate is detected.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the revocation check, if any.
        /// </param>
        /// <param name="plugin">
        /// The licensing plugin associated with the revocation check, if any.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when building request URIs, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The additional key pairs used to verify downloaded data, if any.
        /// </param>
        /// <param name="certificate">
        /// The certificate to check for revocation.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use, if any.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, used when downloading remote
        /// revocation lists, if any.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags that control how the revocation check is
        /// performed.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, which may be
        /// null when the check was queued asynchronously.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode IsRevoked( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            ICertificate certificate,       /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            int? timeout,                   /* in: OPTIONAL */
            NetworkFlags networkFlags,      /* in */
            ref Result result               /* out */
            )
        {
            bool failSafe = CertificateSharedOps.HasFlags(
                networkFlags, NetworkFlags.FailSafe, true);

            bool whatIf = CertificateSharedOps.HasFlags(
                networkFlags, NetworkFlags.WhatIf, true);

            ///////////////////////////////////////////////////////////////////

            ReturnCode code = ReturnCode.Ok;
            Result localResult = null;

            ///////////////////////////////////////////////////////////////////

            ThreadStart callback = new ThreadStart(delegate()
            {
                bool revoked = false;

                try
                {
                    code = IsRevoked( /* OK */
                        interpreter, assembly, plugin,
                        hashAlgorithmName, hashKey,
                        encoding, keyPairs, certificate,
                        cultureInfo, timeout, networkFlags,
                        ref revoked, ref localResult);
                }
                catch (Exception e)
                {
                    localResult = e;
                    code = ReturnCode.Error;
                }

                if ((code != ReturnCode.Ok) && revoked && failSafe)
                    MaybeTripFailSafe(whatIf, code, localResult);
            });

            ///////////////////////////////////////////////////////////////////

            if (CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.Asynchronous, true))
            {
                if (Engine.QueueWorkItem(
                        interpreter, callback, QueueFlags.Default))
                {
                    result = null;
                    return ReturnCode.Ok;
                }
                else
                {
                    localResult = "cannot queue engine work item (2)";

                    if (failSafe)
                        MaybeTripFailSafe(whatIf, code, localResult);

                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            else
            {
                callback();

                result = localResult;
                return code;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the actual revocation check for the specified
        /// certificate, consulting both the local (embedded) revocation list
        /// and, when permitted, a remote revocation list.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the revocation check, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the revocation check, if any.
        /// </param>
        /// <param name="plugin">
        /// The licensing plugin associated with the revocation check, if any.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use, if any.
        /// </param>
        /// <param name="hashKey">
        /// The hash key to use, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when building request URIs, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The additional key pairs used to verify downloaded data, if any.
        /// </param>
        /// <param name="certificate">
        /// The certificate to check for revocation.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use, if any.
        /// </param>
        /// <param name="timeout">
        /// The network timeout, in milliseconds, used when downloading remote
        /// revocation lists, if any.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags that control how the revocation check is
        /// performed.
        /// </param>
        /// <param name="revoked">
        /// Upon return, set to true if the certificate was found to be
        /// revoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the certificate is not revoked;
        /// otherwise, an error return code.
        /// </returns>
        private static ReturnCode IsRevoked( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            Assembly assembly,              /* in: OPTIONAL */
            IPlugin plugin,                 /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            ICertificate certificate,       /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            int? timeout,                   /* in: OPTIONAL */
            NetworkFlags networkFlags,      /* in */
            ref bool revoked,               /* out */
            ref Result result               /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            #region Testing Support (Environment Variable)
            //
            // NOTE: When the appropriate environment variable is set, force
            //       all certificates to always be treated as revoked (fun).
            //
            if (Configuration.DoesVariableExist(
                    Constants.AlwaysRevokedEnvVarName))
            {
                result = OperationStatus.AlwaysRevoked;

                revoked = true;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            Guid id = certificate.Id;
            DateTime dateTime = certificate.TimeStamp;
            StringList list; /* REUSED */
            Result error; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            #region Local Revocation List Check
            error = null;

            list = GetAssemblyList("certificate",
                Constants.TrustRootCertificateRevocationList, ref error);

            if (list == null)
            {
                result = error;
                return ReturnCode.Error;
            }

            error = null;

            if (CheckIdList(
                    id, dateTime, list, ref revoked,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            long flagsKey = Utility.DefaultAttributeFlagsKey();

            ///////////////////////////////////////////////////////////////////

            #region Remote Revocation Restriction Flag Check
            if (!CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.Force, true)
#if DEBUG || EXTRA_DIAGNOSTICS
                || Configuration.DoesVariableExist(
                    Constants.NoNetworkRevocationEnvVarName)
#endif
                )
            {
                if (CertificateSharedOps.MatchFlags(
                        certificate, FlagType.Restriction,
                        flagsKey, null, Restrictions.Revocation,
                        false, false, true) == ReturnCode.Ok)
                {
                    result = OperationStatus.NotRevoked;
                    return ReturnCode.Ok;
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            #region Remote Revocation List Check
            //
            // HACK: Avoid checking remote revocation lists too frequently.
            //
            ReturnCode localCode;
            StringList localList;
            Result localResult;

            if (!CertificateSharedOps.HasFlags(
                    networkFlags, NetworkFlags.NoCache, true) &&
                !CertificateRevocationState.ShouldDownload(
                    interpreter, id, out localCode, out localList,
                    out localResult))
            {
                if (localCode == ReturnCode.Ok)
                {
                    list = localList;
                    goto skipDownload;
                }
                else
                {
                    result = localResult;
                    return localCode;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Uri authority = null;
            UriComponents components = (UriComponents)0;

            error = null;

            if (CertificateNetworkOps.GetAuthorityAndComponents(
                    interpreter, assembly, plugin, certificate,
                    cultureInfo, ref authority, ref components,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            string relativeUri = GetRelativeUri();

            if (relativeUri != null)
                components |= Constants.DefaultRelativeUriComponents;

            error = null;

            Uri localUri = Utility.TryCombineUris(
                authority, BuildRequest(relativeUri, "certificate"),
                encoding, components, UriFormat.Unescaped, UriFlags.None,
                ref error);

            if (localUri == null)
            {
                result = error;
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            IEnumerable<IKeyPair> localKeyPairs = null;

            ///////////////////////////////////////////////////////////////////

            #region Get Trust Root Public Key (Embedded)
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            IEnumerable<IKeyPair> localKeyPairs1 = null;

            error = null;

            if (CertificateKeyPairOps.GetEmbeddedPublicOnly(
                    assembly, null, false, ref localKeyPairs1,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            IEnumerable<IKeyPair> localKeyPairs2 = null;

            error = null;

            if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(),
                    null, false, ref localKeyPairs2,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            localKeyPairs = CertificateKeyPairOps.MergeAll(
                interpreter, localKeyPairs1, localKeyPairs2,
                null, null, null, null, null, PolicyType.Unknown,
                null, false, false, false);

            localKeyPairs = CertificateKeyPairOps.MergeAll(
                interpreter, keyPairs, localKeyPairs, null,
                null, null, null, null, PolicyType.Unknown,
                null, false, false, false);
#else
            localKeyPairs = keyPairs;
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Set HTTPS Security Protocol
#if TEST
            error = null;

            if (Utility.SetWebSecurityProtocol(
                    false, ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            //
            // BUGFIX: We must for use of a hash algorithm here that we know
            //         will be compatible with remote servers.  It does not
            //         matter what the caller specified if the end result is
            //         a failure to verify the signature of the downloaded
            //         data.
            //
            string localHashAlgorithmName =
                CertificateSharedOps.GetHashAlgorithm(hashAlgorithmName,
                    localKeyPairs, certificate, HashAlgorithmType.RemoteUse);

            list = null;
            error = null;

            try
            {
                list = CertificateNetworkOps.DownloadList(
                    interpreter, localHashAlgorithmName, hashKey, encoding,
                    localKeyPairs, localUri, EntityType.List, timeout, true,
                    false, ref error);
            }
            finally
            {
                if (list != null)
                {
                    CertificateRevocationState.WasDownloaded(
                        id, ReturnCode.Ok, list);
                }
                else
                {
                    CertificateRevocationState.WasDownloaded(
                        id, ReturnCode.Error, error);
                }
            }

            if (list == null)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Network certificate revocation check via {0} failed: {1}",
                        Utility.FormatWrapOrNull(localUri),
                        Utility.FormatWrapOrNull(error)),
                    typeof(CertificateRevocationOps).Name,
                    TracePriority.MediumHigh, 0);
#endif

                if (!CertificateSharedOps.HasFlags(
                        networkFlags, NetworkFlags.Strict, true) &&
                    (CertificateSharedOps.MatchFlags(
                        certificate, FlagType.Feature, flagsKey,
                        Features.RelaxedRevocationOrAll, null,
                        false, false, true) == ReturnCode.Ok))
                {
                    result = OperationStatus.UnknownRevoked;
                    return ReturnCode.Ok;
                }
                else
                {
                    result = error;
                    return ReturnCode.Error;
                }
            }

        skipDownload:

            error = null;

            if (CheckIdList(
                    id, dateTime, list, ref revoked,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            result = OperationStatus.NotRevoked;
            return ReturnCode.Ok;
            #endregion
#else
            result = OperationStatus.NotRevoked;
            return ReturnCode.Ok;
#endif
        }
    }
}
