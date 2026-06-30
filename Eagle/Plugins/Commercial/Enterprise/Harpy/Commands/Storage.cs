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

using System;
using System.Reflection;
using System.Security.Cryptography;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "storage" ensemble command, which manages the named
    /// values held by the licensing storage subsystem and exposes the
    /// related sub-commands (e.g. read, write, delete, list, protect, and
    /// unprotect).
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("8ca7f67c-63f0-41c3-8464-09cbd37bfa6a")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("configuration")]
    internal sealed class Storage : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Storage" /> class.
        /// </summary>
        /// <param name="commandData">
        /// The command data used to initialize this command.
        /// </param>
        public Storage(
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
        /// Gets the licensing feature name(s) required to use this command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.StorageOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "delete", "isolated", "list", "options", "protect",
            "read", "type", "unprotect", "write"
        });

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names supported by
        /// this command.
        /// </summary>
        public override EnsembleDictionary SubCommands
        {
            get { return subCommands; }
            set { subCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPolicyEnsemble Members
        /// <summary>
        /// The collection of sub-command names permitted by the active
        /// policy for this command.
        /// </summary>
        private EnsembleDictionary allowedSubCommands =
            new EnsembleDictionary(
                Policies.Storage.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names permitted by
        /// the active policy for this command.
        /// </summary>
        public override EnsembleDictionary AllowedSubCommands
        {
            get { return allowedSubCommands; }
            set { allowedSubCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes this command, dispatching to the appropriate sub-command
        /// based on the supplied <paramref name="arguments" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which this command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The client-specific data associated with this command execution,
        /// if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to this command, including the
        /// command name and the sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of executing the command, or an
        /// error message if execution fails.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an
        /// appropriate error code.
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
                    case "delete":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(StorageType),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid,
                                        "-type", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-security", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

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
                                            IVariant value = null;
                                            StorageType? storageType = null;

                                            if (options.IsPresent("-type", ref value))
                                                storageType = (StorageType)value.Value;

                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            bool? security = null;

                                            if (options.IsPresent("-security", ref value))
                                                security = (bool)value.Value;

                                            IStorageManager storageManager = SharedOps.GetStorageManager(
                                                interpreter, this.Plugin, storageType, security, true);

                                            if (storageManager != null)
                                            {
                                                code = storageManager.DeleteValue(
                                                    arguments[argumentIndex],
                                                    interpreter.CultureInfo,
                                                    perMachine, true, ref result);
                                            }
                                            else
                                            {
                                                result = "storage manager not available";
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
                                                    "wrong # args: should be \"{0} {1} ?options? name\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? name\"",
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
                    case "list":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(StorageType),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid,
                                        "-type", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-security", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int argumentIndex = Index.Invalid;

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    if (arguments.Count > 2)
                                    {
                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }
                                    else
                                    {
                                        code = ReturnCode.Ok;
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            IVariant value = null;
                                            StorageType? storageType = null;

                                            if (options.IsPresent("-type", ref value))
                                                storageType = (StorageType)value.Value;

                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            bool? security = null;

                                            if (options.IsPresent("-security", ref value))
                                                security = (bool)value.Value;

                                            IStorageManager storageManager = SharedOps.GetStorageManager(
                                                interpreter, this.Plugin, storageType, security, true);

                                            if (storageManager != null)
                                            {
                                                string[] names = null;

                                                code = storageManager.ListValues(
                                                    interpreter.CultureInfo,
                                                    perMachine, ref names,
                                                    ref result);

                                                if (code == ReturnCode.Ok)
                                                    result = new StringList(names);
                                            }
                                            else
                                            {
                                                result = "storage manager not available";
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
                                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options?\"",
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
                    case "protect":
                        {
                            if (arguments.Count >= 4)
                            {
#if NATIVE
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

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
                                            ((argumentIndex + 2) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            byte[] data = null;

                                            code = Utility.GetBytesFromString(
                                                arguments[argumentIndex],
                                                interpreter.CultureInfo,
                                                ref data, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                byte[] entropy = null;

                                                code = Utility.GetBytesFromString(
                                                    arguments[argumentIndex + 1],
                                                    interpreter.CultureInfo,
                                                    ref entropy, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
#if !NET_STANDARD_20
                                                    result = Convert.ToBase64String(
                                                        ProtectedData.Protect(data, entropy,
                                                        ProtectOps.GetScope(perMachine)));
#else
                                                    code = ProtectOps.ProtectData(
                                                        entropy, SharedOps.ShouldUsePerMachine(
                                                        perMachine), false, true, null, ref data,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                        result = Convert.ToBase64String(data);
#endif
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
                                                    "wrong # args: should be \"{0} {1} ?options? data entropy\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data entropy\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "read":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(StorageType),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid,
                                        "-type", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-security", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

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
                                            IVariant value = null;
                                            StorageType? storageType = null;

                                            if (options.IsPresent("-type", ref value))
                                                storageType = (StorageType)value.Value;

                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            bool? security = null;

                                            if (options.IsPresent("-security", ref value))
                                                security = (bool)value.Value;

                                            IStorageManager storageManager = SharedOps.GetStorageManager(
                                                interpreter, this.Plugin, storageType, security, true);

                                            if (storageManager != null)
                                            {
                                                byte[] bytes = null;

                                                code = storageManager.ReadValue(
                                                    arguments[argumentIndex],
                                                    interpreter.CultureInfo,
                                                    perMachine, true, ref bytes,
                                                    ref result);

                                                if (code == ReturnCode.Ok)
                                                    result = Convert.ToBase64String(bytes);
                                            }
                                            else
                                            {
                                                result = "storage manager not available";
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
                                                    "wrong # args: should be \"{0} {1} ?options? name\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? name\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "type":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                if (arguments.Count == 3)
                                {
                                    string value = arguments[2];

                                    if (!String.IsNullOrEmpty(value))
                                    {
                                        object enumValue = Utility.TryParseEnum(
                                            typeof(StorageType), value, true,
                                            true, ref result);

                                        if (enumValue is StorageType)
                                        {
                                            CertificateGlobalState.SetStorageType(
                                                (StorageType)enumValue);
                                        }
                                        else
                                        {
                                            code = ReturnCode.Error;
                                        }
                                    }
                                    else
                                    {
                                        CertificateGlobalState.UnsetStorageType();
                                    }
                                }

                                if (code == ReturnCode.Ok)
                                    result = CertificateGlobalState.GetStorageType();
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?type?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "unprotect":
                        {
                            if (arguments.Count >= 4)
                            {
#if NATIVE
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

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
                                            ((argumentIndex + 2) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            byte[] data = null;

                                            code = Utility.GetBytesFromString(
                                                arguments[argumentIndex],
                                                interpreter.CultureInfo,
                                                ref data, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                byte[] entropy = null;

                                                code = Utility.GetBytesFromString(
                                                    arguments[argumentIndex + 1],
                                                    interpreter.CultureInfo,
                                                    ref entropy, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
#if !NET_STANDARD_20
                                                    result = Convert.ToBase64String(
                                                        ProtectedData.Unprotect(data, entropy,
                                                        ProtectOps.GetScope(perMachine)));
#else
                                                    string description = null; /* NOT USED */

                                                    code = ProtectOps.UnprotectData(
                                                        entropy, SharedOps.ShouldUsePerMachine(
                                                        perMachine), false, true, ref description,
                                                        ref data, ref result);

                                                    if (code == ReturnCode.Ok)
                                                        result = Convert.ToBase64String(data);
#endif
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
                                                    "wrong # args: should be \"{0} {1} ?options? data entropy\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data entropy\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "write":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(StorageType),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid,
                                        "-type", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-permachine", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid,
                                        "-security", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

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
                                            ((argumentIndex + 2) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            StorageType? storageType = null;

                                            if (options.IsPresent("-type", ref value))
                                                storageType = (StorageType)value.Value;

                                            bool? perMachine = null;

                                            if (options.IsPresent("-permachine", ref value))
                                                perMachine = (bool)value.Value;

                                            bool? security = null;

                                            if (options.IsPresent("-security", ref value))
                                                security = (bool)value.Value;

                                            IStorageManager storageManager =
                                                SharedOps.GetStorageManager(
                                                    interpreter, this.Plugin, storageType,
                                                    security, true);

                                            if (storageManager != null)
                                            {
                                                byte[] bytes = null;

                                                code = Utility.GetBytesFromString(
                                                    arguments[argumentIndex + 1],
                                                    interpreter.CultureInfo, ref bytes,
                                                    ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    code = storageManager.WriteValue(
                                                        arguments[argumentIndex],
                                                        interpreter.CultureInfo,
                                                        perMachine, true, bytes,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                        result = String.Empty;
                                                }
                                            }
                                            else
                                            {
                                                result = "storage manager not available";
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
                                                    "wrong # args: should be \"{0} {1} ?options? name value\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? name value\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    default:
                        {
                            result = Utility.BadSubCommand(
                                interpreter, null, null, subCommand, this,
                                null, null);

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
