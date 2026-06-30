/*
 * CompletionOps.cs --
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
using _Interfaces = Eagle._Interfaces.Public;
using ICommand = Eagle._Interfaces.Public.ICommand;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Provides the interactive tab-completion logic for the windowed
    /// shell: argument expansion, command/sub-command/procedure/function
    /// matching, object type and member introspection, and option-name
    /// completion via the universal option introspection in the Eagle
    /// core library.
    /// </summary>
    [ObjectId("700d76dd-2187-4673-a083-75e1c78e9b45")]
    internal static class CompletionOps
    {
        #region Private CompletionRequest Helper Class
        /// <summary>
        /// Carries the mutable state for a single tab-completion request
        /// as it flows through the completion strategies.
        /// </summary>
        [ObjectId("1fa410f6-ca45-4e42-b572-de0187a1815c")]
        private sealed class CompletionRequest
        {
            #region Public Fields
            /// <summary>
            /// The interpreter used to resolve and match candidates.
            /// </summary>
            public Interpreter Interpreter;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Non-zero to match names case-insensitively.
            /// </summary>
            public bool NoCase;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The parsed input arguments being completed.
            /// </summary>
            public StringList Arguments;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The expanded forms of the leading arguments, used for display.
            /// </summary>
            public StringList NewArguments;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The expanded command name (the first argument).
            /// </summary>
            public string CommandName;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The expanded sub-command name (the second argument).
            /// </summary>
            public string SubCommandName;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The expanded type or object name (the third argument).
            /// </summary>
            public string TypeOrObjectName;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The expanded member name (the fourth argument).
            /// </summary>
            public string MemberName;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// For each leading argument, non-zero if it was empty.
            /// </summary>
            public bool[] Empty;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Non-zero to treat the object handle as a non-alias, forcing
            /// member completion against its underlying type.
            /// </summary>
            public bool NonAlias;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The candidate matches produced by the completion strategies.
            /// </summary>
            public StringList Matches;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// A short description of what kind of matches were produced.
            /// </summary>
            public string MatchType;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Non-zero if interactive command help should be shown instead
            /// of a completion list.
            /// </summary>
            public bool Help;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Upon failure, the error describing the problem.
            /// </summary>
            public Result Error;
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// The maximum number of completion matches to display before the
        /// list is truncated.
        /// </summary>
        private const int MaximumAutoComplete = 30;

        /// <summary>
        /// The message appended when the completion list is truncated.
        /// </summary>
        private const string TooManyMatches =
            "... <MORE THAN {0} MATCHES> ...";

        /// <summary>
        /// The argument index of the command name.
        /// </summary>
        private const int CommandNameIndex = 0;

        /// <summary>
        /// The argument index of the sub-command name.
        /// </summary>
        private const int SubCommandNameIndex = 1;

        /// <summary>
        /// The argument index of the type or object name.
        /// </summary>
        private const int TypeOrObjectNameIndex = 2;

        /// <summary>
        /// The argument index of the member name.
        /// </summary>
        private const int MemberNameIndex = 3;

        /// <summary>
        /// What is the maximum number of arguments supported when mapping a
        /// list of arguments to the associated logical list of options?
        /// </summary>
        private const int MaximumArgumentCount = 2;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the cached types
        /// dictionary.
        /// </summary>
        private static object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The cached dictionary mapping fully qualified type names to their
        /// types, built lazily from loaded assemblies.
        /// </summary>
        private static IDictionary<string, Type> cachedTypes;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Computes the tab-completion candidates for the specified input
        /// text.  The leading arguments are expanded, then the final argument
        /// is completed by the first applicable strategy (option, interactive
        /// command, expression function, object type or member, procedure,
        /// sub-command, or command).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve and match candidates.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match names case-insensitively.
        /// </param>
        /// <param name="text">
        /// The input text to complete.
        /// </param>
        /// <param name="help">
        /// Upon return, non-zero if the caller should display interactive
        /// command help instead of a completion list.
        /// </param>
        /// <param name="display">
        /// Upon success, receives the list of candidates to display (with
        /// header lines), or null when there is nothing to show.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Complete(
            Interpreter interpreter, /* in */
            bool noCase,             /* in */
            string text,             /* in */
            ref bool help,           /* out */
            ref StringList display,  /* out */
            ref Result error         /* out */
            )
        {
            help = false;
            display = null;

            if (interpreter == null)
                return ReturnCode.Ok;

            if (text == null)
                return ReturnCode.Ok;

            text = text.Trim();

            if (text.Length == 0)
                return ReturnCode.Ok;

            StringList arguments = null;

            if (Parser.SplitList(
                    interpreter, text, 0, Length.Invalid, true,
                    ref arguments, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }

            if (arguments.Count == 0)
                return ReturnCode.Ok;

            CompletionRequest request = new CompletionRequest();

            request.Interpreter = interpreter;
            request.NoCase = noCase;
            request.Arguments = arguments;
            request.NewArguments = new StringList();
            request.Empty = new bool[] { false, false, false, false };

            //
            // NOTE: Expand the leading (command, sub-command, type/object,
            //       member) arguments before attempting any completion.
            //
            if (!ExpandLeadingArguments(request))
            {
                error = request.Error;
                return ReturnCode.Ok;
            }

            //
            // NOTE: Run the completion strategies.  When one produces a
            //       result, format it for display; otherwise, report that
            //       there were no matches.
            //
            if (TryComplete(request))
            {
                StringList matches = request.Matches;

                if ((matches != null) && (matches.Count > 0))
                    display = BuildDisplay(request);
                else
                    request.Error = "no matches";
            }

            help = request.Help;
            error = request.Error;

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether the named object exists in the interpreter and
        /// retrieves it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the object.
        /// </param>
        /// <param name="name">
        /// The name of the object to locate.
        /// </param>
        /// <param name="object">
        /// Upon success, receives the located object.
        /// </param>
        /// <returns>
        /// true if the named object was found; otherwise, false.
        /// </returns>
        private static bool IsObject(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref IObject @object      /* out */
            )
        {
            if (interpreter != null)
            {
                if ((interpreter.GetObject(
                        name, LookupFlags.Default,
                        ref @object) == ReturnCode.Ok) &&
                    (@object != null))
                {
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

#if SHELL && INTERACTIVE_COMMANDS
        /// <summary>
        /// Determines whether the named interactive command exists and
        /// retrieves its help information.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the interactive command.
        /// </param>
        /// <param name="name">
        /// The name of the interactive command to locate.
        /// </param>
        /// <param name="commandHelp">
        /// Upon success, receives the help information for the command.
        /// </param>
        /// <returns>
        /// true if exactly one matching interactive command with help was
        /// found; otherwise, false.
        /// </returns>
        private static bool IsInteractiveCommand(
            Interpreter interpreter,   /* in */
            string name,               /* in */
            ref StringPair commandHelp /* out */
            )
        {
            StringList commands = Utility.GetInteractiveCommandNames(
                interpreter, name, false);

            if ((commands != null) && (commands.Count == 1))
            {
                commandHelp = Utility.GetInteractiveCommandHelpItem(
                    interpreter, commands[0]);

                if (commandHelp != null)
                    return true;
            }

            return false;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the named command exists as a command (and not
        /// as a procedure or other executable) and retrieves it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the command.
        /// </param>
        /// <param name="name">
        /// The name of the command to locate.
        /// </param>
        /// <param name="command">
        /// Upon success, receives the located command.
        /// </param>
        /// <param name="newName">
        /// Upon success, receives the resolved command name.
        /// </param>
        /// <returns>
        /// true if the name resolves unambiguously to a command; otherwise,
        /// false.
        /// </returns>
        private static bool IsCommand(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref ICommand command,    /* out */
            ref string newName       /* out */
            )
        {
            if (interpreter != null)
            {
                IProcedure procedure = null;
                IExecute execute = null;
                bool ambiguous = false;
                long token = 0; /* NOT USED */
                Result error = null; /* NOT USED */

                if ((interpreter.MatchCommand(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref command,
                        ref error) == ReturnCode.Ok) &&
                    (command != null) && !ambiguous &&
                    (interpreter.MatchProcedure(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref procedure,
                        ref error) != ReturnCode.Ok) &&
                    (procedure == null) && !ambiguous &&
                    (interpreter.MatchIExecute(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref execute,
                        ref error) != ReturnCode.Ok) &&
                    (execute == null) && !ambiguous)
                {
                    newName = command.Name;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified command has a sub-command matching
        /// the given name and retrieves the matched name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used for pattern matching.
        /// </param>
        /// <param name="command">
        /// The command whose sub-commands are searched.
        /// </param>
        /// <param name="name">
        /// The sub-command name or pattern to match.
        /// </param>
        /// <param name="newName">
        /// Upon success, receives the resolved sub-command name.
        /// </param>
        /// <returns>
        /// true if a matching sub-command was found; otherwise, false.
        /// </returns>
        private static bool IsSubCommand(
            Interpreter interpreter, /* in */
            ICommand command,        /* in */
            string name,             /* in */
            ref string newName       /* out */
            )
        {
            if (command == null)
                return false;

            EnsembleDictionary subCommands = command.SubCommands;

            if (subCommands == null)
                return false;

            string pattern = String.Format(
                "{0}{1}", name, Characters.Asterisk);

            foreach (string subCommandName in subCommands.Keys)
            {
                if (Utility.SystemStringEquals(
                        subCommandName, name) ||
                    ((name != null) && Parser.StringMatch(
                        interpreter, subCommandName, 0,
                        pattern, 0, false)))
                {
                    newName = subCommandName;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the named command exists as a procedure (and not
        /// as a command or other executable) and retrieves it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the procedure.
        /// </param>
        /// <param name="name">
        /// The name of the procedure to locate.
        /// </param>
        /// <param name="procedure">
        /// Upon success, receives the located procedure.
        /// </param>
        /// <param name="newName">
        /// Upon success, receives the resolved procedure name.
        /// </param>
        /// <returns>
        /// true if the name resolves unambiguously to a procedure; otherwise,
        /// false.
        /// </returns>
        private static bool IsProcedure(
            Interpreter interpreter,  /* in */
            string name,              /* in */
            ref IProcedure procedure, /* out */
            ref string newName        /* out */
            )
        {
            if (interpreter != null)
            {
                ICommand command = null;
                IExecute execute = null;
                bool ambiguous = false;
                long token = 0; /* NOT USED */
                Result error = null; /* NOT USED */

                if ((interpreter.MatchProcedure(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref procedure,
                        ref error) == ReturnCode.Ok) &&
                    (procedure != null) && !ambiguous &&
                    (interpreter.MatchCommand(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref command,
                        ref error) != ReturnCode.Ok) &&
                    (command == null) && !ambiguous &&
                    (interpreter.MatchIExecute(
                        EngineFlags.None, name, LookupFlags.Default,
                        ref ambiguous, ref token, ref execute,
                        ref error) != ReturnCode.Ok) &&
                    (execute == null) && !ambiguous)
                {
                    newName = procedure.Name;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the named command resolves to the expression
        /// command.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the command.
        /// </param>
        /// <param name="commandName">
        /// The name of the command to test.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to compare names case-insensitively.
        /// </param>
        /// <returns>
        /// true if the command resolves to the expression command; otherwise,
        /// false.
        /// </returns>
        private static bool IsExpressionCommand(
            Interpreter interpreter, /* in */
            string commandName,      /* in */
            bool noCase              /* in */
            )
        {
            ICommand command = null; /* NOT USED */
            string newName = null;

            if (!IsCommand(
                    interpreter, commandName, ref command, ref newName))
            {
                return false;
            }

            //
            // HACK: Hard-coded command name here.
            //
            return Utility.SystemStringEquals(newName, "expr", noCase);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the named command resolves to the object command
        /// and the sub-command resolves to one of the create, invoke, or
        /// members sub-commands.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for the command.
        /// </param>
        /// <param name="commandName">
        /// The name of the command to test.
        /// </param>
        /// <param name="subCommandName">
        /// The name of the sub-command to test.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to compare names case-insensitively.
        /// </param>
        /// <returns>
        /// true if the command and sub-command match the recognized object
        /// sub-commands; otherwise, false.
        /// </returns>
        private static bool IsObjectCommand(
            Interpreter interpreter, /* in */
            string commandName,      /* in */
            string subCommandName,   /* in */
            bool noCase              /* in */
            )
        {
            ICommand command = null;
            string newName = null;

            if (!IsCommand(
                    interpreter, commandName, ref command, ref newName))
            {
                return false;
            }

            //
            // HACK: Hard-coded command name here.
            //
            if (!Utility.SystemStringEquals(newName, "object", noCase))
                return false;

            if (!IsSubCommand(
                    interpreter, command, subCommandName, ref newName))
            {
                return false;
            }

            //
            // HACK: Hard-coded sub-command names here.
            //
            if (!Utility.SystemStringEquals(newName, "create", noCase) &&
                !Utility.SystemStringEquals(newName, "invoke", noCase) &&
                !Utility.SystemStringEquals(newName, "members", noCase))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Expands an interactive argument, performing pound-character
        /// handling, interactive command completion, and variable
        /// substitution.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used for substitution and command matching.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match interactive command names case-insensitively.
        /// </param>
        /// <param name="argument">
        /// On input and output, the argument to expand and the resulting
        /// expanded value.
        /// </param>
        /// <param name="empty">
        /// Upon success, set to true when the argument represents a show-all
        /// request.
        /// </param>
        /// <param name="expand">
        /// Upon success, set to true when the argument value was actually
        /// changed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode ExpandArgument(
            Interpreter interpreter, /* in */
            bool noCase,             /* in */
            ref string argument,     /* in, out */
            ref bool empty,          /* out */
            ref bool expand,         /* in, out */
            ref Result error         /* out */
            )
        {
            if (argument == null)
            {
                error = "invalid argument";
                return ReturnCode.Error;
            }

            //
            // HACK: When the argument consists of a single pound
            //       character, treat it as "show all".
            //
            if ((argument.Length == 1) &&
                (argument[0] == Characters.Comment))
            {
                argument = null;
                empty = true;

                return ReturnCode.Ok;
            }

#if SHELL && INTERACTIVE_COMMANDS
            string pattern = String.Format(
                "{0}{1}", argument.Substring(1), Characters.Asterisk);

            StringList list = null;

            if ((argument.Length >= 1) &&
                (argument[0] == Characters.Comment) &&
                (GetMatchingInteractiveCommands(
                        interpreter, pattern, noCase, ref list,
                        ref error) == ReturnCode.Ok) &&
                (list != null) && (list.Count == 1))
            {
                //
                // NOTE: Did we actually expand (i.e. change) anything?
                //
                if (!Utility.SystemStringEquals(
                        argument.Substring(1), list[0]))
                {
                    expand = true;
                }

                argument = String.Format(
                    "{0}{1}", Characters.Comment, list[0]);

                return ReturnCode.Ok;
            }
#endif

            if (interpreter == null)
                return ReturnCode.Ok;

            Result result = null;

            if (interpreter.SubstituteString(
                    argument, SubstitutionFlags.Variables,
                    ref result) == ReturnCode.Ok)
            {
                //
                // NOTE: Did we actually expand (i.e. change) anything?
                //
                if (!Utility.SystemStringEquals(argument, result))
                    expand = true;

                argument = result;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if SHELL && INTERACTIVE_COMMANDS
        /// <summary>
        /// Gets the interactive command names that match the specified
        /// pattern.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to search for interactive commands.
        /// </param>
        /// <param name="pattern">
        /// The pattern used to match interactive command names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match names case-insensitively.
        /// </param>
        /// <param name="commands">
        /// Upon success, receives the list of matching command names.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode GetMatchingInteractiveCommands(
            Interpreter interpreter, /* in */
            string pattern,          /* in */
            bool noCase,             /* in */
            ref StringList commands, /* out */
            ref Result error         /* out: NOT USED */
            )
        {
            commands = Utility.GetInteractiveCommandNames(
                interpreter, pattern, noCase);

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the types whose name or full name matches the specified
        /// pattern, building and using a cached type list from the loaded
        /// assemblies.
        /// </summary>
        /// <param name="appDomain">
        /// The application domain whose assemblies are scanned, or null to use
        /// the current domain.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used for pattern matching.
        /// </param>
        /// <param name="pattern">
        /// The pattern used to match type names, or null to match all types.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match names case-insensitively.
        /// </param>
        /// <param name="types">
        /// Upon success, receives the dictionary of matching types keyed by
        /// full name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode GetMatchingTypes(
            AppDomain appDomain,                 /* in */
            Interpreter interpreter,             /* in */
            string pattern,                      /* in */
            bool noCase,                         /* in */
            ref IDictionary<string, Type> types, /* in, out */
            ref Result error                     /* out */
            )
        {
            lock (syncRoot)
            {
                if (cachedTypes == null)
                {
                    if (appDomain == null)
                        appDomain = AppDomain.CurrentDomain;

                    cachedTypes = new Dictionary<string, Type>();

                    foreach (Assembly assembly in appDomain.GetAssemblies())
                    {
                        if (assembly != null)
                        {
                            foreach (Type type in assembly.GetTypes())
                            {
                                if (type == null)
                                    continue;

                                string fullName = type.FullName;

                                if ((fullName != null) &&
                                    !cachedTypes.ContainsKey(fullName))
                                {
                                    cachedTypes.Add(fullName, type);
                                }
                            }
                        }
                    }
                }

                if (types == null)
                    types = new Dictionary<string, Type>();

                foreach (Type type in cachedTypes.Values)
                {
                    if (type == null)
                        continue;

                    string name = type.Name;
                    string fullName = type.FullName;

                    if ((name != null) && !types.ContainsKey(name) &&
                        ((pattern == null) || Utility.SystemStringEquals(
                            name, pattern, noCase) ||
                        Parser.StringMatch(
                            interpreter, name, 0, pattern, 0, noCase)))
                    {
                        types.Add(fullName, type);
                    }
                    else if ((fullName != null) &&
                        !types.ContainsKey(fullName) &&
                        ((pattern == null) || Utility.SystemStringEquals(
                            fullName, pattern, noCase) ||
                        Parser.StringMatch(
                            interpreter, fullName, 0, pattern, 0, noCase)))
                    {
                        types.Add(fullName, type);
                    }
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the members of the specified type whose name matches the
        /// specified pattern.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used for pattern matching.
        /// </param>
        /// <param name="type">
        /// The type whose members are searched.
        /// </param>
        /// <param name="pattern">
        /// The pattern used to match member names, or null to match all
        /// members.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match names case-insensitively.
        /// </param>
        /// <param name="members">
        /// Upon success, receives the dictionary of matching members keyed by
        /// name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode GetMatchingMembers(
            Interpreter interpreter,                     /* in */
            Type type,                                   /* in */
            string pattern,                              /* in */
            bool noCase,                                 /* in */
            ref IDictionary<string, MemberInfo> members, /* in, out */
            ref Result error                             /* out */
            )
        {
            if (type == null)
            {
                error = "invalid type";
                return ReturnCode.Error;
            }

            if (members == null)
                members = new Dictionary<string, MemberInfo>();

            foreach (MemberInfo memberInfo in type.GetMembers())
            {
                if (memberInfo == null)
                    continue;

                string name = memberInfo.Name;

                if (name == null)
                    continue;

                if ((name != null) && !members.ContainsKey(name) &&
                    ((pattern == null) || Utility.SystemStringEquals(
                        name, pattern, noCase) ||
                    Parser.StringMatch(
                        interpreter, name, 0, pattern, 0, noCase)))
                {
                    members.Add(name, memberInfo);
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to lookup the options that are associated with a logical
        /// list of arguments.  Generally, this will be either a command name
        /// (i.e. single argument) or a sub-command name (i.e. two arguments).
        /// Currently, this will only really work for the core library set of
        /// commands and their built-in sub-commands.  Eventually, this may be
        /// extended to support (arbitrary) plugin commands and sub-commands,
        /// via enhancements to the core library GetCommandOptions method.
        /// </summary>
        /// <param name="arguments">
        /// The parsed input arguments; all but the final one identify the
        /// command (and possibly sub-command).
        /// </param>
        /// <returns>
        /// The logical list of options or null if it cannot be found based on
        /// the specified arguments.
        /// </returns>
        private static OptionDictionary LookupOptions(
            StringList arguments /* in */
            )
        {
            if (arguments == null)
                return null;

            OptionDictionary options = null;
            int maximumCount = arguments.Count - 1;

            if (maximumCount > MaximumArgumentCount)
                maximumCount = MaximumArgumentCount;

            for (int count = maximumCount; count > 0; count--)
            {
                ArgumentList localArguments = new ArgumentList();

                for (int index = 0; index < count; index++)
                    localArguments.Add(arguments[index]);

                options = Utility.GetCommandOptions(localArguments);

                if (options != null)
                    break;
            }

            return options;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes the final argument as an option exposed by the
        /// command (or sub-command) identified by the leading arguments,
        /// using the universal option introspection in the Eagle core
        /// library.  Only core commands that register their options are
        /// covered.
        /// </summary>
        /// <param name="arguments">
        /// The parsed input arguments; all but the final one identify
        /// the command (and possibly sub-command).
        /// </param>
        /// <param name="prefix">
        /// The final argument, the partial option text being completed.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match option names case-insensitively.
        /// </param>
        /// <param name="list">
        /// On input and output, the list that receives the matching
        /// option names.
        /// </param>
        /// <returns>
        /// Non-zero if at least one matching option was found; otherwise,
        /// zero.
        /// </returns>
        private static bool GetMatchingOptions(
            StringList arguments, /* in */
            string prefix,        /* in */
            bool noCase,          /* in */
            ref StringList list   /* in, out */
            )
        {
            //
            // NOTE: Use up to X leading arguments (command and possibly
            //       sub-command, etc) to look up the registered options,
            //       preferring the sub-command form, then falling back
            //       to the command form.
            //
            if (prefix == null)
                return false;

            OptionDictionary options = LookupOptions(arguments);

            if (options == null)
                return false;

            StringComparison comparisonType = noCase ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;

            int count = 0;

            foreach (IOption option in options.Values)
            {
                if (option == null)
                    continue;

                //
                // NOTE: Skip end-of-options markers (e.g. both "--"
                //       and "---").
                //
                if (option.HasFlags(OptionFlags.EndOfOptions, true) ||
                    option.HasFlags(OptionFlags.ListOfOptions, true))
                {
                    continue;
                }

                string name = option.Name;

                if (String.IsNullOrEmpty(name))
                    continue;

                if (name.StartsWith(prefix, comparisonType))
                {
                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }
            }

            return (count > 0);
        }

        ///////////////////////////////////////////////////////////////////////

        #region Completion Strategies
        /// <summary>
        /// Expands the leading command, sub-command, type/object, and member
        /// arguments in place, recording whether each was empty, and appends
        /// the expanded forms to the request's expanded-argument list.
        /// </summary>
        /// <param name="request">
        /// The completion request to expand.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool ExpandLeadingArguments(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            StringList arguments = request.Arguments;

            if (arguments == null)
                return false;

            StringList newArguments = request.NewArguments;

            if (newArguments == null)
                return false;

            bool[] empty = request.Empty;

            if (empty == null)
                return false;

            int argumentCount = arguments.Count;
            bool[] expand = { false, false, false, false };

            if (argumentCount > CommandNameIndex)
            {
                request.CommandName = arguments[CommandNameIndex];

                if (ExpandArgument(
                        interpreter, request.NoCase,
                        ref request.CommandName,
                        ref empty[CommandNameIndex],
                        ref expand[CommandNameIndex],
                        ref request.Error) != ReturnCode.Ok)
                {
                    return false;
                }

                newArguments.Add(request.CommandName);
            }

            if (argumentCount > SubCommandNameIndex)
            {
                request.SubCommandName = arguments[SubCommandNameIndex];

                if (ExpandArgument(
                        interpreter, request.NoCase,
                        ref request.SubCommandName,
                        ref empty[SubCommandNameIndex],
                        ref expand[SubCommandNameIndex],
                        ref request.Error) != ReturnCode.Ok)
                {
                    return false;
                }

                newArguments.Add(request.SubCommandName);
            }

            if (argumentCount > TypeOrObjectNameIndex)
            {
                request.TypeOrObjectName = arguments[TypeOrObjectNameIndex];

                if (ExpandArgument(
                        interpreter, request.NoCase,
                        ref request.TypeOrObjectName,
                        ref empty[TypeOrObjectNameIndex],
                        ref expand[TypeOrObjectNameIndex],
                        ref request.Error) != ReturnCode.Ok)
                {
                    return false;
                }

                newArguments.Add(request.TypeOrObjectName);
            }

            if (argumentCount > MemberNameIndex)
            {
                request.MemberName = arguments[MemberNameIndex];

                if (ExpandArgument(
                        interpreter, request.NoCase,
                        ref request.MemberName,
                        ref empty[MemberNameIndex],
                        ref expand[MemberNameIndex],
                        ref request.Error) != ReturnCode.Ok)
                {
                    return false;
                }

                newArguments.Add(request.MemberName);
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Runs the completion strategies in order, stopping at the first one
        /// that applies: option, then the position-specific strategy, falling
        /// back to command completion.
        /// </summary>
        /// <param name="request">
        /// The completion request carrying the expanded arguments and
        /// receiving the matches.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryComplete(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            StringList arguments = request.Arguments;

            if (arguments == null)
                return false;

            int argumentCount = arguments.Count;
            StringList matches = null;

            if ((argumentCount > 0) && GetMatchingOptions(
                    arguments, arguments[argumentCount - 1],
                    request.NoCase, ref matches))
            {
                request.MatchType = "MATCHING: options";
                request.Matches = matches;

                return true;
            }

            request.Matches = new StringList();

            switch (argumentCount)
            {
                case 1:
                    {
                        return TryCompleteInteractiveCommand(request);
                    }
                case 2:
                    {
                        return TryCompleteExpressionFunction(request);
                    }
                case 3:
                    {
                        return TryCompleteObjectType(request);
                    }
                case 4:
                    {
                        return TryCompleteObjectMember(request);
                    }
                default:
                    {
                        return TryCompleteCommand(request);
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes a single argument: a lone pound character requests
        /// interactive command help, and a pound-prefixed name completes
        /// against the interactive commands; otherwise falls back to command
        /// completion.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteInteractiveCommand(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            //
            // HACK: *SPECIAL* A lone pound character requests interactive
            //       command help.
            //
            bool[] empty = request.Empty;

            if ((empty != null) && empty[CommandNameIndex])
            {
                request.Help = true;
                return false;
            }

            //
            // NOTE: Does the command name start with a pound (i.e. comment)
            //       character?  If so, it could be an interactive command.
            //
            string commandName = request.CommandName;

            if (!String.IsNullOrEmpty(commandName) &&
                (commandName[0] == Characters.Comment))
            {
                string pattern = String.Format("{0}{1}",
                    commandName.Substring(1), Characters.Asterisk);

#if SHELL && INTERACTIVE_COMMANDS
                StringPair pair = null;

                if (IsInteractiveCommand(
                        interpreter, pattern, ref pair))
                {
                    StringList newArguments = request.NewArguments;

                    if ((newArguments != null) &&
                        (CommandNameIndex < newArguments.Count))
                    {
                        newArguments[CommandNameIndex] = String.Format(
                            "{0}{1}(interactive command)", commandName,
                            Characters.Space);
                    }

                    StringList arguments = new StringList();

                    arguments.Add(String.Format("{0}{1}",
                        Parser.Quote(commandName), Characters.Space));

                    if (pair.X != null)
                        arguments.Add(pair.X);

                    request.MatchType = "MATCHING: interactive command arguments";
                    request.Matches.Add(arguments.ToRawString().Trim());

                    return true;
                }

                Result error = null; /* IGNORED: Fall through on failure. */

                if (GetMatchingInteractiveCommands(
                        interpreter, pattern, request.NoCase,
                        ref request.Matches, ref error) == ReturnCode.Ok)
                {
                    request.MatchType = "MATCHING: interactive commands";
                    return true;
                }
#endif
            }

            return TryCompleteCommand(request);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes the second argument as an expression function when the
        /// command is the expression command; otherwise falls back to command
        /// completion.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteExpressionFunction(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            if (IsExpressionCommand(
                    interpreter, request.CommandName, request.NoCase))
            {
                string pattern = String.Format("{0}{1}",
                    request.SubCommandName, Characters.Asterisk);

                if (interpreter.ListFunctions(
                        FunctionFlags.None, FunctionFlags.None,
                        false, false, pattern, request.NoCase,
                        false, false, ref request.Matches,
                        ref request.Error) == ReturnCode.Ok)
                {
                    request.MatchType = "MATCHING: expr functions";
                    return true;
                }

                return false;
            }

            return TryCompleteCommand(request);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes the third argument of an object command as an object
        /// handle (deferring to command completion) or a matching type;
        /// otherwise falls back to command completion.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteObjectType(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            if (IsObjectCommand(
                    interpreter, request.CommandName,
                    request.SubCommandName, request.NoCase))
            {
                IObject @object = null;

                if (IsObject(
                        interpreter, request.TypeOrObjectName,
                        ref @object))
                {
                    request.CommandName = request.TypeOrObjectName;
                    request.SubCommandName = request.MemberName;
                    request.NonAlias = true;

                    return TryCompleteCommand(request);
                }

                string pattern = String.Format("{0}{1}",
                    request.TypeOrObjectName, Characters.Asterisk);

                IDictionary<string, Type> types = null;

                if (GetMatchingTypes(
                        null, interpreter, pattern, request.NoCase,
                        ref types, ref request.Error) == ReturnCode.Ok)
                {
                    request.MatchType = "MATCHING: types";
                    request.Matches = new StringList(types.Keys);

                    return true;
                }
            }

            return TryCompleteCommand(request);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes the fourth argument of an object command as a member of
        /// the named object handle or of a matching type; otherwise falls back
        /// to command completion.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteObjectMember(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            if (IsObjectCommand(
                    interpreter, request.CommandName,
                    request.SubCommandName, request.NoCase))
            {
                string pattern = String.Format("{0}{1}",
                        request.MemberName, Characters.Asterisk);

                StringList matches; /* REUSED */
                IObject @object = null;

                if (IsObject(
                        interpreter, request.TypeOrObjectName,
                        ref @object))
                {
                    object objectValue = @object.Value;

                    if (objectValue == null)
                        return false;

                    IDictionary<string, MemberInfo> members = null;

                    if (GetMatchingMembers(
                            interpreter, objectValue.GetType(),
                            pattern, request.NoCase, ref members,
                            ref request.Error) == ReturnCode.Ok)
                    {
                        if (members.Count > 1)
                            matches = new StringList(members.Keys);
                        else
                            matches = new StringList(members.Values);

                        request.MatchType = "MATCHING: object members";
                        request.Matches = matches;

                        return true;
                    }
                }

                IDictionary<string, Type> types = null;

                if (GetMatchingTypes(
                        null, interpreter, request.TypeOrObjectName,
                        request.NoCase, ref types,
                        ref request.Error) == ReturnCode.Ok)
                {
                    foreach (Type type in types.Values)
                    {
                        IDictionary<string, MemberInfo> members = null;

                        if (GetMatchingMembers(
                                interpreter, type, pattern,
                                request.NoCase, ref members,
                                ref request.Error) == ReturnCode.Ok)
                        {
                            if (members.Count > 1)
                                matches = new StringList(members.Keys);
                            else
                                matches = new StringList(members.Values);

                            request.MatchType = "MATCHING: type members";
                            request.Matches = matches;

                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            return TryCompleteCommand(request);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes against an object alias member, a procedure, or a command
        /// sub-command; failing those, falls back to completing a bare command
        /// name.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteCommand(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            string pattern = String.Format("{0}{1}",
                request.SubCommandName, Characters.Asterisk);

            IObject @object = null;

            if (IsObject(
                    interpreter, request.CommandName, ref @object) &&
                (request.NonAlias ||
                ((@object.ObjectFlags &
                    ObjectFlags.Alias) == ObjectFlags.Alias)))
            {
                object objectValue = @object.Value;

                if (objectValue == null)
                    return false;

                IDictionary<string, MemberInfo> members = null;

                if (GetMatchingMembers(
                        interpreter, objectValue.GetType(), pattern,
                        request.NoCase, ref members,
                        ref request.Error) == ReturnCode.Ok)
                {
                    StringList matches;

                    if (members.Count > 1)
                        matches = new StringList(members.Keys);
                    else
                        matches = new StringList(members.Values);

                    request.MatchType = "MATCHING: object alias members";
                    request.Matches = matches;

                    return true;
                }

                return false;
            }

            StringList newArguments; /* REUSED */
            IProcedure procedure = null;

            if (IsProcedure(
                    interpreter, request.CommandName, ref procedure,
                    ref request.CommandName))
            {
                newArguments = request.NewArguments;

                if ((newArguments != null) &&
                    (CommandNameIndex < newArguments.Count))
                {
                    newArguments[CommandNameIndex] = String.Format(
                        "{0}{1}(procedure)", request.CommandName,
                        Characters.Space);
                }

                StringList arguments = new StringList();

                arguments.Add(String.Format("{0}{1}",
                    Parser.Quote(request.CommandName), Characters.Space));

                ArgumentList procedureArguments = procedure.Arguments;

                if (procedureArguments.Count > 0)
                {
                    arguments.Add(procedureArguments.ToString(
                        ToStringFlags.Decorated));
                }

                request.MatchType = "MATCHING: procedure arguments";
                request.Matches.Add(arguments.ToRawString().Trim());

                return true;
            }

            _Interfaces.ICommand command = null;

            if (IsCommand(
                    interpreter, request.CommandName, ref command,
                    ref request.CommandName))
            {
                newArguments = request.NewArguments;

                if ((newArguments != null) &&
                    (CommandNameIndex < newArguments.Count))
                {
                    newArguments[CommandNameIndex] = String.Format(
                        "{0}{1}(command)", request.CommandName,
                        Characters.Space);
                }

                EnsembleDictionary subCommands = command.SubCommands;

                if (subCommands == null)
                {
                    request.Matches.Add(request.CommandName);
                    return true;
                }

                if (subCommands.ToList(
                        CommandFlags.None, CommandFlags.None,
                        false, false, pattern,
                        request.NoCase, ref request.Matches,
                        ref request.Error) == ReturnCode.Ok)
                {
                    request.MatchType = "MATCHING: sub-commands";
                    return true;
                }

                return false;
            }

            return TryCompleteCommandList(request);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Completes a bare name against the matching commands, procedures,
        /// and other executables, tagging the input when exactly one match is
        /// found.
        /// </summary>
        /// <param name="request">
        /// The completion request.
        /// </param>
        /// <returns>
        /// Non-zero if the matches should be displayed; otherwise, zero.
        /// </returns>
        private static bool TryCompleteCommandList(
            CompletionRequest request /* in, out */
            )
        {
            if (request == null)
                return false;

            Interpreter interpreter = request.Interpreter;

            if (interpreter == null)
                return false;

            StringList matches = request.Matches;

            if (matches == null)
                return false;

            string pattern = String.Format("{0}{1}",
                request.CommandName, Characters.Asterisk);

            int[] count = { 0, 0 };

            count[0] = matches.Count;

            if (interpreter.ListCommands(
                    CommandFlags.None, CommandFlags.None, false,
                    false, pattern, request.NoCase, false, false,
                    ref matches, ref request.Error) != ReturnCode.Ok)
            {
                return false;
            }

            StringList newArguments; /* REUSED */
            bool tagged = false;

            count[1] = matches.Count;

            if (!tagged && (matches.Count == 1) &&
                (count[1] - count[0] == 1))
            {
                newArguments = request.NewArguments;

                if ((newArguments != null) &&
                    (CommandNameIndex < newArguments.Count))
                {
                    newArguments[CommandNameIndex] = String.Format(
                        "{0}{1}(command)", matches[0], Characters.Space);
                }

                tagged = true;
            }

            count[0] = matches.Count;

            if (interpreter.ListProcedures(
                    ProcedureFlags.None, ProcedureFlags.None, false,
                    false, pattern, request.NoCase, false, false,
                    ref matches, ref request.Error) != ReturnCode.Ok)
            {
                return false;
            }

            count[1] = matches.Count;

            if (!tagged && (matches.Count == 1) &&
                (count[1] - count[0] == 1))
            {
                newArguments = request.NewArguments;

                if ((newArguments != null) &&
                    (CommandNameIndex < newArguments.Count))
                {
                    newArguments[CommandNameIndex] = String.Format(
                        "{0}{1}(procedure)", matches[0], Characters.Space);
                }

                tagged = true;
            }

            count[0] = matches.Count;

            if (interpreter.ListIExecutes(
                    pattern, request.NoCase, false, ref matches,
                    ref request.Error) != ReturnCode.Ok)
            {
                return false;
            }

            count[1] = matches.Count;

            if (!tagged && (matches.Count == 1) &&
                (count[1] - count[0] == 1))
            {
                newArguments = request.NewArguments;

                if ((newArguments != null) &&
                    (CommandNameIndex < newArguments.Count))
                {
                    newArguments[CommandNameIndex] = String.Format(
                        "{0}{1}(execute)", matches[0], Characters.Space);
                }

                tagged = true;
            }

            request.MatchType = "MATCHING: commands";
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sorts and truncates the matches, then prepends the diagnostic
        /// header lines that describe the original and expanded input.
        /// </summary>
        /// <param name="request">
        /// The completion request whose matches are formatted.
        /// </param>
        /// <returns>
        /// The display list, ready to be shown.
        /// </returns>
        private static StringList BuildDisplay(
            CompletionRequest request /* in */
            )
        {
            StringList matches = request.Matches;

            if (matches == null)
                return null;

            //
            // NOTE: We need the list in a well-defined order before we
            //       do any mutations to it.
            //
            matches.Sort(); /* O(N log N) */

            //
            // HACK: If the list is too large, truncate the results now.
            //
            int count = matches.Count;

            if (count > MaximumAutoComplete)
            {
                matches.RemoveRange(
                    MaximumAutoComplete, count - MaximumAutoComplete);

                matches.Add(String.Format(
                    TooManyMatches, MaximumAutoComplete));
            }

            //
            // NOTE: Show the (possibly expanded?) input that actually
            //       generated these matches.
            //
            int header = 0;

            matches.Insert(header++,
                String.Format("ORIGINAL: {0}", request.Arguments));

            matches.Insert(header++,
                String.Format("EXPANDED: {0}", request.NewArguments));

            if (request.MatchType != null)
                matches.Insert(header++, request.MatchType);

            matches.Insert(header++, String.Format("MATCHES: {0}", count));
            matches.Insert(header++, null);

            return matches;
        }
        #endregion
        #endregion
    }
}
