/*
 * Keval.cs --
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
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the <c>keval</c> policy engine command, which evaluates a
    /// script or file within the constrained certificate-based shell
    /// environment provided by the licensing subsystem.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("8f626602-cc43-4ab5-aa0f-b1cc955adc49")]
    [CommandFlags(CommandFlags.Safe
#if ENTERPRISE_LOCKDOWN
        | CommandFlags.NoRename
        | CommandFlags.NoRemove
#endif
    )]
    [ObjectGroup("policyEngine")]
    internal sealed class Keval : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="Keval" /> command using
        /// the specified command data.
        /// </summary>
        /// <param name="commandData">
        /// The data used to initialize the command, including its name and
        /// other associated metadata.
        /// </param>
        public Keval(
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
        /// Gets the licensing feature, or features, required in order to use
        /// this command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.KevalOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the <c>keval</c> command, parsing its options and then
        /// evaluating the specified script or file within the certificate
        /// shell environment.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied to the command by its caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command, including any
        /// options and the script or file to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script or file,
        /// or an error message describing why the command failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value indicating the type of failure.
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

            if (arguments.Count < 2)
            {
                result = String.Format(
                    "wrong # args: should be \"{0} ?options? script\"",
                    this.Name);

                return ReturnCode.Error;
            }

            if (CanExecute(interpreter, ref result) != ReturnCode.Ok)
                return ReturnCode.Error;

            IOption defaultsOption = new Option(
                null, OptionFlags.None, Index.Invalid,
                Index.Invalid, "-defaults", null);

            int argumentIndex = Index.Invalid; /* IGNORED */

            OptionDictionary preOptions = new OptionDictionary(
                new IOption[] {
                defaultsOption,
                Option.CreateEndOfOptions()
            });

            CertificateIsolatedOps.MaybeFixupResult(
                interpreter, this.Plugin, result);

            if (interpreter.CheckOptions(
                    preOptions, arguments, 0, 1, Index.Invalid,
                    ref argumentIndex, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool defaults = false;

            if (defaultsOption.IsPresent(null))
                defaults = true;

            //
            // HACK: The "-defaults" option has now been processed;
            //       therefore, permit it to be present (because it
            //       will __still__ be present in the "arguments"
            //       list if it was before) but just ignore it.
            //
            defaultsOption.Flags |= OptionFlags.Ignored;

            ShellFlags? defaultFlags = null;

            if (defaults)
                defaultFlags = CertificateShellState.GetFlags();

            OptionDictionary options = new OptionDictionary(
                new IOption[] {
                defaultsOption,
                new Option(null, OptionFlags.None,
                    Index.Invalid, Index.Invalid, "-danger", null),
                new Option(null, OptionFlags.None,
                    Index.Invalid, Index.Invalid, "-file", null),
                new Option(null, OptionFlags.MustHaveEncodingValue,
                    Index.Invalid, Index.Invalid, "-encoding", null),
                new Option(typeof(ShellFlags), OptionFlags.MustHaveEnumValue,
                    Index.Invalid, Index.Invalid, "-flags",
                    (defaultFlags != null) ? new Variant(defaultFlags) : null),
                new Option(null, OptionFlags.MustHaveIntegerValue,
                    Index.Invalid, Index.Invalid, "-timeout", null),
                Option.CreateEndOfOptions()
            });

            if (SharedOps.FixupOptions(
                    this.Plugin, options, false, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            CertificateIsolatedOps.MaybeFixupResult(
                interpreter, this.Plugin, result);

            if (interpreter.GetOptions(
                    options, arguments, 0, 1, Index.Invalid, true,
                    ref argumentIndex, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if ((argumentIndex == Index.Invalid) ||
                ((argumentIndex + 1) != arguments.Count))
            {
                if ((argumentIndex != Index.Invalid) &&
                    Option.LooksLikeOption(arguments[argumentIndex]))
                {
                    result = OptionDictionary.BadOption(
                        options, arguments[argumentIndex],
                        !interpreter.IsSafe());
                }
                else
                {
                    result = String.Format(
                        "wrong # args: should be \"{0} ?options? script\"",
                        this.Name);
                }

                return ReturnCode.Error;
            }

            bool danger = false;

            if (options.IsPresent("-danger"))
                danger = true;

            bool file = false;

            if (options.IsPresent("-file"))
                file = true;

            IVariant value = null;
            Encoding encoding = null;

            if (options.IsPresent("-encoding", ref value))
                encoding = (Encoding)value.Value;

            ShellFlags? flags = defaultFlags;

            if (options.IsPresent("-flags", ref value))
                flags = (ShellFlags)value.Value;

            CertificateShellState.MaybeForbidFlags(danger, ref flags);

            if ((flags != null) && CertificateShellState.ApplyFlags(
                    interpreter, this.Plugin, (ShellFlags)flags,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int? timeout = SharedOps.GetTimeout(interpreter, null);

            if (options.IsPresent("-timeout", ref value))
                timeout = (int)value.Value;

            ReturnCode code;
            int errorLine = 0;

            if (file)
            {
                if (encoding != null)
                {
                    code = CertificateShellOps.EvaluateEncodedFile(
                        interpreter, encoding, arguments[argumentIndex],
                        timeout, flags, ref result, ref errorLine);
                }
                else
                {
                    code = CertificateShellOps.EvaluateFile(
                        interpreter, arguments[argumentIndex],
                        timeout, flags, ref result, ref errorLine);
                }
            }
            else
            {
                code = CertificateShellOps.EvaluateScript(
                    interpreter, arguments[argumentIndex], flags,
                    ref result, ref errorLine);
            }

            if ((code == ReturnCode.Error) && (result != null))
                result.ErrorLine = errorLine;

            return code;
        }
        #endregion
    }
}
