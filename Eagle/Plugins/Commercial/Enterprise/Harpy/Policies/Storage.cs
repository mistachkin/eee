/*
 * Storage.cs --
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
    /// Provides the command policy used to control access to the storage
    /// sub-commands, exposing the set of permitted sub-command names and the
    /// <see cref="MethodFlags.CommandPolicy" /> callback that the interpreter
    /// invokes to decide whether a given storage sub-command may execute.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("0bf06592-80ac-45a4-8102-5373ca6d5ad2")]
    internal static class Storage
    {
        #region Public Static Data
        /// <summary>
        /// The set of storage sub-command names that are permitted by the
        /// command policy. Any sub-command not present in this
        /// <see cref="StringDictionary" /> (currently <c>isolated</c> and
        /// <c>options</c>) is denied by <see cref="PolicyCallback" />.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "isolated", "options"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback that determines whether a storage
        /// sub-command is permitted to execute, delegating the decision to the
        /// shared sub-command policy logic so that only the sub-commands listed
        /// in <see cref="AllowedSubCommandNames" /> are approved.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> in which the command policy is being
        /// evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra <see cref="IClientData" /> supplied to the command policy,
        /// which may be null.
        /// </param>
        /// <param name="arguments">
        /// The <see cref="ArgumentList" /> for the command being checked by the
        /// policy, including the sub-command name being requested.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the <see cref="Result" /> produced by the
        /// command policy, including any approval, denial, or error
        /// information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the policy was evaluated
        /// successfully (the approval or denial is conveyed via
        /// <paramref name="result" />); otherwise, an appropriate error code.
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
                PolicyFlags.SubCommand, typeof(Commands.Storage),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
