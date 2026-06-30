/*
 * HotKey.cs --
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

#if OBFUSCATION
using System.Reflection;
#endif

using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;
using HotKey.Forms;
using HotKey.Interfaces.Private;
using _Commands = Eagle._Commands;

using StringElementData = HotKey.Components.Private.ListElementData<
    string, string>;

using ElementDictionary = System.Collections.Generic.Dictionary<
    string, HotKey.Components.Private.ListElementData<string, string>>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace HotKey.Commands
{
    /// <summary>
    /// Implements the <c>hotkey</c> ensemble command, the single
    /// script-visible command exposed by the HotKey plugin.  Its sub-commands
    /// define and register global hot-keys, manage the hot-key manager thread,
    /// show editor/viewer/dialog forms, and handle logging.  The command is
    /// marked native-code and unsafe and belongs to the "nativeEnvironment"
    /// object group; in a safe interpreter only the policy-allowed
    /// sub-commands are permitted.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("1b987d09-115a-4c1b-811a-152b178759b0")]
    [CommandFlags(CommandFlags.NativeCode | CommandFlags.Unsafe)]
    [ObjectGroup("nativeEnvironment")]
    internal sealed class _HotKey : _Commands.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="_HotKey" /> command
        /// class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure the command, such as its
        /// name, flags, and associated plugin.
        /// </param>
        public _HotKey(
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
            "about", "add", "anykeys", "autoload", "busy", "certificate",
            "clear", "clearlog", "click", "copylog", "count",
            "directory", "edit", "evaluate", "find", "formid",
            "formlist", "formwait", "get", "isolated", "list",
            "load", "lock", "log", "logging", "messagebox",
            "onhook", "options", "pendingcancel", "previouseventdata",
            "ready", "register", "remove", "resource", "result",
            "root", "save", "script", "secret", "selectdirectory",
            "selectfile", "selectitem", "selectkeys", "set",
            "shutdown", "startup", "status", "template", "title",
            "unregister", "view", "wait", "yesno"
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

        #region IPolicyEnsemble Members
        /// <summary>
        /// The backing field for the <see cref="AllowedSubCommands" />
        /// property, holding the sub-command names permitted in a safe
        /// interpreter.
        /// </summary>
        private EnsembleDictionary allowedSubCommands =
            new EnsembleDictionary(Policies.HotKey.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the dictionary of sub-command names that are permitted
        /// in a safe interpreter.
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
        /// Executes the <c>hotkey</c> command.  The first argument selects the
        /// sub-command, which is first dispatched through the ensemble's
        /// policy-aware <c>Utility.TryExecuteSubCommandFromEnsemble</c>
        /// resolver; if that does not handle it, the built-in sub-commands are
        /// dispatched here.  An unknown sub-command yields an error.
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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
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

            ReturnCode code = ReturnCode.Ok;
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
                    case "add":
                        {
                            if (arguments.Count == 5)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(Keys), null, arguments[2],
                                        interpreter.CultureInfo, true, true, true,
                                        ref result);

                                    if (enumValue is Keys)
                                    {
                                        Keys keys = (Keys)enumValue;

                                        enumValue = Utility.TryParseFlagsEnum(
                                            interpreter, typeof(HotKeyFlags), null,
                                            arguments[3], interpreter.CultureInfo,
                                            true, true, true, ref result);

                                        if (enumValue is HotKeyFlags)
                                        {
                                            HotKeyFlags flags = (HotKeyFlags)enumValue;
                                            int id = 0;

                                            code = hotKeyManager.AddHotKey(
                                                keys, flags, arguments[4], ref id,
                                                ref result); /* throw */

                                            if (code == ReturnCode.Ok)
                                                result = id;
                                        }
                                        else
                                        {
                                            code = ReturnCode.Error;
                                        }
                                    }
                                    else
                                    {
                                        code = ReturnCode.Error;
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} keys flags text\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "autoload":
                        {
                            if (arguments.Count == 2)
                            {
                                Dictionary<string, bool> fileNames = null;

                                ScriptOps.GetAutoLoadFileNames(
                                    subCommand, false, ref fileNames);

                                Dictionary<string, Result> results = null;
                                ResultList errors = null;

                                code = ScriptOps.TryAutoLoadFiles(
                                    interpreter, Shell.Form.GetHotKeyManager(),
                                    fileNames, false, false, ref results,
                                    ref errors);

                                if (code == ReturnCode.Ok)
                                    result = new StringDictionary(results);
                                else
                                    result = errors;
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
                    case "busy":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 4))
                            {
                                bool? busy = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                {
                                    bool localBusy = false;

                                    code = Value.GetBoolean2(
                                        arguments[2], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref localBusy,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                        busy = (bool)localBusy;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (busy != null)
                                    {
                                        int id = 0;

                                        if ((bool)busy)
                                        {
                                            string title = null;

                                            if (arguments.Count >= 4)
                                                title = arguments[3];

                                            id = FormId.GetNext();

                                            code = BusyForm.ShowBusy(
                                                id, interpreter, null, title,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                                result = id;
                                        }
                                        else
                                        {
                                            if (arguments.Count >= 4)
                                            {
                                                code = Value.GetInteger2(
                                                    (IGetValue)arguments[3],
                                                    ValueFlags.AnyInteger,
                                                    interpreter.CultureInfo,
                                                    ref id, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                //
                                                // TODO: Good to default to the
                                                //       asynchronous mode here?
                                                //
                                                result = BaseForm.CloseOneOrAll(
                                                    typeof(BusyForm), id, true);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        result = BaseForm.CountOneOrAll(
                                            typeof(BusyForm), 0);

                                        code = ReturnCode.Ok;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?busy? ?idOrTitle?\"",
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
                    case "clear":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 4))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    bool unregisterOnly = false; /* TODO: Good default? */

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[2], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref unregisterOnly,
                                            ref result);
                                    }

                                    bool force = false; /* TODO: Good default? */

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[3], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref force,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        code = hotKeyManager.ClearHotKeys(
                                            unregisterOnly, force, ref result); /* throw */

                                        if (code == ReturnCode.Ok)
                                            result = String.Empty;
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?unregisterOnly? ?force?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "clearlog":
                        {
                            if (arguments.Count == 2)
                            {
                                code = Shell.Form.ClearHotKeyLog(ref result);

                                if (code == ReturnCode.Ok)
                                    result = String.Empty;
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
                    case "click":
                        {
                            if ((arguments.Count >= 4) && (arguments.Count <= 7))
                            {
                                bool rawFormOnly = false;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 5))
                                {
                                    code = Value.GetBoolean2(
                                        arguments[4], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref rawFormOnly,
                                        ref result);
                                }

                                bool exactOnly = false;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 6))
                                {
                                    code = Value.GetBoolean2(
                                        arguments[5], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref exactOnly,
                                        ref result);
                                }

                                bool asynchronous = false;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 7))
                                {
                                    code = Value.GetBoolean2(
                                        arguments[6], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref asynchronous,
                                        ref result);
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    string formPattern = arguments[2];
                                    string componentName = arguments[3];

                                    if (String.IsNullOrEmpty(componentName))
                                        componentName = null; /* HACK: Form itself. */

                                    code = WinFormsOps.PerformClick(
                                        interpreter.CultureInfo, formPattern,
                                        componentName, rawFormOnly, exactOnly,
                                        asynchronous, ref result);
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} formPattern componentName ?rawFormOnly? ?exactOnly? ?asynchronous?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "copylog":
                        {
                            if (arguments.Count == 2)
                            {
                                code = Shell.Form.CopyLogToClipboard(ref result);

                                if (code == ReturnCode.Ok)
                                    result = String.Empty;
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
                    case "count":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    bool registered = false;

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[2], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref registered,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        int count = 0;

                                        code = hotKeyManager.CountHotKeys(
                                            registered, ref count, ref result);

                                        if (code == ReturnCode.Ok)
                                            result = count;
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?registered?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "directory":
                        {
                            if (arguments.Count == 2)
                            {
                                result = ManagerOps.GetDirectory();
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
                    case "edit":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 6))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    string varName = null;

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                        varName = arguments[3];

                                    bool advanced = false;

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 5))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[4], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref advanced,
                                            ref result);
                                    }

                                    bool template = false;

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 6))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[5], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref template,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        IHotKey hotKey = null;

                                        code = hotKeyManager.GetHotKey(
                                            arguments[2], interpreter.CultureInfo,
                                            ref hotKey, ref result); /* throw */

                                        if (code == ReturnCode.Ok)
                                        {
                                            int id = FormId.GetNext();
                                            bool readOnly = hotKey.Registered;

                                            code = HotKeyEditForm.ShowEditor(
                                                null, interpreter, varName, id,
                                                readOnly, advanced, template,
                                                ref hotKey, ref result);

                                            if ((code == ReturnCode.Ok) && !readOnly)
                                            {
                                                code = hotKeyManager.SetHotKey(
                                                    arguments[2], interpreter.CultureInfo,
                                                    hotKey, ref result);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id ?idVarName? ?advanced? ?template?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "evaluate":
                        {
                            if (arguments.Count == 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IHotKey hotKey = null;

                                    code = hotKeyManager.GetHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref hotKey, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (!hotKey.HasFlags(HotKeyFlags.NoResetResult, true))
                                            /* NO RESULT */
                                            hotKey.ResetResult(); /* throw */

                                        /* NO RESULT */
                                        hotKey.EvaluateScript(interpreter,
                                            HotKeyScriptFlags.ViaCommand); /* throw */

                                        /* NO RESULT */
                                        hotKey.ResultToInterpreter(
                                            interpreter, ref code, ref result); /* throw */
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "find":
                        {
                            if (arguments.Count >= 2)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        new Option(typeof(Keys),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-keys", new Variant(Keys.None)),
                                        new Option(typeof(HotKeyFlags),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-flags",
                                            new Variant(HotKeyFlags.None)),
                                        new Option(null,
                                            OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                            Index.Invalid, "-registered", null),
                                        new Option(null,
                                            OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                            Index.Invalid, "-exact", null),
                                        new Option(null,
                                            OptionFlags.MustHaveBooleanValue, Index.Invalid,
                                            Index.Invalid, "-all", null),
                                        Option.CreateEndOfOptions()
                                    });

                                    int argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        code = interpreter.GetOptions(options, arguments, 0, 2,
                                            Index.Invalid, true, ref argumentIndex, ref result);
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
                                            bool exact = false; /* TODO: Good default? */

                                            if (options.IsPresent("-exact", ref value))
                                                exact = (bool)value.Value;

                                            bool all = false; /* TODO: Good default? */

                                            if (options.IsPresent("-all", ref value))
                                                all = (bool)value.Value;

                                            Keys? keys = null;

                                            if (options.IsPresent("-keys", ref value))
                                                keys = (Keys)value.Value;

                                            HotKeyFlags? flags = null;

                                            if (options.IsPresent("-flags", ref value))
                                                flags = (HotKeyFlags)value.Value;

                                            bool? registered = null;

                                            if (options.IsPresent("-registered", ref value))
                                                registered = (bool)value.Value;

                                            IntList ids = null;

                                            if ((code == ReturnCode.Ok) &&
                                                (keys is Keys))
                                            {
                                                code = hotKeyManager.FindHotKeys(
                                                    (Keys)keys, exact, all, ref ids,
                                                    ref result);
                                            }

                                            if ((code == ReturnCode.Ok) &&
                                                (flags is HotKeyFlags))
                                            {
                                                code = hotKeyManager.FindHotKeys(
                                                    (HotKeyFlags)flags, exact, all,
                                                    ref ids, ref result);
                                            }

                                            if ((code == ReturnCode.Ok) &&
                                                (registered != null))
                                            {
                                                code = hotKeyManager.FindHotKeys(
                                                    (bool)registered, all, ref ids,
                                                    ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (ids != null)
                                                    result = ids.ToString();
                                                else
                                                    result = String.Empty;
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
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
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
                    case "formid":
                        {
                            if (arguments.Count == 2)
                            {
                                result = FormId.GetPrevious();
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
                    case "formlist":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = new StringList();

                                foreach (Form form in BaseForm.CopyOpenForms())
                                {
                                    StringList list2 = new StringList();
                                    IHotKeyForm hotKeyForm = form as IHotKeyForm;

                                    if (hotKeyForm != null)
                                    {
                                        list2.Add("Id");
                                        list2.Add(hotKeyForm.SafeId.ToString());

                                        list2.Add("Name");
                                        list2.Add(hotKeyForm.SafeName);

                                        list2.Add("Text");
                                        list2.Add(hotKeyForm.SafeText);
                                    }
                                    else
                                    {
#if WINFORMS
                                        IntPtr hWnd = IntPtr.Zero;

                                        code = Utility.GetControlHandle(
                                            form, ref hWnd, ref result);

                                        if (code != ReturnCode.Ok)
                                            break;

                                        list2.Add("Id");
                                        list2.Add(hWnd.ToString());

                                        string name = null;

                                        if (!WinFormsOps.GetName(form, ref name))
                                        {
                                            result = "failed to get form name";
                                            code = ReturnCode.Error;
                                            break;
                                        }

                                        list2.Add("Name");
                                        list2.Add(name);

                                        string text = null;

                                        if (!WinFormsOps.GetText(form, ref text))
                                        {
                                            result = "failed to get form text";
                                            code = ReturnCode.Error;
                                            break;
                                        }

                                        list2.Add("Text");
                                        list2.Add(text);
#else
                                        result = "not implemented";
                                        code = ReturnCode.Error;
                                        break;
#endif
                                    }

                                    list.Add(list2.ToString());
                                }

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
                    case "formwait":
                        {
                            if (arguments.Count == 4)
                            {
                                int id = 0;

                                code = Value.GetInteger2(
                                    (IGetValue)arguments[2], ValueFlags.AnyInteger,
                                    interpreter.CultureInfo, ref id, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int timeout = 0;

                                    code = Value.GetInteger2(
                                        (IGetValue)arguments[3], ValueFlags.AnyInteger,
                                        interpreter.CultureInfo, ref timeout, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (BaseForm.WaitForShown(id, timeout))
                                        {
                                            result = String.Empty; // TODO: Something else?
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "form {0} not shown after {1} milliseconds",
                                                id, timeout);

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id milliseconds\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "get":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 4))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    bool full = false;

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[3], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref full,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        IHotKey hotKey = null;

                                        code = hotKeyManager.GetHotKey(
                                            arguments[2], interpreter.CultureInfo,
                                            ref hotKey, ref result); /* throw */

                                        if (code == ReturnCode.Ok)
                                            result = hotKey.ToList(full); /* throw */
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id ?full?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "isolated":
                        {
                            if (arguments.Count == 2)
                            {
                                result = Shell.Form.IsHotKeyIsolated(interpreter);
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
                            if (arguments.Count == 2)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IntList ids = null;

                                    code = hotKeyManager.ListHotKeys(
                                        ref ids, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                        result = ids.ToString();
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
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
                    case "load":
                        {
                            if ((arguments.Count >= 3) && (arguments.Count <= 5))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    bool strictCount = false; /* TODO: Good default? */

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[3], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref strictCount,
                                            ref result);
                                    }

                                    bool strictRegister = false; /* TODO: Good default? */

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 5))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[4], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref strictRegister,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        code = hotKeyManager.LoadHotKeys(
                                            arguments[2], strictCount, strictRegister,
                                            ref result); /* throw */

                                        if (code == ReturnCode.Ok)
                                            result = String.Empty;
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} script ?strictCount? ?strictRegister?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "lock": /* NOTE: One way only.  There is no "unlock". */
                        {
                            if (arguments.Count == 2)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    code = TemplateOps.LockWindow(
                                        interpreter, hotKeyManager.GetHotKeyHandle(),
                                        ref result);
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
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
                            if (arguments.Count == 3)
                            {
                                code = Shell.Form.AppendToHotKeyLog(
                                    interpreter, arguments[2], ref result);

                                if (code == ReturnCode.Ok)
                                    result = String.Empty;
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} text\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "logging":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    if (arguments.Count == 3)
                                    {
                                        bool logging = false;

                                        code = Value.GetBoolean2(
                                            arguments[2], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref logging,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                            hotKeyManager.Logging = logging;
                                    }

                                    if (code == ReturnCode.Ok)
                                        result = hotKeyManager.Logging;
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?enabled?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "messagebox":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-text", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-caption", null),
                                    new Option(typeof(MessageBoxButtons),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-buttons", null),
                                    new Option(typeof(MessageBoxIcon),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-icon", null),
                                    new Option(typeof(MessageBoxDefaultButton),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-default", null),
                                    new Option(typeof(MessageBoxOptions),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-options", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-help", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-helpfile", null),
                                    new Option(typeof(HelpNavigator),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-helpnavigator", null),
                                    new Option(null, OptionFlags.MustHaveObjectValue,
                                        Index.Invalid, Index.Invalid, "-helpparam", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                if (arguments.Count > 2)
                                {
                                    code = interpreter.GetOptions(options, arguments, 0, 2,
                                        Index.Invalid, true, ref argumentIndex, ref result);
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
                                        string text = null;

                                        if (options.IsPresent("-text", ref value))
                                            text = value.ToString();

                                        string caption = null;

                                        if (options.IsPresent("-caption", ref value))
                                            caption = value.ToString();

                                        MessageBoxButtons boxButtons = MessageBoxButtons.OK;

                                        if (options.IsPresent("-buttons", ref value))
                                            boxButtons = (MessageBoxButtons)value.Value;

                                        MessageBoxIcon boxIcon = MessageBoxIcon.None;

                                        if (options.IsPresent("-icon", ref value))
                                            boxIcon = (MessageBoxIcon)value.Value;

                                        MessageBoxDefaultButton boxDefault = MessageBoxDefaultButton.Button1;

                                        if (options.IsPresent("-default", ref value))
                                            boxDefault = (MessageBoxDefaultButton)value.Value;

                                        MessageBoxOptions boxOptions = 0; /* TODO: Good default? */

                                        if (options.IsPresent("-options", ref value))
                                            boxOptions = (MessageBoxOptions)value.Value;

                                        bool help = false;

                                        if (options.IsPresent("-help"))
                                            help = true;

                                        string helpFileName = null;

                                        if (options.IsPresent("-helpfile", ref value))
                                            helpFileName = value.ToString();

                                        HelpNavigator helpNavigator = HelpNavigator.TableOfContents;

                                        if (options.IsPresent("-helpnavigator", ref value))
                                            helpNavigator = (HelpNavigator)value.Value;

                                        IObject helpParam = null;

                                        if (options.IsPresent("-helpparam", ref value))
                                            helpParam = (IObject)value.Value;

                                        if (help)
                                        {
                                            result = MessageBox.Show(null,
                                                text, caption, boxButtons, boxIcon, boxDefault,
                                                boxOptions, helpFileName, helpNavigator,
                                                (helpParam != null) ? helpParam.Value : null);
                                        }
                                        else
                                        {
                                            result = MessageBox.Show(null,
                                                text, caption, boxButtons, boxIcon, boxDefault,
                                                boxOptions);
                                        }

                                        code = ReturnCode.Ok;
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "onhook":
                        {
                            if (arguments.Count >= 2)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        new Option(typeof(HotKeyHookType),
                                            OptionFlags.MustHaveEnumValue, Index.Invalid,
                                            Index.Invalid, "-type",
                                            new Variant(HotKeyHookType.Default)),
                                        new Option(null, OptionFlags.MustHaveValue,
                                            Index.Invalid, Index.Invalid, "-text", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-set", null),
                                        Option.CreateEndOfOptions()
                                    });

                                    int argumentIndex = Index.Invalid;

                                    if (arguments.Count > 2)
                                    {
                                        code = interpreter.GetOptions(options, arguments, 0, 2,
                                            Index.Invalid, true, ref argumentIndex, ref result);
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
                                            HotKeyHookType type = HotKeyHookType.Default;

                                            if (options.IsPresent("-type", ref value))
                                                type = (HotKeyHookType)value.Value;

                                            string text = null;

                                            if (options.IsPresent("-text", ref value))
                                                text = value.ToString();

                                            bool @set = false; // TODO: Good default?  Actually, yes.

                                            if (options.IsPresent("-set", ref value))
                                                @set = (bool)value.Value;

                                            if (@set)
                                            {
                                                code = hotKeyManager.SetHookScriptFor(
                                                    type, ref text, ref result);
                                            }
                                            else
                                            {
                                                code = hotKeyManager.GetHookScriptFor(
                                                    type, ref text, ref result);
                                            }

                                            if (code == ReturnCode.Ok)
                                                result = text;
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
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
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
                    case "pendingcancel":
                        {
                            if (arguments.Count == 2)
                            {
                                result = ScriptOps.IsPendingCancel();
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
                    case "previouseventdata":
                        {
                            if (arguments.Count == 2)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    code = hotKeyManager.GetPreviousEventData(ref result);
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
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
                    case "ready":
                        {
                            if (arguments.Count == 2)
                            {
                                result = Shell.Form.HaveHotKeyManager();
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
                    case "register":
                        {
                            if (arguments.Count == 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IHotKey hotKey = null;

                                    code = hotKeyManager.GetHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref hotKey, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                        code = hotKey.Register(ref result); /* throw */
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "remove":
                        {
                            if (arguments.Count == 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    code = hotKeyManager.RemoveHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref result); /* throw */
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "resource":
                        {
                            if (arguments.Count == 3)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    string value = plugin.GetString(
                                        interpreter, arguments[2],
                                        interpreter.CultureInfo,
                                        ref result);

                                    if (value != null)
                                    {
                                        result = value;
                                        code = ReturnCode.Ok;
                                    }
                                    else
                                    {
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
                                    "wrong # args: should be \"{0} {1} name\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "result":
                        {
                            if (arguments.Count == 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IHotKey hotKey = null;

                                    code = hotKeyManager.GetHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref hotKey, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                    {
                                        /* NO RESULT */
                                        hotKey.ResultToInterpreter(
                                            interpreter, ref code, ref result); /* throw */
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "root":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                string directory;

                                if (arguments.Count == 3)
                                {
                                    directory = arguments[2];

                                    if (String.IsNullOrEmpty(directory))
                                        directory = null;

                                    /* NO RESULT */
                                    Shell.Form.SetHotKeyRootDirectory(directory);
                                }

                                directory = Shell.Form.GetHotKeyRootDirectory();

                                if (directory == null)
                                {
                                    /* TODO: Good default? */
                                    directory = Environment.GetFolderPath(
                                        Environment.SpecialFolder.ProgramFiles);
                                }

                                result = directory;
                                code = ReturnCode.Ok;
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?directory?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "save":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    bool strict = false; /* TODO: Good default? */

                                    if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                    {
                                        code = Value.GetBoolean2(
                                            arguments[2], ValueFlags.AnyBoolean,
                                            interpreter.CultureInfo, ref strict,
                                            ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        string text = null;

                                        code = hotKeyManager.SaveHotKeys(
                                            strict, ref text, ref result); /* throw */

                                        if (code == ReturnCode.Ok)
                                            result = text;
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?strict?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "script":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-varname", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-readonly", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-isolated", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(options, arguments, 0, 2,
                                    Index.Invalid, true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        string varName = null;

                                        if (options.IsPresent("-varname", ref value))
                                            varName = value.ToString();

                                        bool readOnly = false;

                                        if (options.IsPresent("-readonly"))
                                            readOnly = true;

                                        bool isolated = false;

                                        if (options.IsPresent("-isolated"))
                                            isolated = true;

                                        string text = arguments[argumentIndex];

                                        code = ScriptEditForm.ShowEditor(
                                            null, interpreter, varName, FormId.GetNext(),
                                            readOnly, isolated, ref text, ref result);

                                        if (code == ReturnCode.Ok)
                                            result = text;
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
                                                "wrong # args: should be \"{0} {1} ?options? text\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? text\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "secret":
                        {
                            if (arguments.Count >= 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        new Option(null, OptionFlags.MustHaveValue,
                                            Index.Invalid, Index.Invalid, "-varname", null),
                                        new Option(null, OptionFlags.None, Index.Invalid,
                                            Index.Invalid, "-readonly", null),
                                        new Option(null, OptionFlags.None, Index.Invalid,
                                            Index.Invalid, "-visible", null),
                                        new Option(null, OptionFlags.None, Index.Invalid,
                                            Index.Invalid, "-copy", null),
                                        Option.CreateEndOfOptions()
                                    });

                                    int argumentIndex = Index.Invalid;

                                    code = interpreter.GetOptions(options, arguments, 0, 2,
                                        Index.Invalid, true, ref argumentIndex, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if ((argumentIndex != Index.Invalid) &&
                                            ((argumentIndex + 1) == arguments.Count))
                                        {
                                            IVariant value = null;
                                            string varName = null;

                                            if (options.IsPresent("-varname", ref value))
                                                varName = value.ToString();

                                            bool readOnly = false;

                                            if (options.IsPresent("-readonly"))
                                                readOnly = true;

                                            bool visible = false;

                                            if (options.IsPresent("-visible"))
                                                visible = true;

                                            bool copy = false;

                                            if (options.IsPresent("-copy"))
                                                copy = true;

                                            string text = arguments[argumentIndex];

                                            if (copy)
                                            {
                                                try
                                                {
                                                    if (WinFormsOps.CopyTextToClipboard(
                                                            hotKeyManager as Form, text, true) ||
                                                        (text == null))
                                                    {
                                                        result = String.Empty;
                                                        code = ReturnCode.Ok;
                                                    }
                                                    else
                                                    {
                                                        result = "could not copy text to clipboard";
                                                        code = ReturnCode.Error;
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    result = e;
                                                    code = ReturnCode.Error;
                                                }
                                            }
                                            else
                                            {
                                                code = SecretEditForm.ShowEditor(
                                                    null, interpreter, varName, FormId.GetNext(),
                                                    readOnly, visible, ref text, ref result);

                                                if (code == ReturnCode.Ok)
                                                    result = text;
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
                                                    "wrong # args: should be \"{0} {1} ?options? text\"",
                                                    this.Name, subCommand);
                                            }

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? text\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "selectdirectory":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-strict", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-directory", null),
                                    new Option(typeof(Environment.SpecialFolder),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-rootfolder",
                                        new Variant(CommonOps.DefaultSpecialFolder)),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-description", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                if (arguments.Count > 2)
                                {
                                    code = interpreter.GetOptions(options, arguments, 0, 2,
                                        Index.Invalid, true, ref argumentIndex, ref result);
                                }
                                else
                                {
                                    code = ReturnCode.Ok;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        bool strict = false;

                                        if (options.IsPresent("-strict"))
                                            strict = true;

                                        IVariant value = null;
                                        string directory = null;

                                        if (options.IsPresent("-directory", ref value))
                                            directory = value.ToString();

                                        Environment.SpecialFolder rootFolder =
                                            CommonOps.DefaultSpecialFolder;

                                        if (options.IsPresent("-rootfolder", ref value))
                                            rootFolder = (Environment.SpecialFolder)value.Value;

                                        string description = null;

                                        if (options.IsPresent("-description", ref value))
                                            description = value.ToString();

                                        directory = WinFormsOps.SelectDirectory(
                                            description, rootFolder, directory);

                                        if (!strict || (directory != null))
                                        {
                                            result = directory;
                                            code = ReturnCode.Ok;
                                        }
                                        else
                                        {
                                            result = "no directory was selected";
                                            code = ReturnCode.Error;
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "selectfile":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-save", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-strict", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-directory", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-filename", null),
                                    new Option(typeof(Environment.SpecialFolder),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-rootfolder",
                                        new Variant(CommonOps.DefaultSpecialFolder)),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-filter", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-title", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                if (arguments.Count > 2)
                                {
                                    code = interpreter.GetOptions(options, arguments, 0, 2,
                                        Index.Invalid, true, ref argumentIndex, ref result);
                                }
                                else
                                {
                                    code = ReturnCode.Ok;
                                }

                                if (code == ReturnCode.Ok)
                                {
                                    if (argumentIndex == Index.Invalid)
                                    {
                                        bool save = false;

                                        if (options.IsPresent("-save"))
                                            save = true;

                                        bool strict = false;

                                        if (options.IsPresent("-strict"))
                                            strict = true;

                                        IVariant value = null;
                                        Environment.SpecialFolder rootFolder = CommonOps.DefaultSpecialFolder;

                                        if (options.IsPresent("-rootfolder", ref value))
                                            rootFolder = (Environment.SpecialFolder)value.Value;

                                        string directory = (rootFolder != CommonOps.DefaultSpecialFolder) ?
                                            Environment.GetFolderPath(rootFolder) : null;

                                        if (options.IsPresent("-directory", ref value))
                                            directory = value.ToString();

                                        string fileName = null;

                                        if (options.IsPresent("-filename", ref value))
                                            fileName = value.ToString();

                                        string filter = null;

                                        if (options.IsPresent("-filter", ref value))
                                            filter = value.ToString();

                                        string title = null;

                                        if (options.IsPresent("-title", ref value))
                                            title = value.ToString();

                                        fileName = save ?
                                            WinFormsOps.SelectSaveFileName(
                                                title, filter, directory, fileName) :
                                            WinFormsOps.SelectOpenFileName(
                                                title, filter, directory, fileName);

                                        if (!strict || (fileName != null))
                                        {
                                            result = fileName;
                                            code = ReturnCode.Ok;
                                        }
                                        else
                                        {
                                            result = "no file was selected";
                                            code = ReturnCode.Error;
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "selectitem":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-title", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-executable", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-all", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-multiple", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-lists", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-duplicates", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-item", null),
                                    new Option(null, OptionFlags.MustHaveValue, Index.Invalid,
                                        Index.Invalid, "-idvarname", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(options, arguments, 0, 2,
                                    Index.Invalid, true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        string title = null;

                                        if (options.IsPresent("-title", ref value))
                                            title = value.ToString();

                                        bool executable = false;

                                        if (options.IsPresent("-executable"))
                                            executable = true;

                                        bool all = false;

                                        if (options.IsPresent("-all"))
                                            all = true;

                                        bool multiple = false;

                                        if (options.IsPresent("-multiple"))
                                            multiple = true;

                                        bool lists = false;

                                        if (options.IsPresent("-lists"))
                                            lists = true;

                                        bool duplicates = false;

                                        if (options.IsPresent("-duplicates"))
                                            duplicates = true;

                                        string item = null;

                                        if (options.IsPresent("-item", ref value))
                                            item = value.ToString();

                                        string varName = null;

                                        if (options.IsPresent("-idvarname", ref value))
                                            varName = value.ToString();

                                        ElementDictionary elements = null;
                                        StringList list = null;

                                        code = Parser.SplitList(
                                            interpreter, arguments[argumentIndex], 0,
                                            Length.Invalid, true, ref list, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            elements = new ElementDictionary();

                                            foreach (string element in list)
                                            {
                                                string key;
                                                StringElementData data;

                                                if (lists)
                                                {
                                                    StringList subList = null;

                                                    code = Parser.SplitList(
                                                        interpreter, element, 0,
                                                        Length.Invalid, true,
                                                        ref subList, ref result);

                                                    if (code != ReturnCode.Ok)
                                                        break;

                                                    int count = subList.Count;

                                                    if (count == 0)
                                                        continue;

                                                    key = subList[0];

                                                    string text = key;

                                                    if (count >= 2)
                                                        text = subList[1];

                                                    string tag = null;

                                                    if (count >= 3)
                                                        tag = subList[2];

                                                    data = new StringElementData(
                                                        key, text, tag);
                                                }
                                                else
                                                {
                                                    key = element;
                                                    data = null;
                                                }

                                                if (key == null)
                                                    continue;

                                                if (!duplicates &&
                                                    elements.ContainsKey(key))
                                                {
                                                    result = String.Format(
                                                        "duplicate list item key {0}",
                                                        Utility.FormatWrapOrNull(key));

                                                    code = ReturnCode.Error;
                                                    break;
                                                }

                                                elements[key] = data;
                                            }
                                        }

                                        if (code == ReturnCode.Ok)
                                        {
                                            int id = FormId.GetNext();

                                            code = executable ?
                                                SelectListItemForm.ShowExecutableFileList(
                                                    null, interpreter, varName, id, elements,
                                                    all, ref item, ref result) :
                                                SelectListItemForm.ShowItemList(
                                                    null, interpreter, varName, id, title,
                                                    elements, multiple, ref item, ref result);

                                            if (code == ReturnCode.Ok)
                                                result = item;
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
                                                "wrong # args: should be \"{0} {1} ?options? list\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? list\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "anykeys":
                    case "selectkeys":
                        {
                            bool unlimited = Utility.SystemStringEquals(subCommand, "anykeys");

                            if ((arguments.Count >= 2) && (arguments.Count <= 5))
                            {
                                Keys modifiers = Keys.None;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                {
                                    object enumValue = Utility.TryParseFlagsEnum(
                                        interpreter, typeof(Keys), modifiers.ToString(),
                                        arguments[2], interpreter.CultureInfo, true,
                                        true, true, ref result);

                                    if (enumValue is Keys)
                                        modifiers = (Keys)enumValue;
                                    else
                                        code = ReturnCode.Error;
                                }

                                Keys virtualKey = Keys.None;
                                StringList keyNames = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                {
                                    if (unlimited)
                                    {
                                        code = Parser.SplitList(
                                            interpreter, arguments[3], 0, Length.Invalid,
                                            false, ref keyNames, ref result);
                                    }
                                    else
                                    {
                                        object enumValue = Utility.TryParseFlagsEnum(
                                            interpreter, typeof(Keys), virtualKey.ToString(),
                                            arguments[3], interpreter.CultureInfo, true, true,
                                            true, ref result);

                                        if (enumValue is Keys)
                                            virtualKey = (Keys)enumValue;
                                        else
                                            code = ReturnCode.Error;
                                    }
                                }

                                string varName = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 5))
                                    varName = arguments[4];

                                if (code == ReturnCode.Ok)
                                {
                                    int id = FormId.GetNext();

                                    code = SelectHotKeyForm.ShowKeyboard(
                                        null, interpreter, varName, id, unlimited,
                                        ref modifiers, ref virtualKey, ref keyNames,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        result = StringList.MakeList(
                                            "keys", unlimited ?
                                                ((keyNames != null) ?
                                                    keyNames.ToString() : null) :
                                                WinFormsOps.GetKeysToShow(
                                                    modifiers, virtualKey),
                                            "modifiers", modifiers,
                                            "virtualKey", virtualKey);
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?modifiers? ?virtualKey? ?idVarName?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "set":
                        {
                            if (arguments.Count == 4)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IHotKey oldHotKey = null;

                                    code = hotKeyManager.GetHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref oldHotKey, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                    {
                                        IHotKey newHotKey = HotKeyOps.GetFromString(
                                            interpreter, oldHotKey.Form, oldHotKey.Handle,
                                            arguments[3], ref result); /* throw */

                                        if (newHotKey != null)
                                        {
                                            code = hotKeyManager.SetHotKey(
                                                arguments[2], interpreter.CultureInfo,
                                                newHotKey, ref result); /* throw */

                                            if (code == ReturnCode.Ok)
                                                result = newHotKey.ToList(false); /* throw */
                                        }
                                        else
                                        {
                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id hotKey\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "shutdown":
                        {
                            if (arguments.Count == 2)
                            {
                                code = Shell.Form.StopHotKeyManagerThread(
                                    this.Plugin, true, ref result);
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
                    case "startup":
                        {
                            if (arguments.Count == 2)
                            {
                                code = Shell.Form.StartHotKeyManagerThread(
                                    interpreter, this.Plugin, clientData,
                                    true, ref result);
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
                    case "status":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = new StringList();

                                list.Add("thread",
                                    Shell.Form.HaveHotKeyManagerThread() ? "yes" : "no");

                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                list.Add("manager",
                                    (hotKeyManager != null) ? "yes" : "no");

                                list.Add("plugin",
                                    Shell.Form.HaveHotKeyPlugin() ? "yes" : "no");

                                list.Add("isolated",
                                    Shell.Form.IsHotKeyIsolated(interpreter) ? "yes" : "no");

                                list.Add("rootDirectory", Shell.Form.GetHotKeyRootDirectory());

                                if (hotKeyManager != null)
                                {
                                    int count = 0;
                                    Result localResult = null;

                                    if (hotKeyManager.CountHotKeys(false,
                                            ref count, ref localResult) == ReturnCode.Ok)
                                    {
                                        list.Add("totalCount", count.ToString());
                                    }
                                    else
                                    {
                                        list.Add("totalCount", localResult);
                                    }

                                    if (hotKeyManager.CountHotKeys(true,
                                            ref count, ref localResult) == ReturnCode.Ok)
                                    {
                                        list.Add("registeredCount", count.ToString());
                                    }
                                    else
                                    {
                                        list.Add("registeredCount", localResult);
                                    }
                                }

                                result = list;
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
                    case "template":
                        {
                            if (arguments.Count >= 2)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(typeof(HotKeyTemplateType),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-templatetype",
                                        new Variant(HotKeyTemplateType.None)),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-user", null),
                                    new Option(null, OptionFlags.None, Index.Invalid,
                                        Index.Invalid, "-strict", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                if (arguments.Count > 2)
                                {
                                    code = interpreter.GetOptions(options, arguments, 0, 2,
                                        Index.Invalid, true, ref argumentIndex, ref result);
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
                                        HotKeyTemplateType templateType = HotKeyTemplateType.None;

                                        if (options.IsPresent("-templatetype", ref value))
                                            templateType = (HotKeyTemplateType)value.Value;

                                        bool user = false;

                                        if (options.IsPresent("-user"))
                                            user = true;

                                        bool strict = false;

                                        if (options.IsPresent("-strict"))
                                            strict = true;

                                        if (templateType != HotKeyTemplateType.None)
                                        {
                                            result = TemplateOps.GetFileName(
                                                templateType, user, strict);
                                        }
                                        else
                                        {
                                            result = TemplateOps.GetDirectory();
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
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1}\" ?options?",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "title":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 3))
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    if ((code == ReturnCode.Ok) && (arguments.Count == 3))
                                        hotKeyManager.Title = arguments[2];

                                    if (code == ReturnCode.Ok)
                                        result = hotKeyManager.Title;
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?text?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "unregister":
                        {
                            if (arguments.Count == 3)
                            {
                                IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

                                if (hotKeyManager != null)
                                {
                                    IHotKey hotKey = null;

                                    code = hotKeyManager.GetHotKey(
                                        arguments[2], interpreter.CultureInfo,
                                        ref hotKey, ref result); /* throw */

                                    if (code == ReturnCode.Ok)
                                    {
                                        code = hotKey.Unregister(
                                            ref result); /* throw */
                                    }
                                }
                                else
                                {
                                    result = "invalid hot-key manager";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} id\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "view":
                        {
                            if ((arguments.Count >= 2) && (arguments.Count <= 4))
                            {
                                string varName = null;

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 3))
                                    varName = arguments[2];

                                bool advanced = false; /* TODO: Good default? */

                                if ((code == ReturnCode.Ok) && (arguments.Count >= 4))
                                {
                                    code = Value.GetBoolean2(
                                        arguments[3], ValueFlags.AnyBoolean,
                                        interpreter.CultureInfo, ref advanced,
                                        ref result);
                                }

                                int id = FormId.GetNext();

                                code = HotKeyViewForm.ShowViewer(
                                    null, interpreter, varName, id, advanced,
                                    ref result);
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?idVarName? ?advanced?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "wait":
                        {
                            if (arguments.Count == 3)
                            {
                                int timeout = 0;

                                code = Value.GetInteger2(
                                    (IGetValue)arguments[2], ValueFlags.AnyInteger,
                                    interpreter.CultureInfo, ref timeout, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    code = Shell.Form.WaitHotKeyManager(
                                        interpreter, timeout, false, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = Shell.Form.HaveHotKeyManager();
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} milliseconds\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "yesno":
                        {
                            if (arguments.Count >= 3)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveBooleanValue,
                                        Index.Invalid, Index.Invalid, "-cancel", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(options, arguments, 0, 2,
                                    Index.Invalid, true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) == arguments.Count))
                                    {
                                        IVariant value = null;
                                        bool cancel = false;

                                        if (options.IsPresent("-cancel", ref value))
                                            cancel = (bool)value.Value;

                                        if (cancel)
                                        {
                                            result = WinFormsOps.YesNoOrCancel(
                                                null, arguments[argumentIndex]);
                                        }
                                        else
                                        {
                                            result = WinFormsOps.YesOrNo(
                                                null, arguments[argumentIndex]);
                                        }

                                        code = ReturnCode.Ok;
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
                                                "wrong # args: should be \"{0} {1} ?options? text\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? text\"",
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
