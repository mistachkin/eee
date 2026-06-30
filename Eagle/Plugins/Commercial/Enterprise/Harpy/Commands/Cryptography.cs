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

using System;
using System.Reflection;
using System.Security.Cryptography;
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
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "cryptography" ensemble command, exposing symmetric
    /// encryption and decryption, digital signing, and signature
    /// verification (including the combined encrypt-and-sign and
    /// verify-and-decrypt operations, RC4, and file signing) to scripts.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("124265d4-f5cd-48f0-ba19-5801e2d05e66")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("cryptography")]
    internal sealed class Cryptography : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the cryptography command, merging the
        /// command flags from its base type and from itself.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure this command.
        /// </param>
        public Cryptography(
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
            get { return _Features.Commands.CryptographyOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this ensemble
        /// command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "decrypt", "decrypt3", "encrypt", "encrypt3",
            "encryptandsign", "encryptandsign3", "isolated",
            "options", "rc4", "sign", "signfile", "verify",
            "verifyanddecrypt", "verifyanddecrypt3", "verifyfile"
        });

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-commands supported by this
        /// ensemble command.
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
        /// Executes the cryptography command, dispatching to the selected
        /// sub-command (e.g. encrypt, decrypt, sign, verify) based on the
        /// supplied arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments to the command, including the command name
        /// and the sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the command on success or an
        /// error message on failure.
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
                    case "decrypt":
                    case "encrypt":
                        {
                            bool encrypt = CertificateDataOps.StringEquals(subCommand, "encrypt");

                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-filename", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-encodingname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

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

                                        IVariant value = null;
                                        string fileName = null;

                                        if (options.IsPresent("-filename", ref value))
                                            fileName = value.ToString();

                                        string encodingName = null;

                                        if (options.IsPresent("-encodingname", ref value))
                                            encodingName = value.ToString();

                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IRfc2898DataProvider provider = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CryptographyOps.GetRfc2898DataProvider(
                                                interpreter, arguments[argumentIndex + 1],
                                                ref provider, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;

                                            code = CryptographyOps.EncryptOrDecrypt(
                                                provider, fileName, encodingName,
                                                symmetricAlgorithmName, cipherMode,
                                                paddingMode, oldData, encrypt,
                                                ref newData, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                Result.SetValue(
                                                    ref result, OperationStatus.None, true);

                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, null);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data rfc2898\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data rfc2898\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "decrypt3":
                    case "encrypt3":
                        {
                            bool encrypt = CertificateDataOps.StringEquals(subCommand, "encrypt3");

                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-iterations", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 3) == arguments.Count))
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

                                        IVariant value = null;
                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        int iterations = 0;

                                        if (options.IsPresent("-iterations", ref value))
                                            iterations = (int)value.Value;

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        byte[] salt = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 2],
                                                ref salt, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;

                                            code = CryptographyOps.EncryptOrDecrypt(
                                                symmetricAlgorithmName,
                                                arguments[argumentIndex + 1],
                                                salt, iterations,
                                                SharedOps.GetHashAlgorithm(
                                                    hashAlgorithmName, null, null,
                                                    HashAlgorithmType.CommandUse),
                                                cipherMode, paddingMode, oldData,
                                                encrypt, ref newData, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                Result.SetValue(
                                                    ref result, OperationStatus.None, true);

                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, null);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data password salt\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data password salt\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "encryptandsign":
                        {
                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-filename", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-encodingname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 3) == arguments.Count))
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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string fileName = null;

                                        if (options.IsPresent("-filename", ref value))
                                            fileName = value.ToString();

                                        string encodingName = null;

                                        if (options.IsPresent("-encodingname", ref value))
                                            encodingName = value.ToString();

                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IRfc2898DataProvider provider = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CryptographyOps.GetRfc2898DataProvider(
                                                interpreter, arguments[argumentIndex + 1],
                                                ref provider, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 2],
                                                true, true, ref keyPair, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;
                                            byte[] signature = null;

                                            code = CryptographyOps.EncryptAndSign(
                                                provider, fileName, encodingName,
                                                symmetricAlgorithmName, cipherMode,
                                                paddingMode, oldData,
                                                SharedOps.GetHashAlgorithm(
                                                    hashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse),
                                                null, keyPair, ref newData, ref signature,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, signature);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data provider keyPair\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data provider keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "encryptandsign3":
                        {
                            if (arguments.Count >= 6)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-encrypthashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-signhashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-iterations", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 4) == arguments.Count))
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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string encryptHashAlgorithmName = null;

                                        if (options.IsPresent("-encrypthashalgorithm", ref value))
                                            encryptHashAlgorithmName = value.ToString();

                                        string signHashAlgorithmName = null;

                                        if (options.IsPresent("-signhashalgorithm", ref value))
                                            signHashAlgorithmName = value.ToString();

                                        int iterations = 0;

                                        if (options.IsPresent("-iterations", ref value))
                                            iterations = (int)value.Value;

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        byte[] salt = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 2],
                                                ref salt, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 3],
                                                true, true, ref keyPair, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;
                                            byte[] signature = null;

                                            code = CryptographyOps.EncryptAndSign(
                                                symmetricAlgorithmName,
                                                arguments[argumentIndex + 1],
                                                salt, iterations,
                                                SharedOps.GetHashAlgorithm(
                                                    encryptHashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse |
                                                    HashAlgorithmType.OptionalUse),
                                                cipherMode, paddingMode, oldData,
                                                SharedOps.GetHashAlgorithm(
                                                    signHashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse),
                                                null, keyPair, ref newData, ref signature,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, signature);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data password salt keyPair\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data password salt keyPair\"",
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
                    case "rc4":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if NATIVE
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-cryptoapi", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-encrypt", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-obfuscate", null),
#else
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-cryptoapi", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-encrypt", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-obfuscate", null),
#endif
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
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
                                        IVariant value = null;
                                        bool cryptoApi = false;

                                        if (options.IsPresent("-cryptoapi", ref value))
                                            cryptoApi = (bool)value.Value;

                                        bool encrypt = false;

                                        if (options.IsPresent("-encrypt", ref value))
                                            encrypt = (bool)value.Value;

                                        bool obfuscate = false;

                                        if (options.IsPresent("-obfuscate", ref value))
                                            obfuscate = (bool)value.Value;

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] key;
                                        byte[] data;

                                        if (encoding != null)
                                        {
                                            key = encoding.GetBytes(arguments[argumentIndex]);
                                            data = encoding.GetBytes(arguments[argumentIndex + 1]);
                                        }
                                        else
                                        {
                                            key = Convert.FromBase64String(arguments[argumentIndex]);
                                            data = Convert.FromBase64String(arguments[argumentIndex + 1]);
                                        }

                                        if (hashAlgorithmName != null)
                                        {
                                            key = Utility.HashBytes(hashAlgorithmName, key, ref result);

                                            if (key == null)
                                                code = ReturnCode.Error;
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (obfuscate)
                                            {
                                                SharedOps.ObfuscateKey(
                                                    Utility.GetCurrentProcessId(), ref key);
                                            }

                                            if (cryptoApi)
                                            {
#if NATIVE
                                                code = ProtectOps.Rc4EncryptOrDecrypt(
                                                    key, encrypt, ref data, ref result);
#else
                                                result = "not implemented";
                                                code = ReturnCode.Return;
#endif
                                            }
                                            else
                                            {
                                                code = KeyFile.RC4(key, data, 0, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (encoding != null)
                                                {
                                                    result = encoding.GetString(data);
                                                }
                                                else
                                                {
                                                    result = Convert.ToBase64String(data,
                                                        Base64FormattingOptions.InsertLineBreaks);
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
                                                "wrong # args: should be \"{0} {1} ?options? key data\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? key data\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "sign":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 1],
                                                true, true, ref keyPair, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] signature = null;

                                            code = CryptographyOps.Sign(
                                                SharedOps.GetHashAlgorithm(
                                                    hashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse), null,
                                                oldData, keyPair, ref signature, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), null, signature);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data keyPair\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "signfile":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-nowarning", null),
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
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        bool noWarning = false;

                                        if (options.IsPresent("-nowarning"))
                                            noWarning = true;

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            CertificateAssemblyOps.GetObject(),
                                            CertificateAssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex + 1],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.SignFile(
                                                arguments[argumentIndex], keyPair,
                                                noWarning, ref result);
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? fileName keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verify":
                        {
                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 3) == arguments.Count))
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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 1],
                                                true, true, ref keyPair, ref result);
                                        }

                                        byte[] signature = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 2],
                                                ref signature, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CryptographyOps.Verify(
                                                SharedOps.GetHashAlgorithm(
                                                    hashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse), null,
                                                oldData, keyPair, signature, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), null, null);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data keyPair signature\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data keyPair signature\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifyanddecrypt":
                        {
                            if (arguments.Count >= 6)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-filename", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-encodingname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 4) == arguments.Count))
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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string fileName = null;

                                        if (options.IsPresent("-filename", ref value))
                                            fileName = value.ToString();

                                        string encodingName = null;

                                        if (options.IsPresent("-encodingname", ref value))
                                            encodingName = value.ToString();

                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 1],
                                                true, true, ref keyPair, ref result);
                                        }

                                        byte[] signature = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 2],
                                                ref signature, ref result);
                                        }

                                        IRfc2898DataProvider provider = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CryptographyOps.GetRfc2898DataProvider(
                                                interpreter, arguments[argumentIndex + 3],
                                                ref provider, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;

                                            code = CryptographyOps.VerifyAndDecrypt(
                                                provider, fileName, encodingName,
                                                symmetricAlgorithmName, cipherMode,
                                                paddingMode, oldData,
                                                SharedOps.GetHashAlgorithm(
                                                    hashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse), null,
                                                keyPair, signature, ref newData, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, null);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data keyPair signature rfc2898\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data keyPair signature rfc2898\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifyanddecrypt3":
                        {
                            if (arguments.Count >= 7)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-symmetricalgorithm", null),
                                    new Option(typeof(CipherMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-ciphermode",
                                        new Variant(Constants.DefaultCipherMode)),
                                    new Option(typeof(PaddingMode),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-paddingmode",
                                        new Variant(Constants.DefaultPaddingMode)),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-verifyhashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-decrypthashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-iterations", null),
                                    Option.CreateEndOfOptions()
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 5) == arguments.Count))
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

                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string symmetricAlgorithmName = null;

                                        if (options.IsPresent("-symmetricalgorithm", ref value))
                                            symmetricAlgorithmName = value.ToString();

                                        CipherMode cipherMode = Constants.DefaultCipherMode;

                                        if (options.IsPresent("-ciphermode", ref value))
                                            cipherMode = (CipherMode)value.Value;

                                        PaddingMode paddingMode = Constants.DefaultPaddingMode;

                                        if (options.IsPresent("-paddingmode", ref value))
                                            paddingMode = (PaddingMode)value.Value;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string verifyHashAlgorithmName = null;

                                        if (options.IsPresent("-verifyhashalgorithm", ref value))
                                            verifyHashAlgorithmName = value.ToString();

                                        string decryptHashAlgorithmName = null;

                                        if (options.IsPresent("-decrypthashalgorithm", ref value))
                                            decryptHashAlgorithmName = value.ToString();

                                        int iterations = 0;

                                        if (options.IsPresent("-iterations", ref value))
                                            iterations = (int)value.Value;

                                        byte[] oldData = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex],
                                                ref oldData, ref result);
                                        }

                                        IKeyPair keyPair = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex + 1],
                                                true, true, ref keyPair, ref result);
                                        }

                                        byte[] signature = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 2],
                                                ref signature, ref result);
                                        }

                                        byte[] salt = null;

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.GetByteArray(
                                                interpreter, arguments[argumentIndex + 4],
                                                ref salt, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] newData = null;

                                            code = CryptographyOps.VerifyAndDecrypt(
                                                symmetricAlgorithmName,
                                                arguments[argumentIndex + 3],
                                                salt, iterations,
                                                SharedOps.GetHashAlgorithm(
                                                    decryptHashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse |
                                                    HashAlgorithmType.OptionalUse),
                                                cipherMode, paddingMode, oldData,
                                                SharedOps.GetHashAlgorithm(
                                                    verifyHashAlgorithmName, new IKeyPair[] { keyPair },
                                                    null, HashAlgorithmType.CommandUse),
                                                null, keyPair, signature, ref newData,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(
                                                    interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                IAnyTriplet<object, byte[], byte[]> triplet =
                                                    new AnyTriplet<object, byte[], byte[]>(
                                                        Result.GetValue(result), newData, null);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    triplet, true, true, alias, aliasReference,
                                                    false, ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? data keyPair signature password salt\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? data keyPair signature password salt\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifyfile":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCryptographyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
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
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCryptographyCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            CertificateAssemblyOps.GetObject(),
                                            CertificateAssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex + 1],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = CommandOps.VerifyFile(
                                                arguments[argumentIndex], keyPair,
                                                timeout, ref result);
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? fileName keyPair\"",
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
