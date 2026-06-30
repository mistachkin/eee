/*
 * Script.cs --
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
    /// Provides the script execution policy callback and its associated
    /// configuration data used to evaluate whether scripts are permitted to
    /// run under this licensing policy.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a7981134-0a0f-4481-b202-e81e4306c922")]
    internal static class Script
    {
        #region Public Policy Data
        /// <summary>
        /// Stores the script execution policy currently in effect for this
        /// policy.
        /// </summary>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultScriptExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the name of the key used when evaluating this policy.
        /// </summary>
        /* CORE? */
        public static string KeyName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the name of the key ring used when evaluating this policy.
        /// </summary>
        /* CORE? */
        public static string KeyRingName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the script flags used when reading certificate scripts for
        /// this policy.
        /// </summary>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultScriptScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the path flags used when locating files for this policy.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the network flags used when accessing the network for this
        /// policy.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the callback used to renew the certificate for this policy,
        /// if any.
        /// </summary>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Callback Data
        /// <summary>
        /// Stores the plugin data associated with this policy, if any.
        /// </summary>
        /* CORE? */
        public static IPluginData PluginData = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the certificate associated with this policy, if any.
        /// </summary>
        /* CORE? */
        public static ICertificate Certificate = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the assembly containing the certificate associated with
        /// this policy.
        /// </summary>
        /* CORE? */
        public static Assembly Assembly = CertificateAssemblyOps.GetObject();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Script Policy Callback
        /// <summary>
        /// Evaluates the script execution policy to determine whether the
        /// operation described by <paramref name="arguments" /> is permitted
        /// to run in the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that is evaluating the script subject to this
        /// policy.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-defined data associated with this policy
        /// invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments describing the operation being checked by this
        /// policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy decision.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy was
        /// evaluated successfully.
        /// </returns>
        [MethodFlags(MethodFlags.ScriptPolicy)]
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
        /// Performs the shared work of evaluating the script execution
        /// policy, combining this policy with the corresponding other policy
        /// before delegating to the certificate policy support.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that is evaluating the script subject to this
        /// policy.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-defined data associated with this policy
        /// invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments describing the operation being checked by this
        /// policy.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to ignore the base policy when evaluating this policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy decision.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy was
        /// evaluated successfully.
        /// </returns>
        public static ReturnCode PolicyCallbackHelper( /* CORE? */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            bool ignoreBasePolicy,   /* out */
            ref Result result        /* out */
            )
        {
            ExecutionPolicy policy = CurrentPolicy | Other.CurrentPolicy;
            ScriptFlags scriptFlags = ScriptFlags | Other.ScriptFlags;

            return CertificatePolicyOps.ScriptCallback(policy,
                PolicyType.Script, Features.Policies.ScriptOrAll, Certificate,
                Assembly, KeyName, KeyRingName, scriptFlags, interpreter,
                RenewCallback, clientData, arguments, ignoreBasePolicy,
                ref result);
        }
        #endregion
    }
}
