/*
 * Harpy.cs --
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
    /// Provides the command policy used to control access to the Harpy
    /// (licensing) sub-commands.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("8010fa1d-76ff-4f97-a8a0-9b818ac0ecac")]
    internal static class Harpy
    {
        #region Public Static Data
        /// <summary>
        /// The set of Harpy sub-command names that are permitted by the
        /// command policy.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "failsafemode", "isolated", "keyname", "keyringname",
            "options", "policy", "renewcallback", "scriptflags",
            "sdkmode", "security", "testmode"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback used to determine whether a given
        /// Harpy sub-command is permitted to execute within the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command policy is being
        /// evaluated.
        /// </param>
        /// <param name="clientData">
        /// The optional client data associated with the command policy
        /// evaluation.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the command policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the command policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the command
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
                PolicyFlags.SubCommand, typeof(Commands.Harpy),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
