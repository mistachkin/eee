/*
 * Trace.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public.Delegates;

namespace Licensing.Policies
{
    /// <summary>
    /// Provides the trace licensing policy, holding the configurable policy
    /// data that governs tracing behavior together with the callback used to
    /// enforce that policy.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("afde30fd-662b-4c63-92bd-aad18278b859")]
    internal static class Trace
    {
        #region Public Policy Data
        /// <summary>
        /// The execution policy currently in effect for tracing.
        /// </summary>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultTraceExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script flags used when evaluating the trace policy.
        /// </summary>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultTraceScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The path flags used when resolving paths for the trace policy.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The network flags used when accessing the network for the trace
        /// policy.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to renew the trace policy, or null if none is
        /// configured.
        /// </summary>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Trace Policy Callback
        /// <summary>
        /// Enforces the trace licensing policy for the specified interpreter
        /// and arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the policy invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments being checked against the trace policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result produced by the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the policy permits the operation.
        /// </returns>
        [MethodFlags(MethodFlags.TracePolicy)]
        public static ReturnCode PolicyCallback( /* POLICY */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return ReturnCode.Ok;
        }
        #endregion
    }
}
