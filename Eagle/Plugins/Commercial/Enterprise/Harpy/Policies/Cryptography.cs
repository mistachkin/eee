/*
 * Cryptography.cs --
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
    /// Provides the command policy used to control access to the cryptography
    /// sub-commands exposed by the licensing subsystem.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("485d5536-5003-473c-a0b0-22a55494eee6")]
    internal static class Cryptography
    {
        #region Public Static Data
        /// <summary>
        /// The set of cryptography sub-command names that are permitted by the
        /// command policy.
        /// </summary>
        public static readonly StringDictionary AllowedSubCommandNames =
            new StringDictionary(new string[] {
            "encrypt", "isolated", "options", "verify"
        }, true, false);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Command Policy Callback
        /// <summary>
        /// The command policy callback used to allow or deny access to the
        /// cryptography sub-commands based on the configured policy flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the command policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data, if any, supplied when the command policy was
        /// registered.
        /// </param>
        /// <param name="arguments">
        /// The arguments to the command being evaluated by the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the command policy.
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
                PolicyFlags.SubCommand, typeof(Commands.Cryptography),
                0, null, true, interpreter, clientData, arguments,
                ref result);
        }
        #endregion
    }
}
