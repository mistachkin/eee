/*
 * Demo.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;

#if OBFUSCATION
using System.Reflection;
#endif

#if NATIVE && WINDOWS
using System.Threading;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Demo.Components.Private;
using Demo.Interfaces.Public;
using _Commands = Eagle._Commands;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Demo.Commands
{
    /// <summary>
    /// Implements the demo ensemble command, which inspects and controls
    /// the demo host installed by the demo plugin.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("90a4ec5c-ef8c-46e4-aceb-a19a09d839f2")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("managedEnvironment")]
    internal sealed class Demo : _Commands.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Demo" /> command class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure the command.
        /// </param>
        public Demo(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Gets the demo host from the command's plugin.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The demo host, or null upon failure.
        /// </returns>
        private IDemoHost GetDemoHost(
            ref Result error /* out */
            )
        {
            IDemoPlugin demoPlugin = this.Plugin as IDemoPlugin;

            if (demoPlugin == null)
            {
                error = "invalid demo plugin";
                return null;
            }

            IDemoHost demoHost = demoPlugin.DemoHost;

            if (demoHost == null)
                error = "invalid demo host";

            return demoHost;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-commands supported by this ensemble command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
                "about", "active", "basereadline", "beep", "cancel",
                "certificate", "closed", "debuglevel", "endofstream",
                "isolated", "options", "pause", "playmilliseconds",
                "reset", "shutdown", "startup", "stop",
                "stopmilliseconds", "timeoutmilliseconds"
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

        #region IExecute Members
        /// <summary>
        /// Executes the demo command, dispatching to the requested
        /// sub-command.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter executing the command.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The arguments to the command, including the sub-command name.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the command result or an error message.
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
            ReturnCode code;

            if (interpreter != null)
            {
                if (arguments != null)
                {
                    if (arguments.Count >= 2)
                    {
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
                                case "active":
                                    {
                                        if (arguments.Count == 2)
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    result = demoHost.PlayActive;
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
                                                "wrong # args: should be \"{0} {1}\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "basereadline":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool baseReadLine = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref baseReadLine, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.FailOnBaseReadLine = baseReadLine;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.FailOnBaseReadLine;
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
                                                "wrong # args: should be \"{0} {1} ?basereadline?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "beep":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool beep = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref beep, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.PlayPauseBeep = beep;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.PlayPauseBeep;
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
                                                "wrong # args: should be \"{0} {1} ?beep?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "cancel":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool cancel = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref cancel, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.StopOnCancel = cancel;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.StopOnCancel;
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
                                                "wrong # args: should be \"{0} {1} ?cancel?\"",
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
                                case "closed":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool closed = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref closed, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.ClosedOnInactive = closed;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.ClosedOnInactive;
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
                                                "wrong # args: should be \"{0} {1} ?closed?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "debuglevel":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        int level = Level.Invalid;

                                                        code = Value.GetInteger2(
                                                            (IGetValue)arguments[2],
                                                            ValueFlags.AnyInteger,
                                                            interpreter.CultureInfo,
                                                            ref level, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.PlayDebugLevel = level;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.PlayDebugLevel;
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
                                                "wrong # args: should be \"{0} {1} ?level?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "endofstream":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool endOfStream = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref endOfStream, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.StopOnEndOfStream = endOfStream;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.StopOnEndOfStream;
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
                                                "wrong # args: should be \"{0} {1} ?endofstream?\"",
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
                                case "pause":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        bool pause = false;

                                                        code = Value.GetBoolean2(
                                                            arguments[2], ValueFlags.AnyBoolean,
                                                            interpreter.CultureInfo,
                                                            ref pause, ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.PlayUsePause = pause;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.PlayUsePause;
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
                                                "wrong # args: should be \"{0} {1} ?pause?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "playmilliseconds":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        int playMilliseconds = 0;

                                                        code = Value.GetInteger2(
                                                            (IGetValue)arguments[2], ValueFlags.AnyInteger,
                                                            interpreter.CultureInfo, ref playMilliseconds,
                                                            ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.PlayMilliseconds = playMilliseconds;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.PlayMilliseconds;
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
                                                "wrong # args: should be \"{0} {1} ?milliseconds?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "reset":
                                    {
                                        if (arguments.Count == 2)
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                CommonOps.ResetDemoSettings(demoHost);

                                                result = String.Empty;
                                                code = ReturnCode.Ok;
                                            }
                                            else
                                            {
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
                                case "shutdown":
                                    {
                                        if (arguments.Count >= 2)
                                        {
                                            OptionDictionary options = new OptionDictionary(
                                                new IOption[] {
                                                new Option(null, OptionFlags.MustHaveIntegerValue,
                                                    Index.Invalid, Index.Invalid, "-stopmilliseconds", null),
#if NATIVE && WINDOWS
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-native", null),
#else
                                                new Option(null, OptionFlags.MustHaveBooleanValue |
                                                    OptionFlags.Unsupported, Index.Invalid,
                                                    Index.Invalid, "-native", null),
#endif
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-exit", null),
                                                Option.CreateEndOfOptions()
                                            });

                                            int argumentIndex = Index.Invalid;

                                            if (arguments.Count > 2)
                                                code = interpreter.GetOptions(
                                                    options, arguments, 0, 2, Index.Invalid, true,
                                                    ref argumentIndex, ref result);
                                            else
                                                code = ReturnCode.Ok;

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (argumentIndex == Index.Invalid)
                                                {
                                                    IVariant value = null;
                                                    int? stopMilliseconds = Defaults.StopMilliseconds;

                                                    if (options.IsPresent("-stopmilliseconds", ref value))
                                                        stopMilliseconds = (int)value.Value;

                                                    bool? native = Defaults.Native;

                                                    if (options.IsPresent("-native", ref value))
                                                        native = (bool)value.Value;

                                                    bool? exit = Defaults.Exit;

                                                    if (options.IsPresent("-exit", ref value))
                                                        exit = (bool)value.Value;

                                                    IDemoHost demoHost = GetDemoHost(ref result);

                                                    if (demoHost != null)
                                                    {
                                                        if ((native != null) && (bool)native)
                                                        {
#if TEST && NATIVE && WINDOWS
                                                            EventWaitHandle stopEvent;
                                                            EventWaitHandle doneEvent;

                                                            lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                            {
                                                                stopEvent = demoHost.PlayStopEvent;
                                                                doneEvent = demoHost.PlayDoneEvent;
                                                            }

                                                            code = CommonOps.DemoSignalAndOrWait(
                                                                stopEvent, doneEvent, stopMilliseconds,
                                                                ref result);
#else
                                                            result = "not implemented";
                                                            code = ReturnCode.Error;
#endif
                                                        }
                                                        else
                                                        {
                                                            CommonOps.DemoShutdown(demoHost, true, false);

                                                            result = String.Empty;
                                                            code = ReturnCode.Ok;
                                                        }

                                                        if ((code == ReturnCode.Ok) &&
                                                            (exit != null) && (bool)exit)
                                                        {
                                                            //
                                                            // HACK: Attempt to terminate
                                                            //       interactive loop now.
                                                            //
                                                            interpreter.Exit = true;
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
                                case "startup":
                                    {
                                        if (arguments.Count >= 2)
                                        {
                                            OptionDictionary options = new OptionDictionary(
                                                new IOption[] {
                                                new Option(null, OptionFlags.MustHaveValue,
                                                    Index.Invalid, Index.Invalid, "-path", null),
                                                new Option(null, OptionFlags.MustHaveIntegerValue,
                                                    Index.Invalid, Index.Invalid, "-playmilliseconds", null),
                                                new Option(null, OptionFlags.MustHaveIntegerValue,
                                                    Index.Invalid, Index.Invalid, "-stopmilliseconds", null),
                                                new Option(null, OptionFlags.MustHaveIntegerValue,
                                                    Index.Invalid, Index.Invalid, "-timeoutmilliseconds", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-pause", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-beep", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-cancel", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-endofstream", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-basereadline", null),
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-closed", null),
#if NATIVE && WINDOWS
                                                new Option(null, OptionFlags.MustHaveBooleanValue,
                                                    Index.Invalid, Index.Invalid, "-native", null),
#else
                                                new Option(null, OptionFlags.MustHaveBooleanValue |
                                                    OptionFlags.Unsupported, Index.Invalid,
                                                    Index.Invalid, "-native", null),
#endif
                                                Option.CreateEndOfOptions()
                                            });

                                            int argumentIndex = Index.Invalid;

                                            if (arguments.Count > 2)
                                                code = interpreter.GetOptions(
                                                    options, arguments, 0, 2, Index.Invalid, true,
                                                    ref argumentIndex, ref result);
                                            else
                                                code = ReturnCode.Ok;

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (argumentIndex == Index.Invalid)
                                                {
                                                    IVariant value = null;
                                                    string path = Defaults.FileName;

                                                    if (options.IsPresent("-path", ref value))
                                                        path = value.ToString();

                                                    int? playMilliseconds = Defaults.PlayMilliseconds;

                                                    if (options.IsPresent("-playmilliseconds", ref value))
                                                        playMilliseconds = (int)value.Value;

                                                    int? stopMilliseconds = Defaults.StopMilliseconds;

                                                    if (options.IsPresent("-stopmilliseconds", ref value))
                                                        stopMilliseconds = (int)value.Value;

                                                    int? timeoutMilliseconds = Defaults.TimeoutMilliseconds;

                                                    if (options.IsPresent("-timeoutmilliseconds", ref value))
                                                        timeoutMilliseconds = (int)value.Value;

                                                    bool? pause = Defaults.PlayUsePause;

                                                    if (options.IsPresent("-pause", ref value))
                                                        pause = (bool)value.Value;

                                                    bool? beep = Defaults.PlayPauseBeep;

                                                    if (options.IsPresent("-beep", ref value))
                                                        beep = (bool)value.Value;

                                                    bool? cancel = Defaults.StopOnCancel;

                                                    if (options.IsPresent("-cancel", ref value))
                                                        cancel = (bool)value.Value;

                                                    bool? endOfStream = Defaults.StopOnEndOfStream;

                                                    if (options.IsPresent("-endofstream", ref value))
                                                        endOfStream = (bool)value.Value;

                                                    bool? baseReadLine = Defaults.FailOnBaseReadLine;

                                                    if (options.IsPresent("-basereadline", ref value))
                                                        baseReadLine = (bool)value.Value;

                                                    bool? closed = Defaults.ClosedOnInactive;

                                                    if (options.IsPresent("-closed", ref value))
                                                        closed = (bool)value.Value;

                                                    bool? native = Defaults.Native;

                                                    if (options.IsPresent("-native", ref value))
                                                        native = (bool)value.Value;

                                                    if (!String.IsNullOrEmpty(path))
                                                    {
                                                        TextReader textReader = null;

                                                        code = CommonOps.GetDemoStream(
                                                            interpreter, path, ref textReader, ref result);

                                                        if (code == ReturnCode.Ok)
                                                        {
                                                            IDemoHost demoHost = GetDemoHost(ref result);

                                                            if (demoHost != null)
                                                            {
                                                                if ((native != null) && (bool)native)
                                                                {
#if TEST && NATIVE && WINDOWS
                                                                    EventWaitHandle stopEvent;
                                                                    EventWaitHandle doneEvent;

                                                                    lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                                    {
                                                                        stopEvent = demoHost.PlayStopEvent;
                                                                        doneEvent = demoHost.PlayDoneEvent;
                                                                    }

                                                                    code = Eagle._Tests.Default.TestStartKeyboardStream(
                                                                        playMilliseconds, textReader, stopEvent, doneEvent,
                                                                        SimulatedKeyFlags.Default | SimulatedKeyFlags.ConsoleOnly,
                                                                        ref result);
#else
                                                                    result = "not implemented";
                                                                    code = ReturnCode.Error;
#endif
                                                                }
                                                                else
                                                                {
                                                                    lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                                    {
                                                                        CommonOps.InitializeDemoSettings(
                                                                            demoHost, playMilliseconds, pause,
                                                                            beep, stopMilliseconds, cancel,
                                                                            endOfStream, baseReadLine, closed,
                                                                            timeoutMilliseconds, false);

                                                                        demoHost.PlayInput = textReader;
                                                                    }

                                                                    demoHost.RefreshTimeout();

                                                                    result = String.Empty;
                                                                    code = ReturnCode.Ok;
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
                                                        result = "invalid path";
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
                                case "stop":
                                    {
                                        if (arguments.Count >= 2)
                                        {
                                            OptionDictionary options = new OptionDictionary(
                                                new IOption[] {
                                                new Option(null, OptionFlags.MustHaveIntegerValue,
                                                    Index.Invalid, Index.Invalid, "-stopmilliseconds", null),
                                                Option.CreateEndOfOptions()
                                            });

                                            int argumentIndex = Index.Invalid;

                                            if (arguments.Count > 2)
                                                code = interpreter.GetOptions(
                                                    options, arguments, 0, 2, Index.Invalid, true,
                                                    ref argumentIndex, ref result);
                                            else
                                                code = ReturnCode.Ok;

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (argumentIndex == Index.Invalid)
                                                {
                                                    IVariant value = null;
                                                    int stopMilliseconds = Defaults.StopMilliseconds;

                                                    if (options.IsPresent("-stopmilliseconds", ref value))
                                                        stopMilliseconds = (int)value.Value;

                                                    IDemoHost demoHost = GetDemoHost(ref result);

                                                    if (demoHost != null)
                                                        code = demoHost.Stop(stopMilliseconds, ref result);
                                                    else
                                                        code = ReturnCode.Error;
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
                                case "stopmilliseconds":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        int stopMilliseconds = 0;

                                                        code = Value.GetInteger2(
                                                            (IGetValue)arguments[2], ValueFlags.AnyInteger,
                                                            interpreter.CultureInfo, ref stopMilliseconds,
                                                            ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.StopMilliseconds = stopMilliseconds;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.StopMilliseconds;
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
                                                "wrong # args: should be \"{0} {1} ?milliseconds?\"",
                                                this.Name, subCommand);

                                            code = ReturnCode.Error;
                                        }
                                        break;
                                    }
                                case "timeoutmilliseconds":
                                    {
                                        if ((arguments.Count == 2) || (arguments.Count == 3))
                                        {
                                            IDemoHost demoHost = GetDemoHost(ref result);

                                            if (demoHost != null)
                                            {
                                                lock (demoHost.PlaySyncRoot) /* TRANSACTIONAL */
                                                {
                                                    if (arguments.Count == 3)
                                                    {
                                                        int timeoutMilliseconds = 0;

                                                        code = Value.GetInteger2(
                                                            (IGetValue)arguments[2], ValueFlags.AnyInteger,
                                                            interpreter.CultureInfo, ref timeoutMilliseconds,
                                                            ref result);

                                                        if (code == ReturnCode.Ok)
                                                            demoHost.TimeoutMilliseconds = timeoutMilliseconds;
                                                    }

                                                    if (code == ReturnCode.Ok)
                                                        result = demoHost.TimeoutMilliseconds;
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
                                                "wrong # args: should be \"{0} {1} ?milliseconds?\"",
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
                    }
                    else
                    {
                        result = String.Format(
                            "wrong # args: should be \"{0} option ?arg ...?\"",
                            this.Name);

                        code = ReturnCode.Error;
                    }
                }
                else
                {
                    result = "invalid argument list";

                    code = ReturnCode.Error;
                }
            }
            else
            {
                result = "invalid interpreter";

                code = ReturnCode.Error;
            }

            return code;
        }
        #endregion
    }
}
