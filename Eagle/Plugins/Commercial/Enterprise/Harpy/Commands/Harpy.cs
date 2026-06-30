/*
 * Harpy.cs --
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
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using AssemblyOps = Licensing.Components.Private.CertificateAssemblyOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "harpy" command ensemble, which exposes the
    /// licensing and policy engine state associated with the security
    /// plugin to scripts.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("fb38bddd-1b38-4cf5-b381-41a97943bed6")]
    [CommandFlags(CommandFlags.Unsafe
#if ENTERPRISE_LOCKDOWN
        | CommandFlags.NoRename
        | CommandFlags.NoRemove
#endif
    )]
    [ObjectGroup("policyEngine")]
    internal sealed class Harpy : Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Harpy" /> class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure this command.
        /// </param>
        public Harpy(
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
        /// Gets the licensing features required in order to use this
        /// command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.HarpyOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this command
        /// ensemble.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "changecount", "changed", "configurations",
            "demomode", "failsafemode", "failsafetrip",
            "features", "isolated", "keyname", "keyringname",
            "machine", "networkflags", "options", "pathflags",
            "policy", "reconfigure", "renewcallback", "sandboxes",
            "scriptflags", "security", "sdkmode", "source",
            "testmode", "timeout", "uri", "verify"
        });

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names supported by
        /// this command ensemble.
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
        /// The collection of sub-command names that are permitted by the
        /// active policy.
        /// </summary>
        private EnsembleDictionary allowedSubCommands =
            new EnsembleDictionary(
                Policies.Harpy.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names that are
        /// permitted by the active policy.
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
        /// Executes this command using the sub-command and arguments
        /// provided, dispatching to the appropriate licensing or policy
        /// engine operation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context in which this command is being
        /// executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to this command, including its
        /// name and the sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of executing this command or
        /// an error message describing why it could not be executed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an
        /// appropriate error return code.
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
                    case "changecount":
                        {
                            if (arguments.Count == 2)
                            {
                                result = CertificateGlobalState.GetChangeCount();
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
                    case "changed":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 5))
                            {
                                //
                                // HACK: The "local" flag here makes almost no
                                //       difference.  When null, policy related
                                //       state will be skipped.  When false, it
                                //       will check global policy state.  When
                                //       true it will check the plugin related
                                //       policy state (i.e. for the plugin that
                                //       is associated with *this* command).
                                //
                                bool ignore = false;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                {
                                    code = Value.GetBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref ignore,
                                        ref result);
                                }

                                bool? local = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                {
                                    code = Value.GetNullableBoolean2(
                                        arguments[3], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref local,
                                        ref result);
                                }

                                bool? @default = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 5))
                                {
                                    code = Value.GetNullableBoolean2(
                                        arguments[4], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref @default,
                                        ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    int count = 0; /* NOT USED */

                                    code = ScriptContext.CheckForChanges(
                                        interpreter, this.Plugin, clientData,
                                        interpreter.CultureInfo, false, local,
                                        @default, false, ignore, false, ref count,
                                        ref result);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?ignore? ?local? ?default?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "configurations":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                ConfigurationFileFlags flags = ConfigurationFileFlags.Default;

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(ConfigurationFileFlags),
                                        flags.ToString(), arguments[2],
                                        interpreter.CultureInfo, true,
                                        true, true, ref result);

                                    if (enumValue is ConfigurationFileFlags)
                                        flags = (ConfigurationFileFlags)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    StringList okFileNames = null;
                                    StringList errorFileNames = null;
                                    int resetCount = Count.Invalid;

                                    if (SharedOps.HasFlags(
                                            flags, ConfigurationFileFlags.Global, true))
                                    {
                                        IPluginData localPlugin;

                                        if (SharedOps.HasFlags(
                                                flags, ConfigurationFileFlags.PluginOnly, true))
                                        {
                                            localPlugin = this.Plugin;
                                        }
                                        else
                                        {
                                            localPlugin = null;
                                        }

                                        okFileNames = StringList.MaybeCreate(
                                            CertificateSandboxState.CopyOkFileNames(
                                            localPlugin, flags));

                                        errorFileNames = StringList.MaybeCreate(
                                            CertificateSandboxState.CopyErrorFileNames(
                                            localPlugin, flags));

                                        if (SharedOps.HasFlags(
                                                flags, ConfigurationFileFlags.Reset, true))
                                        {
                                            resetCount = CertificateSandboxState.ClearFileNames(null);
                                        }
                                    }
                                    else
                                    {
                                        IConfiguration configuration =
                                            this.Plugin as IConfiguration;

                                        if (configuration != null)
                                        {
                                            okFileNames = StringList.MaybeCreate(
                                                configuration.ConfigurationOkFileNames);

                                            errorFileNames = StringList.MaybeCreate(
                                                configuration.ConfigurationErrorFileNames);

                                            if (SharedOps.HasFlags(
                                                    flags, ConfigurationFileFlags.Reset, true))
                                            {
                                                resetCount = configuration.ClearConfigurationFileNames();
                                            }
                                        }
                                        else
                                        {
                                            result = "configuration unavailable";
                                            code = ReturnCode.Error;
                                        }
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        result = StringList.MakeList(
                                            ReturnCode.Ok, okFileNames,
                                            ReturnCode.Error, errorFileNames,
                                            "ResetCount", resetCount);
                                    }
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
                    case "demomode":
                        {
                            if (arguments.Count == 2)
                            {
#if DEMO_KEY_PAIRS || DEMO_EDITION
                                result = CertificateDemoMode.IsEnabled();
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
                    case "failsafemode":
                        {
                            if (arguments.Count == 2)
                            {
                                result = CertificateFailSafeMode.IsEnabled();
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
                    case "failsafetrip":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                bool count = false;

                                if (arguments.Count == 3)
                                {
                                    code = Value.GetBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref count,
                                        ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (count)
                                        result = CertificateFailSafeMode.TripCount();
                                    else
                                        result = CertificateFailSafeMode.WasTripped();
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?count?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "features":
                        {
                            if (arguments.Count == 2)
                            {
#if !LIMITED_EDITION
                                result = CertificateGlobalState.GetExtraFeatures();
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
                    case "keyname":
                        {
                            if (arguments.Count == 2)
                            {
                                string scriptKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.Script,
                                        ref scriptKeyName))
                                {
                                    scriptKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.Script);
                                }

                                string fileKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.File,
                                        ref fileKeyName))
                                {
                                    fileKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.File);
                                }

                                string streamKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.Stream,
                                        ref streamKeyName))
                                {
                                    streamKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.Stream);
                                }

                                string licenseKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.License,
                                        ref licenseKeyName))
                                {
                                    licenseKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.License);
                                }

                                string keyPairKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.KeyPair,
                                        ref keyPairKeyName))
                                {
                                    keyPairKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.KeyPair);
                                }

                                string traceKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.Trace,
                                        ref traceKeyName))
                                {
                                    traceKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.Trace);
                                }

                                string otherKeyName = null;

                                if (!CertificatePolicyOps.GetKeyName(
                                        this.Plugin, PolicyType.Other,
                                        ref otherKeyName))
                                {
                                    otherKeyName = CertificatePolicyOps.GetKeyName(
                                        PolicyType.Other);
                                }

                                result = StringList.MakeList(
                                    "-script", scriptKeyName, "-file", fileKeyName,
                                    "-stream", streamKeyName, "-license", licenseKeyName,
                                    "-keypair", keyPairKeyName, "-trace", traceKeyName,
                                    "-other", otherKeyName);
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
                    case "keyringname":
                        {
                            if (arguments.Count == 2)
                            {
                                string scriptKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.Script,
                                        ref scriptKeyRingName))
                                {
                                    scriptKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.Script);
                                }

                                string fileKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.File,
                                        ref fileKeyRingName))
                                {
                                    fileKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.File);
                                }

                                string streamKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.Stream,
                                        ref streamKeyRingName))
                                {
                                    streamKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.Stream);
                                }

                                string licenseKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.License,
                                        ref licenseKeyRingName))
                                {
                                    licenseKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.License);
                                }

                                string keyPairKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.KeyPair,
                                        ref keyPairKeyRingName))
                                {
                                    keyPairKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.KeyPair);
                                }

                                string traceKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.Trace,
                                        ref traceKeyRingName))
                                {
                                    traceKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.Trace);
                                }

                                string otherKeyRingName = null;

                                if (!CertificatePolicyOps.GetKeyRingName(
                                        this.Plugin, PolicyType.Other,
                                        ref otherKeyRingName))
                                {
                                    otherKeyRingName = CertificatePolicyOps.GetKeyRingName(
                                        PolicyType.Other);
                                }

                                result = StringList.MakeList(
                                    "-script", scriptKeyRingName, "-file", fileKeyRingName,
                                    "-stream", streamKeyRingName, "-license", licenseKeyRingName,
                                    "-keypair", keyPairKeyRingName, "-trace", traceKeyRingName,
                                    "-other", otherKeyRingName);
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
                    case "machine":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                PathFlags flags =
                                    CertificatePolicyState.GetPathFlagsOrDefault();

                                if (arguments.Count == 3)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(PathFlags),
                                        flags.ToString(), arguments[2],
                                        interpreter.CultureInfo, true,
                                        true, true, ref result);

                                    if (enumValue is PathFlags)
                                        flags = (PathFlags)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    StringList list = null;

                                    code = CertificatePolicyOps.GetMachine(
                                        interpreter, interpreter.CultureInfo,
                                        flags, ref list, ref result);

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
                    case "networkflags":
                        {
                            if (arguments.Count == 2)
                            {
                                NetworkFlags scriptFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.Script,
                                        ref scriptFlags))
                                {
                                    scriptFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.Script);
                                }

                                NetworkFlags fileFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.File,
                                        ref fileFlags))
                                {
                                    fileFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.File);
                                }

                                NetworkFlags streamFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.Stream,
                                        ref streamFlags))
                                {
                                    streamFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.Stream);
                                }

                                NetworkFlags licenseFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.License,
                                        ref licenseFlags))
                                {
                                    licenseFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.License);
                                }

                                NetworkFlags keyPairFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.KeyPair,
                                        ref keyPairFlags))
                                {
                                    keyPairFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.KeyPair);
                                }

                                NetworkFlags traceFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.Trace,
                                        ref traceFlags))
                                {
                                    traceFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.Trace);
                                }

                                NetworkFlags otherFlags = NetworkFlags.None;

                                if (!CertificatePolicyOps.GetNetworkFlags(
                                        this.Plugin, PolicyType.Other,
                                        ref otherFlags))
                                {
                                    otherFlags = CertificatePolicyOps.GetNetworkFlags(
                                        PolicyType.Other);
                                }

                                result = StringList.MakeList(
                                    "-script", scriptFlags, "-file", fileFlags,
                                    "-stream", streamFlags, "-license", licenseFlags,
                                    "-keypair", keyPairFlags, "-trace", traceFlags,
                                    "-other", otherFlags);
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
                    case "pathflags":
                        {
                            if (arguments.Count == 2)
                            {
                                PathFlags scriptFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.Script,
                                        ref scriptFlags))
                                {
                                    scriptFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.Script);
                                }

                                PathFlags fileFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.File,
                                        ref fileFlags))
                                {
                                    fileFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.File);
                                }

                                PathFlags streamFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.Stream,
                                        ref streamFlags))
                                {
                                    streamFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.Stream);
                                }

                                PathFlags licenseFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.License,
                                        ref licenseFlags))
                                {
                                    licenseFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.License);
                                }

                                PathFlags keyPairFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.KeyPair,
                                        ref keyPairFlags))
                                {
                                    keyPairFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.KeyPair);
                                }

                                PathFlags traceFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.Trace,
                                        ref traceFlags))
                                {
                                    traceFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.Trace);
                                }

                                PathFlags otherFlags = PathFlags.None;

                                if (!CertificatePolicyOps.GetPathFlags(
                                        this.Plugin, PolicyType.Other,
                                        ref otherFlags))
                                {
                                    otherFlags = CertificatePolicyOps.GetPathFlags(
                                        PolicyType.Other);
                                }

                                result = StringList.MakeList(
                                    "-script", scriptFlags, "-file", fileFlags,
                                    "-stream", streamFlags, "-license", licenseFlags,
                                    "-keypair", keyPairFlags, "-trace", traceFlags,
                                    "-other", otherFlags);
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
                    case "policy":
                        {
                            if (arguments.Count == 2)
                            {
                                result = StringList.MakeList(
                                    "-script", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.Script),
                                    "-file", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.File),
                                    "-stream", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.Stream),
                                    "-license", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.License),
                                    "-keypair", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.KeyPair),
                                    "-trace", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.Trace),
                                    "-other", CertificatePolicyOps.GetPolicy(
                                        this.Plugin, PolicyType.Other));
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
                    case "reconfigure":
                        {
                            if (arguments.Count == 2)
                            {
                                IConfiguration configuration =
                                    this.Plugin as IConfiguration;

                                if (configuration != null)
                                {
                                    string keyName = null;
                                    string keyRingName = null;

#if LICENSING
                                    Plugins.Default defaultPlugin =
                                        configuration as Plugins.Default;

                                    if (defaultPlugin != null)
                                    {
                                        keyName = defaultPlugin.GetKeyName();
                                        keyRingName = defaultPlugin.GetKeyRingName();
                                    }
#endif

                                    code = configuration.LoadConfigurations(
                                        interpreter, new AnyClientData(clientData, false),
                                        ConfigurationPhase.Demand, keyName, keyRingName,
                                        SharedOps.GetTimeout(interpreter, null), true,
                                        true, ref result);
                                }
                                else
                                {
                                    result = "configurations unavailable";
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
                    case "renewcallback":
                        {
                            if (arguments.Count == 2)
                            {
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
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "sandboxes":
                        {
                            if (arguments.Count == 2)
                            {
                                IConfiguration configuration =
                                    this.Plugin as IConfiguration;

                                if (configuration != null)
                                {
                                    IEnumerable<ulong> tokens =
                                        configuration.SandboxTokens;

                                    if (tokens != null)
                                    {
                                        result = new StringList(tokens);
                                    }
                                    else
                                    {
                                        result = "no sandboxes found";
                                        code = ReturnCode.Error;
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
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "scriptflags":
                        {
                            if (arguments.Count == 2)
                            {
                                ScriptFlags scriptFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.Script,
                                        ref scriptFlags))
                                {
                                    scriptFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.Script);
                                }

                                ScriptFlags fileFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.File,
                                        ref fileFlags))
                                {
                                    fileFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.File);
                                }

                                ScriptFlags streamFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.Stream,
                                        ref streamFlags))
                                {
                                    streamFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.Stream);
                                }

                                ScriptFlags licenseFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.License,
                                        ref licenseFlags))
                                {
                                    licenseFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.License);
                                }

                                ScriptFlags keyPairFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.KeyPair,
                                        ref keyPairFlags))
                                {
                                    keyPairFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.KeyPair);
                                }

                                ScriptFlags traceFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.Trace,
                                        ref traceFlags))
                                {
                                    traceFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.Trace);
                                }

                                ScriptFlags otherFlags = ScriptFlags.None;

                                if (!CertificatePolicyOps.GetScriptFlags(
                                        this.Plugin, PolicyType.Other,
                                        ref otherFlags))
                                {
                                    otherFlags = CertificatePolicyOps.GetScriptFlags(
                                        PolicyType.Other);
                                }

                                result = StringList.MakeList(
                                    "-script", scriptFlags, "-file", fileFlags,
                                    "-stream", streamFlags, "-license", licenseFlags,
                                    "-keypair", keyPairFlags, "-trace", traceFlags,
                                    "-other", otherFlags);
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
                    case "security":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                bool exactMatch = false;

                                if (arguments.Count == 3)
                                {
                                    code = Value.GetBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref exactMatch,
                                        ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    result = StringList.MakeList(
                                        "-script", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.Script, false, exactMatch),
                                        "-file", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.File, false, exactMatch),
                                        "-stream", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.Stream, false, exactMatch),
                                        "-license", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.License, false, exactMatch),
                                        "-keypair", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.KeyPair, false, exactMatch),
                                        "-trace", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.Trace, false, exactMatch),
                                        "-other", CertificatePolicyOps.HavePolicyForCommand(
                                            this.Plugin, PolicyType.Other, false, exactMatch));
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?exact?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "sdkmode":
                        {
                            if (arguments.Count == 2)
                            {
                                result = CertificateSdkMode.IsEnabled();
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
                    case "source":
                        {
                            if (arguments.Count == 2)
                            {
                                Assembly assembly = AssemblyOps.GetObject();

                                if (assembly != null)
                                {
                                    result = StringList.MakeList(
                                        Utility.GetAssemblySourceId(assembly),
                                        Utility.GetAssemblySourceTimeStamp(assembly));
                                }
                                else
                                {
                                    result = "invalid assembly";
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
                    case "testmode":
                        {
                            if (arguments.Count == 2)
                            {
                                result = CertificateTestMode.IsEnabled();
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
                    case "timeout":
                        {
                            if (arguments.Count == 2)
                            {
                                result = SharedOps.GetTimeout(interpreter, null);
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
                    case "uri":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                if (arguments.Count == 3)
                                {
                                    UriType type = UriType.Default;

                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(UriType),
                                        type.ToString(), arguments[2],
                                        interpreter.CultureInfo, true,
                                        true, true, ref result);

                                    if (enumValue is UriType)
                                        type = (UriType)enumValue;
                                    else
                                        code = ReturnCode.Error;

                                    if (code == ReturnCode.Ok)
                                    {
                                        code = SharedOps.GetUri(
                                            interpreter, this.Plugin,
                                            interpreter.CultureInfo, type,
                                            ref result);
                                    }
                                }
                                else
                                {
                                    code = SharedOps.GetUriTypes(ref result);
                                }
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
                    case "verify":
                        {
#if XML && SERIALIZATION
                            //
                            // WARNING: This command called directly by the Eagle
                            //          core library (i.e. via EvaluateBundleFile
                            //          method, et al).  Do not change it without
                            //          making sure that it still works there.
                            //
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null,
                                        OptionFlags.MustHaveIntegerValue |
                                        OptionFlags.Unsafe,
                                        Index.Invalid, Index.Invalid,
                                        "-timeout", null),
                                    Option.CreateEndOfOptions()
                                });

                                code = SharedOps.FixupOptions(
                                    this.Plugin, options, false, ref result);

                                if (code == ReturnCode.Ok)
                                {
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
                                            int? timeout = SharedOps.GetTimeout(interpreter, null);

                                            if (options.IsPresent("-timeout", ref value))
                                                timeout = (int)value.Value;

                                            string fileName = arguments[argumentIndex];

                                            string certificateFileName = DataOps.FormatFileName(
                                                fileName, interpreter.CultureInfo,
                                                null, Utility.IsRemoteUri(fileName));

                                            ICertificate certificate = null;

                                            code = CertificateXmlOps.Import(
                                                certificateFileName, true, false,
                                                true, ref certificate, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                IEnumerable<IKeyPair> keyPairs = null;

                                                code = CertificateKeyRingOps.GetKeyPairs(
                                                    interpreter, null, PolicyType.Script,
                                                    null, false, ref keyPairs, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    IKeyPair keyPair = null;

                                                    code = SharedOps.VerifyFile(
                                                        SharedOps.GetHashAlgorithm(
                                                            null, keyPairs, certificate,
                                                            HashAlgorithmType.ScriptUse),
                                                        null, certificate, null, null,
                                                        keyPairs, fileName, timeout, true,
                                                        true, ref keyPair, ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        result = new StringPairList(
                                                            keyPair.ToList() as IEnumerable<IPair<string>>);
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
#else
                            result = "not implemented";
                            code = ReturnCode.Error;
#endif
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
