/*
 * Zeus.cs --
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

using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Zeus.Components.Private;
using Zeus.Components.Public;
using _Commands = Eagle._Commands;
using Registered = Zeus.Procedures.Registered;
using _Arch = Eagle._Components.Public.ProcessorArchitecture;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Zeus.Commands
{
    /// <summary>
    /// Implements the <c>zeus</c> ensemble command, the single script-visible
    /// command exposed by the Zeus plugin.  Its sub-commands provide CLR
    /// method hooking, registered and obfuscated procedure management, RFC
    /// 2898 (PBKDF2) key derivation, RFC 2898 provider configuration, plugin
    /// diagnostics, and Bailey-Borwein-Plouffe digit-of-pi computation.  The
    /// command is marked unsafe and belongs to the "managedEnvironment"
    /// object group.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("c60340ea-97c5-4e3b-b842-2d174e783bbd")]
    [CommandFlags(CommandFlags.Unsafe)]
    [ObjectGroup("managedEnvironment")]
    internal sealed class Zeus : _Commands.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Zeus" /> command class.
        /// </summary>
        /// <param name="commandData">
        /// The data used to create and configure the command, such as its
        /// name, flags, and associated plugin.
        /// </param>
        public Zeus(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IEnsemble Members
        /// <summary>
        /// The backing field for the <see cref="SubCommands" /> property,
        /// holding the set of sub-command names recognized by this ensemble
        /// command.
        /// </summary>
        private EnsembleDictionary subCommands =
            new EnsembleDictionary(new string[] {
            "about", "callback", "certificate", "clone", "create",
            "derive", "hook", "isolated", "options", "pi", "proc",
            "register", "selftest", "unregister"
        });

        ///////////////////////////////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the <c>zeus</c> command.  The first argument selects the
        /// sub-command, which is first dispatched through the ensemble's
        /// policy-aware <c>Utility.TryExecuteSubCommandFromEnsemble</c>
        /// resolver; if that does not handle it, the built-in sub-commands
        /// (about, callback, certificate, clone, create, derive, hook,
        /// isolated, options, pi, proc, register, selftest, unregister) are
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

            int argumentCount = arguments.Count;

            if (argumentCount < 2)
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
                            if (argumentCount == 2)
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
                    case "callback":
                        {
                            if (argumentCount >= 3)
                            {
                                MarshalFlags marshalFlags = MarshalFlags.MethodHookMask;

                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-name", null),
                                    new Option(typeof(MarshalFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-marshalflags",
                                        new Variant(marshalFlags)),
                                    new Option(typeof(CallbackFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-callbackflags",
                                        new Variant(CallbackFlags.Default)),
                                    new Option(typeof(ObjectFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-objectflags",
                                        new Variant(ObjectFlags.Callback)),
                                    new Option(typeof(ByRefArgumentFlags),
                                        OptionFlags.MustHaveEnumValue, Index.Invalid,
                                        Index.Invalid, "-byrefargumentflags",
                                        new Variant(ByRefArgumentFlags.None)),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if (argumentIndex != Index.Invalid)
                                    {
                                        IVariant value = null;
                                        string name = null;

                                        if (options.IsPresent("-name", ref value))
                                            name = value.ToString();

                                        if (options.IsPresent("-marshalflags", ref value))
                                            marshalFlags = (MarshalFlags)value.Value;

                                        CallbackFlags callbackFlags = CallbackFlags.Default;

                                        if (options.IsPresent("-callbackflags", ref value))
                                            callbackFlags = (CallbackFlags)value.Value;

                                        ObjectFlags objectFlags = ObjectFlags.Callback;

                                        if (options.IsPresent("-objectflags", ref value))
                                            objectFlags = (ObjectFlags)value.Value;

                                        ByRefArgumentFlags byRefArgumentFlags = ByRefArgumentFlags.None;

                                        if (options.IsPresent("-byrefargumentflags", ref value))
                                            byRefArgumentFlags = (ByRefArgumentFlags)value.Value;

                                        long registrationToken = 0;
                                        string registrationName = null;

                                        code = CommonOps.ParseRegistration(
                                            interpreter, arguments[argumentIndex],
                                            ref registrationToken, ref registrationName,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            string procedureName = null;
                                            IProcedure procedure = null;

                                            code = interpreter.GetProcedure(
                                                registrationToken, LookupFlags.NoWrapper,
                                                ref procedureName, ref procedure, ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                code = CommonOps.IsRegisteredProcedure(
                                                    interpreter, registrationName, procedureName,
                                                    procedure, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    StringList newArguments = new StringList(
                                                        arguments, argumentIndex + 1);

                                                    newArguments.Insert(0, procedureName);

                                                    string newName = (name != null) ?
                                                        name : newArguments.ToString();

                                                    ICallback callback = Utility.CreateCommandCallback(
                                                        marshalFlags, callbackFlags, objectFlags,
                                                        byRefArgumentFlags, interpreter, clientData,
                                                        newName, newArguments, ref result);

                                                    if (callback != null)
                                                    {
                                                        //
                                                        // HACK: Use the default set of options for the
                                                        //       new opaque object handle -AND- do *NOT*
                                                        //       use the alias option, because a command
                                                        //       cannot be added due to the existing
                                                        //       (registered) procedure by that name.
                                                        //
                                                        ObjectOptionType objectOptionType =
                                                            ObjectOptionType.Default;

                                                        code = Utility.FixupReturnValue(
                                                            interpreter, null, objectFlags,
                                                            Utility.GetInvokeOptions(objectOptionType),
                                                            objectOptionType, newName, callback, false,
                                                            false, ref result);
                                                    }
                                                    else
                                                    {
                                                        code = ReturnCode.Error;
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
                                                "wrong # args: should be \"{0} {1} ?options? arg ?arg ...?\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? arg ?arg ...?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "certificate":
                        {
                            if (argumentCount == 2)
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
                    case "clone":
                        {
                            if (argumentCount == 3)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    code = Rfc2898Ops.MaybeUseProvider(
                                        interpreter, arguments[2], ref result);
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
                                    "wrong # args: should be \"{0} {1} provider\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "create":
                        {
                            if (argumentCount >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.None,
                                        Index.Invalid, Index.Invalid, "-allownull", null),
                                    new Option(null, OptionFlags.MustHavePluginValue,
                                        Index.Invalid, Index.Invalid, "-plugin", null),
                                    new Option(null, OptionFlags.MustHaveObjectValue,
                                        Index.Invalid, Index.Invalid, "-clientdata", null)
                                }, Utility.GetFixupReturnValueOptions().Values);

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 2) == argumentCount))
                                    {
                                        ObjectFlags objectFlags;
                                        string objectName;
                                        string interpName;
                                        bool alias;
                                        bool aliasRaw;
                                        bool aliasAll;
                                        bool aliasReference;

                                        Utility.ProcessFixupReturnValueOptions(
                                            options, null, out objectFlags,
                                            out objectName, out interpName,
                                            out alias, out aliasRaw, out aliasAll,
                                            out aliasReference);

                                        IVariant value = null;
                                        IPlugin plugin = this.Plugin;

                                        if (options.IsPresent("-plugin", ref value))
                                            plugin = (IPlugin)value.Value;

                                        IClientData providerClientData = clientData;

                                        if (options.IsPresent("-clientdata", ref value))
                                        {
                                            IObject @object = (IObject)value.Value;

                                            if ((@object.Value == null) ||
                                                (@object.Value is IClientData))
                                            {
                                                providerClientData = (IClientData)@object.Value;
                                            }
                                            else
                                            {
                                                result = "option value has invalid clientData";
                                                code = ReturnCode.Error;
                                            }
                                        }

                                        bool allowNull = false;

                                        if (options.IsPresent("-allownull"))
                                            allowNull = true;

                                        if (code == ReturnCode.Ok)
                                        {
                                            string assemblyName = arguments[argumentIndex];

                                            if (assemblyName.Length == 0)
                                                assemblyName = null;

                                            string typeName = arguments[argumentIndex + 1];

                                            if (typeName.Length == 0)
                                                typeName = null;

                                            Result error = null;

                                            object provider = ProviderManager.Create(
                                                interpreter, plugin, providerClientData,
                                                assemblyName, typeName, ref error);

                                            if (provider != null)
                                            {
                                                ObjectOptionType objectOptionType =
                                                    Utility.GetOptionType(aliasRaw, aliasAll);

                                                code = Utility.FixupReturnValue(
                                                    interpreter, IsolatedOps.GetBinder(interpreter,
                                                    this.Plugin), interpreter.CultureInfo, null,
                                                    objectFlags, options, Utility.GetInvokeOptions(
                                                        objectOptionType), objectOptionType,
                                                    objectName, interpName, provider, true, true,
                                                    alias, aliasReference, false, ref result);
                                            }
                                            else if (allowNull)
                                            {
                                                //
                                                // HACK: Ok, null return is allowed.
                                                //
                                                result = String.Empty;
                                            }
                                            else
                                            {
                                                result = String.Format(
                                                    "could not create provider: {0}",
                                                    Utility.FormatWrapOrNull(error));

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
                                                options, arguments[argumentIndex],
                                                !interpreter.IsSafe());
                                        }
                                        else
                                        {
                                            result = String.Format(
                                                "wrong # args: should be \"{0} {1} " +
                                                "?options? assemblyName typeName\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} " +
                                    "?options? assemblyName typeName\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "derive":
                        {
                            if (argumentCount >= 4)
                            {
                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveEncodingValue,
                                        Index.Invalid, Index.Invalid, "-encoding", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-count", null),
                                    new Option(null, OptionFlags.MustHaveIntegerValue,
                                        Index.Invalid, Index.Invalid, "-iterations", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-hashalgorithm", null),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        ((argumentIndex + 1) < argumentCount))
                                    {
                                        IVariant value = null;
                                        Encoding encoding = null;

                                        if (options.IsPresent("-encoding", ref value))
                                            encoding = (Encoding)value.Value;

                                        int deriveCount = CryptographyOps.DefaultDeriveCount;

                                        if (options.IsPresent("-count", ref value))
                                            deriveCount = (int)value.Value;

                                        int iterationCount = CryptographyOps.DefaultIterationCount;

                                        if (options.IsPresent("-iterations", ref value))
                                            iterationCount = (int)value.Value;

                                        string hashAlgorithmName = null;

                                        if (options.IsPresent("-hashalgorithm", ref value))
                                            hashAlgorithmName = value.ToString();

                                        string salt = arguments[argumentIndex++];
                                        StringBuilder password = new StringBuilder();

                                        for (; argumentIndex < argumentCount; argumentIndex++)
                                            password.Append(arguments[argumentIndex]);

                                        byte[] bytes = null;

                                        code = CryptographyOps.DeriveBytes(
                                            encoding, password.ToString(), salt, iterationCount,
                                            hashAlgorithmName, deriveCount, ref bytes, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            result = Convert.ToBase64String(bytes,
                                                Base64FormattingOptions.InsertLineBreaks);
                                        }
                                    }
                                    else
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
                                                "wrong # args: should be \"{0} {1} " +
                                                "?options? salt password ?password ...?\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} " +
                                    "?options? salt password ?password ...?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "hook":
                        {
                            if (argumentCount >= 2)
                            {
#if EMIT && NATIVE
                                if (!Utility.IsCrossAppDomain(interpreter, this.Plugin))
                                {
                                    BindingFlags bindingFlags = Utility.GetDefaultBindingFlags();

                                    OptionDictionary options = new OptionDictionary(
                                        new IOption[] {
                                        new Option(null, OptionFlags.MustHaveTypeValue,
                                            Index.Invalid, Index.Invalid, "-methodtype", null),
                                        new Option(null, OptionFlags.MustHaveValue,
                                            Index.Invalid, Index.Invalid, "-methodname", null),
                                        new Option(typeof(BindingFlags),
                                            OptionFlags.MustHaveEnumValue,
                                            Index.Invalid, Index.Invalid, "-bindingflags",
                                            new Variant(bindingFlags)),
                                        new Option(typeof(MarshalFlags),
                                            OptionFlags.MustHaveEnumListValue,
                                            Index.Invalid, Index.Invalid, "-parametermarshalflags",
                                            new Variant(MarshalFlags.None)),
                                        new Option(typeof(MarshalFlags),
                                            OptionFlags.MustHaveEnumValue,
                                            Index.Invalid, Index.Invalid, "-marshalflags",
                                            new Variant(MarshalFlags.MethodHookMask)),
                                        new Option(null, OptionFlags.MustHaveCallbackValue,
                                            Index.Invalid, Index.Invalid, "-callback", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-allowlegacy", null),
                                        new Option(null, OptionFlags.MustHaveBooleanValue,
                                            Index.Invalid, Index.Invalid, "-allowfallback", null),
                                        Option.CreateEndOfOptions()
                                    });

                                    int argumentIndex = Index.Invalid;

                                    code = interpreter.GetOptions(
                                        options, arguments, 0, 2, Index.Invalid,
                                        true, ref argumentIndex, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        if (argumentIndex == Index.Invalid)
                                        {
                                            IVariant value = null;
                                            Type methodType = null;

                                            if (options.IsPresent("-methodtype", ref value))
                                                methodType = (Type)value.Value;

                                            string methodName = null;

                                            if (options.IsPresent("-methodname", ref value))
                                                methodName = value.ToString();

                                            ICallback callback = null;

                                            if (options.IsPresent("-callback", ref value))
                                                callback = (ICallback)value.Value;

                                            if (options.IsPresent("-bindingflags", ref value))
                                                bindingFlags = (BindingFlags)value.Value;

                                            MarshalFlags marshalFlags = MarshalFlags.MethodHookMask;

                                            if (options.IsPresent("-marshalflags", ref value))
                                                marshalFlags = (MarshalFlags)value.Value;

                                            MarshalFlagsList parameterMarshalFlags = null;

                                            if (options.IsPresent("-parametermarshalflags", ref value))
                                                parameterMarshalFlags = (MarshalFlagsList)value.Value;

                                            bool? allowLegacy = null; // TODO: Good default?

                                            if (options.IsPresent("-allowlegacy", ref value))
                                                allowLegacy = (bool)value.Value;

                                            bool? allowFallback = null; // TODO: Good default?

                                            if (options.IsPresent("-allowfallback", ref value))
                                                allowFallback = (bool)value.Value;

                                            MethodBase oldMethod = null;

                                            code = HookOps.LookupMethodBase(
                                                methodType, methodName, bindingFlags, ref oldMethod,
                                                ref result);

                                            if (code == ReturnCode.Ok)
                                            {
                                                if (callback != null)
                                                {
                                                    Type returnType = null;
                                                    TypeList parameterTypes = null;

                                                    HookOps.GetReturnAndParameterTypes(
                                                        oldMethod, ref returnType, ref parameterTypes);

                                                    MethodBase newMethod = callback.GetMethod(
                                                        oldMethod, returnType, parameterTypes,
                                                        parameterMarshalFlags, null, marshalFlags,
                                                        ref result);

                                                    if (newMethod != null)
                                                    {
                                                        PatchFlags? patchFlags = null;

                                                        HookOps.ChangePatchFlags(
                                                            allowLegacy, allowFallback, ref patchFlags);

                                                        /* Zeus.Components.Private.HookOps.HookClientData */
                                                        IClientData hookClientData = null;

                                                        try
                                                        {
                                                            //
                                                            // NOTE: Attempt to hook the target method, do not
                                                            //       enable any legacy fallback logic, use the
                                                            //       detected processor architecture, and use
                                                            //       the default limit for traversing through
                                                            //       jump-stubs.
                                                            //
                                                            HookOps.Initialize(false);

                                                            code = HookOps.Start(
                                                                oldMethod, newMethod, _Arch.Unknown, 0,
                                                                patchFlags, ref hookClientData, ref result);

                                                            if (code == ReturnCode.Ok)
                                                            {
                                                                //
                                                                // NOTE: Use the hook client data to create the
                                                                //       opaque object handle name, it must be
                                                                //       a "unique" name and the ToString method
                                                                //       of the HookClientData class is required
                                                                //       to conform to that expectation.
                                                                //
                                                                string newName = null;

                                                                if (hookClientData != null)
                                                                {
                                                                    newName = String.Format(
                                                                        "{0}#{1}", hookClientData.GetType(),
                                                                        hookClientData);
                                                                }

                                                                //
                                                                // HACK: Use the default set of options for the
                                                                //       new opaque object handle -AND- do *NOT*
                                                                //       use the alias option, because a command
                                                                //       cannot be added due to the existing
                                                                //       (registered) procedure by that name.
                                                                //
                                                                ObjectOptionType objectOptionType =
                                                                    ObjectOptionType.Default;

                                                                code = Utility.FixupReturnValue(
                                                                    interpreter, null, ObjectFlags.None,
                                                                    Utility.GetInvokeOptions(objectOptionType),
                                                                    objectOptionType, newName, hookClientData,
                                                                    false, false, ref result);
                                                            }
                                                        }
                                                        finally
                                                        {
                                                            //
                                                            // NOTE: *SECURITY* This is a "fail-safe" to be sure
                                                            //       that the original method is unhooked if the
                                                            //       sub-command fails in any way, e.g. not able
                                                            //       to register the opaque object handle, etc.
                                                            //
                                                            if (hookClientData != null)
                                                            {
                                                                if (code != ReturnCode.Ok)
                                                                {
                                                                    Utility.TryDisposeObjectOrComplain<IClientData>(
                                                                        interpreter, ref hookClientData);
                                                                }

                                                                hookClientData = null;
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
                                                    //
                                                    // HACK: The "-callback" option is not really
                                                    //       optional for this sub-command, per se;
                                                    //       however, that may change in the future.
                                                    //
                                                    result = "please use the -callback option";
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
                                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                                    this.Name, subCommand);
                                            }

                                            code = ReturnCode.Error;
                                        }
                                    }
                                }
                                else
                                {
                                    result = "cannot hook methods with plugin isolated";
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
                                    "wrong # args: should be \"{0} {1} ?options?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "isolated":
                        {
                            if (argumentCount == 2)
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    result = Utility.IsCrossAppDomain(
                                        interpreter, plugin);
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
                    case "pi":
                        {
                            if ((argumentCount >= 3) && (argumentCount <= 4))
                            {
#if NET_40
                                long startIndex = 0;

                                code = Value.GetWideInteger2(
                                    (IGetValue)arguments[2], ValueFlags.AnyWideInteger,
                                    interpreter.CultureInfo, ref startIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    int count = 1;

                                    if (argumentCount == 4)
                                    {
                                        code = Value.GetInteger2(
                                            (IGetValue)arguments[3], ValueFlags.AnyInteger,
                                            interpreter.CultureInfo, ref count, ref result);
                                    }

                                    if (code == ReturnCode.Ok)
                                    {
                                        result = BaileyBorweinPlouffe.GetDigits(
                                            interpreter, startIndex, count, null);
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
                                    "wrong # args: should be \"{0} {1} digit ?count?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "proc":
                        {
                            if ((argumentCount == 2) || (argumentCount == 3))
                            {
                                IPlugin plugin = this.Plugin;

                                if (plugin != null)
                                {
                                    object @object; /* REUSED */

                                    if (argumentCount == 3)
                                    {
                                        @object = null;

                                        code = Rfc2898Ops.GetDataOrProvider(
                                            interpreter, arguments[2], ref @object,
                                            ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            if (Rfc2898Ops.SetDataOrProvider(
                                                    plugin, @object, ref result))
                                            {
                                                code = IsolatedOps.InstallNewProcedureCallbacks(
                                                    interpreter, plugin, (@object != null),
                                                    ref result);
                                            }
                                            else
                                            {
                                                code = ReturnCode.Error;
                                            }
                                        }
                                    }

                                    if (code == ReturnCode.Ok)
                                        result = Rfc2898Ops.GetStatus(plugin);
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
                                    "wrong # args: should be \"{0} {1} ?provider?\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "register": /* creates a "registered procedure" */
                        {
                            if (argumentCount >= 3)
                            {
                                ProcedureFlags procedureFlags = ProcedureFlags.None;

                                OptionDictionary options = new OptionDictionary(
                                    new IOption[] {
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-group", null),
                                    new Option(null, OptionFlags.MustHaveValue,
                                        Index.Invalid, Index.Invalid, "-description", null),
                                    new Option(typeof(ProcedureFlags),
                                        OptionFlags.MustHaveEnumValue,
                                        Index.Invalid, Index.Invalid, "-flags",
                                        new Variant(procedureFlags)),
                                    Option.CreateEndOfOptions()
                                });

                                int argumentIndex = Index.Invalid;

                                code = interpreter.GetOptions(
                                    options, arguments, 0, 2, Index.Invalid,
                                    true, ref argumentIndex, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    if ((argumentIndex != Index.Invalid) &&
                                        (((argumentIndex + 1) == argumentCount) ||
                                        ((argumentIndex + 2) == argumentCount)))
                                    {
                                        IVariant value = null;

                                        if (options.IsPresent("-flags", ref value))
                                            procedureFlags = (ProcedureFlags)value.Value;

                                        string group = null;

                                        if (options.IsPresent("-group", ref value))
                                            group = value.ToString();

                                        string description = null;

                                        if (options.IsPresent("-description", ref value))
                                            description = value.ToString();

                                        ProcedureFlags? savedProcedureFlags = procedureFlags;

                                        CommonOps.MaskRegisteredFlags(ref procedureFlags);

                                        string registrationName = null;
                                        IScriptLocation body = null;

                                        code = CommonOps.CreateNameForRegisteredProcedure(
                                            interpreter, arguments, argumentIndex,
                                            savedProcedureFlags, ref registrationName,
                                            ref body, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            string savedArguments = null;
                                            ArgumentList formalArguments = null;
                                            ArgumentDictionary namedArguments = null;

                                            if ((argumentIndex + 2) == argumentCount)
                                            {
                                                savedArguments = arguments[argumentIndex];

                                                StringList list1 = null;

                                                code = Parser.SplitList(
                                                    interpreter, savedArguments, 0, Length.Invalid,
                                                    true, ref list1, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    StringPairList list2 = null;

                                                    code = Utility.GetFormalArgumentNamesAndDefaults(
                                                        interpreter, list1, ref list2, ref result);

                                                    if (code == ReturnCode.Ok)
                                                    {
                                                        if (Utility.HasFlags(procedureFlags,
                                                                ProcedureFlags.NamedArguments, true))
                                                        {
                                                            code = Utility.GetFormalAndNamedArguments(
                                                                registrationName, list2, ref formalArguments,
                                                                ref namedArguments, ref result);
                                                        }
                                                        else if (Utility.HasFlags(procedureFlags,
                                                                ProcedureFlags.PositionalArguments, true))
                                                        {
                                                            formalArguments = new ArgumentList(
                                                                list2, ArgumentFlags.NameOnly);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                //
                                                // HACK: First, assume a default procedure type
                                                //       of "positional arguments" -AND- there
                                                //       are no formal arguments.  It actually
                                                //       does not matter if there are no formal
                                                //       arguments.
                                                //
                                                formalArguments = new ArgumentList();

                                                //
                                                // HACK: Next, if there are supposed to be named
                                                //       arguments, create an empty list of them,
                                                //       i.e. since there are none.
                                                //
                                                if (Utility.HasFlags(procedureFlags,
                                                        ProcedureFlags.NamedArguments, true))
                                                {
                                                    namedArguments = new ArgumentDictionary();
                                                }
                                            }

                                            if (code == ReturnCode.Ok)
                                            {
                                                bool isLibrary;
                                                bool isPrivate;
                                                bool isFast;
                                                bool isAtomic;
                                                bool isInline;

#if ARGUMENT_CACHE || PARSE_CACHE
                                                bool isNonCaching;
#endif

                                                bool isMatchTypes;
                                                ArgumentList overwriteArguments = null;
                                                ArgumentList cleanArguments = null;

                                                Utility.ShouldProcedureHaveFlags(
                                                    interpreter, registrationName,
                                                    (Argument)body, interpreter.CultureInfo,
                                                    out isLibrary, out isPrivate, out isFast,
                                                    out isAtomic, out isInline,
#if ARGUMENT_CACHE || PARSE_CACHE
                                                    out isNonCaching,
#endif
                                                    out isMatchTypes, out overwriteArguments,
                                                    out cleanArguments);

                                                code = Utility.SanityCheckAndModifyProcedureFlags(
                                                    isLibrary, isPrivate, isFast, isAtomic, isInline,
#if ARGUMENT_CACHE || PARSE_CACHE
                                                    isNonCaching,
#endif
                                                    isMatchTypes, ref procedureFlags, ref result);

                                                if (code == ReturnCode.Ok)
                                                {
                                                    string procedureName = interpreter.AreNamespacesEnabled() ?
                                                        Utility.MakeQualifiedName(interpreter, registrationName, false) :
                                                        Utility.MakeCommandName(registrationName);

                                                    IProcedure procedure = Utility.NewProcedure(
                                                        interpreter, procedureName, group, description,
                                                        procedureFlags, formalArguments, namedArguments,
                                                        overwriteArguments, cleanArguments, (Argument)body,
                                                        body, clientData, ref result);

                                                    if (procedure != null)
                                                    {
                                                        //
                                                        // HACK: If the procedure we just created is actually
                                                        //       an obfuscated one (i.e. because our procedure
                                                        //       callback is installed), then use it verbatim;
                                                        //       however, make sure it has the correct saved
                                                        //       arguments and procedure flags, for use during
                                                        //       pre-execution verification.
                                                        //
                                                        // HACK: Inside the CommonOps method called here, it
                                                        //       actually checks the (new) procedure against
                                                        //       the Registered class type, which is the base
                                                        //       class for the Obfuscated class.
                                                        //
                                                        long registrationToken = 0;

                                                        if (CommonOps.MaybeSetSavedArgumentsAndProcedureFlags(
                                                                procedure, savedArguments, savedProcedureFlags))
                                                        {
                                                            code = interpreter.AddOrUpdateProcedure(
                                                                procedure, clientData, ref registrationToken,
                                                                ref result);
                                                        }
                                                        else
                                                        {
                                                            code = interpreter.AddOrUpdateProcedure(new Registered(
                                                                procedure, savedArguments, savedProcedureFlags),
                                                                clientData, ref registrationToken, ref result);
                                                        }

                                                        if (code == ReturnCode.Ok)
                                                        {
                                                            result = StringList.MakeList(
                                                                registrationToken, procedureName);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        code = ReturnCode.Error;
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
                                                "wrong # args: should be \"{0} {1} ?options? ?arguments? body\"",
                                                this.Name, subCommand);
                                        }

                                        code = ReturnCode.Error;
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} ?options? ?arguments? body\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "selftest":
                        {
                            if (argumentCount == 2)
                            {
#if EMIT && NATIVE
                                if (!Utility.IsMono())
                                {
                                    code = HookSelfTest.PerformTest(
                                        interpreter, HookSelfTest.GetExpectedList(),
                                        0, PatchFlags.SelfTest, ref result);
                                }
                                else
                                {
                                    result = "not supported on this platform";
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
                                    "wrong # args: should be \"{0} {1}\"",
                                    this.Name, subCommand);

                                code = ReturnCode.Error;
                            }
                            break;
                        }
                    case "unregister": /* deletes a "registered procedure" */
                        {
                            if (argumentCount == 3)
                            {
                                long registrationToken = 0;
                                string registrationName = null;

                                code = CommonOps.ParseRegistration(
                                    interpreter, arguments[2], ref registrationToken,
                                    ref registrationName, ref result);

                                if (code == ReturnCode.Ok)
                                {
                                    string procedureName = null;
                                    IProcedure procedure = null;

                                    code = interpreter.GetProcedure(
                                        registrationToken, LookupFlags.NoWrapper,
                                        ref procedureName, ref procedure, ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        ProcedureFlags procedureFlags = ProcedureFlags.None;

                                        code = CommonOps.IsRegisteredProcedure(
                                            interpreter, registrationName, procedureName,
                                            procedure, ref procedureFlags, ref result);

                                        if (code == ReturnCode.Ok)
                                        {
                                            //
                                            // HACK: The read-only flag is being used to
                                            //       prevent __other__ code from removing
                                            //       this procedure; however, it should be
                                            //       obvious that this code is allowed to
                                            //       remove this procedure.
                                            //
                                            procedureFlags &= ~ProcedureFlags.ReadOnly;
                                            procedure.Flags = procedureFlags;

                                            code = interpreter.RemoveProcedure(
                                                registrationToken, clientData, ref result);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                result = String.Format(
                                    "wrong # args: should be \"{0} {1} registration\"",
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
