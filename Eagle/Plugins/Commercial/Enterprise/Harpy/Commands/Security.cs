/*
 * Security.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "security" policy command, which enables or disables
    /// certificate security policy enforcement for the interpreter.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("275dc4e3-40ec-4ff6-937b-6dac2ff85801")]
    [CommandFlags(CommandFlags.Unsafe
#if ENTERPRISE_LOCKDOWN
        | CommandFlags.NoRename
        | CommandFlags.NoRemove
#endif
    )]
    [ObjectGroup("policyEngine")]
    internal sealed class Security : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Security" /> command,
        /// initializing its command flags from the base command type and from
        /// this type.
        /// </summary>
        /// <param name="commandData">
        /// The command data used to initialize the new command instance.
        /// </param>
        public Security(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseCommandData Members
        /// <summary>
        /// Gets the licensing feature name(s) required in order to use this
        /// command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.SecurityOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the "security" command, parsing its arguments to enable
        /// or disable certificate security policy enforcement accordingly.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the command invocation.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of executing the command or an
        /// error message if it could not be executed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public override ReturnCode Execute(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (arguments == null)
            {
                result = "invalid argument list";
                return ReturnCode.Error;
            }

            int argumentCount = arguments.Count;

            if ((argumentCount != 2) && (argumentCount != 3))
            {
                result = String.Format(
                    "wrong # args: should be \"{0} ?force? enabled\"",
                    this.Name);

                return ReturnCode.Error;
            }

            if (CanExecute(
                    interpreter, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool ignoreErrors = false;
            int argumentIndex = 1;

            if ((argumentCount == 3) && Utility.SystemStringEquals(
                    arguments[argumentIndex], "force"))
            {
                ignoreErrors = true;
                argumentIndex++;
            }

            string enabledString = arguments[argumentIndex];

            if (String.IsNullOrEmpty(enabledString))
                enabledString = null;

            bool? enabled = null;

            if (Value.GetNullableBoolean2(
                    enabledString, ValueFlags.AnyBoolean,
                    interpreter.CultureInfo, ref enabled,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return CertificatePolicyOps.MaybeEnableOrDisable(
                interpreter, this.Plugin, enabled, false, true,
                ignoreErrors, ref result);
        }
        #endregion
    }
}
