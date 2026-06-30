/*
 * Secret.cs --
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

#if XML && NETWORK && WEB
using System.Text;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;

#if XML && NETWORK && WEB
using Eagle._Constants;
#endif

using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Private;
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
    /// Implements the "secret" ensemble command, which manages licensing
    /// secrets by generating, requesting, caching, and deleting them via
    /// the certificate web service.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("9ea78f0b-143d-4a04-935b-788405be1a39")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("cryptography")]
    internal sealed class Secret : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of this class.
        /// </summary>
        /// <param name="commandData">
        /// An <see cref="ICommandData" /> instance containing the data
        /// necessary to create a command of this type.
        /// </param>
        public Secret(
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
        /// Gets the name of the licensing feature(s) required in order to use
        /// this command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.SecretOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "cache", "delete", "generate",
            "isolated", "options", "request"
        });

        ///////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////

        #region IPolicyEnsemble Members
        /// <summary>
        /// The collection of sub-command names permitted by policy for this
        /// command.
        /// </summary>
        private EnsembleDictionary allowedSubCommands =
            new EnsembleDictionary(
                Policies.Secret.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-commands permitted by policy
        /// for this command.
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
        /// Executes this command using the specified arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which this command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data, if any, supplied when this command was created or
        /// invoked.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to this command, including the name
        /// of the command itself.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result produced by this command; upon
        /// failure, receives an error message.
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
                                    code = plugin.About(
                                        interpreter, ref result);
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
                    case "cache":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 4))
                            {
#if XML && SERIALIZATION
                                bool overwrite = false;

                                if (arguments.Count >= 4)
                                {
                                    code = Value.GetBoolean2(
                                        arguments[3], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref overwrite,
                                        ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    ICertificate certificate = null;

                                    code = CertificateVerifyOps.Import(
                                        interpreter, this.Plugin, Constants.DefaultEncoding,
                                        null, arguments[2], interpreter.CultureInfo,
                                        SharedOps.GetTimeout(interpreter, null), true, true,
                                        true, true, true, ref certificate, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (certificate != null)
                                        {
                                            Guid id = certificate.Id;

                                            if (CertificateLicenseState.AddCertificate(
                                                    id, certificate, overwrite, ref result))
                                            {
                                                result = id;
                                                code = ReturnCode.Ok;
                                            }
                                            else
                                            {
                                                code = ReturnCode.Error;
                                            }
                                        }
                                        else
                                        {
                                            result = "cannot cache invalid certificate";
                                            code = ReturnCode.Error;
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
                                    "wrong # args: should be \"{0} {1} fileName ?overwrite?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "delete":
                        {
                            if (arguments.Count >= 3)
                            {
#if XML && NETWORK && WEB
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveAbsoluteUriValue,
                                        Index.Invalid, Index.Invalid, "-uri", null),
                                    new Option(null, OptionFlags.MustHaveByteArrayValue,
                                        Index.Invalid, Index.Invalid, "-apikey", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(
                                    interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        Uri uri = null;

                                        if (options.IsPresent("-uri", ref value))
                                            uri = (Uri)value.Value;

                                        byte[] apiKey = null;

                                        if (options.IsPresent("-apikey", ref value))
                                            apiKey = (byte[])value.Value;

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        byte[] serverId = null;

                                        code = Utility.GetBytesFromString(
                                            arguments[argumentIndex], interpreter.CultureInfo,
                                            ref serverId, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = SecretOps.MakeDeleteRequest(
                                                interpreter, this.Plugin,
                                                interpreter.CultureInfo, encoding,
                                                uri, serverId, apiKey, timeout,
                                                ref result);
                                        }
                                    }
                                    else
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
                                                "wrong # args: should be \"{0} {1} ?options? id\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
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
                                    "wrong # args: should be \"{0} {1} ?options? id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "generate":
                        {
                            if (arguments.Count >= 2)
                            {
#if XML && NETWORK && WEB
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-iterations", null),
                                    new Option(null, OptionFlags.MustHaveListValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithms", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-salt", null),
                                    new Option(null, OptionFlags.MustHaveGuidValue,
                                        Index.Invalid, Index.Invalid, "-id", null),
                                    new Option(null, OptionFlags.MustHaveGuidValue,
                                        Index.Invalid, Index.Invalid, "-certificate", null),
                                    new Option(null, OptionFlags.MustHaveAbsoluteUriValue,
                                        Index.Invalid, Index.Invalid, "-uri", null),
                                    new Option(null, OptionFlags.MustHaveByteArrayValue,
                                        Index.Invalid, Index.Invalid, "-apikey", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-encrypted", null),
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultSecretCommandPolicyType)),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keypair", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                if (arguments.Count > 2)
                                {
                                    CertificateIsolatedOps.MaybeFixupResult(
                                        interpreter, this.Plugin, result);

                                    code = interpreter.GetOptions(
                                        options, arguments, 0, 2, Index.Invalid,
                                        true, ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        IVariant value = null;
                                        int iterations = 0;

                                        if (options.IsPresent("-iterations", ref value))
                                            iterations = (int)value.Value;

                                        StringList hashAlgorithmNames = null;

                                        if (options.IsPresent("-hashalgorithms", ref value))
                                            hashAlgorithmNames = (StringList)value.Value;

                                        byte[] salt = null; /* REUSED */

                                        if (options.IsPresent("-salt", ref value))
                                            salt = Convert.FromBase64String(value.ToString());

                                        Guid id = Guid.Empty;

                                        if (options.IsPresent("-id", ref value))
                                            id = (Guid)value.Value;

                                        Uri uri = null;

                                        if (options.IsPresent("-uri", ref value))
                                            uri = (Uri)value.Value;

                                        byte[] apiKey = null;

                                        if (options.IsPresent("-apikey", ref value))
                                            apiKey = (byte[])value.Value;

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        Guid clientId = Guid.Empty;

                                        if (options.IsPresent("-certificate", ref value))
                                            clientId = (Guid)value.Value;

                                        bool encrypted = false;

                                        if (options.IsPresent("-encrypted", ref value))
                                            encrypted = (bool)value.Value;

                                        PolicyType policyType = Constants.DefaultSecretCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string objectName = null;

                                        if (options.IsPresent("-keypair", ref value))
                                            objectName = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        string clientHashAlgorithmName = null;
                                        string serverHashAlgorithmName = null;
                                        string signatureHashAlgorithmName = null;

                                        if (hashAlgorithmNames != null)
                                        {
                                            if (hashAlgorithmNames.Count > 0)
                                            {
                                                clientHashAlgorithmName = hashAlgorithmNames[0];

                                                if (String.IsNullOrEmpty(clientHashAlgorithmName))
                                                    clientHashAlgorithmName = null;
                                            }

                                            if (hashAlgorithmNames.Count > 1)
                                            {
                                                serverHashAlgorithmName = hashAlgorithmNames[1];

                                                if (String.IsNullOrEmpty(serverHashAlgorithmName))
                                                    serverHashAlgorithmName = null;
                                            }

                                            if (hashAlgorithmNames.Count > 2)
                                            {
                                                signatureHashAlgorithmName = hashAlgorithmNames[2];

                                                if (String.IsNullOrEmpty(signatureHashAlgorithmName))
                                                    signatureHashAlgorithmName = null;
                                            }
                                        }

                                        IKeyPair keyPair = null;

                                        if (objectName != null)
                                        {
                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                CertificateAssemblyOps.GetObject(),
                                                CertificateAssemblyOps.GetName(),
                                                interpreter, objectName, true, true,
                                                ref keyPair, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            byte[] serverId = null;
                                            IRfc2898Data rfc2898Data = null;

                                            code = SecretOps.GenerateData(
                                                interpreter, iterations,
                                                serverHashAlgorithmName,
                                                ref serverId, ref rfc2898Data,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                code = SecretOps.MakeGenerateRequest(
                                                    interpreter, this.Plugin,
                                                    interpreter.CultureInfo, clientId,
                                                    encoding, clientHashAlgorithmName,
                                                    uri, serverId, apiKey, rfc2898Data,
                                                    signatureHashAlgorithmName, keyPair,
                                                    timeout, encrypted, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    Guid guid = Guid.Empty;

                                                    code = CryptographyOps.GetGuidFromSalt(
                                                        serverId, out guid, ref result);

                                                    if (code == ReturnCode.Ok)
                                                        result = guid;
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
                                                "wrong # args: should be \"{0} {1} ?options?\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
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
                                    "wrong # args: should be \"{0} {1} ?options?\"",
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
                                    result = Utility.IsCrossAppDomain(
                                        interpreter, plugin);

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
                                    code = plugin.Options(
                                        interpreter, ref result);
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
                    case "request":
                        {
                            if (arguments.Count >= 3)
                            {
#if XML && NETWORK && WEB
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveListValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithms", null),
                                    new Option(null, OptionFlags.MustHaveGuidValue,
                                        Index.Invalid, Index.Invalid, "-certificate", null),
                                    new Option(null, OptionFlags.MustHaveAbsoluteUriValue,
                                        Index.Invalid, Index.Invalid, "-uri", null),
                                    new Option(null, OptionFlags.MustHaveByteArrayValue,
                                        Index.Invalid, Index.Invalid, "-apikey", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultSecretCommandPolicyType)),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                CertificateIsolatedOps.MaybeFixupResult(
                                    interpreter, this.Plugin, result);

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        StringList hashAlgorithmNames = null;

                                        if (options.IsPresent("-hashalgorithms", ref value))
                                            hashAlgorithmNames = (StringList)value.Value;

                                        Uri uri = null;

                                        if (options.IsPresent("-uri", ref value))
                                            uri = (Uri)value.Value;

                                        byte[] apiKey = null;

                                        if (options.IsPresent("-apikey", ref value))
                                            apiKey = (byte[])value.Value;

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        Guid clientId = Guid.Empty;

                                        if (options.IsPresent("-certificate", ref value))
                                            clientId = (Guid)value.Value;

                                        PolicyType policyType = Constants.DefaultSecretCommandPolicyType;

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

                                        string clientHashAlgorithmName = null;
                                        string serverHashAlgorithmName = null; /* NOT USED */
                                        string signatureHashAlgorithmName = null;

                                        if (hashAlgorithmNames != null)
                                        {
                                            if (hashAlgorithmNames.Count > 0)
                                            {
                                                clientHashAlgorithmName = hashAlgorithmNames[0];

                                                if (String.IsNullOrEmpty(clientHashAlgorithmName))
                                                    clientHashAlgorithmName = null;
                                            }

                                            if (hashAlgorithmNames.Count > 1)
                                            {
                                                serverHashAlgorithmName = hashAlgorithmNames[1];

                                                if (String.IsNullOrEmpty(serverHashAlgorithmName))
                                                    serverHashAlgorithmName = null;
                                            }

                                            if (hashAlgorithmNames.Count > 2)
                                            {
                                                signatureHashAlgorithmName = hashAlgorithmNames[2];

                                                if (String.IsNullOrEmpty(signatureHashAlgorithmName))
                                                    signatureHashAlgorithmName = null;
                                            }
                                        }

                                        byte[] serverId = null;

                                        code = Utility.GetBytesFromString(
                                            arguments[argumentIndex], interpreter.CultureInfo,
                                            ref serverId, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            IRfc2898Data rfc2898Data = null;

                                            code = SecretOps.MakeLookupRequest(
                                                interpreter, this.Plugin,
                                                interpreter.CultureInfo,
                                                clientId, encoding,
                                                clientHashAlgorithmName,
                                                uri, serverId, apiKey,
                                                signatureHashAlgorithmName,
                                                keyRingName, policyType, timeout,
                                                matchKeyRingName, ref rfc2898Data,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                code = SecretOps.ExtractData(
                                                    rfc2898Data, true, ref result);
                                            }
                                        }
                                    }
                                    else
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
                                                "wrong # args: should be \"{0} {1} ?options? id\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
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
                                    "wrong # args: should be \"{0} {1} ?options? id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    default:
                        {
                            result = Utility.BadSubCommand(
                                interpreter, null, null,
                                subCommand, this, null, null);

                            code = ReturnCode.Error;
                            break;
                        }
                }
            }

            CertificateIsolatedOps.MaybeFixupResult(
                interpreter, this.Plugin, result);

            return code;
        }
        #endregion
    }
}
