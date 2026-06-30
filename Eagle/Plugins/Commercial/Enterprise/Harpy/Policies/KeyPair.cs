/*
 * KeyPair.cs --
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
using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Policies
{
    /// <summary>
    /// Provides the key pair certificate policy used to govern the execution
    /// of licensed scripts and commands within the Harpy licensing
    /// subsystem.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("bb4ac4a4-6b6f-4f1c-a164-8e5714181c57")]
    internal static class KeyPair
    {
        #region Public Policy Data
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The execution policy currently in effect for the key pair
        /// certificate policy.
        /// </summary>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultKeyPairExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key used by the key pair certificate policy.
        /// </summary>
        /* CORE? */
        public static string KeyName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key ring used by the key pair certificate policy.
        /// </summary>
        /* CORE? */
        public static string KeyRingName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script flags used when locating and evaluating scripts for the
        /// key pair certificate policy.
        /// </summary>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultKeyPairScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The path flags used when locating resources for the key pair
        /// certificate policy.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The network flags used when fetching resources over the network
        /// for the key pair certificate policy.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to renew the certificate associated with the key
        /// pair certificate policy.
        /// </summary>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Callback Data
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// The plugin data associated with the certificate policy callback.
        /// </summary>
        /* CORE? */
        public static IPluginData PluginData = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The certificate associated with the key pair certificate policy.
        /// </summary>
        /* CORE? */
        public static ICertificate Certificate = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The assembly associated with the certificate used by the key pair
        /// certificate policy.
        /// </summary>
        /* CORE? */
        public static Assembly Assembly = CertificateAssemblyOps.GetObject();
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Data
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// The collection of sub-command names permitted by the key pair
        /// certificate policy.
        /// </summary>
        /* CORE? */
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "assembly", "expired", "isolated", "options",
            "revoked", "root", "script"
        }, true, false);
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// Implements the command policy callback that determines whether a
        /// sub-command of the key pair certificate plugin is permitted to
        /// execute.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the command is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The opaque, caller-specific data associated with the policy
        /// evaluation, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command being checked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        [MethodFlags(MethodFlags.CommandPolicy)]
        public static ReturnCode PolicyCallback( /* POLICY */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            return _Utility.SubCommandPolicy(
                PolicyFlags.SubCommand, typeof(Commands._KeyPair),
                0, null, true, interpreter, clientData, arguments,
                ref result);
#else
            return ReturnCode.Ok;
#endif
        }
        #endregion
    }
}
