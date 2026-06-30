/*
 * Secret.cs --
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

namespace Licensing.Policies
{
    /// <summary>
    /// Provides the command policy used to control access to the secret
    /// command and its sub-commands.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("1da07bf8-1925-4124-a282-cd7f327d0f73")]
    internal static class Secret
    {
        #region Public Static Data
        /// <summary>
        /// The set of secret sub-command names that are permitted by the
        /// command policy.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "isolated", "options"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// Implements the command policy callback that authorizes execution
        /// of the secret command and its sub-commands.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with this policy invocation.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command being checked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation,
        /// including any error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> value indicating the outcome of the
        /// policy evaluation.
        /// </returns>
        [MethodFlags(MethodFlags.CommandPolicy)]
        private static ReturnCode PolicyCallback( /* POLICY */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Utility.SubCommandPolicy(
                PolicyFlags.SubCommand, typeof(Commands.Secret),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
