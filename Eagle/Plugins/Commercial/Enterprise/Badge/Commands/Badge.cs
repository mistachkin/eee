/*
 * Badge.cs --
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

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Badge.Components.Private;
using _Commands = Eagle._Commands;

namespace Badge.Commands
{
    /// <summary>
    /// Implements the <c>badge</c> ensemble command, the single script-visible
    /// command exposed by the Badge plugin.  Its sub-commands query plugin and
    /// certificate information and get, set, list, and clear the plugin's
    /// override strings (which augment or replace the embedded resource
    /// strings used to service licensing).  The command is marked unsafe and
    /// belongs to the "certificateManagement" object group.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a84206de-265d-4631-a0d3-0eb6e03b7ea7")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("certificateManagement")]
    internal sealed class Badge : _Commands.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Badge" /> command
        /// class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure the command, such as its
        /// name, flags, and associated plugin.
        /// </param>
        public Badge(
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
            "about", "certificate", "clearstrings", "enable",
            "getstring", "isolated", "liststrings", "names",
            "nullstring", "options", "removestring",
            "renullstring", "resetstring", "setstring",
            "test"
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
        /// Executes the <c>badge</c> command.  The first argument selects the
        /// sub-command, which is first dispatched through the ensemble's
        /// policy-aware <c>Utility.TryExecuteSubCommandFromEnsemble</c>
        /// resolver; if that does not handle it, the built-in sub-commands are
        /// dispatched here, most of which are forwarded to the plugin's
        /// request handler.  An unknown sub-command yields an error.
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
                    case "clearstrings":
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object request = new string[] {
                                        subCommand
                                    };

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = (int)response;
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
                    case "enable":
                        {
                            if ((arguments.Count == 2) || (arguments.Count == 3))
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object request;

                                    if (arguments.Count == 3)
                                    {
                                        request = new string[] {
                                            subCommand, arguments[2]
                                        };
                                    }
                                    else
                                    {
                                        request = new string[] {
                                            subCommand
                                        };
                                    }

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = (bool)response;
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
                                    "wrong # args: should be \"{0} {1} ?enabled?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "getstring":
                        {
                            if (arguments.Count == 3)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object request = new string[] {
                                        subCommand, arguments[2]
                                    };

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        string value = (string)response;

                                        if (value != null)
                                        {
                                            result = value;
                                        }
                                        else
                                        {
                                            result = "string is null";
                                            code = ReturnCode.Error;
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
                                    "wrong # args: should be \"{0} {1} name\"",
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
                    case "liststrings":
                        {
                            if (arguments.Count == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object request = new string[] {
                                        subCommand
                                    };

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = (StringList)response;
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
                    case "names":
                        {
                            if (arguments.Count == 2)
                            {
                                StringList list = null;

                                code = Utility.GetResourceNames(
                                    this.Plugin, null, null, ref list,
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
                    case "removestring":
                        {
                            if (arguments.Count == 3)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object request = new string[] {
                                        subCommand, arguments[2]
                                    };

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = (string)response;
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
                    case "nullstring":
                    case "renullstring":
                    case "resetstring":
                    case "setstring":
                        {
                            bool nullString = Utility.SystemStringEquals(
                                subCommand, "nullstring");

                            bool renullString = Utility.SystemStringEquals(
                                subCommand, "renullstring");

                            if (((nullString || renullString) &&
                                    (arguments.Count == 3)) ||
                                ((!nullString && !renullString) &&
                                    (arguments.Count == 4)))
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    string value = (nullString || renullString) ?
                                        null : arguments[3];

                                    object request = new string[] {
                                        subCommand, arguments[2],
                                        value
                                    };

                                    object response = null;

                                    code = plugin.Execute(
                                        interpreter, clientData, request,
                                        ref response, ref result);

                                    if (code == ReturnCode.Ok)
                                        result = (bool)response;
                                }
                                else
                                {
                                    result = "invalid command plugin";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                if (nullString || renullString)
                                {
                                    result = String.Format(
                                        "wrong # args: should be \"{0} {1} name\"",
                                        this.Name, subCommand);
                                }
                                else
                                {
                                    result = String.Format(
                                        "wrong # args: should be \"{0} {1} name value\"",
                                        this.Name, subCommand);
                                }

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "test":
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
                                    }
                                    else
                                    {
                                        result = "string not found";
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

            return code;
        }
        #endregion
    }
}
