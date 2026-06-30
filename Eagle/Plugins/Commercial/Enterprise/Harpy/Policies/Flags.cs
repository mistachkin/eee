/*
 * Flags.cs --
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
    /// Provides the command policy used to govern access to the sub-commands
    /// of the <see cref="Commands.Flags" /> command.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("5a2fff8c-7147-4b7c-bad0-d998b9bb9125")]
    internal static class Flags
    {
        #region Public Static Data
        /// <summary>
        /// The set of sub-command names that are permitted by the command
        /// policy associated with the <see cref="Commands.Flags" /> command.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "change", "check", "have", "isolated", "options", "verify"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback that decides whether a given
        /// sub-command of the <see cref="Commands.Flags" /> command is allowed
        /// to execute.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data, if any, associated with the policy invocation.
        /// </param>
        /// <param name="arguments">
        /// The arguments being passed to the command, used to determine which
        /// sub-command is being invoked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
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
                PolicyFlags.SubCommand, typeof(Commands.Flags),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
