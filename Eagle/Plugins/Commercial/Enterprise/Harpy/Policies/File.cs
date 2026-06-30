/*
 * File.cs --
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
    /// Provides the static data and callback methods that implement the file
    /// execution policy used to determine whether scripts loaded from files
    /// are permitted to be evaluated.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("c44082a4-ba01-481e-b01b-691cbb493ba6")]
    internal static class File
    {
        #region Public Policy Data
        /// <summary>
        /// The <see cref="ExecutionPolicy" /> currently in effect for scripts
        /// loaded from files.
        /// </summary>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultFileExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key used to verify scripts loaded from files.
        /// </summary>
        /* CORE? */
        public static string KeyName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key ring used to verify scripts loaded from files.
        /// </summary>
        /* CORE? */
        public static string KeyRingName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script flags applied when locating and evaluating scripts
        /// loaded from files.
        /// </summary>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultFileScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The path flags used when resolving the locations of scripts loaded
        /// from files.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The network flags used when evaluating scripts that are loaded
        /// from network locations.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback invoked to renew the licensing state while evaluating
        /// the file execution policy.
        /// </summary>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Callback Data
        /// <summary>
        /// The <see cref="IPluginData" /> for the plugin that owns this file
        /// execution policy.
        /// </summary>
        /* CORE? */
        public static IPluginData PluginData = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="ICertificate" /> used to verify scripts loaded from
        /// files.
        /// </summary>
        /* CORE? */
        public static ICertificate Certificate = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="Assembly" /> that contains the certificate used to
        /// verify scripts loaded from files.
        /// </summary>
        /* CORE? */
        public static Assembly Assembly = CertificateAssemblyOps.GetObject();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region File Policy Callback
        /// <summary>
        /// The policy callback that evaluates the file execution policy for
        /// the command currently being processed by the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with this policy invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments to the command being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        [MethodFlags(MethodFlags.FilePolicy)]
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
        /// Performs the shared work of evaluating the file execution policy
        /// on behalf of <see cref="PolicyCallback" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with this policy invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments to the command being checked against the policy.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to ignore the base policy when evaluating the file
        /// execution policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
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
            /* IGNORED */
            CertificateKeyPairState.RemoveAllApproved(interpreter, true);

            ExecutionPolicy policy = CurrentPolicy | Other.CurrentPolicy;
            ScriptFlags scriptFlags = ScriptFlags | Other.ScriptFlags;

            return CertificatePolicyOps.FileCallback(policy,
                PolicyType.File, Features.Policies.FileOrAll, Certificate,
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
