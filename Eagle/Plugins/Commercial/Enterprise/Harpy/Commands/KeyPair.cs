/*
 * KeyPair.cs --
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
using System.IO;
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Provides the "keyPair" command ensemble, which exposes sub-commands
    /// for generating, opening, saving, inspecting, and otherwise managing
    /// the cryptographic key pairs used by the licensing subsystem.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("34905bb9-7330-4f44-993a-74cf2556a89c")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("keyManagement")]
    internal sealed class _KeyPair : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="_KeyPair" /> command
        /// using the specified command data.
        /// </summary>
        /// <param name="commandData">
        /// The data used to initialize the new command instance.
        /// </param>
        public _KeyPair(
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
        /// Gets the licensing feature name(s) required in order to use this
        /// command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.KeyPairOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// Stores the collection of sub-command names supported by this
        /// command ensemble.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "assembly", "dump", "expired",
            "generate", "isolated", "metadata", "open",
            "options", "resources", "revoked", "root",
            "save", "script", "token"
        });

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names supported by this
        /// command ensemble.
        /// </summary>
        public override EnsembleDictionary SubCommands
        {
            get { return subCommands; }
            set { subCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IPolicyEnsemble Members
        /// <summary>
        /// Stores the collection of sub-command names that are permitted by
        /// the command policy for this command ensemble.
        /// </summary>
        private EnsembleDictionary allowedSubCommands = new EnsembleDictionary(
            Policies.KeyPair.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names that are
        /// permitted by the command policy for this command ensemble.
        /// </summary>
        public override EnsembleDictionary AllowedSubCommands
        {
            get { return allowedSubCommands; }
            set { allowedSubCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the "keyPair" command, dispatching to the appropriate
        /// sub-command based on the supplied arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in whose context this command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command, including the
        /// command name itself.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result produced by the command or an
        /// error message describing why it failed.
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

            if (arguments.Count < 2)
            {
                result = String.Format(
                    "wrong # args: should be \"{0} ?options? script\"",
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
                    case "assembly":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyname", null),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-pvk", null),
                                    new Option(null, OptionFlags.Unsafe |
                                        OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-password", null),
                                    new Option(typeof(AssemblyKeyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-keytype",
                                        new Variant(AssemblyKeyType.Default)),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-public", null),
                                    new Option(null, OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-private", null)
                                }, Utility.GetFixupReturnValueOptions().Values);

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
                                        ObjectFlags objectFlags;
                                        string objectName;
                                        string interpName;
                                        bool alias;
                                        bool aliasRaw;
                                        bool aliasAll;
                                        bool aliasReference;

                                        Utility.ProcessFixupReturnValueOptions(
                                            options, null, out objectFlags, out objectName,
                                            out interpName, out alias, out aliasRaw,
                                            out aliasAll, out aliasReference);

                                        bool pvk = false;

                                        if (options.IsPresent("-pvk"))
                                            pvk = true;

                                        IVariant value = null;
                                        AssemblyKeyType keyType = AssemblyKeyType.Default;

                                        if (options.IsPresent("-keytype", ref value))
                                            keyType = (AssemblyKeyType)value.Value;

                                        bool publicKey = false;

                                        if (options.IsPresent("-public"))
                                            publicKey = true;

                                        bool privateKey = false;

                                        if (options.IsPresent("-private"))
                                            privateKey = true;

                                        string password = null;

                                        if (options.IsPresent("-password", ref value))
                                            password = value.ToString();

                                        string keyName = null;

                                        if (options.IsPresent("-keyname", ref value))
                                            keyName = value.ToString();

                                        Assembly assembly = null;

                                        code = CommandOps.GetAssemblyObject(
                                            interpreter, arguments[argumentIndex], false,
                                            ref assembly, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (assembly == null)
                                                assembly = CertificateAssemblyOps.GetObject();

                                            IKeyPair keyPair = null;

                                            code = CertificateKeyPairOps.GetAssemblyOrEmbedded( /* OK */
                                                assembly, (assembly != null) ? assembly.GetName() : null,
                                                keyName, pvk, password, keyType, publicKey, privateKey,
                                                ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                    this.Plugin), interpreter.CultureInfo, null, objectFlags |
                                                    CommandOps.GetExtraObjectFlags(interpreter, true),
                                                    options, Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName, keyPair,
                                                    true, true, alias, aliasReference, false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? assembly\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? assembly\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "dump":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                        Index.Invalid, "-chainonly", null),
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
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
                                        IVariant value = null;
                                        bool chainOnly = false;

                                        if (options.IsPresent("-chainonly", ref value))
                                            chainOnly = (bool)value.Value;

                                        PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            CertificateAssemblyOps.GetObject(),
                                            CertificateAssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (keyPair != null)
                                            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                                                if (chainOnly)
                                                    result = keyPair.Chain() as StringPairList;
                                                else
                                                    result = keyPair.Dump() as StringPairList;
#else
                                                result = keyPair.ToString();
#endif
                                            }
                                            else
                                            {
                                                result = "invalid key pair";
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "expired":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
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
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            CertificateAssemblyOps.GetObject(),
                                            CertificateAssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            //
                                            // HACK: If a particular key pair is expired, it cannot be
                                            //       used to verify anything.  This does not apply if
                                            //       a key pair has the "ExpireSignature" usage flag.
                                            //
                                            code = SharedOps.CheckKeyExpiration(
                                                keyPair, null, ref result);

                                            if (code == ReturnCode.Ok)
                                                result = String.Empty;
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                                this.Name, subCommand);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "generate":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(KeyNumber), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keynumber",
                                        new Variant(KeyNumber.AT_DEFAULT)),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-keysize", null),
                                    new Option(typeof(KeyPairType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keypairtype", null),
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
                                            KeyNumber keyNumber = KeyNumber.AT_DEFAULT;

                                            if (options.IsPresent("-keynumber", ref value))
                                                keyNumber = (KeyNumber)value.Value;

                                            int keySize = 0;

                                            if (options.IsPresent("-keysize", ref value))
                                                keySize = (int)value.Value;

                                            KeyPairType? keyPairType = null;

                                            if (options.IsPresent("-keypairtype", ref value))
                                                keyPairType = (KeyPairType)value.Value;

                                            if (keyPairType == null)
                                                keyPairType = KeyPairType.Legacy;

                                            code = CertificateKeyPairOps.Generate(
                                                keyPairType, arguments[argumentIndex],
                                                keyNumber, keySize, ref result);

                                            if (code == ReturnCode.Ok)
                                                result = String.Empty;
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
                                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
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
                    case "metadata":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(KeyPairType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keypairtype",
                                        new Variant(KeyPairType.None)),
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if ((argumentIndex == Index.Invalid) ||
                                            ((argumentIndex + 2) == arguments.Count) ||
                                            ((argumentIndex + 3) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            KeyPairType? keyPairType = null;

                                            if (options.IsPresent("-keypairtype", ref value))
                                                keyPairType = (KeyPairType)value.Value;

                                            if ((argumentIndex != Index.Invalid) &&
                                                (((argumentIndex + 2) == arguments.Count) ||
                                                ((argumentIndex + 3) == arguments.Count)))
                                            {
                                                PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                                if (options.IsPresent("-policytype", ref value))
                                                    policyType = (PolicyType)value.Value;

                                                bool matchKeyRingName = false;

                                                if (options.IsPresent("-matchkeyringname"))
                                                    matchKeyRingName = true;

                                                string keyRingName = null;

                                                if (options.IsPresent("-keyringname", ref value))
                                                    keyRingName = value.ToString();

                                                IKeyPair keyPair = null;

                                                code = CertificateKeyPairOps.GetOne( /* OK */
                                                    keyRingName, policyType, matchKeyRingName,
                                                    CertificateAssemblyOps.GetObject(),
                                                    CertificateAssemblyOps.GetName(),
                                                    interpreter, arguments[argumentIndex],
                                                    true, true, ref keyPair, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    Type metadataType = CommandOps.GetMetadataType(
                                                        keyPair, keyPairType);

                                                    string propertyName = arguments[argumentIndex + 1];

                                                    if ((argumentIndex + 3) == arguments.Count)
                                                    {
                                                        if (code == ReturnCode.Ok)
                                                        {
                                                            object propertyValue = arguments[argumentIndex + 2].Value;

                                                            code = CommandOps.GetMetadataValue(
                                                                interpreter, metadataType, keyPair,
                                                                propertyName, interpreter.CultureInfo,
                                                                ref propertyValue, ref result);

                                                            if (code == ReturnCode.Ok)
                                                            {
                                                                try
                                                                {
                                                                    CommandOps.SetMetadataPropertyValue(
                                                                        metadataType, propertyName, keyPair,
                                                                        propertyValue);
                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    result = e;
                                                                    code = ReturnCode.Error;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            object returnValue = CommandOps.GetMetadataPropertyValue(
                                                                metadataType, propertyName, keyPair);

                                                            code = CommandOps.GetMetadataResult(
                                                                returnValue, ref result);
                                                        }
                                                        catch (Exception e)
                                                        {
                                                            result = e;
                                                            code = ReturnCode.Error;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    if (keyPairType == null)
                                                        keyPairType = KeyPairType.Legacy;

                                                    StringList list = CommandOps.GetMetadataPropertyNames(
                                                        CommandOps.GetMetadataType(null, keyPairType));

                                                    result = (list != null) ? list : null;
                                                }
                                                catch (Exception e)
                                                {
                                                    result = e;
                                                    code = ReturnCode.Error;
                                                }
                                            }

                                            if (code == ReturnCode.Ok)
                                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);
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
                                                    "wrong # args: should be \"{0} {1} ?options?\" -OR- \"{0} {1} ?options? keyPair propertyName\" -OR- \"{0} {1} ?options? keyPair propertyName propertyValue\"",
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
                                    "wrong # args: should be \"{0} {1} ?options?\" -OR- \"{0} {1} ?options? keyPair propertyName\" -OR- \"{0} {1} ?options? keyPair propertyName propertyValue\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "open":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(KeyPairType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keypairtype", null),
                                    new Option(typeof(KeyFileFormat), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keyfileformat", null),
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-pvk", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-password", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-public", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-private", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-usestream", null)
                                }, Utility.GetFixupReturnValueOptions().Values);

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
                                            ObjectFlags objectFlags;
                                            string objectName;
                                            string interpName;
                                            bool alias;
                                            bool aliasRaw;
                                            bool aliasAll;
                                            bool aliasReference;

                                            Utility.ProcessFixupReturnValueOptions(
                                                options, null, out objectFlags, out objectName,
                                                out interpName, out alias, out aliasRaw,
                                                out aliasAll, out aliasReference);

                                            bool pvk = false;

                                            if (options.IsPresent("-pvk"))
                                                pvk = true;

                                            bool publicKey = false;

                                            if (options.IsPresent("-public"))
                                                publicKey = true;

                                            bool privateKey = false;

                                            if (options.IsPresent("-private"))
                                                privateKey = true;

                                            bool useStream = false;

                                            if (options.IsPresent("-usestream"))
                                                useStream = true;

                                            IVariant value = null;
                                            PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string password = null;

                                            if (options.IsPresent("-password", ref value))
                                                password = value.ToString();

                                            KeyPairType? keyPairType = null;

                                            if (options.IsPresent("-keypairtype", ref value))
                                                keyPairType = (KeyPairType)value.Value;

                                            KeyFileFormat? format = null;

                                            if (options.IsPresent("-keyfileformat", ref value))
                                                format = (KeyFileFormat)value.Value;

                                            IKeyPair keyPair = null;

                                            //
                                            // NOTE: If the "useStream" option is enabled, we assume
                                            //       that the "fileName" is actually an opaque object
                                            //       handle that refers to a Stream.
                                            //
                                            if (useStream)
                                            {
                                                Stream stream = null;

                                                code = CommandOps.GetStream(
                                                    interpreter, arguments[argumentIndex],
                                                    ref stream, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    if (keyPairType == null)
                                                        keyPairType = KeyPairType.Legacy;

                                                    code = KeyFile.Open(stream,
                                                        KeyFile.GetReadCallback(
                                                            keyPair, keyPairType),
                                                        KeyFile.GetFormat(
                                                            keyPair, keyPairType,
                                                            format), pvk, password,
                                                        publicKey, privateKey,
                                                        ref keyPair, ref result);
                                                }
                                            }
                                            else
                                            {
                                                ResultList errors = null;
                                                Result localResult = null;

                                                code = CertificateKeyPairOps.GetOne( /* OK */
                                                    keyRingName, policyType, matchKeyRingName,
                                                    CertificateAssemblyOps.GetObject(),
                                                    CertificateAssemblyOps.GetName(),
                                                    interpreter, arguments[argumentIndex],
                                                    true, true, ref keyPair, ref localResult);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    if (keyPair.HavePublicKey != publicKey)
                                                    {
                                                        if (errors == null)
                                                            errors = new ResultList();

                                                        errors.Add(String.Format(
                                                            "public key is {0}present",
                                                            publicKey ? "not " : String.Empty));

                                                        code = ReturnCode.Error;
                                                    }

                                                    if (keyPair.HavePrivateKey != privateKey)
                                                    {
                                                        if (errors == null)
                                                            errors = new ResultList();

                                                        errors.Add(String.Format(
                                                            "private key is {0}present",
                                                            privateKey ? "not " : String.Empty));

                                                        code = ReturnCode.Error;
                                                    }
                                                }
                                                else
                                                {
                                                    if (localResult != null)
                                                    {
                                                        if (errors == null)
                                                            errors = new ResultList();

                                                        errors.Add(localResult);
                                                    }

                                                    if (keyPairType == null)
                                                    {
                                                        keyPairType = KeyFile.GuessKeyPairType(
                                                            arguments[argumentIndex]);
                                                    }

                                                    if (keyPairType == null)
                                                        keyPairType = KeyPairType.Legacy;

                                                    localResult = null;

                                                    code = KeyFile.Open(
                                                        arguments[argumentIndex],
                                                        KeyFile.GetReadCallback(
                                                        keyPair, keyPairType),
                                                        KeyFile.GetFormat(keyPair,
                                                        keyPairType, format), pvk,
                                                        password, publicKey,
                                                        privateKey, ref keyPair,
                                                        ref localResult);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        result = localResult;
                                                    }
                                                    else
                                                    {
                                                        if (localResult != null)
                                                        {
                                                            if (errors == null)
                                                                errors = new ResultList();

                                                            errors.Add(localResult);
                                                        }
                                                    }
                                                }

                                                if (code != ReturnCode.Ok)
                                                    result = errors;
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                    this.Plugin), interpreter.CultureInfo, null, objectFlags |
                                                    CommandOps.GetExtraObjectFlags(interpreter, true),
                                                    options, Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName, keyPair,
                                                    true, true, alias, aliasReference, false, ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
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
                    case "resources":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                Assembly assembly = CertificateAssemblyOps.GetObject();

                                if (assembly != null)
                                {
                                    if (arguments.Count == 3)
                                    {
                                        StringList list = new StringList();
                                        string pattern = arguments[2];

                                        foreach (string resourceName in
                                                assembly.GetManifestResourceNames())
                                        {
                                            if ((pattern == null) ||
                                                Parser.StringMatch(interpreter,
                                                    resourceName, 0, pattern, 0, false))
                                            {
                                                list.Add(resourceName);
                                            }
                                        }

                                        result = list;
                                    }
                                    else
                                    {
                                        result = new StringList(
                                            assembly.GetManifestResourceNames());
                                    }

                                    code = ReturnCode.Ok;
                                }
                                else
                                {
                                    result = "invalid executing assembly";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?pattern?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "revoked":
                        {
                            if (arguments.Count >= 3)
                            {
#if CERTIFICATE_POLICY
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveDateTimeValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-when", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-forcenetwork", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-strictnetwork", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-nocache", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-failsafe", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-whatif", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-timeout", null),
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
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        DateTime when = Utility.GetUtcNow();

                                        if (options.IsPresent("-when", ref value))
                                            when = (DateTime)value.Value;

                                        string pattern = null;

                                        if (options.IsPresent("-keypairs", ref value))
                                            pattern = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        bool forceNetwork = false;

                                        if (options.IsPresent("-forcenetwork"))
                                            forceNetwork = true;

                                        bool strictNetwork = false;

                                        if (options.IsPresent("-strictnetwork"))
                                            strictNetwork = true;

                                        bool noCache = false;

                                        if (options.IsPresent("-nocache"))
                                            noCache = true;

                                        bool failSafe = false;

                                        if (options.IsPresent("-failsafe"))
                                            failSafe = true;

                                        bool whatIf = failSafe;

                                        if (options.IsPresent("-whatif"))
                                            whatIf = true;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            CertificateAssemblyOps.GetObject(),
                                            CertificateAssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            IEnumerable<IKeyPair> keyPairs = null;

                                            code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                pattern, false, interpreter, EntityType.None,
                                                true, true, true, true, false, ref keyPairs,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (keyPairs != null)
                                                {
                                                    //
                                                    // NOTE: If no encoding was specified, use the
                                                    //       typical default for XML, which is UTF8.
                                                    //
                                                    if (encoding == null)
                                                        encoding = CertificateDataOps.GetDefaultEncoding();

                                                    NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                                                        policyType);

                                                    if (forceNetwork)
                                                        networkFlags |= NetworkFlags.Force;

                                                    if (strictNetwork)
                                                        networkFlags |= NetworkFlags.Strict;

                                                    if (noCache)
                                                        networkFlags |= NetworkFlags.NoCache;

                                                    if (failSafe)
                                                        networkFlags |= NetworkFlags.FailSafe;

                                                    if (whatIf)
                                                        networkFlags |= NetworkFlags.WhatIf;

                                                    code = CertificateRevocationOps.IsRevoked( /* OK */
                                                        interpreter, CertificateAssemblyOps.GetObject(),
                                                        this.Plugin, SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, keyPairs, null,
                                                            HashAlgorithmType.RemoteUse |
                                                            HashAlgorithmType.CommandUse),
                                                        null, encoding, keyPairs, keyPair,
                                                        interpreter.CultureInfo, when, timeout,
                                                        networkFlags, ref result);
                                                }
                                                else
                                                {
                                                    result = "invalid key pair list";
                                                    code = ReturnCode.Error;
                                                }
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                                this.Name, subCommand);
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
                                    "wrong # args: should be \"{0} {1} ?options? keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "root":
                        {
                            if (arguments.Count == 2)
                            {
#if CERTIFICATE_POLICY
                                code = CertificateKeyPairOps.GetRootPublicKeyToken(
                                    CertificateAssemblyOps.GetObject(), ref result);
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
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
                    case "save":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(KeyPairType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keypairtype", null),
                                    new Option(typeof(KeyFileFormat), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keyfileformat", null),
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultKeyPairCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-pvk", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-password", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-public", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-private", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-usestream", null),
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
                                            bool pvk = false;

                                            if (options.IsPresent("-pvk"))
                                                pvk = true;

                                            bool publicKey = false;

                                            if (options.IsPresent("-public"))
                                                publicKey = true;

                                            bool privateKey = false;

                                            if (options.IsPresent("-private"))
                                                privateKey = true;

                                            bool useStream = false;

                                            if (options.IsPresent("-usestream"))
                                                useStream = true;

                                            IVariant value = null;
                                            PolicyType policyType = Constants.DefaultKeyPairCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string password = null;

                                            if (options.IsPresent("-password", ref value))
                                                password = value.ToString();

                                            KeyPairType? keyPairType = null;

                                            if (options.IsPresent("-keypairtype", ref value))
                                                keyPairType = (KeyPairType)value.Value;

                                            KeyFileFormat? format = null;

                                            if (options.IsPresent("-keyfileformat", ref value))
                                                format = (KeyFileFormat)value.Value;

                                            IKeyPair keyPair = null;

                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 1],
                                                true, true, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: If the "useStream" option is enabled, we assume
                                                //       that the "fileName" is actually an opaque object
                                                //       handle that refers to a Stream.
                                                //
                                                if (useStream)
                                                {
                                                    Stream stream = null;

                                                    code = CommandOps.GetStream(
                                                        interpreter, arguments[argumentIndex],
                                                        ref stream, ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        code = KeyFile.Save(stream,
                                                            KeyFile.GetWriteCallback(
                                                                keyPair, keyPairType),
                                                            KeyFile.GetFormat(
                                                                keyPair, keyPairType,
                                                                format), pvk, password,
                                                            publicKey, privateKey,
                                                            keyPair, ref result);
                                                    }
                                                }
                                                else
                                                {
                                                    code = KeyFile.Save(
                                                        arguments[argumentIndex],
                                                        KeyFile.GetWriteCallback(
                                                            keyPair, keyPairType),
                                                        KeyFile.GetFormat(
                                                            keyPair, keyPairType,
                                                            format), pvk, password,
                                                        publicKey, privateKey,
                                                        keyPair, ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? fileName keyPair\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? fileName keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "script":
                        {
                            if (arguments.Count == 2)
                            {
#if CERTIFICATE_POLICY
                                code = CertificateKeyPairOps.GetScriptPublicKeyToken(
                                    interpreter, this.Plugin, interpreter.CultureInfo,
                                    CertificatePolicyOps.MaskPolicy(
                                        this.Plugin, PolicyType.Script,
                                        ExecutionPolicy.UseApprovedData, false),
                                    ref result);
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
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
                    case "token":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(KeyPairType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keypairtype", null),
                                    new Option(typeof(KeyFileFormat), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-keyfileformat", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-strict", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(this.Plugin, options, false, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if ((argumentIndex == Index.Invalid) ||
                                            ((argumentIndex + 1) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            KeyPairType? keyPairType = null;

                                            if (options.IsPresent("-keypairtype", ref value))
                                                keyPairType = (KeyPairType)value.Value;

                                            KeyFileFormat? format = null;

                                            if (options.IsPresent("-keyfileformat", ref value))
                                                format = (KeyFileFormat)value.Value;

                                            bool strict = false;

                                            if (options.IsPresent("-strict"))
                                                strict = true;

                                            if (argumentIndex != Index.Invalid)
                                            {
                                                if (keyPairType == null)
                                                {
                                                    keyPairType = KeyFile.GuessKeyPairType(
                                                        arguments[argumentIndex]);
                                                }

                                                if (keyPairType == null)
                                                    keyPairType = KeyPairType.Legacy;

                                                IKeyPair keyPair = null;
                                                Result localResult = null;

                                                code = KeyFile.Open(
                                                    arguments[argumentIndex],
                                                    KeyFile.GetReadCallback(
                                                        keyPair, keyPairType),
                                                    KeyFile.GetFormat(
                                                        keyPair, keyPairType,
                                                        format), false, null,
                                                    true, false, ref keyPair,
                                                    ref localResult);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    result = CertificateDataOps.FormatPublicKeyToken(
                                                        keyPair.PublicKeyToken, false, false);
                                                }
                                                else if (strict)
                                                {
                                                    result = localResult;
                                                }
                                                else
                                                {
                                                    result = String.Empty;
                                                    code = ReturnCode.Ok;
                                                }
                                            }
                                            else
                                            {
                                                IPlugin plugin = this.Plugin;

                                                if (plugin != null)
                                                {
                                                    result = Utility.GetAssemblyPublicKeyToken(
                                                        plugin.AssemblyName);
                                                }
                                                else
                                                {
                                                    result = "invalid command plugin";
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
                                                    "wrong # args: should be \"{0} {1} ?options? ?fileName?\"",
                                                    this.Name, subCommand);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? ?fileName?\"",
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
