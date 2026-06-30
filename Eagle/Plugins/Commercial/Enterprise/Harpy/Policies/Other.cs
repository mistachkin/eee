/*
 * Other.cs --
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
    /// Provides the policy data and policy callback used to evaluate
    /// miscellaneous (other) script requests against the configured
    /// licensing policy.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("64831f0c-b083-44bf-a4d0-21e13b6247be")]
    internal static class Other
    {
        #region Public Policy Data
        /// <summary>
        /// The current execution policy applied to other script requests,
        /// initialized to the default other execution policy.
        /// </summary>
        public static ExecutionPolicy CurrentPolicy =
            Constants.DefaultOtherExecutionPolicy;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script flags used when evaluating other script requests,
        /// initialized to the default other script flags.
        /// </summary>
        public static ScriptFlags ScriptFlags =
            Constants.DefaultOtherScriptFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The path flags used when resolving paths for other script
        /// requests, initialized to the machine path flags.
        /// </summary>
        /* CORE? */
        public static PathFlags PathFlags =
            Constants.MachinePathFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The network flags used when handling network access for other
        /// script requests, initialized to the script network flags.
        /// </summary>
        /* CORE? */
        public static NetworkFlags NetworkFlags =
            Constants.ScriptNetworkFlags;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to renew licensing for other script requests,
        /// or null if no renew callback is configured.
        /// </summary>
        public static RenewCallback RenewCallback = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Other Policy Callback
        /// <summary>
        /// The policy callback invoked to evaluate other script requests
        /// against the configured licensing policy.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the script request being
        /// evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the script request being
        /// evaluated.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the script request being
        /// evaluated.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the policy evaluation was
        /// successful; otherwise, an error return code.
        /// </returns>
        [MethodFlags(MethodFlags.OtherPolicy)]
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
