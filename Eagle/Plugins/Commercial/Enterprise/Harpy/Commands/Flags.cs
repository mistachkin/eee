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

using System;
using System.Collections.Generic;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using DataOps = Licensing.Components.Private.CertificateDataOps;

using FlagsPair = System.Collections.Generic.KeyValuePair<long, string>;
using FlagsDictionary = System.Collections.Generic.IDictionary<long, string>;
using SortedFlagsPair = System.Collections.Generic.KeyValuePair<ulong, string>;

using SortedFlagsDictionary = System.Collections.Generic.SortedDictionary<
    ulong, string>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "flags" ensemble command, which provides sub-commands
    /// for inspecting and manipulating the attribute flags associated with a
    /// license certificate.  The supported sub-commands include "about",
    /// "change", "check", "have", "isolated", "options", "show", and
    /// "verify".
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a2971dc0-3c12-4a29-8e2b-23215d97c6f7")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("string")]
    internal sealed class Flags : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Flags" /> command.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure this command.
        /// </param>
        public Flags(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region ILicenseCommandData Members
        /// <summary>
        /// Gets the licensing feature(s) required in order to use this
        /// command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.FlagsOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-commands supported by this command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "change", "check", "have",
            "isolated", "options", "show", "verify"
        });

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-commands supported by this
        /// command.
        /// </summary>
        public override EnsembleDictionary SubCommands
        {
            get { return subCommands; }
            set { subCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes this command using the specified sub-command and
        /// arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which this command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data, if any, supplied by the caller.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to this command, including its
        /// name and sub-command.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of executing this command; upon
        /// failure, receives an appropriate error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code
        /// such as <see cref="ReturnCode.Error" />.
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
                    "wrong # args: should be \"{0} option ?arg ...?\"",
                    this.Name);

                return ReturnCode.Error;
            }

            if (CanExecute(interpreter, ref result) != ReturnCode.Ok)
                return ReturnCode.Error;

            ReturnCode code;
            string subCommand = arguments[1];
            bool tried = false;

            code = Utility.TryExecuteSubCommandFromEnsemble(
                interpreter, this, clientData, arguments, true,
                null, ref subCommand, ref tried, ref result);

            if ((code == ReturnCode.Ok) && !tried)
            {
                switch (subCommand)
                {
                    case "about":
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    code = plugin.About(interpreter, ref result);
                                }
                                else
                                {
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "change":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-complex", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-space", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-sort", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-legacy", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-compact", null),
                                    new Option(null, OptionFlags.MustHaveWideIntegerValue, Index.Invalid, Index.Invalid, "-key", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 2) == arguments.Count))
                                    {
                                        bool complex = false;

                                        if (options.IsPresent("-complex"))
                                            complex = true;

                                        bool space = false;

                                        if (options.IsPresent("-space"))
                                            space = true;

                                        bool sort = false;

                                        if (options.IsPresent("-sort"))
                                            sort = true;

                                        bool legacy = false;

                                        if (options.IsPresent("-legacy"))
                                            legacy = true;

                                        bool compact = false;

                                        if (options.IsPresent("-compact"))
                                            compact = true;

                                        IVariant value = null;
                                        long key = Utility.DefaultAttributeFlagsKey();

                                        if (options.IsPresent("-key", ref value))
                                            key = (long)value.Value;

                                        string newText = null;

                                        code = CertificateFlagOps.Change(
                                            arguments[argumentIndex],
                                            arguments[argumentIndex + 1],
                                            key, complex, legacy, compact,
                                            space, sort, ref newText,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                            result = newText;
                                    }
                                    else
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            Option.LooksLikeOption(arguments[argumentIndex]))
                                        {
                                            result = OptionDictionary.BadOption(
                                                options, arguments[argumentIndex], !interpreter.IsSafe());
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options? flags changeFlags\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? flags changeFlags\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "check":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(FlagRuleType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-ruletype", new Variant(FlagRuleType.Default)),
                                    new Option(null, OptionFlags.MustHaveListValue, Index.Invalid, Index.Invalid, "-allow", null),
                                    new Option(null, OptionFlags.MustHaveListValue, Index.Invalid, Index.Invalid, "-deny", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-complex", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-space", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-sort", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-all", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-strict", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = CertificateSharedOps.FixupOptions(this.Plugin, options, false, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int argumentIndex = Index.Invalid;

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.GetOptions(
                                        options, arguments, 0, 2, Index.Invalid,
                                        true, ref argumentIndex, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            ((argumentIndex + 1) == arguments.Count))
                                        {
                                            bool complex = false;

                                            if (options.IsPresent("-complex"))
                                                complex = true;

                                            bool space = false;

                                            if (options.IsPresent("-space"))
                                                space = true;

                                            bool sort = false;

                                            if (options.IsPresent("-sort"))
                                                sort = true;

                                            bool all = false;

                                            if (options.IsPresent("-all"))
                                                all = true;

                                            bool strict = false;

                                            if (options.IsPresent("-strict"))
                                                strict = true;

                                            IVariant value = null;
                                            FlagRuleType ruleType = FlagRuleType.Default;

                                            if (options.IsPresent("-ruletype", ref value))
                                                ruleType = (FlagRuleType)value.Value;

                                            IEnumerable<string> allowRules = null;

                                            if (options.IsPresent("-allow", ref value))
                                                allowRules = (IEnumerable<string>)value.Value;

                                            IEnumerable<string> denyRules = null;

                                            if (options.IsPresent("-deny", ref value))
                                                denyRules = (IEnumerable<string>)value.Value;

                                            bool? flagResult = null;

                                            code = CertificateFlagOps.Check(
                                                arguments[argumentIndex], allowRules,
                                                denyRules, ruleType, complex, space,
                                                sort, all, strict, ref flagResult,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                                result = flagResult;
                                        }
                                        else
                                        {
                                            if ((argumentIndex != Index.Invalid) &&
                                                Option.LooksLikeOption(arguments[argumentIndex]))
                                            {
                                                result = OptionDictionary.BadOption(
                                                    options, arguments[argumentIndex], !interpreter.IsSafe());
                                            }
                                            else
                                            {
                                                result = String.Format(
                                                    "wrong # args: should be \"{0} {1} ?options? flags\"",
                                                    this.Name, subCommand);
                                            }

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? flags\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "have":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-complex", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-space", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-sort", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-all", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-strict", null),
                                    new Option(null, OptionFlags.MustHaveWideIntegerValue, Index.Invalid, Index.Invalid, "-key", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 2) == arguments.Count))
                                    {
                                        bool complex = false;

                                        if (options.IsPresent("-complex"))
                                            complex = true;

                                        bool space = false;

                                        if (options.IsPresent("-space"))
                                            space = true;

                                        bool sort = false;

                                        if (options.IsPresent("-sort"))
                                            sort = true;

                                        bool all = false;

                                        if (options.IsPresent("-all"))
                                            all = true;

                                        bool strict = false;

                                        if (options.IsPresent("-strict"))
                                            strict = true;

                                        IVariant value = null;
                                        long key = Utility.DefaultAttributeFlagsKey();

                                        if (options.IsPresent("-key", ref value))
                                            key = (long)value.Value;

                                        bool? flagResult = null;

                                        code = CertificateFlagOps.Have(
                                            arguments[argumentIndex],
                                            arguments[argumentIndex + 1],
                                            key, complex, space, sort,
                                            all, strict, ref flagResult,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                            result = flagResult;
                                    }
                                    else
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            Option.LooksLikeOption(arguments[argumentIndex]))
                                        {
                                            result = OptionDictionary.BadOption(
                                                options, arguments[argumentIndex], !interpreter.IsSafe());
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options? flags haveFlags\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? flags haveFlags\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "isolated":
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    result = Utility.IsCrossAppDomain(interpreter, plugin);
                                    code = ReturnCode.Ok;
                                }
                                else
                                {
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "options":
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    code = plugin.Options(interpreter, ref result);
                                }
                                else
                                {
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "show":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-complex", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-decimal", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-space", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-sort", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        bool complex = false;

                                        if (options.IsPresent("-complex"))
                                            complex = true;

                                        bool @decimal = false;

                                        if (options.IsPresent("-decimal"))
                                            @decimal = true;

                                        bool space = false;

                                        if (options.IsPresent("-space"))
                                            space = true;

                                        bool sort = false;

                                        if (options.IsPresent("-sort"))
                                            sort = true;

                                        FlagsDictionary flags = null;
                                        Result error = null;

                                        flags = Utility.ParseAttributeFlags(
                                            arguments[argumentIndex], complex,
                                            space, sort, ref error);

                                        if (flags != null)
                                        {
                                            SortedFlagsDictionary sortedFlags =
                                                CertificateFlagOps.GetSorted(flags);

                                            if (sortedFlags != null)
                                            {
                                                StringList list = new StringList();

                                                foreach (SortedFlagsPair pair in sortedFlags)
                                                {
                                                    list.Add(@decimal ? pair.Key.ToString() :
                                                        DataOps.FormatHexadecimal(pair.Key));

                                                    list.Add(pair.Value);
                                                }

                                                result = list;
                                            }
                                            else
                                            {
                                                result = "could not sort flags";
                                                code = ReturnCode.Error;
                                            }
                                        }
                                        else
                                        {
                                            result = error;
                                            code = ReturnCode.Error;
                                        }
                                    }
                                    else
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            Option.LooksLikeOption(arguments[argumentIndex]))
                                        {
                                            result = OptionDictionary.BadOption(
                                                options, arguments[argumentIndex], !interpreter.IsSafe());
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options? flags\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? flags\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verify":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-complex", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-fromobject", null),
                                    new Option(null, OptionFlags.None, Index.Invalid, Index.Invalid, "-space", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        bool complex = false;

                                        if (options.IsPresent("-complex"))
                                            complex = true;

                                        bool fromObject = false;

                                        if (options.IsPresent("-fromobject"))
                                            fromObject = true;

                                        bool space = false;

                                        if (options.IsPresent("-space"))
                                            space = true;

                                        string text = null;

                                        if (fromObject)
                                        {
                                            IObject @object = null;

                                            CertificateIsolatedOps.MaybeFixupResult(
                                                interpreter, this.Plugin, result);

                                            code = interpreter.GetObject(
                                                arguments[argumentIndex],
                                                LookupFlags.Default,
                                                ref @object, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                text = Utility.GetStringFromObject(
                                                    @object.Value);
                                            }
                                        }
                                        else
                                        {
                                            text = arguments[argumentIndex];
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (Utility.VerifyAttributeFlags(
                                                    text, complex, space,
                                                    ref result))
                                            {
                                                code = ReturnCode.Ok;
                                            }
                                            else
                                            {
                                                code = ReturnCode.Error;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            Option.LooksLikeOption(arguments[argumentIndex]))
                                        {
                                            result = OptionDictionary.BadOption(
                                                options, arguments[argumentIndex], !interpreter.IsSafe());
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options? flags\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? flags\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    default:
                        {
                            result = Utility.BadSubCommand(
                                interpreter, null, null, subCommand, this, null, null);

                            code = ReturnCode.Error;
                            break;
                        }
                }
            }

            CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

            return code;
        }
        #endregion
    }
}
