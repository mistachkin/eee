/*
 * KeyRing.cs --
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
    /// Provides the command policy support for the "keyRing" command,
    /// restricting the set of sub-commands that are permitted to be used.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("f2bf1b20-36fc-439b-b444-af7019c54ecc")]
    internal static class KeyRing
    {
        #region Public Static Data
        /// <summary>
        /// The set of sub-command names that are permitted by the command
        /// policy callback.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "assembly", "embedded", "isolated", "options", "script"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback used to permit only the sub-commands
        /// named in <see cref="AllowedSubCommandNames" /> for the "keyRing"
        /// command.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the command policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied when the command policy was created, if
        /// any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments to the command being checked against the
        /// policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result produced by evaluating the
        /// command policy.
        /// </param>
        /// <returns>
        /// The return code indicating the outcome of the command policy
        /// evaluation.
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
                PolicyFlags.SubCommand, typeof(Commands.KeyRing),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
