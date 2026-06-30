/*
 * Support.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;
using IsolatedState = Licensing.Components.Private.CertificateIsolatedState;
using LicenseOps = Licensing.Sdk.Private.LicenseOps;

namespace Licensing.Commands
{
    /// <summary>
    /// Implements the "support" ensemble command, which exposes licensing
    /// support information (e.g. the support URI) together with diagnostic,
    /// isolation, and option-related sub-commands.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("44175fba-f4fc-4d0c-a676-c51a444833ee")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("introspection")]
    internal sealed class Support : Default
    {
        #region Private Constants
        /// <summary>
        /// Non-zero if extra diagnostic information should be collected and
        /// reported by this command.
        /// </summary>
        private const bool extraDiagnostics =
#if DEBUG || EXTRA_DIAGNOSTICS
            true;
#else
            false;
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if tracing should be forced on for this command.
        /// </summary>
        private const bool forceTrace =
#if DEBUG || FORCE_TRACE
            true;
#else
            false;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Support" /> command.
        /// </summary>
        /// <param name="commandData">
        /// The data used to initialize the new command instance.
        /// </param>
        public Support(
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
        /// Gets the set of licensing features required to use this command.
        /// </summary>
        public override string Features
        {
            get { return _Features.Commands.SupportOrAll; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The collection of sub-command names supported by this command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "diagnostic", "isolated", "options"
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
        /// The collection of sub-command names that are permitted by the
        /// active policy.
        /// </summary>
        private EnsembleDictionary allowedSubCommands = new EnsembleDictionary(
            Policies.Support.AllowedSubCommandNames);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the collection of sub-command names that are permitted
        /// by the active policy.
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
        /// The interpreter context in which the command is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the command.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of executing the command or an
        /// error message describing the failure.
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

            int argumentCount = arguments.Count;

            if (argumentCount < 1)
            {
                result = String.Format(
                    "wrong # args: should be \"{0} ?option? ?arg ...?\"",
                    this.Name);

                return ReturnCode.Error;
            }

            if (CanExecute(interpreter, ref result) != ReturnCode.Ok)
                return ReturnCode.Error;

            Uri uri;
            IPlugin plugin = this.Plugin;
            Result error = null; /* REUSED */

            uri = LicenseManager.GetSupport(plugin, false, ref error);

            if (uri == null)
            {
                result = error;
                return ReturnCode.Error;
            }

            if (argumentCount == 1)
            {
                result = uri;
                return ReturnCode.Ok;
            }
            else
            {
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
                                if (argumentCount == 2)
                                {
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
                        case "diagnostic":
                            {
                                StringPairList traceStatus = null;
                                StringList fileNames = null;
                                Result localResult; /* REUSED */

                                if (argumentCount == 3)
                                {
                                    StringList oldList = null;

                                    code = Parser.SplitList(
                                        interpreter, arguments[2], 0, Length.Invalid,
                                        true, ref oldList, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        CultureInfo cultureInfo = interpreter.CultureInfo;
                                        ObjectArrayList newList = new ObjectArrayList();

                                        foreach (string element in oldList)
                                        {
                                            object enumValue = Utility.TryParseFlagsEnum(
                                                interpreter, typeof(SupportDiagnostic),
                                                SupportDiagnostic.Default.ToString(),
                                                element, cultureInfo, true, true, true,
                                                ref result);

                                            if (!(enumValue is SupportDiagnostic))
                                            {
                                                code = ReturnCode.Error;
                                                break;
                                            }

                                            SupportDiagnostic diagnostic =
                                                (SupportDiagnostic)enumValue;

                                            switch (diagnostic)
                                            {
                                                case SupportDiagnostic.GetExtraDiagnostics:
                                                    {
                                                        newList.Add(new object[] {
                                                            diagnostic, extraDiagnostics
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetForceTrace:
                                                    {
                                                        newList.Add(new object[] {
                                                            diagnostic, forceTrace
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetUri:
                                                    {
                                                        newList.Add(new object[] {
                                                            diagnostic, uri
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetNormalizeErrors:
                                                    {
                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetNormalizeErrors()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.EnableNormalizeErrors:
                                                    {
                                                        IsolatedState.SetNormalizeErrors(true);

                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetNormalizeErrors()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.DisableNormalizeErrors:
                                                    {
                                                        IsolatedState.SetNormalizeErrors(false);

                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetNormalizeErrors()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetIncludePublicKeyToken:
                                                    {
                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetIncludePublicKeyToken()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.EnableIncludePublicKeyToken:
                                                    {
                                                        IsolatedState.SetIncludePublicKeyToken(true);

                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetIncludePublicKeyToken()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.DisableIncludePublicKeyToken:
                                                    {
                                                        IsolatedState.SetIncludePublicKeyToken(false);

                                                        newList.Add(new object[] {
                                                            diagnostic,
                                                            IsolatedState.GetIncludePublicKeyToken()
                                                        });

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetTracing:
                                                    {
                                                        code = Utility.QueryTraceStatus(
                                                            interpreter, ref traceStatus, ref result);

                                                        if (code == ReturnCode.Ok)
                                                        {
                                                            newList.Add(new object[] {
                                                                diagnostic,
                                                                traceStatus
                                                            });
                                                        }

                                                        break;
                                                    }
                                                case SupportDiagnostic.EnableTracing:
                                                    {
                                                        localResult = null;

                                                        if (LicenseOps.MaybeSetupTraceSubsystem(
                                                                interpreter, TracePriority.HasPrioritiesMask,
                                                                true, true, ref localResult))
                                                        {
                                                            newList.Add(new object[] {
                                                                diagnostic,
                                                                localResult
                                                            });
                                                        }
                                                        else
                                                        {
                                                            result = localResult;
                                                            code = ReturnCode.Error;
                                                        }

                                                        break;
                                                    }
                                                case SupportDiagnostic.DisableTracing:
                                                    {
                                                        localResult = null;

                                                        if (LicenseOps.MaybeSetupTraceSubsystem(
                                                                interpreter, TracePriority.DefaultMask,
                                                                false, false, ref localResult))
                                                        {
                                                            newList.Add(new object[] {
                                                                diagnostic,
                                                                localResult
                                                            });
                                                        }
                                                        else
                                                        {
                                                            result = localResult;
                                                            code = ReturnCode.Error;
                                                        }

                                                        break;
                                                    }
                                                case SupportDiagnostic.GetLogFileNames:
                                                    {
                                                        if (Utility.ExtractTraceLogFileNames(
                                                                false, ref fileNames, ref result))
                                                        {
                                                            newList.Add(new object[] {
                                                                diagnostic,
                                                                fileNames
                                                            });
                                                        }
                                                        else
                                                        {
                                                            code = ReturnCode.Error;
                                                        }

                                                        break;
                                                    }
                                                case SupportDiagnostic.EnableExtraDiagnostics:
                                                case SupportDiagnostic.DisableExtraDiagnostics:
                                                case SupportDiagnostic.EnableForceTrace:
                                                case SupportDiagnostic.DisableForceTrace:
                                                case SupportDiagnostic.EnableUri:
                                                case SupportDiagnostic.DisableUri:
                                                case SupportDiagnostic.EnableLogFileNames:
                                                case SupportDiagnostic.DisableLogFileNames:
                                                    {
                                                        result = String.Format(
                                                            "unsupported support diagnostic {0}",
                                                            diagnostic);

                                                        code = ReturnCode.Error;
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        result = String.Format(
                                                            "unrecognized support diagnostic {0}",
                                                            diagnostic);

                                                        code = ReturnCode.Error;
                                                        break;
                                                    }
                                            }

                                            if (code != ReturnCode.Ok)
                                                break;
                                        }

                                        if (code == ReturnCode.Ok)
                                            result = newList.ToString();
                                    }
                                }
                                else if (argumentCount == 2)
                                {
                                    error = null;

                                    if (Utility.QueryTraceStatus(
                                            interpreter, ref traceStatus,
                                            ref error) != ReturnCode.Ok)
                                    {
                                        traceStatus.Add("error", error);
                                    }

                                    StringPairList logStatus = new StringPairList();

                                    error = null;

                                    if (Utility.ExtractTraceLogFileNames(
                                            false, ref fileNames, ref error))
                                    {
                                        logStatus.Add("fileNames", fileNames);
                                    }
                                    else
                                    {
                                        logStatus.Add("error", error);
                                    }

                                    result = StringList.MakeList(
                                        SupportDiagnostic.GetExtraDiagnostics,
                                        extraDiagnostics,
                                        SupportDiagnostic.GetForceTrace,
                                        forceTrace,
                                        SupportDiagnostic.GetUri, uri,
                                        SupportDiagnostic.GetNormalizeErrors,
                                        IsolatedState.GetNormalizeErrors(),
                                        SupportDiagnostic.GetIncludePublicKeyToken,
                                        IsolatedState.GetIncludePublicKeyToken(),
                                        SupportDiagnostic.GetTracing,
                                        traceStatus,
                                        SupportDiagnostic.GetLogFileNames,
                                        logStatus);
                                }
                                else
                                {
                                    result = String.Format(
                                        "wrong # args: should be \"{0} {1} ?list?\"",
                                        this.Name, subCommand);

                                    code = ReturnCode.Error;
                                }
                                break;
                            }
                        case "isolated":
                            {
                                if (argumentCount == 2)
                                {
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
                                if (argumentCount == 2)
                                {
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
                        default:
                            {
                                result = Utility.BadSubCommand(
                                    interpreter, null, null, subCommand,
                                    this, null, null);

                                code = ReturnCode.Error;
                                break;
                            }
                    }
                }

                return code;
            }
        }
        #endregion
    }
}
