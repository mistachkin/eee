/*
 * Certificate.cs --
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
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;

#if NETWORK
using NetworkState = Licensing.Components.Private.CertificateNetworkState;
#endif

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the certificate command ensemble, which provides licensing
    /// certificate management sub-commands such as signing, verification,
    /// encryption, decryption, hashing, and policy management.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("7b8d432c-39df-479e-9db7-b4f639b6d654")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("certificateManagement")]
    internal sealed class _Certificate : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="_Certificate" />
        /// command using the specified command data.
        /// </summary>
        /// <param name="commandData">
        /// The command data used to initialize this command.
        /// </param>
        public _Certificate(
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
        /// Gets the name(s) of the licensing feature(s) required in order to
        /// use this command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.CertificateOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// Stores the dictionary of sub-command names supported by this
        /// command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "certificate", "cleanup", "current", "decrypt",
            "defaultpolicy", "discard", "downloadlist", "evaluate", "expired",
            "export", "extract", "flags", "formattimestamp", "hash",
            "hashfile", "hashstring", "import", "isolated", "keyname",
            "keyringname", "loadandverify", "manager", "metadata",
            "networkflags", "networktime", "options", "pathflags",
            "policy", "policytrace",
            "renewcallback", "reset", "revoked",
            "scriptflags", "shell", "sign", "signfile", "signhash",
            "signstring", "simplepolicy", "softwareupdates", "source",
            "subject", "time", "trace", "unsetpolicy", "verify",
            "verifyfile", "verifyhash", "verifystream", "verifystring",
            "warning"
        });

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the dictionary of sub-command names supported by this
        /// command.
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
        /// Stores the dictionary of sub-command names that are permitted by
        /// the active policy.
        /// </summary>
        private EnsembleDictionary allowedSubCommands = new EnsembleDictionary(
            Policies.Certificate.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the dictionary of sub-command names that are
        /// permitted by the active policy.
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
        /// Executes this certificate command, dispatching to the appropriate
        /// sub-command based on the supplied <paramref name="arguments" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which this command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to this command, where the first
        /// argument is the command name and the second is the sub-command
        /// name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the command or an error
        /// message if it could not be executed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
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
                    case "certificate": // NOTE: See also "current" (below).
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    Result error = null;

                                    result = plugin.GetCertificateFileName(
                                        interpreter, null, ref error);

                                    if (result == null)
                                    {
                                        result = error;
                                        code = ReturnCode.Error;
                                    }
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
                    case "cleanup":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                if (arguments.Count == 3)
                                {
                                    IPlugin plugin = this.Plugin;

                                    if (plugin != null)
                                    {
                                        IConfiguration configuration = plugin as IConfiguration;

                                        if (configuration != null)
                                        {
                                            ulong token = 0;

                                            code = Value.GetUnsignedWideInteger2(
                                                arguments[2], ValueFlags.AnyWideInteger |
                                                ValueFlags.Unsigned, interpreter.CultureInfo,
                                                ref token, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                int count = 0;
                                                ResultList errors = null;

                                                if (CertificateScriptOps.CleanupInterpreter(token))
                                                {
                                                    count++;
                                                }
                                                else
                                                {
                                                    if (errors == null)
                                                        errors = new ResultList();

                                                    errors.Add(String.Format(
                                                        "could not cleanup interpreter {0}", token));
                                                }

                                                if (configuration.RemoveSandboxToken(token))
                                                {
                                                    count++;
                                                }
                                                else
                                                {
                                                    if (errors == null)
                                                        errors = new ResultList();

                                                    errors.Add(String.Format(
                                                        "could not remove sandbox token {0}", token));
                                                }

                                                if (errors == null)
                                                    result = count;
                                                else
                                                    result = errors;
                                            }
                                        }
                                        else
                                        {
                                            result = "configuration unavailable";
                                            code = ReturnCode.Error;
                                        }
                                    }
                                    else
                                    {
                                        result = "invalid command plugin";
                                        code = ReturnCode.Error;
                                    }
                                }
                                else
                                {
                                    result = CertificateSandboxState.CleanupInterpreters(null);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?token?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "current": // NOTE: See also "certificate" (above).
                        {
                            if (arguments.Count == 2)
                            {
                                ILicenseCertificateData licenseCertificateData =
                                    SharedOps.GetLicenseCertificateData(this.Plugin);

                                if (licenseCertificateData != null)
                                    result = licenseCertificateData.CertificateFileName;
                                else
                                    result = String.Empty;

                                code = ReturnCode.Ok;
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
                        {
                            if (arguments.Count >= 3)
                            {
#if XML
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-noremote", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-resource", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-anyresourcekey", null),
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
                                        bool allowRemoteUri = true; /* TODO: Good default? */

                                        if (options.IsPresent("-noremote"))
                                            allowRemoteUri = false;

                                        bool anyResourcePublicKey = false; /* TODO: Good default? */

                                        if (options.IsPresent("-anyresourcekey"))
                                            anyResourcePublicKey = true;

                                        bool useResource = false;

                                        if (options.IsPresent("-resource"))
                                            useResource = true;

                                        IVariant value = null;
                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        if (encoding == null)
                                            encoding = DataOps.GetDefaultEncoding();

                                        string text;

                                        text = SharedOps.GetDataFromFile(
                                            interpreter, encoding, arguments[argumentIndex],
                                            timeout, allowRemoteUri, anyResourcePublicKey,
                                            false, ref useResource, ref result) as string;

                                        if (text != null)
                                        {
                                            code = CertificateScriptOps.Decrypt(
                                                interpreter, this.Plugin, encoding,
                                                interpreter.CultureInfo, timeout,
                                                ref text, ref result);

                                            if (code == ReturnCode.Ok)
                                                result = text;
                                        }
                                        else
                                        {
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
                                                "wrong # args: should be \"{0} {1} ?options? fileName\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "defaultpolicy":
                        {
                            if (arguments.Count == 2)
                            {
                                result = StringList.MakeList(
#if CERTIFICATE_POLICY
                                    "-script", Constants.DefaultScriptExecutionPolicy,
                                    "-file", Constants.DefaultFileExecutionPolicy,
                                    "-stream", Constants.DefaultStreamExecutionPolicy,
#endif
                                    "-license", Constants.DefaultLicenseExecutionPolicy,
#if CERTIFICATE_POLICY
                                    "-keypair", Constants.DefaultKeyPairExecutionPolicy,
#endif
                                    "-trace", Constants.DefaultTraceExecutionPolicy,
                                    "-other", Constants.DefaultOtherExecutionPolicy);
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
                    case "discard":
                        {
                            if (arguments.Count == 4)
                            {
#if XML && SERIALIZATION
                                string text = arguments[3];

                                code = CertificateXmlOps.Discard(
                                    arguments[2], ref text, ref result);

                                if (code == ReturnCode.Ok)
                                    result = text;
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} fileName text\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "downloadlist":
                        {
                            if (arguments.Count >= 3)
                            {
#if NETWORK
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-signed", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-first", null),
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
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
#if CERTIFICATE_POLICY
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string pattern = null;

                                        if (options.IsPresent("-keypairs", ref value))
                                            pattern = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();
#endif

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        bool signed = false;

                                        if (options.IsPresent("-signed"))
                                            signed = true;

                                        bool first = false;

                                        if (options.IsPresent("-first"))
                                            first = true;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        IEnumerable<IKeyPair> keyPairs = null;

#if CERTIFICATE_POLICY
                                        code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                            pattern, false, interpreter, EntityType.None,
                                            true, true, true, true, false, ref keyPairs,
                                            ref result);
#endif

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (!signed || (keyPairs != null))
                                            {
                                                Uri uri = null;

                                                code = Value.GetUri(
                                                    arguments[argumentIndex], UriKind.Absolute,
                                                    interpreter.CultureInfo, ref uri, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
#if TEST
                                                    code = Utility.SetWebSecurityProtocol(false, ref result);
#endif

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        //
                                                        // NOTE: If no encoding was specified, use the
                                                        //       typical default for XML, which is UTF8.
                                                        //
                                                        if (encoding == null)
                                                            encoding = DataOps.GetDefaultEncoding();

                                                        StringList list = CertificateNetworkOps.DownloadList(
                                                            interpreter, SharedOps.GetHashAlgorithm(
                                                                hashAlgorithmName, keyPairs, null,
                                                                HashAlgorithmType.RemoteUse |
                                                                HashAlgorithmType.CommandUse),
                                                            null, encoding, keyPairs, uri, EntityType.List,
                                                            timeout, signed, first, ref result);

                                                        if (list != null)
                                                        {
                                                            result = list;
                                                        }
                                                        else
                                                        {
                                                            code = ReturnCode.Error;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                result = "invalid key pair list";
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
                                                "wrong # args: should be \"{0} {1} ?options? uri\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? uri\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "evaluate":
                        {
                            if (arguments.Count >= 3)
                            {
#if SHELL && CERTIFICATE_POLICY
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

                                code = interpreter.CheckOptions(
                                    preOptions, arguments, 0, 2, Index.Invalid,
                                    ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
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

                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

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
                                                    "wrong # args: should be \"{0} {1} ?options? script\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? script\"",
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
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null,
                                        OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keypairs", null),
                                    new Option(null,
                                        OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveDateTimeValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-installed", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-forcenetwork", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-strictnetwork", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-viahttp", new Variant(
                                            CertificateTimeState.ShouldQueryViaHttp())),
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
                                        ((argumentIndex + 1) <= arguments.Count) &&
                                        ((argumentIndex + 2) >= arguments.Count))
                                    {
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string pattern = null;

                                        if (options.IsPresent("-keypairs", ref value))
                                            pattern = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        DateTime? installed = null;

                                        if (options.IsPresent("-installed", ref value))
                                            installed = (DateTime)value.Value;

                                        bool forceNetwork = false;

                                        if (options.IsPresent("-forcenetwork"))
                                            forceNetwork = true;

                                        bool strictNetwork = false;

                                        if (options.IsPresent("-strictnetwork"))
                                            strictNetwork = true;

                                        bool viaHttp = CertificateTimeState.ShouldQueryViaHttp();

                                        if (options.IsPresent("-viahttp", ref value))
                                            viaHttp = (bool)value.Value;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        ICertificate certificate = null;

                                        code = CommandOps.GetObject(
                                            interpreter, arguments[argumentIndex],
                                            ref certificate, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            Assembly assembly = AssemblyOps.GetObject();
                                            IEnumerable<IKeyPair> keyPairs = null;

#if CERTIFICATE_POLICY
                                            code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                assembly, AssemblyOps.GetName(), pattern,
                                                false, interpreter, EntityType.None, true,
                                                true, true, true, false, ref keyPairs,
                                                ref result);
#endif

                                            if (code == ReturnCode.Ok)
                                            {
                                                IKeyPair keyPair = null;

                                                if ((argumentIndex + 2) == arguments.Count)
                                                {
                                                    code = CertificateKeyPairOps.GetOne( /* OK */
                                                        keyRingName, policyType, matchKeyRingName,
                                                        assembly, AssemblyOps.GetName(), interpreter,
                                                        arguments[argumentIndex + 1], true, true,
                                                        ref keyPair, ref result);
                                                }

                                                if (code == ReturnCode.Ok)
                                                {
                                                    NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                                                        policyType);

                                                    if (forceNetwork)
                                                        networkFlags |= NetworkFlags.Force;

                                                    if (strictNetwork)
                                                        networkFlags |= NetworkFlags.Strict;

                                                    if (viaHttp)
                                                        networkFlags |= NetworkFlags.ViaHttp;
                                                    else
                                                        networkFlags &= ~NetworkFlags.ViaHttp;

                                                    code = SharedOps.IsExpired(
                                                        interpreter, assembly, this.Plugin,
                                                        certificate, keyPairs, keyPair,
                                                        interpreter.CultureInfo, installed,
                                                        timeout, policyType, networkFlags,
                                                        ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? certificate ?keyPair?\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? certificate ?keyPair?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "export":
                        {
                            if (arguments.Count >= 4)
                            {
#if XML && SERIALIZATION
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-validate", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-novalidate", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-usestream", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
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
                                        bool validate = false;

                                        if (options.IsPresent("-validate"))
                                            validate = true;

                                        if (options.IsPresent("-novalidate"))
                                            validate = false;

                                        bool useStream = false;

                                        if (options.IsPresent("-usestream"))
                                            useStream = true;

                                        IVariant value = null;
                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        ICertificate certificate = null;

                                        code = CommandOps.GetObject(
                                            interpreter, arguments[argumentIndex],
                                            ref certificate, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            //
                                            // NOTE: If no encoding was specified, use the
                                            //       typical default for XML, which is UTF8.
                                            //
                                            if (encoding == null)
                                                encoding = DataOps.GetDefaultEncoding();

                                            //
                                            // NOTE: If the "useStream" option is enabled, we assume
                                            //       that the "fileName" is actually an opaque object
                                            //       handle that refers to a Stream.
                                            //
                                            if (useStream)
                                            {
                                                Stream stream = null;

                                                code = CommandOps.GetStream(
                                                    interpreter, arguments[argumentIndex + 1],
                                                    ref stream, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    code = CertificateXmlOps.Export(
                                                        stream, encoding, certificate,
                                                        validate, ref result);
                                                }
                                            }
                                            else
                                            {
                                                code = CertificateXmlOps.Export(
                                                    arguments[argumentIndex + 1],
                                                    encoding, certificate, validate,
                                                    ref result);
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
                                                "wrong # args: should be \"{0} {1} ?options? certificate fileName\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate fileName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "extract":
                        {
                            if (arguments.Count >= 3)
                            {
#if XML && SERIALIZATION
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-noremote", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-anyresourcekey", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-validate", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-novalidate", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-textvar", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
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

                                        bool validate = false;

                                        if (options.IsPresent("-validate"))
                                            validate = true;

                                        if (options.IsPresent("-novalidate"))
                                            validate = false;

                                        bool allowRemoteUri = true; /* TODO: Good default? */

                                        if (options.IsPresent("-noremote"))
                                            allowRemoteUri = false;

                                        bool anyResourcePublicKey = false; /* TODO: Good default? */

                                        if (options.IsPresent("-anyresourcekey"))
                                            anyResourcePublicKey = true;

                                        IVariant value = null;
                                        string textVarName = null;

                                        if (options.IsPresent("-textvar", ref value))
                                            textVarName = value.ToString();

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        string text;
                                        bool useResource = false;

                                        text = SharedOps.GetDataFromFile(
                                            interpreter, null, arguments[argumentIndex],
                                            timeout, allowRemoteUri, anyResourcePublicKey,
                                            false, ref useResource, ref result) as string;

                                        if (text != null)
                                        {
                                            ICertificate certificate = null;

                                            code = CertificateXmlOps.Extract(
                                                arguments[argumentIndex], validate, ref text,
                                                ref certificate, ref result);

                                            if ((code == ReturnCode.Ok) && (textVarName != null))
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                code = interpreter.SetVariableValue(
                                                    VariableFlags.None, textVarName, text, null, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, CommandOps.GetBinder(interpreter,
                                                        this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags | CommandOps.GetExtraObjectFlags(
                                                        interpreter, true), options,
                                                    Utility.GetInvokeOptions(objectOptionType),
                                                    objectOptionType, objectName, interpName,
                                                    certificate, true, true, alias, aliasReference,
                                                    false, ref result);
                                            }
                                        }
                                        else
                                        {
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
                                                "wrong # args: should be \"{0} {1} ?options? fileName\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? fileName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "flags":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(FlagType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-flagtype",
                                        new Variant(FlagType.Default)),
                                    new Option(null, OptionFlags.MustHaveWideIntegerValue,
                                        Index.Invalid, Index.Invalid, "-key", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hasflags", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-nothasflags", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-hasall", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-nothasall", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-strict", null),
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
                                            FlagType flagType = FlagType.Default;

                                            if (options.IsPresent("-flagtype", ref value))
                                                flagType = (FlagType)value.Value;

                                            if (flagType == FlagType.Default)
                                                flagType = FlagType.Feature;

                                            long key = Utility.DefaultAttributeFlagsKey();

                                            if (options.IsPresent("-key", ref value))
                                                key = (long)value.Value;

                                            string hasFlags = null;

                                            if (options.IsPresent("-hasflags", ref value))
                                                hasFlags = value.ToString();

                                            string notHasFlags = null;

                                            if (options.IsPresent("-nothasflags", ref value))
                                                notHasFlags = value.ToString();

                                            bool hasAll = false;

                                            if (options.IsPresent("-hasall"))
                                                hasAll = true;

                                            bool notHasAll = false;

                                            if (options.IsPresent("-nothasall"))
                                                notHasAll = true;

                                            bool strict = false;

                                            if (options.IsPresent("-strict"))
                                                strict = true;

                                            ICertificate certificate = null;

                                            code = CommandOps.GetObject(
                                                interpreter, arguments[argumentIndex],
                                                ref certificate, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                code = SharedOps.MatchFlags(
                                                    certificate, flagType, key, hasFlags,
                                                    notHasFlags, hasAll, notHasAll, strict,
                                                    ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "formattimestamp":
                        {
                            if ((arguments.Count == 3) || (arguments.Count == 4))
                            {
                                IObject dateTimeObject = null;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                code = interpreter.GetObject(
                                    arguments[2], LookupFlags.Default,
                                    ref dateTimeObject, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((dateTimeObject != null) && (dateTimeObject.Value is DateTime))
                                    {
                                        DateTime dateTime = (DateTime)dateTimeObject.Value;
                                        bool never = false;

                                        if (arguments.Count == 4)
                                        {
                                            code = Value.GetBoolean2(arguments[3], ValueFlags.AnyBoolean,
                                                interpreter.CultureInfo, ref never, ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                            result = DataOps.FormatTimeStamp(dateTime, never);
                                    }
                                    else
                                    {
                                        result = "invalid date/time object";
                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} dateTime ?never?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "hash":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-hexadecimal", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
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
                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool hexadecimal = false;

                                            if (options.IsPresent("-hexadecimal"))
                                                hexadecimal = true;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            ICertificate certificate = null;

                                            code = CommandOps.GetObject(
                                                interpreter, arguments[argumentIndex],
                                                ref certificate, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();

                                                byte[] hashBytes = null;

                                                code = SharedOps.Hash(
                                                    SharedOps.GetHashAlgorithm(
                                                        hashAlgorithmName, null, certificate,
                                                        HashAlgorithmType.CommandUse),
                                                    null, certificate, certificateHashFlags,
                                                    encoding, ref hashBytes, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    if (hexadecimal)
                                                        result = DataOps.FormatHexadecimal(hashBytes);
                                                    else
                                                        result = new ByteList(hashBytes);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "hashfile":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-hexadecimal", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
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
                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool hexadecimal = false;

                                            if (options.IsPresent("-hexadecimal"))
                                                hexadecimal = true;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

                                            if (code == ReturnCode.Ok)
                                            {
                                                ICertificate certificate = null;

                                                if (!String.IsNullOrEmpty(arguments[argumentIndex]))
                                                {
                                                    code = CommandOps.GetObject(
                                                        interpreter, arguments[argumentIndex],
                                                        ref certificate, ref result);
                                                }

                                                if (code == ReturnCode.Ok)
                                                {
                                                    string fileName = arguments[argumentIndex + 1];

                                                    /*
                                                    //
                                                    // NOTE: If no encoding was specified, use the
                                                    //       typical default for XML, which is UTF8.
                                                    //
                                                    if (encoding == null)
                                                        encoding = DataOps.GetDefaultEncoding();
                                                    */

                                                    byte[] hashBytes = null;

                                                    code = SharedOps.HashFile(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, null, certificate,
                                                            HashAlgorithmType.CommandUse),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, fileName, timeout, ref hashBytes,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        if (hexadecimal)
                                                            result = DataOps.FormatHexadecimal(hashBytes);
                                                        else
                                                            result = new ByteList(hashBytes);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate fileName\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate fileName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "hashstring":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-hexadecimal", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
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
                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool hexadecimal = false;

                                            if (options.IsPresent("-hexadecimal"))
                                                hexadecimal = true;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            if (code == ReturnCode.Ok)
                                            {
                                                ICertificate certificate = null;

                                                if (!String.IsNullOrEmpty(arguments[argumentIndex]))
                                                {
                                                    code = CommandOps.GetObject(
                                                        interpreter, arguments[argumentIndex],
                                                        ref certificate, ref result);
                                                }

                                                if (code == ReturnCode.Ok)
                                                {
                                                    /*
                                                    //
                                                    // NOTE: If no encoding was specified, use the
                                                    //       typical default for XML, which is UTF8.
                                                    //
                                                    if (encoding == null)
                                                        encoding = DataOps.GetDefaultEncoding();
                                                    */

                                                    byte[] hashBytes = null;

                                                    code = SharedOps.HashString(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, null, certificate,
                                                            HashAlgorithmType.CommandUse),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, arguments[argumentIndex + 1],
                                                        ref hashBytes, ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        if (hexadecimal)
                                                            result = DataOps.FormatHexadecimal(hashBytes);
                                                        else
                                                            result = new ByteList(hashBytes);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate string\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate string\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "import":
                        {
                            if (arguments.Count >= 3)
                            {
#if XML && SERIALIZATION
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-noremote", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-trace", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-validate", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-novalidate", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-usestream", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-anyresourcekey", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-thisassembly", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-encrypted", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null)
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

                                        IVariant value = null;
                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        bool allowRemoteUri = true; /* TODO: Good default? */

                                        if (options.IsPresent("-noremote"))
                                            allowRemoteUri = false;

                                        bool traceOnError = false;

                                        if (options.IsPresent("-trace"))
                                            traceOnError = true;

                                        bool validate = false;

                                        if (options.IsPresent("-validate"))
                                            validate = true;

                                        if (options.IsPresent("-novalidate"))
                                            validate = false;

                                        bool useStream = false;

                                        if (options.IsPresent("-usestream"))
                                            useStream = true;

                                        bool anyResourcePublicKey = false; /* TODO: Good default? */

                                        if (options.IsPresent("-anyresourcekey"))
                                            anyResourcePublicKey = true;

                                        bool isForThisAssembly = false;

                                        if (options.IsPresent("-thisassembly"))
                                            isForThisAssembly = true;

                                        bool encrypted = false;

                                        if (options.IsPresent("-encrypted"))
                                            encrypted = true;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        ICertificate certificate = null;

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
                                                code = CertificateXmlOps.Import(
                                                    stream, validate, ref certificate,
                                                    ref result);
                                            }
                                        }
                                        else if (encrypted)
                                        {
                                            if (encoding == null)
                                                encoding = Constants.DefaultEncoding;

                                            code = CertificateVerifyOps.Import(
                                                interpreter, this.Plugin, encoding,
                                                null, arguments[argumentIndex],
                                                interpreter.CultureInfo, timeout,
                                                traceOnError, allowRemoteUri,
                                                anyResourcePublicKey, isForThisAssembly,
                                                validate, ref certificate, ref result);
                                        }
                                        else
                                        {
                                            code = CertificateXmlOps.Import(
                                                arguments[argumentIndex], anyResourcePublicKey,
                                                isForThisAssembly, validate, ref certificate,
                                                ref result);
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                            ObjectOptionType objectOptionType =
                                                Utility.GetOptionType(aliasRaw, aliasAll);

                                            code = Utility.FixupReturnValue(
                                                interpreter, CommandOps.GetBinder(interpreter,
                                                    this.Plugin), interpreter.CultureInfo, null,
                                                objectFlags | CommandOps.GetExtraObjectFlags(
                                                    interpreter, true), options,
                                                Utility.GetInvokeOptions(objectOptionType),
                                                objectOptionType, objectName, interpName,
                                                certificate, true, true, alias, aliasReference,
                                                false, ref result);
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
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
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
                    case "keyname":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-local", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-unset", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-script", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-file", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-stream", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-license", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keypair", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-trace", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-other", null),
                                    Option.CreateEndOfOptions()
                                });

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
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        IVariant value = null;
                                        bool local = false;

                                        if (options.IsPresent("-local", ref value))
                                            local = (bool)value.Value;

                                        bool unset = false;

                                        if (options.IsPresent("-unset", ref value))
                                            unset = (bool)value.Value;

                                        string scriptKeyName = null;

                                        if (options.IsPresent("-script", ref value))
                                            scriptKeyName = value.ToString();

                                        if (scriptKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.Script);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.Script,
                                                        scriptKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.Script);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.Script, scriptKeyName);
                                                }
                                            }
                                        }

                                        string fileKeyName = null;

                                        if (options.IsPresent("-file", ref value))
                                            fileKeyName = value.ToString();

                                        if (fileKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.File);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.File,
                                                        fileKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.File);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.File, fileKeyName);
                                                }
                                            }
                                        }

                                        string streamKeyName = null;

                                        if (options.IsPresent("-stream", ref value))
                                            streamKeyName = value.ToString();

                                        if (streamKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.Stream);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.Stream,
                                                        streamKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.Stream);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.Stream, streamKeyName);
                                                }
                                            }
                                        }

                                        string licenseKeyName = null;

                                        if (options.IsPresent("-license", ref value))
                                            licenseKeyName = value.ToString();

                                        if (licenseKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.License);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.License,
                                                        licenseKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.License);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.License, licenseKeyName);
                                                }
                                            }
                                        }

                                        string keyPairKeyName = null;

                                        if (options.IsPresent("-keypair", ref value))
                                            keyPairKeyName = value.ToString();

                                        if (keyPairKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.KeyPair);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.KeyPair,
                                                        keyPairKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.KeyPair);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.KeyPair, keyPairKeyName);
                                                }
                                            }
                                        }

                                        string traceKeyName = null;

                                        if (options.IsPresent("-trace", ref value))
                                            traceKeyName = value.ToString();

                                        if (traceKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.Trace);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.Trace,
                                                        traceKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.Trace);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.Trace, traceKeyName);
                                                }
                                            }
                                        }

                                        string otherKeyName = null;

                                        if (options.IsPresent("-other", ref value))
                                            otherKeyName = value.ToString();

                                        if (otherKeyName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        this.Plugin, PolicyType.Other);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        this.Plugin, PolicyType.Other,
                                                        otherKeyName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyName(
                                                        PolicyType.Other);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyName(
                                                        PolicyType.Other, otherKeyName);
                                                }
                                            }
                                        }

                                        if (local)
                                        {
                                            string currentScriptKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.Script,
                                                ref currentScriptKeyName);

                                            string currentFileKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.File,
                                                ref currentFileKeyName);

                                            string currentStreamKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.Stream,
                                                ref currentStreamKeyName);

                                            string currentLicenseKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.License,
                                                ref currentLicenseKeyName);

                                            string currentKeyPairKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.KeyPair,
                                                ref currentKeyPairKeyName);

                                            string currentTraceKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.Trace,
                                                ref currentTraceKeyName);

                                            string currentOtherKeyName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyName(
                                                this.Plugin, PolicyType.Other,
                                                ref currentOtherKeyName);

                                            result = StringList.MakeList(
                                                "-script", currentScriptKeyName,
                                                "-file", currentFileKeyName,
                                                "-stream", currentStreamKeyName,
                                                "-license", currentLicenseKeyName,
                                                "-keypair", currentKeyPairKeyName,
                                                "-trace", currentTraceKeyName,
                                                "-other", currentOtherKeyName);
                                        }
                                        else
                                        {
                                            result = StringList.MakeList(
                                                "-script", CertificatePolicyOps.GetKeyName(PolicyType.Script),
                                                "-file", CertificatePolicyOps.GetKeyName(PolicyType.File),
                                                "-stream", CertificatePolicyOps.GetKeyName(PolicyType.Stream),
                                                "-license", CertificatePolicyOps.GetKeyName(PolicyType.License),
                                                "-keypair", CertificatePolicyOps.GetKeyName(PolicyType.KeyPair),
                                                "-trace", CertificatePolicyOps.GetKeyName(PolicyType.Trace),
                                                "-other", CertificatePolicyOps.GetKeyName(PolicyType.Other));
                                        }
                                    }
                                    else
                                    {
                                        result = String.Format(
                                            "wrong # args: should be \"{0} {1} ?options?\"",
                                            this.Name, subCommand);

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
                    case "keyringname":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-local", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-unset", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-script", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-file", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-stream", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-license", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-keypair", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-trace", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-other", null),
                                    Option.CreateEndOfOptions()
                                });

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
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        IVariant value = null;
                                        bool local = false;

                                        if (options.IsPresent("-local", ref value))
                                            local = (bool)value.Value;

                                        bool unset = false;

                                        if (options.IsPresent("-unset", ref value))
                                            unset = (bool)value.Value;

                                        string scriptKeyRingName = null;

                                        if (options.IsPresent("-script", ref value))
                                            scriptKeyRingName = value.ToString();

                                        if (scriptKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.Script);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.Script,
                                                        scriptKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.Script);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.Script, scriptKeyRingName);
                                                }
                                            }
                                        }

                                        string fileKeyRingName = null;

                                        if (options.IsPresent("-file", ref value))
                                            fileKeyRingName = value.ToString();

                                        if (fileKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.File);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.File,
                                                        fileKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.File);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.File, fileKeyRingName);
                                                }
                                            }
                                        }

                                        string streamKeyRingName = null;

                                        if (options.IsPresent("-stream", ref value))
                                            streamKeyRingName = value.ToString();

                                        if (streamKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.Stream);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.Stream,
                                                        streamKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.Stream);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.Stream, streamKeyRingName);
                                                }
                                            }
                                        }

                                        string licenseKeyRingName = null;

                                        if (options.IsPresent("-license", ref value))
                                            licenseKeyRingName = value.ToString();

                                        if (licenseKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.License);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.License,
                                                        licenseKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.License);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.License, licenseKeyRingName);
                                                }
                                            }
                                        }

                                        string keyPairKeyRingName = null;

                                        if (options.IsPresent("-keypair", ref value))
                                            keyPairKeyRingName = value.ToString();

                                        if (keyPairKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.KeyPair);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.KeyPair,
                                                        keyPairKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.KeyPair);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.KeyPair, keyPairKeyRingName);
                                                }
                                            }
                                        }

                                        string traceKeyRingName = null;

                                        if (options.IsPresent("-trace", ref value))
                                            traceKeyRingName = value.ToString();

                                        if (traceKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.Trace);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.Trace,
                                                        traceKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.Trace);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.Trace, traceKeyRingName);
                                                }
                                            }
                                        }

                                        string otherKeyRingName = null;

                                        if (options.IsPresent("-other", ref value))
                                            otherKeyRingName = value.ToString();

                                        if (otherKeyRingName != null)
                                        {
                                            if (local)
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        this.Plugin, PolicyType.Other);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        this.Plugin, PolicyType.Other,
                                                        otherKeyRingName);
                                                }
                                            }
                                            else
                                            {
                                                if (unset)
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.UnsetKeyRingName(
                                                        PolicyType.Other);
                                                }
                                                else
                                                {
                                                    /* IGNORED */
                                                    CertificatePolicyOps.SetKeyRingName(
                                                        PolicyType.Other, otherKeyRingName);
                                                }
                                            }
                                        }

                                        if (local)
                                        {
                                            string currentScriptKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.Script,
                                                ref currentScriptKeyRingName);

                                            string currentFileKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.File,
                                                ref currentFileKeyRingName);

                                            string currentStreamKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.Stream,
                                                ref currentStreamKeyRingName);

                                            string currentLicenseKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.License,
                                                ref currentLicenseKeyRingName);

                                            string currentKeyPairKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.KeyPair,
                                                ref currentKeyPairKeyRingName);

                                            string currentTraceKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.Trace,
                                                ref currentTraceKeyRingName);

                                            string currentOtherKeyRingName = null;

                                            /* IGNORED */
                                            CertificatePolicyOps.GetKeyRingName(
                                                this.Plugin, PolicyType.Other,
                                                ref currentOtherKeyRingName);

                                            result = StringList.MakeList(
                                                "-script", currentScriptKeyRingName,
                                                "-file", currentFileKeyRingName,
                                                "-stream", currentStreamKeyRingName,
                                                "-license", currentLicenseKeyRingName,
                                                "-keypair", currentKeyPairKeyRingName,
                                                "-trace", currentTraceKeyRingName,
                                                "-other", currentOtherKeyRingName);
                                        }
                                        else
                                        {
                                            result = StringList.MakeList(
                                                "-script", CertificatePolicyOps.GetKeyRingName(PolicyType.Script),
                                                "-file", CertificatePolicyOps.GetKeyRingName(PolicyType.File),
                                                "-stream", CertificatePolicyOps.GetKeyRingName(PolicyType.Stream),
                                                "-license", CertificatePolicyOps.GetKeyRingName(PolicyType.License),
                                                "-keypair", CertificatePolicyOps.GetKeyRingName(PolicyType.KeyPair),
                                                "-trace", CertificatePolicyOps.GetKeyRingName(PolicyType.Trace),
                                                "-other", CertificatePolicyOps.GetKeyRingName(PolicyType.Other));
                                        }
                                    }
                                    else
                                    {
                                        result = String.Format(
                                            "wrong # args: should be \"{0} {1} ?options?\"",
                                            this.Name, subCommand);

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
                    case "loadandverify":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                IOption policyTypeOption = new Option(
                                    typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                    Index.Invalid, Index.Invalid, "-policytype", null);

                                IOption policyOption = new Option(
                                    typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue,
                                    Index.Invalid, Index.Invalid, "-policy", null);
#else
                                IOption policyTypeOption = new Option(
                                    typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                    OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                    "-policytype", null);

                                IOption policyOption = new Option(
                                    typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue |
                                    OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                    "-policy", null);
#endif

                                int argumentIndex = Index.Invalid; /* IGNORED */

                                if (arguments.Count > 2)
                                {
                                    policyOption.Flags |= OptionFlags.Ignored;

                                    OptionDictionary preOptions = new OptionDictionary(
                                        new IOption[] {
                                        policyTypeOption,
                                        policyOption,
                                        Option.CreateEndOfOptions()
                                    });

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.CheckOptions(
                                        preOptions, arguments, 0, 2, Index.Invalid,
                                        ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    IVariant value = null;
                                    PolicyType? policyType = null;

                                    if (policyTypeOption.IsPresent(null, ref value))
                                        policyType = (PolicyType)value.Value;

                                    ExecutionPolicy? policy = null;

#if CERTIFICATE_POLICY
                                    if (policyType != null)
                                    {
                                        policy = CertificatePolicyOps.GetPolicy(
                                            this.Plugin, (PolicyType)policyType);
                                    }
#endif

                                    //
                                    // HACK: The "-policytype" option has now been processed;
                                    //       therefore, permit it to be present (because it
                                    //       will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    policyTypeOption.Flags |= OptionFlags.Ignored;

                                    //
                                    // HACK: The "-policy" option has not been processed;
                                    //       it must be processed now, based on the execution
                                    //       policy configured for the selected policy type.
                                    //
                                    policyOption.Flags &= ~OptionFlags.Ignored;

                                    policyOption.Value = (policy != null) ?
                                        new Variant((ExecutionPolicy)policy) : null;

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        policyTypeOption,
                                        policyOption,
#if CERTIFICATE_POLICY
                                        new Option(null, OptionFlags.None, Index.Invalid,
                                            Index.Invalid, "-matchkeyringname", null),
#else
                                        new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                            Index.Invalid, "-matchkeyringname", null),
#endif
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-hashalgorithm", null),
                                        new Option(null, OptionFlags.MustHaveEncodingValue, Index.Invalid,
                                            Index.Invalid, "-encoding", null),
                                        new Option(null, OptionFlags.MustHavePluginValue, Index.Invalid,
                                            Index.Invalid, "-plugin", null),
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-filename", null),
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-keyname", null),
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-keyringname", null), /* EXEMPT: RenewCallback */
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-features", null),
                                        new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                            Index.Invalid, "-restrictions", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-force", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-embedded", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-validate", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-useplugin", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-useassembly", null),
                                        new Option(null, OptionFlags.MustHaveDictionaryValue,
                                            Index.Invalid, Index.Invalid, "-scriptclientdata", null),
                                        new Option(null, OptionFlags.MustHaveIntegerValue,
                                            Index.Invalid, Index.Invalid, "-timeout", null),
                                        Option.CreateEndOfOptions()
                                    }, Utility.GetFixupReturnValueOptions().Values);

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

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            if (options.IsPresent("-policy", ref value))
                                                policy = (ExecutionPolicy)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool usePlugin = true; /* TODO: Good default? */

                                            if (options.IsPresent("-useplugin", ref value))
                                                usePlugin = (bool)value.Value;

                                            bool useAssembly = true; /* TODO: Good default? */

                                            if (options.IsPresent("-useassembly", ref value))
                                                useAssembly = (bool)value.Value;

                                            bool force = Constants.CertificateForce;

                                            if (options.IsPresent("-force", ref value))
                                                force = (bool)value.Value;

                                            bool embedded = Constants.CertificateEmbedded;

                                            if (options.IsPresent("-embedded", ref value))
                                                embedded = (bool)value.Value;

                                            bool validate = Constants.CertificateValidate;

                                            if (options.IsPresent("-validate", ref value))
                                                validate = (bool)value.Value;

                                            IPlugin plugin = null;

                                            if (options.IsPresent("-plugin", ref value))
                                                plugin = (IPlugin)value.Value;

                                            string fileName = null;

                                            if (options.IsPresent("-filename", ref value))
                                                fileName = value.ToString();

                                            string keyName = null;

                                            if (options.IsPresent("-keyname", ref value))
                                                keyName = value.ToString();

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string features = null;

                                            if (options.IsPresent("-features", ref value))
                                                features = value.ToString();

                                            string restrictions = null;

                                            if (options.IsPresent("-restrictions", ref value))
                                                restrictions = value.ToString();

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            StringDictionary dictionary = null;

                                            if (options.IsPresent("-scriptclientdata", ref value))
                                                dictionary = (StringDictionary)value.Value;

                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

                                            IKeyPair keyPair = null;

                                            if (argumentIndex != Index.Invalid)
                                            {
                                                code = CertificateKeyPairOps.GetOne( /* OK */
                                                    keyRingName, (policyType != null) ?
                                                        (PolicyType)policyType :
                                                        Constants.DefaultCertificateVerifyCommandPolicyType,
                                                    matchKeyRingName,
                                                    AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                    interpreter, arguments[argumentIndex],
                                                    true, true, ref keyPair, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                /*
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();
                                                */

                                                if (usePlugin && (plugin == null))
                                                    plugin = this.Plugin;

                                                ElementSelectionCallback fileNameCallback =
                                                    CertificatePluginOps.GetFileNameCallback(
                                                        plugin, false);

                                                RenewCallback renewCallback = null;

#if NETWORK && CERTIFICATE_RENEWAL
                                                renewCallback = CertificateRenewalOps.GetRenewCallback(
                                                    plugin, false);
#endif

                                                Assembly assembly;

                                                if ((plugin != null) &&
                                                    !SharedOps.IsCrossAppDomain(interpreter, plugin))
                                                {
                                                    assembly = plugin.Assembly;
                                                }
                                                else if (useAssembly)
                                                {
                                                    assembly = AssemblyOps.GetObject();
                                                }
                                                else
                                                {
                                                    assembly = null;
                                                }

                                                AssemblyName assemblyName;

                                                if (plugin != null)
                                                    assemblyName = plugin.AssemblyName;
                                                else if (useAssembly)
                                                    assemblyName = AssemblyOps.GetName();
                                                else
                                                    assemblyName = null;

                                                IAnyClientData loadClientData = null;

                                                try
                                                {
                                                    loadClientData = (dictionary != null) ?
                                                        new ScriptClientData(dictionary, clientData, false) :
                                                        new AnyClientData(clientData, false);

                                                    ICertificate certificate = null;

                                                    code = CertificateVerifyOps.LoadAndProcess(
                                                        interpreter, assembly, assemblyName, plugin,
                                                        hashAlgorithmName, null, encoding,
                                                        (keyPair != null) ?
                                                            new IKeyPair[] { keyPair } : null,
                                                        features, restrictions, policy, keyName,
                                                        keyRingName, timeout, force, embedded,
                                                        validate, fileNameCallback, renewCallback,
                                                        loadClientData, ref fileName, ref certificate,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        CertificateIsolatedOps.MaybeFixupResult(
                                                            interpreter, this.Plugin, result);

                                                        ObjectOptionType objectOptionType =
                                                            Utility.GetOptionType(aliasRaw, aliasAll);

                                                        code = Utility.FixupReturnValue(
                                                            interpreter, CommandOps.GetBinder(interpreter,
                                                                this.Plugin), interpreter.CultureInfo, null,
                                                            objectFlags | CommandOps.GetExtraObjectFlags(
                                                                interpreter, true), options,
                                                            Utility.GetInvokeOptions(objectOptionType),
                                                            objectOptionType, objectName, interpName,
                                                            certificate, true, true, alias, aliasReference,
                                                            false, ref result);
                                                    }
                                                }
                                                finally
                                                {
                                                    Utility.DisposeOrComplain<IAnyClientData>(
                                                        interpreter, ref loadClientData);
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
                                                    "wrong # args: should be \"{0} {1} ?options? ?keyPair?\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? ?keyPair?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "manager":
                        {
                            if (arguments.Count == 2)
                            {
                                ILicensePluginManagerData licensePluginManagerData =
                                    SharedOps.GetLicensePluginManagerData(
                                        this.Plugin);

                                if (licensePluginManagerData != null)
                                {
                                    result = (licensePluginManagerData.LicenseManager != null) ?
                                        "custom" : "default";
                                }
                                else
                                {
                                    result = "none";
                                }

                                code = ReturnCode.Ok;
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
                            if ((arguments.Count == 2) || (arguments.Count == 4) || (arguments.Count == 5))
                            {
                                if ((arguments.Count == 4) || (arguments.Count == 5))
                                {
                                    ICertificate certificate = null;

                                    code = CommandOps.GetObject(
                                        interpreter, arguments[2], ref certificate,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        Type metadataType = typeof(ICertificate);
                                        string propertyName = arguments[3];

                                        if (arguments.Count == 5)
                                        {
                                            object propertyValue = arguments[4].Value;

                                            code = CommandOps.GetMetadataValue(
                                                interpreter, metadataType, certificate,
                                                propertyName, interpreter.CultureInfo,
                                                ref propertyValue, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                try
                                                {
                                                    CommandOps.SetMetadataPropertyValue(
                                                        metadataType, propertyName, certificate,
                                                        propertyValue);
                                                }
                                                catch (Exception e)
                                                {
                                                    result = e;
                                                    code = ReturnCode.Error;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                object returnValue = CommandOps.GetMetadataPropertyValue(
                                                    metadataType, propertyName, certificate);

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
                                        StringList list = CommandOps.GetMetadataPropertyNames(
                                            typeof(ICertificate));

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
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\" -OR- \"{0} {1} certificate propertyName\" -OR- \"{0} {1} certificate propertyName propertyValue\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "networkflags":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                IOption localOption = new Option(
                                    null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                    Index.Invalid, "-local", null);

                                int argumentIndex = Index.Invalid; /* IGNORED */

                                if (arguments.Count > 2)
                                {
                                    OptionDictionary preOptions = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        Option.CreateEndOfOptions()
                                    });

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.CheckOptions(
                                        preOptions, arguments, 0, 2, Index.Invalid,
                                        ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    IVariant value = null;
                                    bool local = false;

                                    if (localOption.IsPresent(null, ref value))
                                        local = (bool)value.Value;

                                    //
                                    // HACK: The "-local" option has now been processed;
                                    //       therefore, permit it to be present (because
                                    //       it will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    localOption.Flags |= OptionFlags.Ignored;

                                    NetworkFlags currentScriptFlags = NetworkFlags.None;
                                    NetworkFlags currentFileFlags = NetworkFlags.None;
                                    NetworkFlags currentStreamFlags = NetworkFlags.None;
                                    NetworkFlags currentLicenseFlags = NetworkFlags.None;
                                    NetworkFlags currentKeyPairFlags = NetworkFlags.None;
                                    NetworkFlags currentTraceFlags = NetworkFlags.None;
                                    NetworkFlags currentOtherFlags = NetworkFlags.None;

                                    if (local)
                                    {
                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.Script,
                                            ref currentScriptFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.File,
                                            ref currentFileFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.Stream,
                                            ref currentStreamFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.License,
                                            ref currentLicenseFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.KeyPair,
                                            ref currentKeyPairFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.Trace,
                                            ref currentTraceFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetNetworkFlags(
                                            this.Plugin, PolicyType.Other,
                                            ref currentOtherFlags);
                                    }
                                    else
                                    {
                                        currentScriptFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.Script);

                                        currentFileFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.File);

                                        currentStreamFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.Stream);

                                        currentLicenseFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.License);

                                        currentKeyPairFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.KeyPair);

                                        currentTraceFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.Trace);

                                        currentOtherFlags =
                                            CertificatePolicyOps.GetNetworkFlags(
                                                PolicyType.Other);
                                    }

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-unset", null),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-script",
                                            new Variant(currentScriptFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-file",
                                            new Variant(currentFileFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-stream",
                                            new Variant(currentStreamFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-license",
                                            new Variant(currentLicenseFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-keypair",
                                            new Variant(currentKeyPairFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-trace",
                                            new Variant(currentTraceFlags)),
                                        new Option(typeof(NetworkFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-other",
                                            new Variant(currentOtherFlags)),
                                        Option.CreateEndOfOptions()
                                    });

                                    argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            bool unset = false;

                                            if (options.IsPresent("-unset", ref value))
                                                unset = (bool)value.Value;

                                            NetworkFlags? scriptFlags = null;

                                            if (options.IsPresent("-script", ref value))
                                                scriptFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? fileFlags = null;

                                            if (options.IsPresent("-file", ref value))
                                                fileFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? streamFlags = null;

                                            if (options.IsPresent("-stream", ref value))
                                                streamFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? licenseFlags = null;

                                            if (options.IsPresent("-license", ref value))
                                                licenseFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? keyPairFlags = null;

                                            if (options.IsPresent("-keypair", ref value))
                                                keyPairFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? traceFlags = null;

                                            if (options.IsPresent("-trace", ref value))
                                                traceFlags = (NetworkFlags)value.Value;

                                            NetworkFlags? otherFlags = null;

                                            if (options.IsPresent("-other", ref value))
                                                otherFlags = (NetworkFlags)value.Value;

                                            if (scriptFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.Script,
                                                            (NetworkFlags)scriptFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.Script,
                                                            (NetworkFlags)scriptFlags);
                                                    }
                                                }
                                            }

                                            if (fileFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.File,
                                                            (NetworkFlags)fileFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.File,
                                                            (NetworkFlags)fileFlags);
                                                    }
                                                }
                                            }

                                            if (streamFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.Stream,
                                                            (NetworkFlags)streamFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.Stream,
                                                            (NetworkFlags)streamFlags);
                                                    }
                                                }
                                            }

                                            if (licenseFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.License,
                                                            (NetworkFlags)licenseFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.License,
                                                            (NetworkFlags)licenseFlags);
                                                    }
                                                }
                                            }

                                            if (keyPairFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.KeyPair,
                                                            (NetworkFlags)keyPairFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.KeyPair,
                                                            (NetworkFlags)keyPairFlags);
                                                    }
                                                }
                                            }

                                            if (traceFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.Trace,
                                                            (NetworkFlags)traceFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.Trace,
                                                            (NetworkFlags)traceFlags);
                                                    }
                                                }
                                            }

                                            if (otherFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetNetworkFlags(
                                                            this.Plugin, PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            this.Plugin, PolicyType.Other,
                                                            (NetworkFlags)otherFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetNetworkFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetNetworkFlags(
                                                            PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetNetworkFlags(
                                                            PolicyType.Other,
                                                            (NetworkFlags)otherFlags);
                                                    }
                                                }
                                            }

                                            if (local)
                                            {
                                                currentScriptFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.Script,
                                                    ref currentScriptFlags);

                                                currentFileFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.File,
                                                    ref currentFileFlags);

                                                currentStreamFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.Stream,
                                                    ref currentStreamFlags);

                                                currentLicenseFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.License,
                                                    ref currentLicenseFlags);

                                                currentKeyPairFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.KeyPair,
                                                    ref currentKeyPairFlags);

                                                currentTraceFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.Trace,
                                                    ref currentTraceFlags);

                                                currentOtherFlags = NetworkFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetNetworkFlags(
                                                    this.Plugin, PolicyType.Other,
                                                    ref currentOtherFlags);

                                                //
                                                // NOTE: The result is *only* the LOCAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", currentScriptFlags,
                                                    "-file", currentFileFlags,
                                                    "-stream", currentStreamFlags,
                                                    "-license", currentLicenseFlags,
                                                    "-keypair", currentKeyPairFlags,
                                                    "-trace", currentTraceFlags,
                                                    "-other", currentOtherFlags);
                                            }
                                            else
                                            {
                                                //
                                                // NOTE: The result is *only* the GLOBAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", CertificatePolicyOps.GetNetworkFlags(PolicyType.Script),
                                                    "-file", CertificatePolicyOps.GetNetworkFlags(PolicyType.File),
                                                    "-stream", CertificatePolicyOps.GetNetworkFlags(PolicyType.Stream),
                                                    "-license", CertificatePolicyOps.GetNetworkFlags(PolicyType.License),
                                                    "-keypair", CertificatePolicyOps.GetNetworkFlags(PolicyType.KeyPair),
                                                    "-trace", CertificatePolicyOps.GetNetworkFlags(PolicyType.Trace),
                                                    "-other", CertificatePolicyOps.GetNetworkFlags(PolicyType.Other));
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options?\"",
                                                this.Name, subCommand);

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
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "networktime":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 3))
                            {
#if NETWORK
                                DateTime now = Utility.GetUtcNow();

                                if (arguments.Count == 3)
                                {
                                    bool enable = false;

                                    code = Value.GetBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref enable,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (enable)
                                            NetworkState.ResetCheckedRecently();
                                        else
                                            NetworkState.SetCheckedRecently(now);
                                    }
                                }

                                if (code == ReturnCode.Ok)
                                    result = NetworkState.WasCheckedRecently(now, false);
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} ?enable?\"",
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
                    case "pathflags":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                IOption localOption = new Option(
                                    null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                    Index.Invalid, "-local", null);

                                int argumentIndex = Index.Invalid; /* IGNORED */

                                if (arguments.Count > 2)
                                {
                                    OptionDictionary preOptions = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        Option.CreateEndOfOptions()
                                    });

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.CheckOptions(
                                        preOptions, arguments, 0, 2, Index.Invalid,
                                        ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    IVariant value = null;
                                    bool local = false;

                                    if (localOption.IsPresent(null, ref value))
                                        local = (bool)value.Value;

                                    //
                                    // HACK: The "-local" option has now been processed;
                                    //       therefore, permit it to be present (because
                                    //       it will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    localOption.Flags |= OptionFlags.Ignored;

                                    PathFlags currentScriptFlags = PathFlags.None;
                                    PathFlags currentFileFlags = PathFlags.None;
                                    PathFlags currentStreamFlags = PathFlags.None;
                                    PathFlags currentLicenseFlags = PathFlags.None;
                                    PathFlags currentKeyPairFlags = PathFlags.None;
                                    PathFlags currentTraceFlags = PathFlags.None;
                                    PathFlags currentOtherFlags = PathFlags.None;

                                    if (local)
                                    {
                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.Script,
                                            ref currentScriptFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.File,
                                            ref currentFileFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.Stream,
                                            ref currentStreamFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.License,
                                            ref currentLicenseFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.KeyPair,
                                            ref currentKeyPairFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.Trace,
                                            ref currentTraceFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPathFlags(
                                            this.Plugin, PolicyType.Other,
                                            ref currentOtherFlags);
                                    }
                                    else
                                    {
                                        currentScriptFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.Script);

                                        currentFileFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.File);

                                        currentStreamFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.Stream);

                                        currentLicenseFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.License);

                                        currentKeyPairFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.KeyPair);

                                        currentTraceFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.Trace);

                                        currentOtherFlags =
                                            CertificatePolicyOps.GetPathFlags(
                                                PolicyType.Other);
                                    }

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-unset", null),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-script",
                                            new Variant(currentScriptFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-file",
                                            new Variant(currentFileFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-stream",
                                            new Variant(currentStreamFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-license",
                                            new Variant(currentLicenseFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-keypair",
                                            new Variant(currentKeyPairFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-trace",
                                            new Variant(currentTraceFlags)),
                                        new Option(typeof(PathFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-other",
                                            new Variant(currentOtherFlags)),
                                        Option.CreateEndOfOptions()
                                    });

                                    argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            bool unset = false;

                                            if (options.IsPresent("-unset", ref value))
                                                unset = (bool)value.Value;

                                            PathFlags? scriptFlags = null;

                                            if (options.IsPresent("-script", ref value))
                                                scriptFlags = (PathFlags)value.Value;

                                            PathFlags? fileFlags = null;

                                            if (options.IsPresent("-file", ref value))
                                                fileFlags = (PathFlags)value.Value;

                                            PathFlags? streamFlags = null;

                                            if (options.IsPresent("-stream", ref value))
                                                streamFlags = (PathFlags)value.Value;

                                            PathFlags? licenseFlags = null;

                                            if (options.IsPresent("-license", ref value))
                                                licenseFlags = (PathFlags)value.Value;

                                            PathFlags? keyPairFlags = null;

                                            if (options.IsPresent("-keypair", ref value))
                                                keyPairFlags = (PathFlags)value.Value;

                                            PathFlags? traceFlags = null;

                                            if (options.IsPresent("-trace", ref value))
                                                traceFlags = (PathFlags)value.Value;

                                            PathFlags? otherFlags = null;

                                            if (options.IsPresent("-other", ref value))
                                                otherFlags = (PathFlags)value.Value;

                                            if (scriptFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.Script,
                                                            (PathFlags)scriptFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.Script,
                                                            (PathFlags)scriptFlags);
                                                    }
                                                }
                                            }

                                            if (fileFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.File,
                                                            (PathFlags)fileFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.File,
                                                            (PathFlags)fileFlags);
                                                    }
                                                }
                                            }

                                            if (streamFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.Stream,
                                                            (PathFlags)streamFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.Stream,
                                                            (PathFlags)streamFlags);
                                                    }
                                                }
                                            }

                                            if (licenseFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.License,
                                                            (PathFlags)licenseFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.License,
                                                            (PathFlags)licenseFlags);
                                                    }
                                                }
                                            }

                                            if (keyPairFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.KeyPair,
                                                            (PathFlags)keyPairFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.KeyPair,
                                                            (PathFlags)keyPairFlags);
                                                    }
                                                }
                                            }

                                            if (traceFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.Trace,
                                                            (PathFlags)traceFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.Trace,
                                                            (PathFlags)traceFlags);
                                                    }
                                                }
                                            }

                                            if (otherFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPathFlags(
                                                            this.Plugin, PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            this.Plugin, PolicyType.Other,
                                                            (PathFlags)otherFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetPathFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetPathFlags(
                                                            PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPathFlags(
                                                            PolicyType.Other,
                                                            (PathFlags)otherFlags);
                                                    }
                                                }
                                            }

                                            if (local)
                                            {
                                                currentScriptFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.Script,
                                                    ref currentScriptFlags);

                                                currentFileFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.File,
                                                    ref currentFileFlags);

                                                currentStreamFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.Stream,
                                                    ref currentStreamFlags);

                                                currentLicenseFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.License,
                                                    ref currentLicenseFlags);

                                                currentKeyPairFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.KeyPair,
                                                    ref currentKeyPairFlags);

                                                currentTraceFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.Trace,
                                                    ref currentTraceFlags);

                                                currentOtherFlags = PathFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPathFlags(
                                                    this.Plugin, PolicyType.Other,
                                                    ref currentOtherFlags);

                                                //
                                                // NOTE: The result is *only* the LOCAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", currentScriptFlags,
                                                    "-file", currentFileFlags,
                                                    "-stream", currentStreamFlags,
                                                    "-license", currentLicenseFlags,
                                                    "-keypair", currentKeyPairFlags,
                                                    "-trace", currentTraceFlags,
                                                    "-other", currentOtherFlags);
                                            }
                                            else
                                            {
                                                //
                                                // NOTE: The result is *only* the GLOBAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", CertificatePolicyOps.GetPathFlags(PolicyType.Script),
                                                    "-file", CertificatePolicyOps.GetPathFlags(PolicyType.File),
                                                    "-stream", CertificatePolicyOps.GetPathFlags(PolicyType.Stream),
                                                    "-license", CertificatePolicyOps.GetPathFlags(PolicyType.License),
                                                    "-keypair", CertificatePolicyOps.GetPathFlags(PolicyType.KeyPair),
                                                    "-trace", CertificatePolicyOps.GetPathFlags(PolicyType.Trace),
                                                    "-other", CertificatePolicyOps.GetPathFlags(PolicyType.Other));
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options?\"",
                                                this.Name, subCommand);

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
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "policy":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
#if !ENTERPRISE_LOCKDOWN
                                IOption enabledOption = new Option(
                                    null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                    Index.Invalid, "-enabled", null);

                                IOption localOption = new Option(
                                    null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                    Index.Invalid, "-local", null);

                                int argumentIndex = Index.Invalid; /* IGNORED */

                                if (arguments.Count > 2)
                                {
                                    OptionDictionary preOptions = new OptionDictionary(
                                        new IOption[] {
                                        enabledOption,
                                        localOption,
                                        Option.CreateEndOfOptions()
                                    });

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.CheckOptions(
                                        preOptions, arguments, 0, 2, Index.Invalid,
                                        ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    IVariant value = null;
                                    bool? enabled = null;

                                    if (enabledOption.IsPresent(null, ref value))
                                        enabled = (bool)value.Value;

                                    bool local = false;

                                    if (localOption.IsPresent(null, ref value))
                                        local = (bool)value.Value;

                                    //
                                    // HACK: The "-enabled" option has now been processed;
                                    //       therefore, permit it to be present (because
                                    //       it will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    enabledOption.Flags |= OptionFlags.Ignored;

                                    //
                                    // HACK: The "-local" option has now been processed;
                                    //       therefore, permit it to be present (because
                                    //       it will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    localOption.Flags |= OptionFlags.Ignored;

                                    //
                                    // NOTE: Obtain the current policy settings.  Then, possibly
                                    //       modify them based on the "-enabled" option.  These
                                    //       will be the base values for the other flags options
                                    //       (i.e. "-script", "-file", "-stream", "-license",
                                    //       "-keypair", "-trace", and "-other") processed below.
                                    //
                                    ExecutionPolicy currentScriptPolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentFilePolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentStreamPolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentLicensePolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentKeyPairPolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentTracePolicy = ExecutionPolicy.Undefined;
                                    ExecutionPolicy currentOtherPolicy = ExecutionPolicy.Undefined;

                                    if (local)
                                    {
                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.Script,
                                            ref currentScriptPolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.File,
                                            ref currentFilePolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.Stream,
                                            ref currentStreamPolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.License,
                                            ref currentLicensePolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.KeyPair,
                                            ref currentKeyPairPolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.Trace,
                                            ref currentTracePolicy);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetPolicy(
                                            this.Plugin, PolicyType.Other,
                                            ref currentOtherPolicy);
                                    }
                                    else
                                    {
                                        currentScriptPolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.Script);

                                        currentFilePolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.File);

                                        currentStreamPolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.Stream);

                                        currentLicensePolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.License);

                                        currentKeyPairPolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.KeyPair);

                                        currentTracePolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.Trace);

                                        currentOtherPolicy =
                                            CertificatePolicyOps.GetPolicy(
                                                PolicyType.Other);
                                    }

                                    if (enabled != null)
                                    {
                                        currentScriptPolicy &= ~ExecutionPolicy.Undefined;
                                        currentFilePolicy &= ~ExecutionPolicy.Undefined;
                                        currentStreamPolicy &= ~ExecutionPolicy.Undefined;
                                        currentLicensePolicy &= ~ExecutionPolicy.Undefined;
                                        currentKeyPairPolicy &= ~ExecutionPolicy.Undefined;
                                        currentTracePolicy &= ~ExecutionPolicy.Undefined;
                                        currentOtherPolicy &= ~ExecutionPolicy.Undefined;

                                        if ((bool)enabled)
                                        {
                                            currentScriptPolicy |=
                                                Constants.SimpleScriptExecutionPolicy;

                                            currentFilePolicy |=
                                                Constants.SimpleFileExecutionPolicy;

                                            currentStreamPolicy |=
                                                Constants.SimpleStreamExecutionPolicy;

                                            currentLicensePolicy |=
                                                Constants.SimpleLicenseExecutionPolicy;

                                            currentKeyPairPolicy |=
                                                Constants.SimpleKeyPairExecutionPolicy;

                                            currentTracePolicy |=
                                                Constants.SimpleTraceExecutionPolicy;

                                            currentOtherPolicy |=
                                                Constants.SimpleOtherExecutionPolicy;
                                        }
                                        else
                                        {
                                            currentScriptPolicy &=
                                                ~Constants.SimpleScriptExecutionPolicy;

                                            currentFilePolicy &=
                                                ~Constants.SimpleFileExecutionPolicy;

                                            currentStreamPolicy &=
                                                ~Constants.SimpleStreamExecutionPolicy;

                                            currentLicensePolicy &=
                                                ~Constants.SimpleLicenseExecutionPolicy;

                                            currentKeyPairPolicy &=
                                                ~Constants.SimpleKeyPairExecutionPolicy;

                                            currentTracePolicy &=
                                                ~Constants.SimpleTraceExecutionPolicy;

                                            currentOtherPolicy &=
                                                ~Constants.SimpleOtherExecutionPolicy;
                                        }
                                    }

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        enabledOption,
                                        localOption,
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-unset", null),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-script",
                                            new Variant(currentScriptPolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-file",
                                            new Variant(currentFilePolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-stream",
                                            new Variant(currentStreamPolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-license",
                                            new Variant(currentLicensePolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-keypair",
                                            new Variant(currentKeyPairPolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-trace",
                                            new Variant(currentTracePolicy)),
                                        new Option(typeof(ExecutionPolicy),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-other",
                                            new Variant(currentOtherPolicy)),
                                        Option.CreateEndOfOptions()
                                    });

                                    argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            bool unset = false;

                                            if (options.IsPresent("-unset", ref value))
                                                unset = (bool)value.Value;

                                            ExecutionPolicy? scriptPolicy = null;

                                            if (options.IsPresent("-script", ref value))
                                                scriptPolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                scriptPolicy = currentScriptPolicy;

                                            ExecutionPolicy? filePolicy = null;

                                            if (options.IsPresent("-file", ref value))
                                                filePolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                filePolicy = currentFilePolicy;

                                            ExecutionPolicy? streamPolicy = null;

                                            if (options.IsPresent("-stream", ref value))
                                                streamPolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                streamPolicy = currentStreamPolicy;

                                            ExecutionPolicy? licensePolicy = null;

                                            if (options.IsPresent("-license", ref value))
                                                licensePolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                licensePolicy = currentLicensePolicy;

                                            ExecutionPolicy? keyPairPolicy = null;

                                            if (options.IsPresent("-keypair", ref value))
                                                keyPairPolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                keyPairPolicy = currentKeyPairPolicy;

                                            ExecutionPolicy? tracePolicy = null;

                                            if (options.IsPresent("-trace", ref value))
                                                tracePolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                tracePolicy = currentTracePolicy;

                                            ExecutionPolicy? otherPolicy = null;

                                            if (options.IsPresent("-other", ref value))
                                                otherPolicy = (ExecutionPolicy)value.Value;
                                            else if (enabled != null)
                                                otherPolicy = currentOtherPolicy;

                                            if (scriptPolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.Script,
                                                            (ExecutionPolicy)scriptPolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.Script,
                                                            (ExecutionPolicy)scriptPolicy);
                                                    }
                                                }
                                            }

                                            if (filePolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.File,
                                                            (ExecutionPolicy)filePolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.File,
                                                            (ExecutionPolicy)filePolicy);
                                                    }
                                                }
                                            }

                                            if (streamPolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.Stream,
                                                            (ExecutionPolicy)streamPolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.Stream,
                                                            (ExecutionPolicy)streamPolicy);
                                                    }
                                                }
                                            }

                                            if (licensePolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.License,
                                                            (ExecutionPolicy)licensePolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.License,
                                                            (ExecutionPolicy)licensePolicy);
                                                    }
                                                }
                                            }

                                            if (keyPairPolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.KeyPair,
                                                            (ExecutionPolicy)keyPairPolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.KeyPair,
                                                            (ExecutionPolicy)keyPairPolicy);
                                                    }
                                                }
                                            }

                                            if (tracePolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.Trace,
                                                            (ExecutionPolicy)tracePolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.Trace,
                                                            (ExecutionPolicy)tracePolicy);
                                                    }
                                                }
                                            }

                                            if (otherPolicy != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            this.Plugin, PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            this.Plugin, PolicyType.Other,
                                                            (ExecutionPolicy)otherPolicy);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetPolicy(
                                                            PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetPolicy(
                                                            PolicyType.Other,
                                                            (ExecutionPolicy)otherPolicy);
                                                    }
                                                }
                                            }

                                            if (local)
                                            {
                                                currentScriptPolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.Script,
                                                    ref currentScriptPolicy);

                                                currentFilePolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.File,
                                                    ref currentFilePolicy);

                                                currentStreamPolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.Stream,
                                                    ref currentStreamPolicy);

                                                currentLicensePolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.License,
                                                    ref currentLicensePolicy);

                                                currentKeyPairPolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.KeyPair,
                                                    ref currentKeyPairPolicy);

                                                currentTracePolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.Trace,
                                                    ref currentTracePolicy);

                                                currentOtherPolicy = ExecutionPolicy.Undefined;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetPolicy(
                                                    this.Plugin, PolicyType.Other,
                                                    ref currentOtherPolicy);

                                                //
                                                // NOTE: The result is *only* the LOCAL policy settings.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", currentScriptPolicy,
                                                    "-file", currentFilePolicy,
                                                    "-stream", currentStreamPolicy,
                                                    "-license", currentLicensePolicy,
                                                    "-keypair", currentKeyPairPolicy,
                                                    "-trace", currentTracePolicy,
                                                    "-other", currentOtherPolicy);
                                            }
                                            else
                                            {
                                                //
                                                // NOTE: The result is *only* the GLOBAL policy settings.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", CertificatePolicyOps.GetPolicy(PolicyType.Script),
                                                    "-file", CertificatePolicyOps.GetPolicy(PolicyType.File),
                                                    "-stream", CertificatePolicyOps.GetPolicy(PolicyType.Stream),
                                                    "-license", CertificatePolicyOps.GetPolicy(PolicyType.License),
                                                    "-keypair", CertificatePolicyOps.GetPolicy(PolicyType.KeyPair),
                                                    "-trace", CertificatePolicyOps.GetPolicy(PolicyType.Trace),
                                                    "-other", CertificatePolicyOps.GetPolicy(PolicyType.Other));
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
#else
                                result = "cannot modify certificate policy: lockdown";
                                code = ReturnCode.Error;
#endif
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
                    case "policytrace":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                PolicyTraceFlags? oldFlags =
                                    CertificateTraceOps.MaybeForceForPolicy(
                                        interpreter, null);

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyTraceFlags),
                                        (oldFlags != null) ?
                                            ((PolicyTraceFlags)oldFlags).ToString() :
                                            null,
                                        arguments[2], interpreter.CultureInfo,
                                        true, true, true, ref result);

                                    if (enumValue is PolicyTraceFlags)
                                    {
                                        result = CertificateTraceOps.MaybeForceForPolicy(
                                            interpreter, (PolicyTraceFlags)enumValue);
                                    }
                                    else
                                    {
                                        code = ReturnCode.Error;
                                    }
                                }
                                else
                                {
                                    result = oldFlags;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?flags?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "renewcallback":
                        {
                            if (arguments.Count >= 2)
                            {
#if NETWORK && CERTIFICATE_POLICY && CERTIFICATE_RENEWAL
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-unset", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-script", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-file", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-stream", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-license", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-keypair", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-trace", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-other", null),
                                    Option.CreateEndOfOptions()
                                });

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
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        IVariant value = null;
                                        bool unset = false;

                                        if (options.IsPresent("-unset", ref value))
                                            unset = (bool)value.Value;

                                        bool? scriptRenewCallback = null;

                                        if (options.IsPresent("-script", ref value))
                                            scriptRenewCallback = (bool)value.Value;

                                        bool? fileRenewCallback = null;

                                        if (options.IsPresent("-file", ref value))
                                            fileRenewCallback = (bool)value.Value;

                                        bool? streamRenewCallback = null;

                                        if (options.IsPresent("-stream", ref value))
                                            streamRenewCallback = (bool)value.Value;

                                        bool? licenseRenewCallback = null;

                                        if (options.IsPresent("-license", ref value))
                                            licenseRenewCallback = (bool)value.Value;

                                        bool? keyPairRenewCallback = null;

                                        if (options.IsPresent("-keypair", ref value))
                                            keyPairRenewCallback = (bool)value.Value;

                                        bool? traceRenewCallback = null;

                                        if (options.IsPresent("-trace", ref value))
                                            traceRenewCallback = (bool)value.Value;

                                        bool? otherRenewCallback = null;

                                        if (options.IsPresent("-other", ref value))
                                            otherRenewCallback = (bool)value.Value;

                                        RenewCallback renewCallback =
                                            CertificateRenewalOps.GetRenewCallback(
                                                this.Plugin, true);

                                        if (scriptRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.Script);
                                            }
                                            else if ((bool)scriptRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Script, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Script, null);
                                            }
                                        }

                                        if (fileRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.File);
                                            }
                                            else if ((bool)fileRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.File, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.File, null);
                                            }
                                        }

                                        if (streamRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.Stream);
                                            }
                                            else if ((bool)streamRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Stream, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Stream, null);
                                            }
                                        }

                                        if (licenseRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.License);
                                            }
                                            else if ((bool)licenseRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.License, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.License, null);
                                            }
                                        }

                                        if (keyPairRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.KeyPair);
                                            }
                                            else if ((bool)keyPairRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.KeyPair, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.KeyPair, null);
                                            }
                                        }

                                        if (traceRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.Trace);
                                            }
                                            else if ((bool)traceRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Trace, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Trace, null);
                                            }
                                        }

                                        if (otherRenewCallback != null)
                                        {
                                            if (unset)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.UnsetRenewCallback(
                                                    PolicyType.Other);
                                            }
                                            else if ((bool)otherRenewCallback)
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Other, renewCallback);
                                            }
                                            else
                                            {
                                                /* IGNORED */
                                                CertificatePolicyOps.SetRenewCallback(
                                                    PolicyType.Other, null);
                                            }
                                        }

                                        //
                                        // NOTE: The result is *only* the GLOBAL policy settings.
                                        //
                                        result = StringList.MakeList(
                                            "-script", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.Script) != null,
                                            "-file", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.File) != null,
                                            "-stream", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.Stream) != null,
                                            "-license", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.License) != null,
                                            "-keypair", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.KeyPair) != null,
                                            "-trace", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.Trace) != null,
                                            "-other", CertificatePolicyOps.GetRenewCallback(
                                                PolicyType.Other) != null);
                                    }
                                    else
                                    {
                                        result = String.Format(
                                            "wrong # args: should be \"{0} {1} ?options?\"",
                                            this.Name, subCommand);

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
                    case "reset":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                ResetFlags resetFlags = ResetFlags.DefaultMask;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(ResetFlags),
                                        resetFlags.ToString(), arguments[2],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is ResetFlags)
                                        resetFlags = (ResetFlags)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }
                                else
                                {
                                    code = ReturnCode.Ok;
                                }

                                bool stopOnError = SharedOps.HasFlags(
                                    resetFlags, ResetFlags.StopOnError, true);

                                StringList list = new StringList();

                                if (code == ReturnCode.Ok)
                                {
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalLicenseCache, true))
                                    {
                                        Result error = null;

                                        if (CertificateLicenseState.ClearCertificates(
                                                ref error))
                                        {
                                            list.AddObject(ResetFlags.GlobalLicenseCache);
                                        }
                                        else if (stopOnError)
                                        {
                                            result = error;
                                            code = ReturnCode.Error;
                                        }
                                    }
                                }

#if CERTIFICATE_POLICY
                                ResultList errors; /* REUSED */

                                if (code == ReturnCode.Ok)
                                {
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.LocalPolicyData, false))
                                    {
                                        errors = null;

                                        if (CertificatePolicyOps.ResetData(
                                                this.Plugin, true, false,
                                                ref errors) == ReturnCode.Ok)
                                        {
                                            list.AddObject(ResetFlags.LocalPolicyData);
                                        }
                                        else if (stopOnError)
                                        {
                                            result = errors;
                                            code = ReturnCode.Error;
                                        }
                                    }
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalPolicyData, false))
                                    {
                                        errors = null;

                                        if (CertificatePolicyOps.ResetData(
                                                false, ref errors) == ReturnCode.Ok)
                                        {
                                            list.AddObject(ResetFlags.GlobalPolicyData);
                                        }
                                        else if (stopOnError)
                                        {
                                            result = errors;
                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
#endif

                                if (code == ReturnCode.Ok)
                                {
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalConfiguration, false))
                                    {
                                        //
                                        // HACK: The "local" flag here makes almost
                                        //       no difference.  When null, policy
                                        //       related state will be skipped.
                                        //       When false, it will check global
                                        //       policy state.  When true it will
                                        //       check the plugin related policy
                                        //       state (i.e. for the plugin that is
                                        //       associated with *this* command).
                                        //
                                        bool? local = null;
                                        bool? @default = null;
                                        int count = 0;

                                        code = ScriptContext.CheckForChanges(
                                            interpreter, this.Plugin, clientData,
                                            interpreter.CultureInfo, false, local,
                                            @default, true, true, true, ref count,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            list.AddObject(ResetFlags.GlobalConfiguration);
                                            list.AddObject(count);
                                        }
                                        else if (!stopOnError)
                                        {
                                            code = ReturnCode.Ok;
                                        }
                                    }
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalDurationData, true))
                                    {
                                        /* NO RESULT */
                                        CertificateTimeState.InitializeDurations(true);

                                        list.AddObject(ResetFlags.GlobalDurationData);
                                    }

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalVersionRangeData, true))
                                    {
                                        /* NO RESULT */
                                        CertificateVersionState.InitializeRanges(true);

                                        list.AddObject(ResetFlags.GlobalVersionRangeData);
                                    }

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.SettingsInterpreter, true))
                                    {
                                        /* NO RESULT */
                                        Utility.ClearInterpreterForSettings();

                                        list.AddObject(ResetFlags.SettingsInterpreter);
                                    }

#if CERTIFICATE_POLICY
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalKeyRingState, true))
                                    {
                                        /* IGNORED */
                                        CertificateKeyRingState.RemoveAllTrusted(
                                            true);

                                        list.AddObject(ResetFlags.GlobalKeyRingState);
                                    }

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.LocalKeyRingState, true))
                                    {
                                        /* IGNORED */
                                        CertificateKeyRingState.RemoveAllTrusted(
                                            interpreter, true);

                                        list.AddObject(ResetFlags.LocalKeyRingState);
                                    }
#endif

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalFileCache, true))
                                    {
                                        /* IGNORED */
                                        CertificateLicenseState.ClearCachedFiles();

                                        list.AddObject(ResetFlags.GlobalFileCache);
                                    }

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.GlobalLicenseState, true))
                                    {
                                        /* NO RESULT */
                                        CertificateLicenseState.ResetFileName();

                                        /* NO RESULT */
                                        CertificateLicenseState.ResetCertificate();

                                        list.AddObject(ResetFlags.GlobalLicenseState);
                                    }

#if CERTIFICATE_POLICY
                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.PluginLicenseState, true))
                                    {
                                        /* NO RESULT */
                                        CertificatePolicyOps.UnsetCertificatesViaPlugin();

                                        list.AddObject(ResetFlags.PluginLicenseState);
                                    }

                                    if (SharedOps.HasFlags(resetFlags,
                                            ResetFlags.PolicyLicenseState, true))
                                    {
                                        /* IGNORED */
                                        CertificatePolicyOps.ResetCertificates();

                                        /* IGNORED */
                                        CertificatePolicyOps.ResetPluginDatas();

                                        list.AddObject(ResetFlags.PolicyLicenseState);
                                    }
#endif

                                    if (code == ReturnCode.Ok)
                                        result = list;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?flags?\"",
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
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
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
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

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

                                        ICertificate certificate = null;

                                        code = CommandOps.GetObject(
                                            interpreter, arguments[argumentIndex],
                                            ref certificate, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            IEnumerable<IKeyPair> keyPairs = null;

                                            code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
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
                                                        encoding = DataOps.GetDefaultEncoding();

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
                                                        interpreter, AssemblyOps.GetObject(),
                                                        this.Plugin, SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, keyPairs, certificate,
                                                            HashAlgorithmType.RemoteUse |
                                                            HashAlgorithmType.CommandUse), null,
                                                        encoding, keyPairs, certificate,
                                                        interpreter.CultureInfo, timeout,
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
                                                "wrong # args: should be \"{0} {1} ?options? certificate\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "scriptflags":
                        {
                            if (arguments.Count >= 2)
                            {
#if CERTIFICATE_POLICY
                                IOption localOption = new Option(
                                    null, OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                    Index.Invalid, "-local", null);

                                int argumentIndex = Index.Invalid; /* IGNORED */

                                if (arguments.Count > 2)
                                {
                                    OptionDictionary preOptions = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        Option.CreateEndOfOptions()
                                    });

                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                    code = interpreter.CheckOptions(
                                        preOptions, arguments, 0, 2, Index.Invalid,
                                        ref argumentIndex, ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    IVariant value = null;
                                    bool local = false;

                                    if (localOption.IsPresent(null, ref value))
                                        local = (bool)value.Value;

                                    //
                                    // HACK: The "-local" option has now been processed;
                                    //       therefore, permit it to be present (because
                                    //       it will __still__ be present in the "arguments"
                                    //       list if it was before) but just ignore it.
                                    //
                                    localOption.Flags |= OptionFlags.Ignored;

                                    ScriptFlags currentScriptFlags = ScriptFlags.None;
                                    ScriptFlags currentFileFlags = ScriptFlags.None;
                                    ScriptFlags currentStreamFlags = ScriptFlags.None;
                                    ScriptFlags currentLicenseFlags = ScriptFlags.None;
                                    ScriptFlags currentKeyPairFlags = ScriptFlags.None;
                                    ScriptFlags currentTraceFlags = ScriptFlags.None;
                                    ScriptFlags currentOtherFlags = ScriptFlags.None;

                                    if (local)
                                    {
                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.Script,
                                            ref currentScriptFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.File,
                                            ref currentFileFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.Stream,
                                            ref currentStreamFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.License,
                                            ref currentLicenseFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.KeyPair,
                                            ref currentKeyPairFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.Trace,
                                            ref currentTraceFlags);

                                        /* IGNORED */
                                        CertificatePolicyOps.GetScriptFlags(
                                            this.Plugin, PolicyType.Other,
                                            ref currentOtherFlags);
                                    }
                                    else
                                    {
                                        currentScriptFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.Script);

                                        currentFileFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.File);

                                        currentStreamFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.Stream);

                                        currentLicenseFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.License);

                                        currentKeyPairFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.KeyPair);

                                        currentTraceFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.Trace);

                                        currentOtherFlags =
                                            CertificatePolicyOps.GetScriptFlags(
                                                PolicyType.Other);
                                    }

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        localOption,
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-unset", null),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-script",
                                            new Variant(currentScriptFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-file",
                                            new Variant(currentFileFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-stream",
                                            new Variant(currentStreamFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-license",
                                            new Variant(currentLicenseFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-keypair",
                                            new Variant(currentKeyPairFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-trace",
                                            new Variant(currentTraceFlags)),
                                        new Option(typeof(ScriptFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-other",
                                            new Variant(currentOtherFlags)),
                                        Option.CreateEndOfOptions()
                                    });

                                    argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                        code = interpreter.GetOptions(
                                            options, arguments, 0, 2, Index.Invalid,
                                            true, ref argumentIndex, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            bool unset = false;

                                            if (options.IsPresent("-unset", ref value))
                                                unset = (bool)value.Value;

                                            ScriptFlags? scriptFlags = null;

                                            if (options.IsPresent("-script", ref value))
                                                scriptFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? fileFlags = null;

                                            if (options.IsPresent("-file", ref value))
                                                fileFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? streamFlags = null;

                                            if (options.IsPresent("-stream", ref value))
                                                streamFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? licenseFlags = null;

                                            if (options.IsPresent("-license", ref value))
                                                licenseFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? keyPairFlags = null;

                                            if (options.IsPresent("-keypair", ref value))
                                                keyPairFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? traceFlags = null;

                                            if (options.IsPresent("-trace", ref value))
                                                traceFlags = (ScriptFlags)value.Value;

                                            ScriptFlags? otherFlags = null;

                                            if (options.IsPresent("-other", ref value))
                                                otherFlags = (ScriptFlags)value.Value;

                                            if (scriptFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.Script,
                                                            (ScriptFlags)scriptFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.Script);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.Script,
                                                            (ScriptFlags)scriptFlags);
                                                    }
                                                }
                                            }

                                            if (fileFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.File,
                                                            (ScriptFlags)fileFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.File);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.File,
                                                            (ScriptFlags)fileFlags);
                                                    }
                                                }
                                            }

                                            if (streamFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.Stream,
                                                            (ScriptFlags)streamFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.Stream);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.Stream,
                                                            (ScriptFlags)streamFlags);
                                                    }
                                                }
                                            }

                                            if (licenseFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.License,
                                                            (ScriptFlags)licenseFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.License);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.License,
                                                            (ScriptFlags)licenseFlags);
                                                    }
                                                }
                                            }

                                            if (keyPairFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.KeyPair,
                                                            (ScriptFlags)keyPairFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.KeyPair);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.KeyPair,
                                                            (ScriptFlags)keyPairFlags);
                                                    }
                                                }
                                            }

                                            if (traceFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.Trace,
                                                            (ScriptFlags)traceFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.Trace);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.Trace,
                                                            (ScriptFlags)traceFlags);
                                                    }
                                                }
                                            }

                                            if (otherFlags != null)
                                            {
                                                if (local)
                                                {
                                                    if (unset)
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.UnsetScriptFlags(
                                                            this.Plugin, PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            this.Plugin, PolicyType.Other,
                                                            (ScriptFlags)otherFlags);
                                                    }
                                                }
                                                else
                                                {
                                                    if (unset)
                                                    {
                                                        //
                                                        // HACK: Do not call UnsetScriptFlags;
                                                        //       this must revert to default,
                                                        //       not none.
                                                        //
                                                        /* IGNORED */
                                                        CertificatePolicyOps.ResetScriptFlags(
                                                            PolicyType.Other);
                                                    }
                                                    else
                                                    {
                                                        /* IGNORED */
                                                        CertificatePolicyOps.SetScriptFlags(
                                                            PolicyType.Other,
                                                            (ScriptFlags)otherFlags);
                                                    }
                                                }
                                            }

                                            if (local)
                                            {
                                                currentScriptFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.Script,
                                                    ref currentScriptFlags);

                                                currentFileFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.File,
                                                    ref currentFileFlags);

                                                currentStreamFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.Stream,
                                                    ref currentStreamFlags);

                                                currentLicenseFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.License,
                                                    ref currentLicenseFlags);

                                                currentKeyPairFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.KeyPair,
                                                    ref currentKeyPairFlags);

                                                currentTraceFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.Trace,
                                                    ref currentTraceFlags);

                                                currentOtherFlags = ScriptFlags.None;

                                                /* IGNORED */
                                                CertificatePolicyOps.GetScriptFlags(
                                                    this.Plugin, PolicyType.Other,
                                                    ref currentOtherFlags);

                                                //
                                                // NOTE: The result is *only* the LOCAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", currentScriptFlags,
                                                    "-file", currentFileFlags,
                                                    "-stream", currentStreamFlags,
                                                    "-license", currentLicenseFlags,
                                                    "-keypair", currentKeyPairFlags,
                                                    "-trace", currentTraceFlags,
                                                    "-other", currentOtherFlags);
                                            }
                                            else
                                            {
                                                //
                                                // NOTE: The result is *only* the GLOBAL script flags.
                                                //
                                                result = StringList.MakeList(
                                                    "-script", CertificatePolicyOps.GetScriptFlags(PolicyType.Script),
                                                    "-file", CertificatePolicyOps.GetScriptFlags(PolicyType.File),
                                                    "-stream", CertificatePolicyOps.GetScriptFlags(PolicyType.Stream),
                                                    "-license", CertificatePolicyOps.GetScriptFlags(PolicyType.License),
                                                    "-keypair", CertificatePolicyOps.GetScriptFlags(PolicyType.KeyPair),
                                                    "-trace", CertificatePolicyOps.GetScriptFlags(PolicyType.Trace),
                                                    "-other", CertificatePolicyOps.GetScriptFlags(PolicyType.Other));
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} ?options?\"",
                                                this.Name, subCommand);

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
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "shell":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
#if SHELL && CERTIFICATE_POLICY
                                ShellFlags shellFlags = ShellFlags.None;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(ShellFlags), null,
                                        arguments[2], interpreter.CultureInfo,
                                        true, true, true, ref result);

                                    if (enumValue is ShellFlags)
                                        shellFlags = (ShellFlags)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }
                                else
                                {
                                    code = ReturnCode.Ok;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    CertificateShellState.MaybeSetFlags(
                                        ref shellFlags);

                                    code = CertificateShellState.ApplyFlags(
                                        interpreter, this.Plugin, shellFlags,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        StringList list = new StringList();

                                        list.Add(Utility.FormatDelegateMethodName(
                                            interpreter.EvaluateScriptCallback,
                                            true, false));

                                        list.Add(Utility.FormatDelegateMethodName(
                                            interpreter.EvaluateFileCallback,
                                            true, false));

                                        list.Add(Utility.FormatDelegateMethodName(
                                            interpreter.EvaluateEncodedFileCallback,
                                            true, false));

                                        list.Add(StringList.MakeList("Flags",
                                            CertificateShellState.GetFlags()));

                                        result = list;
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
                                    "wrong # args: should be \"{0} {1} ?flags?\"",
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
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-setid", null),
                                    new Option(null, OptionFlags.MustHaveGuidValue |
                                        OptionFlags.Nullable, Index.Invalid, Index.Invalid,
                                        "-maybesetid", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-settimestamp", null),
                                    new Option(null, OptionFlags.MustHaveDateTimeValue |
                                        OptionFlags.Nullable, Index.Invalid, Index.Invalid,
                                        "-maybesettimestamp", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-setkey", null),
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
                                            bool setId = false;

                                            if (options.IsPresent("-setid"))
                                                setId = true;

                                            IVariant value = null;
                                            Guid? maybeSetId = null;

                                            if (options.IsPresent("-maybesetid", ref value))
                                                maybeSetId = (Guid?)value.Value;

                                            bool setTimeStamp = false;

                                            if (options.IsPresent("-settimestamp"))
                                                setTimeStamp = true;

                                            DateTime? maybeSetTimeStamp = null;

                                            if (options.IsPresent("-maybesettimestamp", ref value))
                                                maybeSetTimeStamp = (DateTime?)value.Value;

                                            bool setKey = false;

                                            if (options.IsPresent("-setkey"))
                                                setKey = true;

                                            PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            ICertificate certificate = null;
                                            IKeyPair keyPair = null;

                                            code = CommandOps.GetObjectAndKeyPair( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                arguments[argumentIndex + 1], true,
                                                ref certificate, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();

                                                if (setId && (maybeSetId != null))
                                                {
                                                    certificate.Id = (Guid)maybeSetId;
                                                    setId = false;
                                                }

                                                if (setTimeStamp && (maybeSetTimeStamp != null))
                                                {
                                                    certificate.TimeStamp = (DateTime)maybeSetTimeStamp;
                                                    setTimeStamp = false;
                                                }

                                                code = CommandOps.Sign(
                                                    SharedOps.GetHashAlgorithm(
                                                        hashAlgorithmName, new IKeyPair[] { keyPair },
                                                        certificate, HashAlgorithmType.CommandUse),
                                                    null, certificate, certificateHashFlags,
                                                    encoding, keyPair, setId, setTimeStamp,
                                                    setKey, ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "signfile":
                    case "signstring":
                        {
                            bool signFile = DataOps.StringEquals(subCommand, "signfile");

                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-setid", null),
                                    new Option(null, OptionFlags.MustHaveGuidValue |
                                        OptionFlags.Nullable, Index.Invalid, Index.Invalid,
                                        "-maybesetid", null),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-settimestamp", null),
                                    new Option(null, OptionFlags.MustHaveDateTimeValue |
                                        OptionFlags.Nullable, Index.Invalid, Index.Invalid,
                                        "-maybesettimestamp", null),
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-setkey", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
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
                                            ((argumentIndex + 3) == arguments.Count))
                                        {
                                            bool setId = false;

                                            if (options.IsPresent("-setid"))
                                                setId = true;

                                            IVariant value = null;
                                            Guid? maybeSetId = null;

                                            if (options.IsPresent("-maybesetid", ref value))
                                                maybeSetId = (Guid?)value.Value;

                                            bool setTimeStamp = false;

                                            if (options.IsPresent("-settimestamp"))
                                                setTimeStamp = true;

                                            DateTime? maybeSetTimeStamp = null;

                                            if (options.IsPresent("-maybesettimestamp", ref value))
                                                maybeSetTimeStamp = (DateTime?)value.Value;

                                            bool setKey = false;

                                            if (options.IsPresent("-setkey"))
                                                setKey = true;

                                            PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

                                            ICertificate certificate = null;
                                            IKeyPair keyPair = null;

                                            code = CommandOps.GetObjectAndKeyPair( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                arguments[argumentIndex + 1], true,
                                                ref certificate, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: Get the string (or the name of the file)
                                                //       they wish to sign.
                                                //
                                                string localValue = arguments[argumentIndex + 2];

                                                /*
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();
                                                */

                                                if (setId && (maybeSetId != null))
                                                {
                                                    certificate.Id = (Guid)maybeSetId;
                                                    setId = false;
                                                }

                                                if (setTimeStamp && (maybeSetTimeStamp != null))
                                                {
                                                    certificate.TimeStamp = (DateTime)maybeSetTimeStamp;
                                                    setTimeStamp = false;
                                                }

                                                if (signFile)
                                                {
                                                    code = CommandOps.SignFile(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            certificate, HashAlgorithmType.CommandUse),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, keyPair, localValue, timeout,
                                                        setId, setTimeStamp, setKey, ref result);
                                                }
                                                else
                                                {
                                                    code = CommandOps.SignString(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            certificate, HashAlgorithmType.CommandUse),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, keyPair, localValue, setId,
                                                        setTimeStamp, setKey, ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair fileNameOrString\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair fileNameOrString\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "signhash":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null)
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
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

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

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            IObject dataObject = null;

                                            CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                            code = interpreter.GetObject(
                                                arguments[argumentIndex + 1],
                                                LookupFlags.Default, ref dataObject,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                if ((dataObject != null) && (dataObject.Value is byte[]))
                                                {
                                                    byte[] data = (byte[])dataObject.Value;
                                                    byte[] hashBytes = null;

                                                    code = SharedOps.HashBytes(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            null, HashAlgorithmType.CommandUse),
                                                        null, data, ref hashBytes, ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        byte[] signature = null;

                                                        code = CommandOps.SignHash(
                                                            SharedOps.GetHashAlgorithm(
                                                                hashAlgorithmName, new IKeyPair[] { keyPair },
                                                                null, HashAlgorithmType.CommandUse),
                                                            hashBytes, keyPair, ref signature, ref result);

                                                        if (code == ReturnCode.Ok)
                                                        {
                                                            CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                            ObjectOptionType objectOptionType =
                                                                Utility.GetOptionType(aliasRaw, aliasAll);

                                                            code = Utility.FixupReturnValue(
                                                                interpreter, CommandOps.GetBinder(interpreter,
                                                                    this.Plugin), interpreter.CultureInfo, null,
                                                                objectFlags | CommandOps.GetExtraObjectFlags(
                                                                    interpreter, true), options,
                                                                Utility.GetInvokeOptions(objectOptionType),
                                                                objectOptionType, objectName, interpName,
                                                                signature, true, true, alias, aliasReference,
                                                                false, ref result);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    result = "invalid data byte array";
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair data\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? keyPair data\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "simplepolicy":
                        {
                            if (arguments.Count == 2)
                            {
#if CERTIFICATE_POLICY
                                result = StringList.MakeList(
                                    "-script", Constants.SimpleScriptExecutionPolicy,
                                    "-file", Constants.SimpleFileExecutionPolicy,
                                    "-stream", Constants.SimpleStreamExecutionPolicy,
                                    "-license", Constants.SimpleLicenseExecutionPolicy,
                                    "-keypair", Constants.SimpleKeyPairExecutionPolicy,
                                    "-trace", Constants.SimpleTraceExecutionPolicy,
                                    "-other", Constants.SimpleOtherExecutionPolicy);
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
                    case "softwareupdates":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
#if NETWORK
                                bool? wasTrusted; /* REUSED */

                                if (arguments.Count == 3)
                                {
                                    wasTrusted = Utility.IsSoftwareUpdateTrusted();

                                    bool? trusted = null;

                                    code = Value.GetNullableBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref trusted,
                                        ref result);

                                    if ((code == ReturnCode.Ok) && (trusted != null))
                                    {
                                        if (wasTrusted != null)
                                        {
                                            if ((bool)trusted != (bool)wasTrusted)
                                            {
                                                code = Utility.SetSoftwareUpdateTrusted(
                                                    trusted, ref result);
                                            }
                                            else
                                            {
                                                result = String.Format(
                                                    "software update certificate is already {0}",
                                                    (bool)wasTrusted ? "trusted" : "untrusted");

                                                code = ReturnCode.Error;
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "software update certificate status is unknown, " +
                                                "cannot {0}", (bool)trusted ? "enable" : "disable");

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                                else
                                {
                                    code = ReturnCode.Ok;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    wasTrusted = Utility.IsSoftwareUpdateTrusted();

                                    if (wasTrusted != null)
                                    {
                                        result = String.Format(
                                            "software update certificate is {0}",
                                            (bool)wasTrusted ? "trusted" : "untrusted");
                                    }
                                    else
                                    {
                                        result = "software update certificate status is unknown";
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
                                    "wrong # args: should be \"{0} {1} ?trusted?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "source":
                        {
                            if (arguments.Count >= 4)
                            {
                                //
                                // WARNING: Options currently have superfluous "Unsafe" flags on them;
                                //          just in case this gets added to the allowed sub-commands
                                //          for "safe" interpreters at some point.
                                //
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-policy", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policy", null),
                                    new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-usestream", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-assembly", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyusage", null),
                                    new Option(typeof(TrustFlags), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid, "-trustflags",
                                        new Variant(Constants.CommandTrustFlags)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-untrusted", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-useshared", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-useplugin", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-usecontext", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-withuniqueid", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-withcommands", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-removecommands", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-swapcommands", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-withframe", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-noremote", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-noglobal", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-local", null),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-noapply", null),
                                    new Option(null, OptionFlags.MustHaveUnsignedWideIntegerValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid, "-sandboxtoken",
                                        null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid, "-timeout",
                                        null),
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
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        ExecutionPolicy? policy = null;

                                        if (options.IsPresent("-policy", ref value))
                                            policy = (ExecutionPolicy)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string pattern = null;

                                        if (options.IsPresent("-keypairs", ref value))
                                            pattern = value.ToString();

                                        string keyName = null;

                                        if (options.IsPresent("-keyname", ref value))
                                            keyName = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        string assemblyString = null;

                                        if (options.IsPresent("-assembly", ref value))
                                            assemblyString = value.ToString();

                                        bool useStream = false;

                                        if (options.IsPresent("-usestream"))
                                            useStream = true;

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        string keyUsage = null;

                                        if (options.IsPresent("-keyusage", ref value))
                                            keyUsage = value.ToString();

                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        TrustFlags trustFlags = Constants.CommandTrustFlags;

                                        if (options.IsPresent("-trustflags", ref value))
                                            trustFlags = (TrustFlags)value.Value;

                                        bool untrusted = false;

                                        if (options.IsPresent("-untrusted"))
                                            untrusted = true;

                                        bool useShared = false;

                                        if (options.IsPresent("-useshared"))
                                            useShared = true;

                                        bool usePlugin = false;

                                        if (options.IsPresent("-useplugin"))
                                            usePlugin = true;

                                        bool useContext = false;

                                        if (options.IsPresent("-usecontext"))
                                            useContext = true;

                                        bool withUniqueId = false;

                                        if (options.IsPresent("-withuniqueid"))
                                            withUniqueId = true;

                                        bool withCommands = false;

                                        if (options.IsPresent("-withcommands"))
                                            withCommands = true;

                                        bool removeCommands = false;

                                        if (options.IsPresent("-removecommands"))
                                            removeCommands = true;

                                        bool swapCommands = false;

                                        if (options.IsPresent("-swapcommands"))
                                            swapCommands = true;

                                        bool withFrame = false;

                                        if (options.IsPresent("-withframe"))
                                            withFrame = true;

                                        bool allowRemoteUri = true; /* TODO: Good default? */

                                        if (options.IsPresent("-noremote"))
                                            allowRemoteUri = false;

                                        bool noGlobalOnly = false;

                                        if (options.IsPresent("-noglobal"))
                                            noGlobalOnly = true;

                                        bool allowLocalPolicy = false; /* TODO: Good default? */

                                        if (options.IsPresent("-local"))
                                            allowLocalPolicy = true;

                                        bool extractAndApply = true; /* TODO: Good default? */

                                        if (options.IsPresent("-noapply"))
                                            extractAndApply = false;

                                        ulong? sandboxToken = null;

                                        if (options.IsPresent("-sandboxtoken", ref value))
                                            sandboxToken = (ulong)value.Value;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        IEnumerable<IKeyPair> keyPairs = null;

#if CERTIFICATE_POLICY
                                        code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                            pattern, false, interpreter, EntityType.None,
                                            true, true, true, true, false, ref keyPairs,
                                            ref result);
#endif

                                        if (code == ReturnCode.Ok)
                                        {
                                            IKeyPair keyPair = null;

                                            code = CertificateKeyPairOps.GetOne( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                true, true, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                keyPairs = CertificateKeyPairOps.MergeAll(
                                                    interpreter, keyPairs, null, keyPair,
                                                    null, null, null, null, PolicyType.Script,
                                                    null, false, false, false);

                                                string fileName = arguments[argumentIndex + 1];

                                                string signatureFileName = DataOps.FormatSignatureFileName(
                                                    fileName);

                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();

                                                Stream stream = null;
                                                byte[] signature = null;

                                                if (useStream)
                                                {
                                                    Assembly assembly;

                                                    if (assemblyString != null)
                                                    {
                                                        assembly = Utility.FindAssemblyInAppDomain(
                                                            interpreter, null, MatchMode.Glob,
                                                            assemblyString, false, null, ref result);

                                                        if (assembly == null)
                                                        {
                                                            code = ReturnCode.Error;
                                                            goto streamDone;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        assembly = AssemblyOps.GetObject();
                                                    }

                                                    stream = SharedOps.GetStream(
                                                        assembly, fileName, ref result);

                                                    if (stream == null)
                                                    {
                                                        code = ReturnCode.Error;
                                                        goto streamDone;
                                                    }

                                                    if (!DataOps.TryReadSignatureStream(
                                                            assembly, encoding, signatureFileName,
                                                            ref signature, ref result))
                                                    {
                                                        code = ReturnCode.Error;
                                                    }

                                                streamDone:
                                                    ;
                                                }
                                                else
                                                {
                                                    if (!DataOps.TryReadSignatureFile(
                                                            interpreter, encoding, signatureFileName,
                                                            timeout, allowRemoteUri, ref signature,
                                                            ref result))
                                                    {
                                                        code = ReturnCode.Error;
                                                    }
                                                }

                                                if (code == ReturnCode.Ok)
                                                {
                                                    IPlugin plugin = null;
                                                    Type pluginType = null;
                                                    string variantName = null;

                                                    if (usePlugin)
                                                    {
                                                        plugin = this.Plugin;

                                                        if (plugin != null)
                                                            pluginType = plugin.GetType();

                                                        variantName = AssemblyOps.GetConfiguration();
                                                    }

                                                    if (keyUsage == null)
                                                        keyUsage = KeyUsage.Source;
                                                    else if (keyUsage.Length == 0)
                                                        keyUsage = null;

                                                    string directory = Configuration.GetDirectory(
                                                        this.Plugin as IConfiguration);

                                                    using (EvaluateClientData evaluateClientData =
                                                        new EvaluateClientData(
                                                            interpreter.CultureInfo, null, null,
                                                            withUniqueId ?
                                                                DataOps.GetNewId(false) : Guid.Empty,
                                                            null, null, typeof(Certificate).Name,
                                                            new SharedEventWaitHandle(
                                                                false, EventResetMode.ManualReset),
                                                            sandboxToken, new LongList(),
                                                            CertificateScriptOps.GetSettingsFileName,
                                                            null, interpreter, plugin, pluginType,
                                                            null, null, variantName,
                                                            SharedOps.GetHashAlgorithm(
                                                                hashAlgorithmName, keyPairs,
                                                                null, HashAlgorithmType.CommandUse),
                                                            null, encoding, null, null,
                                                            directory, fileName, stream,
                                                            keyPairs, null, keyName, keyRingName,
                                                            null, signature, keyUsage,
                                                            ConfigurationPhase.Demand, trustFlags |
                                                                (useShared ?
                                                                    TrustFlags.Shared :
                                                                    TrustFlags.None) |
                                                                (withFrame ?
                                                                    TrustFlags.WithScopeFrame :
                                                                    TrustFlags.None), policyType,
                                                            policy, timeout, 0, untrusted,
                                                            allowRemoteUri, useContext,
                                                            withCommands, removeCommands,
                                                            swapCommands, noGlobalOnly,
                                                            allowLocalPolicy, extractAndApply,
                                                            false, false))
                                                    {
#if TEST
                                                        IClientData savedClientData = null;

                                                        CertificateScriptOps.BeginClientData(
                                                            interpreter, evaluateClientData,
                                                            ref savedClientData);

                                                        try
                                                        {
#endif
                                                            if (useStream)
                                                            {
                                                                code = CertificateScriptOps.EvaluateStream(
                                                                    evaluateClientData, ref result);
                                                            }
                                                            else
                                                            {
                                                                code = CertificateScriptOps.EvaluateFile(
                                                                    evaluateClientData, ref result);
                                                            }
#if TEST
                                                        }
                                                        finally
                                                        {
                                                            CertificateScriptOps.EndClientData(
                                                                interpreter, ref savedClientData);
                                                        }
#endif
                                                    }
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair fileName\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? keyPair fileName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "subject":
                        {
                            if ((arguments.Count == 4) || (arguments.Count == 5))
                            {
                                ExecutionPolicy? policy = null;

                                if (arguments.Count >= 5)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(ExecutionPolicy), null,
                                        arguments[4], interpreter.CultureInfo, true,
                                        true, true, ref result);

                                    if (enumValue is ExecutionPolicy)
                                        policy = (ExecutionPolicy)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    Assembly assembly = null;

                                    code = CommandOps.GetAssemblyObject(
                                        interpreter, arguments[2], false,
                                        ref assembly, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (assembly == null)
                                            assembly = AssemblyOps.GetObject();

                                        ICertificate certificate = null;

                                        code = CommandOps.GetObject(
                                            interpreter, arguments[3], ref certificate,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            code = SharedOps.MatchSubject(
                                                assembly, certificate, policy, ref result);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} assembly certificate ?policy?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "time":
                        {
                            if (arguments.Count >= 2)
                            {
#if NETWORK
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keypairs", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#endif
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hostnameoraddress", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-retries", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-refresh", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-signed", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-viahttp",
                                        new Variant(
                                            CertificateTimeState.ShouldQueryViaHttp())),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
                                    Option.CreateEndOfOptions()
                                });

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
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                        if (options.IsPresent("-policytype", ref value))
                                            policyType = (PolicyType)value.Value;

                                        bool matchKeyRingName = false;

                                        if (options.IsPresent("-matchkeyringname"))
                                            matchKeyRingName = true;

                                        string pattern = null;

                                        if (options.IsPresent("-keypairs", ref value))
                                            pattern = value.ToString();

                                        string keyRingName = null;

                                        if (options.IsPresent("-keyringname", ref value))
                                            keyRingName = value.ToString();

                                        bool viaHttp = CertificateTimeState.ShouldQueryViaHttp();

                                        if (options.IsPresent("-viahttp", ref value))
                                            viaHttp = (bool)value.Value;

                                        string hostNameOrAddress =
                                            SharedOps.GetTimeHostNameOrAddress(
                                                viaHttp, false);

                                        if (options.IsPresent("-hostnameoraddress", ref value))
                                            hostNameOrAddress = value.ToString();

                                        int? retries = null;

                                        if (options.IsPresent("-retries", ref value))
                                            retries = (int)value.Value;

                                        bool refresh = false;

                                        if (options.IsPresent("-refresh"))
                                            refresh = true;

                                        bool signed = false;

                                        if (options.IsPresent("-signed"))
                                            signed = true;

                                        int? timeout = SharedOps.GetTimeout(interpreter, null);

                                        if (options.IsPresent("-timeout", ref value))
                                            timeout = (int)value.Value;

                                        IEnumerable<IKeyPair> keyPairs = null;

#if CERTIFICATE_POLICY
                                        code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                            pattern, false, interpreter, EntityType.None,
                                            true, true, true, true, false, ref keyPairs,
                                            ref result);
#endif

                                        if (code == ReturnCode.Ok)
                                        {
                                            IKeyPair keyPair = null;

                                            if (argumentIndex != Index.Invalid)
                                            {
                                                code = CertificateKeyPairOps.GetOne( /* OK */
                                                    keyRingName, policyType, matchKeyRingName,
                                                    AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                    interpreter, arguments[argumentIndex],
                                                    true, true, ref keyPair, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                keyPairs = CertificateKeyPairOps.MergeAll(
                                                    interpreter, keyPairs, null, keyPair,
                                                    null, null, null, null, PolicyType.Unknown,
                                                    null, false, false, false);

                                                DateTime dateTime = DateTime.MinValue;

                                                code = CertificateNetworkOps.TryQueryTime(
                                                    interpreter, hostNameOrAddress,
                                                    keyPairs, interpreter.CultureInfo,
                                                    DataOps.GetTimeStamp(), timeout,
                                                    retries, viaHttp, refresh, true,
                                                    signed, ref dateTime, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    result = StringList.MakeList(
                                                        dateTime.Kind, dateTime.Ticks);
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
                                                "wrong # args: should be \"{0} {1} ?options? ?keyPair?\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? ?keyPair?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "trace":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(TracePriority),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-priority",
                                        new Variant(interpreter.GetTracePriority())),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-category", null),
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
                                        string category = typeof(_Certificate).Name;

                                        if (options.IsPresent("-category", ref value))
                                            category = value.ToString();

                                        TracePriority priority = interpreter.GetTracePriority();

                                        if (options.IsPresent("-priority", ref value))
                                            priority = (TracePriority)value.Value;

                                        CertificateTraceOps.DebugTrace(
                                            arguments[argumentIndex], category, priority);

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
                                                "wrong # args: should be \"{0} {1} ?options? message\"",
                                                this.Name, subCommand);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? message\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "unsetpolicy":
                        {
                            if (arguments.Count == 2)
                            {
#if CERTIFICATE_POLICY
                                ReturnCode localCode; /* REUSED */
                                ResultList localErrors = null;

                                localCode = CertificatePolicyOps.UnsetData(
                                    this.Plugin, true, false, ref localErrors);

                                StringList list = new StringList();

                                list.Add("local");
                                list.Add(localCode.ToString());
                                list.Add(localErrors);

                                if (localCode != ReturnCode.Ok)
                                    code = ReturnCode.Error;

                                localCode = CertificatePolicyOps.UnsetData(
                                    false, ref localErrors);

                                list.Add("global");
                                list.Add(localCode.ToString());
                                list.Add(localErrors);

                                if (localCode != ReturnCode.Ok)
                                    code = ReturnCode.Error;

                                result = list;
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
                    case "verify":
                        {
                            if (arguments.Count >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateVerifyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateVerifyCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-matchpublickeytoken", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue |
                                        OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                                        "-checkrevocation", null),
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
                                            PolicyType policyType = Constants.DefaultCertificateVerifyCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool matchPublicKeyToken = true; /* TODO: Good default? */

                                            if (options.IsPresent("-matchpublickeytoken", ref value))
                                                matchPublicKeyToken = (bool)value.Value;

                                            bool checkRevocation = true; /* TODO: Good default? */

                                            if (options.IsPresent("-checkrevocation", ref value))
                                                checkRevocation = (bool)value.Value;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            ICertificate certificate = null;
                                            IKeyPair keyPair = null;

                                            code = CommandOps.GetObjectAndKeyPair( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                arguments[argumentIndex + 1], true,
                                                ref certificate, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();

                                                code = CertificateVerifyOps.Process(
                                                    SharedOps.GetHashAlgorithm(
                                                        hashAlgorithmName, new IKeyPair[] { keyPair },
                                                        certificate, HashAlgorithmType.Legacy),
                                                    null, certificate, certificateHashFlags,
                                                    encoding, keyPair, matchPublicKeyToken,
                                                    checkRevocation, ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifyfile":
                    case "verifystring":
                        {
                            bool verifyFile = DataOps.StringEquals(subCommand, "verifyfile");

                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null,
                                        OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                                        Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue | OptionFlags.Unsafe |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashflags", null),
                                    new Option(null,
                                        OptionFlags.MustHaveValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null,
                                        OptionFlags.MustHaveEncodingValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-matchpublickeytoken", null),
                                    new Option(null,
                                        OptionFlags.MustHaveBooleanValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-checkrevocation", null),
                                    new Option(null,
                                        OptionFlags.MustHaveIntegerValue | OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid, "-timeout", null),
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
                                            ((argumentIndex + 3) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool matchPublicKeyToken = true; /* TODO: Good default? */

                                            if (options.IsPresent("-matchpublickeytoken", ref value))
                                                matchPublicKeyToken = (bool)value.Value;

                                            bool checkRevocation = true; /* TODO: Good default? */

                                            if (options.IsPresent("-checkrevocation", ref value))
                                                checkRevocation = (bool)value.Value;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

                                            ICertificate certificate = null;
                                            IKeyPair keyPair = null;

                                            code = CommandOps.GetObjectAndKeyPair( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                arguments[argumentIndex + 1], true,
                                                ref certificate, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: Get the string (or the name of the file)
                                                //       they wish to verify.
                                                //
                                                string localValue = arguments[argumentIndex + 2];

                                                /*
                                                //
                                                // NOTE: If no encoding was specified, use the
                                                //       typical default for XML, which is UTF8.
                                                //
                                                if (encoding == null)
                                                    encoding = DataOps.GetDefaultEncoding();
                                                */

                                                if (verifyFile)
                                                {
                                                    code = SharedOps.VerifyFile(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            certificate, HashAlgorithmType.Legacy),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, keyPair, localValue, timeout,
                                                        matchPublicKeyToken, checkRevocation,
                                                        ref result);
                                                }
                                                else
                                                {
                                                    code = SharedOps.VerifyString(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            certificate, HashAlgorithmType.Legacy),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, keyPair, localValue,
                                                        matchPublicKeyToken, checkRevocation,
                                                        ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair fileNameOrString\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair fileNameOrString\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifyhash":
                        {
                            if (arguments.Count >= 5)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
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
                                        ((argumentIndex + 3) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

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

                                        IKeyPair keyPair = null;

                                        code = CertificateKeyPairOps.GetOne( /* OK */
                                            keyRingName, policyType, matchKeyRingName,
                                            AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                            interpreter, arguments[argumentIndex],
                                            true, true, ref keyPair, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            IObject dataObject = null;

                                            CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                            code = interpreter.GetObject(
                                                arguments[argumentIndex + 1],
                                                LookupFlags.Default, ref dataObject,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                if ((dataObject != null) && (dataObject.Value is byte[]))
                                                {
                                                    byte[] data = (byte[])dataObject.Value;
                                                    IObject signatureObject = null;

                                                    CertificateIsolatedOps.MaybeFixupResult(interpreter, this.Plugin, result);

                                                    code = interpreter.GetObject(
                                                        arguments[argumentIndex + 2],
                                                        LookupFlags.Default, ref signatureObject,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        if ((signatureObject != null) && (signatureObject.Value is byte[]))
                                                        {
                                                            byte[] signature = (byte[])signatureObject.Value;
                                                            byte[] hashBytes = null;

                                                            code = SharedOps.HashBytes(
                                                                SharedOps.GetHashAlgorithm(
                                                                    hashAlgorithmName, new IKeyPair[] { keyPair },
                                                                    null, HashAlgorithmType.CommandUse),
                                                                null, data, ref hashBytes, ref result);

                                                            if (code == ReturnCode.Ok)
                                                            {
                                                                code = SharedOps.VerifyHash(
                                                                    hashBytes, SharedOps.GetHashAlgorithm(
                                                                        hashAlgorithmName, new IKeyPair[] { keyPair },
                                                                        null, HashAlgorithmType.CommandUse),
                                                                    signature, keyPair, ref result);

                                                                if ((code != ReturnCode.Ok) && (result == null))
                                                                    result = "could not verify hash";
                                                            }
                                                        }
                                                        else
                                                        {
                                                            result = "invalid signature byte array";
                                                            code = ReturnCode.Error;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    result = "invalid data byte array";
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
                                                "wrong # args: should be \"{0} {1} ?options? keyPair data signature\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? keyPair data signature\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "verifystream":
                        {
                            if (arguments.Count >= 5)
                            {
                                //
                                // WARNING: Options currently have superfluous "Unsafe" flags on them;
                                //          just in case this gets added to the allowed sub-commands
                                //          for "safe" interpreters at some point.
                                //
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
#if CERTIFICATE_POLICY
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-keyringname", null),
#else
                                    new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-policytype",
                                        new Variant(Constants.DefaultCertificateOtherCommandPolicyType)),
                                    new Option(null, OptionFlags.Unsupported, Index.Invalid,
                                        Index.Invalid, "-matchkeyringname", null),
                                    new Option(null, OptionFlags.MustHaveValue |
                                        OptionFlags.Unsupported, Index.Invalid, Index.Invalid,
                                        "-keyringname", null),
#endif
                                    new Option(typeof(CertificateHashFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-hashflags", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-matchpublickeytoken", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-checkrevocation", null),
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
                                            ((argumentIndex + 3) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            PolicyType policyType = Constants.DefaultCertificateOtherCommandPolicyType;

                                            if (options.IsPresent("-policytype", ref value))
                                                policyType = (PolicyType)value.Value;

                                            bool matchKeyRingName = false;

                                            if (options.IsPresent("-matchkeyringname"))
                                                matchKeyRingName = true;

                                            CertificateHashFlags? certificateHashFlags = null;

                                            if (options.IsPresent("-hashflags", ref value))
                                                certificateHashFlags = (CertificateHashFlags)value.Value;

                                            string keyRingName = null;

                                            if (options.IsPresent("-keyringname", ref value))
                                                keyRingName = value.ToString();

                                            string hashAlgorithmName = null;

                                            if (options.IsPresent("-hashalgorithm", ref value))
                                                hashAlgorithmName = value.ToString();

                                            bool matchPublicKeyToken = true; /* TODO: Good default? */

                                            if (options.IsPresent("-matchpublickeytoken", ref value))
                                                matchPublicKeyToken = (bool)value.Value;

                                            bool checkRevocation = true; /* TODO: Good default? */

                                            if (options.IsPresent("-checkrevocation", ref value))
                                                checkRevocation = (bool)value.Value;

                                            Encoding encoding = null;

                                            if (options.IsPresent("-encoding", ref value))
                                                encoding = (Encoding)value.Value;

                                            ICertificate certificate = null;
                                            IKeyPair keyPair = null;

                                            code = CommandOps.GetObjectAndKeyPair( /* OK */
                                                keyRingName, policyType, matchKeyRingName,
                                                AssemblyOps.GetObject(), AssemblyOps.GetName(),
                                                interpreter, arguments[argumentIndex],
                                                arguments[argumentIndex + 1], true,
                                                ref certificate, ref keyPair, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // NOTE: Get the stream they wish to verify.
                                                //
                                                Stream stream = null;

                                                code = CommandOps.GetStream(
                                                    interpreter, arguments[argumentIndex + 2],
                                                    ref stream, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    /*
                                                    //
                                                    // NOTE: If no encoding was specified, use the
                                                    //       typical default for XML, which is UTF8.
                                                    //
                                                    if (encoding == null)
                                                        encoding = DataOps.GetDefaultEncoding();
                                                    */

                                                    code = SharedOps.VerifyStream(
                                                        SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, new IKeyPair[] { keyPair },
                                                            certificate, HashAlgorithmType.Legacy),
                                                        null, certificate, certificateHashFlags,
                                                        encoding, keyPair, stream,
                                                        matchPublicKeyToken, checkRevocation,
                                                        ref result);
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
                                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair stream\"",
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
                                    "wrong # args: should be \"{0} {1} ?options? certificate keyPair stream\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "warning":
                        {
                            if (arguments.Count >= 2)
                            {
#if XML && SERIALIZATION
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-type", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-filename", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                });

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
                                        string warningType = Constants.LicenseWarningType;

                                        if (options.IsPresent("-type", ref value))
                                            warningType = value.ToString();

                                        string warningFileName = null;

                                        if (options.IsPresent("-filename", ref value))
                                            warningFileName = value.ToString();

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        if (DataOps.StringEquals(
                                                warningType, Constants.LicenseWarningType) ||
                                            DataOps.StringEquals(
                                                warningType, Constants.ScriptWarningType))
                                        {
                                            using (Stream stream = CommandOps.GetWarningStream(
                                                    this.Plugin, warningFileName, false, ref result))
                                            {
                                                if (stream != null)
                                                {
                                                    string certificateFileName;
                                                    Encoding encoding;

                                                    if (argumentIndex != Index.Invalid)
                                                    {
                                                        certificateFileName = arguments[argumentIndex];
                                                        encoding = DataOps.GetDefaultEncoding();
                                                    }
                                                    else
                                                    {
                                                        certificateFileName = null;
                                                        encoding = null;
                                                    }

                                                    byte[] hashValue;
                                                    HashAlgorithmType hashAlgorithmType;
                                                    string baseFileName = null;

                                                    if (DataOps.StringEquals(
                                                            warningType, Constants.ScriptWarningType) &&
                                                        SharedOps.IsSignatureFileName(
                                                            certificateFileName, ref baseFileName))
                                                    {
                                                        if (hashAlgorithmName != null)
                                                            hashAlgorithmType = HashAlgorithmType.CommandUse;
                                                        else
                                                            hashAlgorithmType = HashAlgorithmType.ScriptUse;

                                                        hashAlgorithmName = SharedOps.GetHashAlgorithm(
                                                            hashAlgorithmName, null, null, hashAlgorithmType);

                                                        hashValue = Utility.HashFile(
                                                            hashAlgorithmName, baseFileName, encoding,
                                                            ref result);

                                                        if (hashValue == null)
                                                            code = ReturnCode.Error;
                                                    }
                                                    else
                                                    {
                                                        hashAlgorithmType = HashAlgorithmType.None;
                                                        hashValue = null;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        if (certificateFileName != null)
                                                        {
                                                            code = CertificateXmlOps.AddWarning(
                                                                certificateFileName, stream,
                                                                encoding, warningType,
                                                                hashAlgorithmName, hashValue,
                                                                false, ref result);
                                                        }
                                                        else
                                                        {
                                                            //
                                                            // NOTE: No certificate file name,
                                                            //       just return the entire
                                                            //       contents of the warning
                                                            //       stream.
                                                            //
                                                            using (StreamReader streamReader =
                                                                    new StreamReader(stream))
                                                            {
                                                                result = String.Format(
                                                                    streamReader.ReadToEnd(),
                                                                    warningType, null);

                                                                code = ReturnCode.Ok;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    code = ReturnCode.Error;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "unsupported warning type, must be \"{0}\" or \"{1}\"",
                                                Constants.LicenseWarningType, Constants.ScriptWarningType);

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
                                                "wrong # args: should be \"{0} {1} ?options? ?fileName?\"",
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
