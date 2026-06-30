/*
 * HotKey.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;

namespace HotKey.Policies
{
    /// <summary>
    /// Provides the command policy for the <c>hotkey</c> command, restricting
    /// which of its sub-commands may run in a safe interpreter.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("bfbd0417-48ec-4c3f-aecd-8926587edf7b")]
    internal static class HotKey
    {
        #region Public Static Data
        /// <summary>
        /// The set of <c>hotkey</c> sub-command names permitted in a safe
        /// interpreter; currently only the "add" sub-command is allowed.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            ScriptOps.addSubCommandName
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The policy callback invoked to decide whether a particular
        /// <c>hotkey</c> sub-command invocation is permitted, delegating to
        /// the shared sub-command policy.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The arguments of the command being checked.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision details or an error
        /// message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> when the policy check completes;
        /// otherwise, another <see cref="ReturnCode" /> value that indicates
        /// the type of failure.
        /// </returns>
        [MethodFlags(MethodFlags.CommandPolicy)]
        private static ReturnCode PolicyCallback( /* POLICY */
            Interpreter interpreter,
            IClientData clientData,
            ArgumentList arguments,
            ref Result result
            )
        {
            return Utility.SubCommandPolicy(
                PolicyFlags.SubCommand, typeof(Commands._HotKey),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
    }
}
