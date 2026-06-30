/*
 * Certificate.cs --
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
    /// Provides the command policy used to control access to the sub-commands
    /// of the certificate command.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("4c1efc97-3644-41bd-969c-6805ba4b2366")]
    internal static class Certificate
    {
        #region Public Static Data
        /// <summary>
        /// The set of sub-command names that are permitted by the command
        /// policy for the certificate command.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "evaluate", "expired", "flags", "formattimestamp",
            "hash", "hashstring", "isolated", "manager",
            "options", "revoked", "subject", "verify",
            "verifystring"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback that determines whether a given
        /// sub-command of the certificate command is permitted to execute.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data, if any, supplied for use by the policy callback.
        /// </param>
        /// <param name="arguments">
        /// The arguments to the command being checked by the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result produced by evaluating the policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
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
                PolicyFlags.SubCommand, typeof(Commands._Certificate),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
