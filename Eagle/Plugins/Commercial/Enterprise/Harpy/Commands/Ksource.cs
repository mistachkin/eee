/*
 * Ksource.cs --
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
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using DataOps = Licensing.Components.Private.CertificateDataOps;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using ScriptOps = Licensing.Components.Private.CertificateScriptOps;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the <c>ksource</c> command, which reads, verifies, and
    /// then evaluates a digitally signed script source file (or embedded
    /// assembly stream) using the licensing certificate infrastructure.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("1ce44a82-3b58-4b27-82d3-cfb1fb09fd3b")]
    [CommandFlags(CommandFlags.Safe
#if ENTERPRISE_LOCKDOWN
        | CommandFlags.NoRename
        | CommandFlags.NoRemove
#endif
    )]
    [ObjectGroup("policyEngine")]
    internal sealed class Ksource : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="Ksource" /> class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure this command.
        /// </param>
        public Ksource(
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
            get { return _Features.Commands.KsourceOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the command, reading and verifying the signature for the
        /// specified script source file (or embedded stream) and then, unless
        /// suppressed, evaluating that script within the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command, including any
        /// options and the file name.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of evaluating the script; upon
        /// failure, receives an appropriate error message.
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
                    "wrong # args: should be \"{0} ?options? fileName\"",
                    this.Name);

                return ReturnCode.Error;
            }

            if (CanExecute(interpreter, ref result) != ReturnCode.Ok)
                return ReturnCode.Error;

            OptionDictionary options = new OptionDictionary(
                new IOption[] {
#if PLUGIN_COMMANDS
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-usestream", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-assembly", null),
                new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-policytype",
                    new Variant(Constants.DefaultKsourceCommandPolicyType)),
                new Option(typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-policy", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-matchkeyringname", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-keyname", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-keyringname", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-hashalgorithm", null),
                new Option(null, OptionFlags.MustHaveEncodingValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-encoding", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-keypairs", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-keyusage", null),
                new Option(typeof(TrustFlags), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-trustflags", new Variant(Constants.CommandTrustFlags)),
                new Option(null, OptionFlags.MustHaveIntegerValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-timeout", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-untrusted", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-useshared", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-useplugin", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-usecontext", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-withuniqueid", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-withcommands", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-removecommands", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-swapcommands", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-withframe", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-noremote", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-useanykey", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-noglobal", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-local", null),
                new Option(null, OptionFlags.Unsafe, Index.Invalid,
                    Index.Invalid, "-noapply", null),
                new Option(null,
                    OptionFlags.MustHaveUnsignedWideIntegerValue |
                    OptionFlags.Unsafe, Index.Invalid, Index.Invalid,
                    "-sandboxtoken", null),
#else
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-usestream", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-assembly", null),
                new Option(typeof(PolicyType), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-policytype",
                    new Variant(Constants.DefaultKsourceCommandPolicyType)),
                new Option(typeof(ExecutionPolicy), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-policy", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-matchkeyringname", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-keyringname", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                new Option(null, OptionFlags.MustHaveEncodingValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-encoding", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-keypairs", null),
                new Option(null, OptionFlags.MustHaveValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-keyusage", null),
                new Option(typeof(TrustFlags), OptionFlags.MustHaveEnumValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-trustflags",
                    new Variant(Constants.DefaultTrustFlags)),
                new Option(null, OptionFlags.MustHaveIntegerValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-timeout", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-untrusted", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-useshared", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-useplugin", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-usecontext", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-withuniqueid", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-withcommands", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-removecommands", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-swapcommands", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-withframe", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-noremote", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-useanykey", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-noglobal", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-local", null),
                new Option(null, OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-noapply", null),
                new Option(null,
                    OptionFlags.MustHaveUnsignedWideIntegerValue |
                    OptionFlags.Unsafe | OptionFlags.Unsupported,
                    Index.Invalid, Index.Invalid, "-sandboxtoken", null),
#endif
                Option.CreateEndOfOptions()
            });

            if (SharedOps.FixupOptions(
                    this.Plugin, options, false, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            int argumentIndex = Index.Invalid;

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
                        "wrong # args: should be \"{0} ?options? fileName\"",
                        this.Name);
                }

                return ReturnCode.Error;
            }

            //
            // HACK: When Harpy is compiled without the "enterprise"
            //       plugin commands, hard-code these option values,
            //       in order to force their defaults to be used.
            //
            string assemblyString = null;
            bool useStream = false;
            ulong? sandboxToken = null;
            string hashAlgorithmName = null;
            string keyName = null;
            string keyRingName = null;
            string keyUsage = null;
            Encoding encoding = null;
            TrustFlags trustFlags = Constants.CommandTrustFlags;
            PolicyType policyType = Constants.DefaultKsourceCommandPolicyType;
            ExecutionPolicy? policy = null;
            int? timeout = SharedOps.GetTimeout(interpreter, null);
            bool untrusted = false; /* TODO: Good default? */
            bool useShared = false; /* TODO: Good default? */
            bool usePlugin = false; /* TODO: Good default? */
            bool useContext = false; /* TODO: Good default? */
            bool withUniqueId = false; /* TODO: Good default? */
            bool withCommands = false; /* TODO: Good default? */
            bool removeCommands = false; /* TODO: Good default? */
            bool swapCommands = false; /* TODO: Good default? */
            bool withFrame = false; /* TODO: Good default? */
            bool allowRemoteUri = true; /* TODO: Good default? */
            bool noGlobalOnly = false; /* TODO: Good default? */
            bool allowLocalPolicy = false; /* TODO: Good default? */
            bool extractAndApply = true; /* TODO: Good default? */

#if PLUGIN_COMMANDS
            IVariant value = null;

            if (options.IsPresent("-policytype", ref value))
                policyType = (PolicyType)value.Value;

            policy = null;

            if (options.IsPresent("-policy", ref value))
                policy = (ExecutionPolicy)value.Value;

            bool matchKeyRingName = false;

            if (options.IsPresent("-matchkeyringname"))
                matchKeyRingName = true;

            if (options.IsPresent("-keyname", ref value))
                keyName = value.ToString();

            if (options.IsPresent("-keyringname", ref value))
                keyRingName = value.ToString();

            string pattern = null;

            if (options.IsPresent("-keypairs", ref value))
                pattern = value.ToString();

            if (options.IsPresent("-hashalgorithm", ref value))
                hashAlgorithmName = value.ToString();

            if (options.IsPresent("-keyusage", ref value))
                keyUsage = value.ToString();

            if (options.IsPresent("-encoding", ref value))
                encoding = (Encoding)value.Value;

            if (options.IsPresent("-trustflags", ref value))
                trustFlags = (TrustFlags)value.Value;

            if (options.IsPresent("-timeout", ref value))
                timeout = (int)value.Value;

            if (options.IsPresent("-untrusted"))
                untrusted = true;

            if (options.IsPresent("-useshared"))
                useShared = true;

            if (options.IsPresent("-useplugin"))
                usePlugin = true;

            if (options.IsPresent("-usecontext"))
                useContext = true;

            if (options.IsPresent("-withuniqueid"))
                withUniqueId = true;

            if (options.IsPresent("-withcommands"))
                withCommands = true;

            if (options.IsPresent("-removecommands"))
                removeCommands = true;

            if (options.IsPresent("-swapcommands"))
                swapCommands = true;

            if (options.IsPresent("-withframe"))
                withFrame = true;

            if (options.IsPresent("-noremote"))
                allowRemoteUri = false;

            bool useAnyKey = false;

            if (options.IsPresent("-useanykey"))
                useAnyKey = true;

            if (options.IsPresent("-noglobal"))
                noGlobalOnly = true;

            if (options.IsPresent("-local"))
                allowLocalPolicy = true;

            if (options.IsPresent("-noapply"))
                extractAndApply = false;

            if (options.IsPresent("-sandboxtoken", ref value))
                sandboxToken = (ulong)value.Value;

            if (options.IsPresent("-usestream"))
                useStream = true;

            if (options.IsPresent("-assembly", ref value))
                assemblyString = value.ToString();
#endif

            IEnumerable<IKeyPair> keyPairs = null;

#if PLUGIN_COMMANDS
            if (useAnyKey || (pattern != null))
            {
                if (CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                        keyRingName, policyType, matchKeyRingName,
                        AssemblyOps.GetObject(), AssemblyOps.GetName(),
                        pattern, false, interpreter, EntityType.None,
                        true, true, true, true, false, ref keyPairs,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }
            else
#endif
            {
                if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                        AssemblyOps.GetObject(), null, false,
                        ref keyPairs, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            string fileName = arguments[argumentIndex];

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
                        return ReturnCode.Error;
                }
                else
                {
                    assembly = AssemblyOps.GetObject();
                }

                stream = SharedOps.GetStream(
                    assembly, fileName, ref result);

                if (stream == null)
                    return ReturnCode.Error;

                if (!DataOps.TryReadSignatureStream(
                        assembly, encoding, signatureFileName,
                        ref signature, ref result))
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                if (!DataOps.TryReadSignatureFile(
                        interpreter, encoding, signatureFileName,
                        timeout, allowRemoteUri, ref signature,
                        ref result))
                {
                    return ReturnCode.Error;
                }
            }

            if (keyUsage == null)
                keyUsage = KeyUsage.Source;
            else if (keyUsage.Length == 0)
                keyUsage = null;

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

            string directory = Configuration.GetDirectory(
                this.Plugin as IConfiguration);

            using (EvaluateClientData evaluateClientData =
                new EvaluateClientData(
                    interpreter.CultureInfo, null, null,
                    withUniqueId ?
                        DataOps.GetNewId(false) : Guid.Empty,
                    null, null, typeof(Ksource).Name,
                    new SharedEventWaitHandle(
                        false, EventResetMode.ManualReset),
                    sandboxToken, new LongList(),
                    ScriptOps.GetSettingsFileName,
                    null, interpreter, plugin, pluginType,
                    null, null, variantName,
                    SharedOps.GetHashAlgorithm(
                        hashAlgorithmName, keyPairs, null,
                        HashAlgorithmType.CommandUse),
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

                ScriptOps.BeginClientData(
                    interpreter, evaluateClientData,
                    ref savedClientData);

                try
                {
#endif
                    if (useStream)
                    {
                        return ScriptOps.EvaluateStream(
                            evaluateClientData, ref result);
                    }
                    else
                    {
                        return ScriptOps.EvaluateFile(
                            evaluateClientData, ref result);
                    }
#if TEST
                }
                finally
                {
                    ScriptOps.EndClientData(
                        interpreter, ref savedClientData);
                }
#endif
            }
        }
        #endregion
    }
}
