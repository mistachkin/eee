/*
 * Kapok.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Private;
using Kapok.Components.Shared;
using _Commands = Eagle._Commands;
using TokenManagement = Kapok.Components.Shared.SandboxOps.TokenManagement;

using SlidingThrottlePair = System.Collections.Generic.KeyValuePair<
    Eagle._Containers.Public.ThrottleDictionary.ThrottleKey, ulong>;

using SlidingThrottleDictionary = Eagle._Containers.Public.ThrottleDictionary;

using FixedThrottleValue = Eagle._Components.Public.MutableAnyTriplet<
    System.DateTime, long, long>;

using FixedThrottlePair = System.Collections.Generic.KeyValuePair<
    string, Eagle._Components.Public.MutableAnyTriplet<
        System.DateTime, long, long>>;

using FixedThrottleDictionary = System.Collections.Generic.Dictionary<
    string, Eagle._Components.Public.MutableAnyTriplet<
        System.DateTime, long, long>>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.Commands
{
    /// <summary>
    /// Implements the <c>kapok</c> ensemble command, the single script-visible
    /// command exposed by the Kapok plugin.  Its sub-commands manage API-key
    /// access control and throttling, evaluate scripts in sandboxed
    /// interpreters, query server settings and diagnostics, and clean up
    /// cached interpreters.  The command is marked unsafe and belongs to the
    /// "managedEnvironment" object group.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("16d34cf9-f48a-4a96-9772-14a1484328ff")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("managedEnvironment")]
    internal sealed class Kapok : _Commands.Default
    {
        #region Private Methods
        /// <summary>
        /// Determines whether the supplied access-change flags contain the
        /// given flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        private static bool HasFlags(
            AccessChangeType flags,    /* in */
            AccessChangeType hasFlags, /* in */
            bool all                   /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != AccessChangeType.None);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Kapok" /> command
        /// class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure the command, such as its
        /// name, flags, and associated plugin.
        /// </param>
        public Kapok(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The backing field for the <see cref="SubCommands" /> property,
        /// holding the set of sub-command names recognized by this ensemble
        /// command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "access", "certificate", "cleanup", "done",
            "evaluate", "isolated", "log", "options", "setting",
            "source"
        });

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the dictionary of sub-command names supported by this
        /// ensemble command.
        /// </summary>
        public override EnsembleDictionary SubCommands
        {
            get { return subCommands; }
            set { subCommands = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the <c>kapok</c> command.  The first argument selects the
        /// sub-command, which is dispatched through the ensemble's
        /// policy-aware resolver and, failing that, the built-in sub-commands
        /// (about, access, certificate, cleanup, done, evaluate, isolated,
        /// log, options, setting, source).  An unknown sub-command yields an
        /// error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command, including the
        /// command name and the selected sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the command, or an error
        /// message describing why it failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
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
                    case "access":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 5))
                            {
                                AccessChangeType changeType = AccessChangeType.None;
                                int argumentIndex = 2;

                                if (arguments.Count >= 4)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(AccessChangeType),
                                        changeType.ToString(), arguments[argumentIndex],
                                        interpreter.CultureInfo, true, true, true,
                                        ref result);

                                    if (enumValue is AccessChangeType)
                                    {
                                        changeType = (AccessChangeType)enumValue;
                                        argumentIndex++;
                                    }
                                    else
                                    {
                                        code = ReturnCode.Error;
                                    }
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    Guid? apiKeyId = null;

                                    code = Value.GetNullableGuid(
                                        arguments[argumentIndex],
                                        interpreter.CultureInfo,
                                        ref apiKeyId, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (apiKeyId != null)
                                        {
                                        retry:

                                            ulong token; /* REUSED */
                                            bool noAnonymous; /* REUSED */

                                            if (changeType == AccessChangeType.None)
                                            {
                                                bool isAllowed = TokenManagement.IsAllowed(
                                                    (Guid)apiKeyId);

                                                bool isDenied = TokenManagement.IsDenied(
                                                    (Guid)apiKeyId);

                                                bool isAdministrator = TokenManagement.IsAdministrator(
                                                        (Guid)apiKeyId);

                                                bool isFake = TokenManagement.IsFake(
                                                    (Guid)apiKeyId);

                                                IRuleSet ruleSet = TokenManagement.HasRuleSet(
                                                    (Guid)apiKeyId);

                                                if (TokenManagement.Have(
                                                        apiKeyId, out token, out noAnonymous))
                                                {
                                                    result = StringList.MakeList(
                                                        "result", true, "token", token,
                                                        "noAnonymous", noAnonymous,
                                                        "isAllowed", isAllowed,
                                                        "isDenied", isDenied,
                                                        "isAdministrator", isAdministrator,
                                                        "isFake", isFake, "ruleSet", ruleSet);
                                                }
                                                else
                                                {
                                                    result = StringList.MakeList(
                                                        "result", false, "token", token,
                                                        "noAnonymous", noAnonymous,
                                                        "isAllowed", isAllowed,
                                                        "isDenied", isDenied,
                                                        "isAdministrator", isAdministrator,
                                                        "isFake", isFake, "ruleSet", ruleSet);
                                                }
                                            }
                                            else if (HasFlags(changeType, AccessChangeType.Hits, true))
                                            {
                                                if (HasFlags(changeType, AccessChangeType.Sliding, true))
                                                {
                                                    SlidingThrottleDictionary slidingRequests =
                                                        ThrottleOps.CopyRequests(true) as SlidingThrottleDictionary;

                                                    if (slidingRequests != null)
                                                    {
                                                        StringList list = new StringList();

                                                        foreach (SlidingThrottlePair pair in slidingRequests)
                                                        {
                                                            list.Add(ThrottleOps.FormatSlidingValue(
                                                                pair.Key, pair.Value));
                                                        }

                                                        result = list;
                                                    }
                                                    else
                                                    {
                                                        result = null;
                                                    }
                                                }
                                                else
                                                {
                                                    FixedThrottleDictionary fixedRequests =
                                                        ThrottleOps.CopyRequests(false) as FixedThrottleDictionary;

                                                    if (fixedRequests != null)
                                                    {
                                                        StringDictionary dictionary = new StringDictionary();

                                                        foreach (FixedThrottlePair pair in fixedRequests)
                                                        {
                                                            dictionary.Add(pair.Key,
                                                                ThrottleOps.FormatFixedValue(pair.Value));
                                                        }

                                                        result = dictionary.KeysAndValuesToString(null, false);
                                                    }
                                                    else
                                                    {
                                                        result = null;
                                                    }
                                                }
                                            }
                                            else if (HasFlags(changeType, AccessChangeType.Throttle, true))
                                            {
                                                ThrottleFlags throttleFlags =
                                                    ThrottleFlags.Default | ThrottleFlags.HaveRaw;

                                                if (HasFlags(changeType, AccessChangeType.Sliding, true))
                                                    throttleFlags |= ThrottleFlags.Sliding;

                                                /* IGNORED */
                                                TokenManagement.Have(
                                                    apiKeyId, out token, out noAnonymous);

                                                if (noAnonymous)
                                                    throttleFlags |= ThrottleFlags.NoAnonymous;

                                                string userHostAddress = null;

                                                if (arguments.Count == 5)
                                                    userHostAddress = arguments[4];

                                                ResultList errors = null;

                                                if (ThrottleOps.IsBadRequest(
                                                        apiKeyId, userHostAddress, throttleFlags,
                                                        ApiKeyStatus.None, "evaluate", 0, 0, 0, 0,
                                                        ref errors))
                                                {
                                                    result = errors;
                                                    code = ReturnCode.Error;
                                                }
                                            }
                                            else if (HasFlags(changeType, AccessChangeType.Reset, true))
                                            {
                                                result = ThrottleOps.ResetRequests(HasFlags(
                                                    changeType, AccessChangeType.Sliding, true));
                                            }
                                            else
                                            {
                                                IRuleSet ruleSet = null;

                                                if (arguments.Count == 5)
                                                {
                                                    ruleSet = RuleSet.Create(
                                                        arguments[4], interpreter.CultureInfo,
                                                        ref result);

                                                    if (ruleSet == null)
                                                        code = ReturnCode.Error;
                                                }

                                                if (code == ReturnCode.Ok)
                                                {
                                                    code = TokenManagement.Change(
                                                        (Guid)apiKeyId, changeType, ruleSet,
                                                        ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        changeType = AccessChangeType.None;
                                                        goto retry;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            result = "invalid API key";
                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                                else
                                {
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?type? apiKeyId ?value?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "certificate":
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
                            if ((arguments.Count >= 2) && (arguments.Count <= 3))
                            {
                                if (arguments.Count == 3)
                                {
                                    Guid? apiKeyId = null;

                                    code = Value.GetNullableGuid(
                                        arguments[2], interpreter.CultureInfo,
                                        ref apiKeyId, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        code = SandboxOps.CleanupInterpreter(
                                            apiKeyId, ref result);
                                    }
                                }
                                else
                                {
                                    code = SandboxOps.CleanupInterpreters(
                                        ref result);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?apiKeyId?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "done":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 4))
                            {
                                object enumValue = Utility.TryParseFlagsEnum(
                                    interpreter, typeof(ConfigurationAction),
                                    ConfigurationAction.None.ToString(),
                                    arguments[2], interpreter.CultureInfo,
                                    true, true, true, ref result);

                                if (enumValue is ConfigurationAction)
                                {
                                    ConfigurationAction action =
                                        (ConfigurationAction)enumValue;

                                    bool? mark = null;

                                    if (arguments.Count == 4)
                                    {
                                        code = Value.GetNullableBoolean2(
                                            arguments[3], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref mark,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (mark != null)
                                        {
                                            result = ConfigurationActions.TryMarkDone(
                                                action, (bool)mark);
                                        }
                                        else
                                        {
                                            result = ConfigurationActions.IsDone(action);
                                        }
                                    }
                                }
                                else
                                {
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} action ?value?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "evaluate":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveGuidValue,
                                        Index.Invalid, Index.Invalid, "-apikeyid", null),
                                    new Option(null, OptionFlags.MustHaveListValue,
                                        Index.Invalid, Index.Invalid, "-args", null),
                                    new Option(null, OptionFlags.MustHaveRuleSetValue |
                                        OptionFlags.CouldBePath,
                                        Index.Invalid, Index.Invalid, "-ruleset", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-rulesetfilename", null),
                                    new Option(typeof(RuleSetType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-rulesettype",
                                        new Variant(RuleSetType.KapokDefault)),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-unsafe", null),
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-nobuiltins", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-host", null),
                                    new Option(null, OptionFlags.MustHaveListValue,
                                        Index.Invalid, Index.Invalid, "-allowhosts", null),
                                    new Option(null, OptionFlags.MustHaveListValue,
                                        Index.Invalid, Index.Invalid, "-denyhosts", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid, false,
                                    ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if (argumentIndex != Index.Invalid)
                                    {
                                        IVariant value = null;
                                        Guid? apiKeyId = null;

                                        if (options.IsPresent("-apikeyid", ref value))
                                            apiKeyId = (Guid)value.Value;

                                        bool? @unsafe = null;

                                        if (options.IsPresent("-unsafe", ref value))
                                            @unsafe = (bool)value.Value;

                                        bool? noBuiltIns = null;

                                        if (options.IsPresent("-nobuiltins", ref value))
                                            noBuiltIns = (bool)value.Value;

                                        StringList args = null;

                                        if (options.IsPresent("-args", ref value))
                                            args = (StringList)value.Value;

                                        IRuleSet ruleSet = null;

                                        if (options.IsPresent("-ruleset", ref value))
                                            ruleSet = (IRuleSet)value.Value;

                                        string ruleSetFileName = null;

                                        if (options.IsPresent("-rulesetfilename", ref value))
                                            ruleSetFileName = value.ToString();

                                        RuleSetType ruleSetType = RuleSetType.KapokDefault;

                                        if (options.IsPresent("-rulesettype", ref value))
                                            ruleSetType = (RuleSetType)value.Value;

                                        string host = null;

                                        if (options.IsPresent("-host", ref value))
                                            host = value.ToString();

                                        StringList allowHosts = null;

                                        if (options.IsPresent("-allowhosts", ref value))
                                            allowHosts = (StringList)value.Value;

                                        StringList denyHosts = null;

                                        if (options.IsPresent("-denyhosts", ref value))
                                            denyHosts = (StringList)value.Value;

                                        code = SandboxOps.EvaluateScript(
                                            apiKeyId, host, allowHosts, denyHosts,
                                            ruleSet, ruleSetFileName, ruleSetType,
                                            args, arguments[argumentIndex], @unsafe,
                                            noBuiltIns, ref result);
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

                                        code = ReturnCode.Error;
                                    }
                                }
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
                    case "log":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 4))
                            {
                                TracePriority priority = TracePriority.AlwaysDemand;

                                if (arguments.Count == 4)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(TracePriority),
                                        priority.ToString(), arguments[3],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is TracePriority)
                                        priority = (TracePriority)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    string message = arguments[2];

#if NATIVE
                                    Utility.OutputDebugString(message);
#endif

                                    string category = typeof(Kapok).Name;

                                    Utility.DebugTrace(message, category, priority);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} message ?priority?\"",
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
                    case "setting":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 4))
                            {
                                SettingDataType dataType = SettingDataType.DefaultAndExpand;

                                if (arguments.Count == 4)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(SettingDataType),
                                        dataType.ToString(), arguments[3],
                                        interpreter.CultureInfo, true, true,
                                        true, ref result);

                                    if (enumValue is SettingDataType)
                                        dataType = (SettingDataType)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    result = WebSettingsOps.GetGlobal(
                                        arguments[2], dataType);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} name ?flags?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "source":
                        {
                            if (arguments.Count == 2)
                            {
                                Assembly assembly = WebGlobalState.GetAssembly();

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
                    default:
                        {
                            result = Utility.BadSubCommand(
                                interpreter, null, null, subCommand, this, null, null);

                            code = ReturnCode.Error;
                            break;
                        }
                }
            }

            return code;
        }
        #endregion
    }
}
