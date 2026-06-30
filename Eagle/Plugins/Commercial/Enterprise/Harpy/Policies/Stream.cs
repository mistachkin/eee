/*
 * Stream.cs --
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
using Licensing.Interfaces.Public;

namespace Licensing.Policies
{
    /// <summary>
    /// Provides the stream execution policy callback together with the
    /// configuration and callback data it uses to authorize stream
    /// operations within the licensing subsystem.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("3a045c63-faee-4b70-8533-ff7dde47c434")]
    internal static class Stream
    {
        #region Public Policy Data
        /// <summary>
        /// The stream execution policy that is currently in effect.
        /// </summary>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultStreamExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key used when verifying stream certificates.
        /// </summary>
        /* CORE? */
        public static string KeyName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key ring used when verifying stream certificates.
        /// </summary>
        /* CORE? */
        public static string KeyRingName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script flags used when evaluating stream policy scripts.
        /// </summary>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultStreamScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The path flags used when locating stream policy resources.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The network flags used when accessing stream policy resources.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to renew the stream certificate, if any.
        /// </summary>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Callback Data
        /// <summary>
        /// The plugin data associated with the stream policy callback.
        /// </summary>
        /* CORE? */
        public static IPluginData PluginData = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The certificate used to authorize stream operations.
        /// </summary>
        /* CORE? */
        public static ICertificate Certificate = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The assembly associated with the stream certificate.
        /// </summary>
        /* CORE? */
        public static Assembly Assembly = CertificateAssemblyOps.GetObject();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Stream Policy Callback
        /// <summary>
        /// Implements the stream policy callback, delegating to
        /// <see cref="PolicyCallbackHelper" /> to authorize a stream
        /// operation without ignoring the base policy.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the stream policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the policy invocation, if any.
        /// </param>
        /// <param name="arguments">
        /// The arguments describing the stream operation being authorized.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        [MethodFlags(MethodFlags.StreamPolicy)]
        public static ReturnCode PolicyCallback( /* POLICY */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return PolicyCallbackHelper(
                interpreter, clientData, arguments, false, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Shared Policy Support Methods
        /// <summary>
        /// Evaluates the combined stream execution policy for the specified
        /// stream operation, using the configured certificate, assembly,
        /// key, key ring, and script flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the stream policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the policy invocation, if any.
        /// </param>
        /// <param name="arguments">
        /// The arguments describing the stream operation being authorized.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to ignore the base policy when evaluating the stream
        /// operation.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        public static ReturnCode PolicyCallbackHelper( /* CORE? */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            bool ignoreBasePolicy,   /* out */
            ref Result result        /* out */
            )
        {
#if XML && SERIALIZATION
            ExecutionPolicy policy = CurrentPolicy | Other.CurrentPolicy;
            ScriptFlags scriptFlags = ScriptFlags | Other.ScriptFlags;

            return CertificatePolicyOps.StreamCallback(policy,
                PolicyType.Stream, Features.Policies.StreamOrAll, Certificate,
                Assembly, KeyName, KeyRingName, scriptFlags, interpreter,
                RenewCallback, clientData, arguments, ignoreBasePolicy,
                ref result);
#else
            return ReturnCode.Ok;
#endif
        }
        #endregion
    }
}
