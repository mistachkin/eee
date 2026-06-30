/*
 * License.cs --
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
    /// Provides the centralized, static configuration used to enforce
    /// licensing rules for this plugin.  The fields exposed by this class
    /// supply the default cryptographic key information, script, path, and
    /// network flags, the optional <see cref="RenewCallback" /> used to renew
    /// an expired or expiring license, and the plugin, certificate, and
    /// assembly context used by the <see cref="PolicyCallback" /> method that
    /// the Eagle core invokes to evaluate the licensing policy.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("fc9a509c-936c-48f8-94e8-b6a1308a1ce3")]
    internal static class License
    {
        #region Public Policy Data
        /// <summary>
        /// The <see cref="ExecutionPolicy" /> currently in effect for license
        /// processing.  This is initialized to
        /// <see cref="Constants.DefaultLicenseExecutionPolicy" /> and governs
        /// how license-related operations are permitted to execute.
        /// </summary>
        /// <value>
        /// The active <see cref="ExecutionPolicy" /> for license processing.
        /// </value>
        /* CORE? */
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultLicenseExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key used when verifying or processing a license,
        /// or null to use the default key.
        /// </summary>
        /// <value>
        /// The name of the key, or null when no specific key is configured.
        /// </value>
        /* CORE? */
        public static string KeyName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key ring containing the key used when verifying or
        /// processing a license, or null to use the default key ring.
        /// </summary>
        /// <value>
        /// The name of the key ring, or null when no specific key ring is
        /// configured.
        /// </value>
        /* CORE? */
        public static string KeyRingName = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="ScriptFlags" /> used when locating and evaluating
        /// license-related scripts.  This is initialized to
        /// <see cref="Constants.DefaultLicenseScriptFlags" />.
        /// </summary>
        /// <value>
        /// The <see cref="ScriptFlags" /> controlling license script lookup
        /// and evaluation.
        /// </value>
        /* CORE? */
        public static ScriptFlags ScriptFlags =
            Constants.DefaultLicenseScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="PathFlags" /> used when resolving paths during
        /// license verification.  This is initialized to
        /// <see cref="Constants.VerifyPathFlags" />.
        /// </summary>
        /// <value>
        /// The <see cref="PathFlags" /> controlling path resolution during
        /// license verification.
        /// </value>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.VerifyPathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="NetworkFlags" /> used when performing network
        /// operations related to licensing.  This is initialized to
        /// <see cref="Constants.LicenseNetworkFlags" />.
        /// </summary>
        /// <value>
        /// The <see cref="NetworkFlags" /> controlling license-related network
        /// operations.
        /// </value>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.LicenseNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="RenewCallback" /> invoked to renew a license, or
        /// null if license renewal is not supported in the current
        /// configuration.
        /// </summary>
        /// <value>
        /// The <see cref="RenewCallback" /> used to renew a license, or null
        /// when renewal is unsupported.
        /// </value>
        /* CORE? */
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Callback Data
        /// <summary>
        /// The <see cref="IPluginData" /> associated with the licensing policy
        /// callback, or null if it has not yet been established.
        /// </summary>
        /// <value>
        /// The <see cref="IPluginData" /> for the plugin being licensed, or
        /// null.
        /// </value>
        /* CORE? */
        public static IPluginData PluginData = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="ICertificate" /> associated with the licensing
        /// policy callback, or null if it has not yet been established.
        /// </summary>
        /// <value>
        /// The <see cref="ICertificate" /> in effect for the licensing policy,
        /// or null.
        /// </value>
        /* CORE? */
        public static ICertificate Certificate = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The <see cref="Assembly" /> associated with the certificate used by
        /// the licensing policy callback.  This is initialized by querying
        /// <see cref="CertificateAssemblyOps.GetObject" /> for the assembly
        /// that carries the embedded certificate.
        /// </summary>
        /// <value>
        /// The <see cref="Assembly" /> that carries the licensing certificate.
        /// </value>
        /* CORE? */
        public static Assembly Assembly = CertificateAssemblyOps.GetObject();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Policy Callback
        /// <summary>
        /// The policy callback invoked by the Eagle core to enforce licensing
        /// rules.  It is registered as a license policy via the
        /// <see cref="MethodFlags.LicensePolicy" /> attribute flag and is
        /// consulted when the interpreter decides whether a licensed operation
        /// may proceed.  This implementation unconditionally permits the
        /// operation by returning <see cref="ReturnCode.Ok" />.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> in whose context the policy is being
        /// evaluated.
        /// </param>
        /// <param name="clientData">
        /// The <see cref="IClientData" /> associated with the policy
        /// invocation.
        /// </param>
        /// <param name="arguments">
        /// The <see cref="ArgumentList" /> supplied to the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the <see cref="Result" /> of the policy
        /// evaluation, including any error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code
        /// indicating that the policy denied or failed the operation.
        /// </returns>
        [MethodFlags(MethodFlags.LicensePolicy)]
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
