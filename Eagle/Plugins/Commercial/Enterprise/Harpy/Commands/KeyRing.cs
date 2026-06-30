/*
 * KeyRing.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "keyring" command ensemble, which exposes sub-commands
    /// for inspecting and managing the trusted key rings used during
    /// licensing certificate verification.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("264f7f51-c70a-4eea-89e1-10b8dc0722d6")]
    [CommandFlags(CommandFlags.Unsafe
#if ENTERPRISE_LOCKDOWN
        | CommandFlags.NoRename
        | CommandFlags.NoRemove
#endif
    )]
    [ObjectGroup("keyManagement")]
    internal sealed class KeyRing : Default
    {
        #region Private Data
        /// <summary>
        /// The <see cref="PolicyType" /> used by default when a sub-command
        /// does not explicitly specify one.
        /// </summary>
        private PolicyType DefaultPolicyType =
            Constants.DefaultKeyRingCommandPolicyType;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="KeyRing" /> command using
        /// the specified command data.
        /// </summary>
        /// <param name="commandData">
        /// The data used to initialize the new command instance.
        /// </param>
        public KeyRing(
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
            get { return _Features.Commands.KeyRingOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "assembly", "bootstrap", "clear", "directory",
            "embedded", "fetch", "isolated", "license", "loaded",
            "merge", "metadata", "options", "policytype", "remove",
            "restore", "save", "script", "share", "usage"
        });

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names supported by this
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
        /// The collection of sub-command names permitted by the active
        /// policy.
        /// </summary>
        private EnsembleDictionary allowedSubCommands =
            new EnsembleDictionary(
                Policies.KeyRing.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names permitted by the
        /// active policy.
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
        /// Executes the command using the specified arguments, dispatching to
        /// the requested sub-command.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command, including its name
        /// and the sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of the command; otherwise,
        /// receives an error message.
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
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    StringList list = null;

                                    code = CertificateKeyPairOps.ListAssemblyPublicKeyTokens(
                                        plugin.AssemblyName, null, false, ref list, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = list;
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
                    case "bootstrap":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                PolicyType policyType = DefaultPolicyType;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        policyType.ToString(), arguments[2],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        policyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    int loaded = 0;

                                    code = CertificateKeyRingOps.BootstrapKeyPairsPublicOnly(
                                        interpreter, this.Plugin, interpreter.CultureInfo,
                                        policyType, ref loaded, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = loaded;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "clear":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                PolicyType policyType = DefaultPolicyType;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        policyType.ToString(), arguments[2],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        policyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    code = CertificateKeyRingOps.ClearKeyPairs(
                                        interpreter, null, policyType, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = String.Empty;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "directory":
                        {
                            if (arguments.Count == 2)
                            {
                                result = CertificateKeyRingOps.GetBootstrapDirectory(this.Plugin);
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
                    case "embedded":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = null;

                                code = CertificateKeyPairOps.ListEmbeddedPublicKeyTokens(
                                    CertificateAssemblyOps.GetObject(), null, false,
                                    ref list, ref result);

                                if (code == ReturnCode.Ok)
                                    result = list;
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
                    case "fetch":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
#if XML && NETWORK && WEB
                                Uri baseUri = Utility.GetAssemblyUri(
                                    CertificateAssemblyOps.GetObject(),
                                    Constants.KeyRingUriName);

                                if (baseUri != null)
                                {
                                    StringDictionary data = null;

                                    if (arguments.Count == 3)
                                    {
                                        string name = arguments[2];

                                        if (String.IsNullOrEmpty(name))
                                            name = null;

                                        if (name != null)
                                        {
                                            data = new StringDictionary();
                                            data.Add("name", name);
                                        }
                                    }

                                    string text = null;

                                    code = Helpers.MakeUriRequest(
                                        interpreter, baseUri, null,
                                        null, data, null, null,
                                        SharedOps.GetTimeout(
                                            interpreter, null),
                                        false, true, ref text,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                        result = text;
                                }
                                else
                                {
                                    result = String.Format(
                                        "assembly URI {0} not available",
                                        Utility.FormatWrapOrNull(
                                            Constants.KeyRingUriName));

                                    code = ReturnCode.Error;
                                }
#else
                                result = "not implemented";
                                code = ReturnCode.Error;
#endif
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?name?\"",
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
                    case "license":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = null;

                                code = CertificateKeyRingOps.KeyPairsToList(
                                    interpreter, null, PolicyType.License, ref list,
                                    ref result);

                                if (code == ReturnCode.Ok)
                                    result = list;
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
                    case "loaded":
                        {
                            if (arguments.Count == 3)
                            {
                                byte[] hashValue = null;

                                code = Utility.GetBytesFromString(
                                    arguments[2], interpreter.CultureInfo,
                                    ref hashValue, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    string fileName = null;

                                    code = CertificateKeyRingState.GetFile(
                                        interpreter, hashValue, ref fileName,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                        result = fileName;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} hashValue\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "merge":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 4))
                            {
                                PolicyType policyType = DefaultPolicyType;

                                if (arguments.Count == 4)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        policyType.ToString(), arguments[3],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        policyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    string path = null;

                                    if (arguments.Count >= 3)
                                        path = arguments[2];

                                    if (String.IsNullOrEmpty(path))
                                        path = CertificateAssemblyOps.GetDirectory();

                                    if (Directory.Exists(path))
                                    {
                                        int loaded = 0;

                                        code = CertificateKeyRingOps.LoadKeyPairsPublicOnlyFrom(
                                            interpreter, null, new string[] { path },
                                            interpreter.CultureInfo,
                                            CertificatePolicyOps.GetPolicy(
                                                this.Plugin, policyType), policyType,
                                            TracePriority.Default, false, true, true,
                                            false, ref loaded, ref result); /* EXEMPT */

                                        if (code == ReturnCode.Ok)
                                            result = loaded;
                                    }
                                    else
                                    {
                                        code = CertificateKeyRingOps.LoadKeyPairsPublicOnly(
                                            interpreter, null, policyType, path,
                                            interpreter.CultureInfo, null, true,
                                            true, ref result); /* EXEMPT */

                                        if (code == ReturnCode.Ok)
                                            result = String.Empty;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?path? ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "metadata":
                        {
                            if ((arguments.Count == 3) || (arguments.Count == 4))
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    string pattern = arguments[2];

                                    if (String.IsNullOrEmpty(pattern))
                                        pattern = null;

                                    PolicyType policyType = DefaultPolicyType;

                                    if (arguments.Count == 4)
                                    {
                                        object enumValue = Utility.TryParseFlagsEnum(
                                            interpreter, typeof(PolicyType),
                                            policyType.ToString(), arguments[3],
                                            interpreter.CultureInfo, true, true,
                                            true, ref result);

                                        if (enumValue is PolicyType)
                                            policyType = (PolicyType)enumValue;
                                        else
                                            code = ReturnCode.Error;
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        IEnumerable<IKeyPair> keyPairs = null;

                                        code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                            null, policyType, true, CertificateAssemblyOps.GetObject(),
                                            plugin.AssemblyName, pattern, false, interpreter,
                                            EntityType.None, true, true, true, true, false,
                                            ref keyPairs, ref result); /* EXEMPT */

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (keyPairs != null)
                                            {
                                                StringList list = null;

                                                foreach (IKeyPair keyPair in keyPairs)
                                                {
                                                    if (keyPair == null)
                                                        continue;

                                                    if (list == null)
                                                        list = new StringList();

                                                    IStringList subList = keyPair.ToList();

                                                    if (subList == null)
                                                        continue;

                                                    list.Add(subList.ToString());
                                                }

                                                result = list;
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
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} pattern ?policyType?\"",
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
                    case "policytype":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 3))
                            {
                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        DefaultPolicyType.ToString(), arguments[2],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        DefaultPolicyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                    result = DefaultPolicyType;
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "remove":
                        {
                            if (arguments.Count == 3)
                            {
                                code = CertificateKeyRingState.RemoveTrusted(
                                    interpreter, arguments[2], ref result);

                                if (code == ReturnCode.Ok)
                                    result = String.Empty;
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} name\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "restore":
                        {
                            if ((arguments.Count == 3) || (arguments.Count == 4))
                            {
                                PolicyType policyType = DefaultPolicyType;

                                if (arguments.Count == 4)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        policyType.ToString(), arguments[3],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        policyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    string keyRingName = CertificateKeyRingOps.GetName(
                                        null, policyType);

                                    code = CertificateKeyRingState.RestoreTrusted(
                                        interpreter, arguments[2], keyRingName, false,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                        result = String.Empty;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} name ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "save":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                PolicyType policyType = DefaultPolicyType;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PolicyType),
                                        policyType.ToString(), arguments[2],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is PolicyType)
                                        policyType = (PolicyType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    string keyRingName = CertificateKeyRingOps.GetName(
                                        null, policyType);

                                    string name = null;

                                    code = CertificateKeyRingState.SaveTrusted(
                                        interpreter, keyRingName, ref name, false,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                        result = name;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "script":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = null;

                                code = CertificateKeyRingOps.KeyPairsToList(
                                    interpreter, null, PolicyType.Script, ref list,
                                    ref result);

                                if (code == ReturnCode.Ok)
                                    result = list;
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
                    case "share":
                        {
                            if ((arguments.Count == 3) || (arguments.Count == 4))
                            {
                                string path = arguments[2];
                                Interpreter childInterpreter = null;
                                string name = null; /* NOT USED */

                                CertificateIsolatedOps.MaybeFixupResult(
                                    interpreter, this.Plugin, result);

                                code = interpreter.GetChildInterpreter(
                                    path, LookupFlags.Interpreter, true, false,
                                    ref childInterpreter, ref name, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    PolicyType policyType = DefaultPolicyType;

                                    if (arguments.Count == 4)
                                    {
                                        object enumValue = Utility.TryParseFlagsEnum(
                                            interpreter, typeof(PolicyType),
                                            policyType.ToString(), arguments[3],
                                            interpreter.CultureInfo, true, true,
                                            true, ref result);

                                        if (enumValue is PolicyType)
                                            policyType = (PolicyType)enumValue;
                                        else
                                            code = ReturnCode.Error;
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        string keyRingName = CertificateKeyRingOps.GetName(
                                            null, policyType);

                                        code = CertificateKeyRingState.CopyTrusted(
                                            interpreter, childInterpreter, keyRingName,
                                            keyRingName, true, true, false, ref result);

                                        if (code == ReturnCode.Ok)
                                            result = String.Empty;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} interp ?policyType?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "usage":
                        {
                            if ((arguments.Count == 3) || (arguments.Count == 4))
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    string pattern = arguments[2];

                                    if (String.IsNullOrEmpty(pattern))
                                        pattern = null;

                                    PolicyType policyType = DefaultPolicyType;

                                    if (arguments.Count == 4)
                                    {
                                        object enumValue = Utility.TryParseFlagsEnum(
                                            interpreter, typeof(PolicyType),
                                            policyType.ToString(), arguments[3],
                                            interpreter.CultureInfo, true, true,
                                            true, ref result);

                                        if (enumValue is PolicyType)
                                            policyType = (PolicyType)enumValue;
                                        else
                                            code = ReturnCode.Error;
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        IEnumerable<IKeyPair> keyPairs = null;

                                        code = CertificateKeyPairOps.GetAnyPublicOnly( /* OK */
                                            null, policyType, true, CertificateAssemblyOps.GetObject(),
                                            plugin.AssemblyName, pattern, false, interpreter,
                                            EntityType.None, true, true, true, true, false,
                                            ref keyPairs, ref result); /* EXEMPT */

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (keyPairs != null)
                                            {
                                                StringList list = null;

                                                foreach (IKeyPair keyPair in keyPairs)
                                                {
                                                    if (keyPair == null)
                                                        continue;

                                                    if (list == null)
                                                        list = new StringList();

                                                    StringPairList subList = new StringPairList();

                                                    subList.Add("PublicKeyToken",
                                                        CertificateDataOps.FormatPublicKeyToken(
                                                            keyPair.PublicKeyToken, false, false));

                                                    CertificateKeyPairOps.KeyUsageToList(
                                                        keyPair.KeyUsage,
                                                        Utility.DefaultAttributeFlagsKey(),
                                                        ref subList);

                                                    list.Add(subList.ToString());
                                                }

                                                result = list;
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
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} pattern ?policyType?\"",
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
