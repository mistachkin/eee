/*
 * Support.cs --
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
    /// Provides the command policy used to control access to the Support
    /// command and its allowed sub-commands.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("4bc85008-88d2-4f40-b6b7-f2984b4bb4bf")]
    internal static class Support
    {
        #region Public Static Data
        /// <summary>
        /// The set of Support sub-command names that are permitted by this
        /// policy.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "isolated", "options"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// Implements the command policy callback for the Support command,
        /// permitting only its allowed sub-commands.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
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
                PolicyFlags.SubCommand, typeof(Commands.Support),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
