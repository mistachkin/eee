/*
 * Default.cs --
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
using System.Globalization;
using System.IO;
using Eagle._Attributes;
using Eagle._Components.Public;

#if PLUGIN_COMMANDS
using Eagle._Constants;
#endif

using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using Badge.Components.Private;

#if PLUGIN_COMMANDS
using _Assembly = System.Reflection.Assembly;
using _AssemblyName = System.Reflection.AssemblyName;
#endif

using _Plugins = Eagle._Plugins;

#if !CONSOLE
using ConsoleColor = Eagle._Components.Public.ConsoleColor;
#endif

namespace Badge.Plugins
{
    /// <summary>
    /// Provides the base Badge plugin implementation shared by the Badge
    /// plugin variants.  It services resource-string lookups (optionally
    /// overridden by an in-memory string dictionary), performs license
    /// certificate verification, and handles the request operations behind the
    /// <c>badge</c> command's string-management sub-commands.
    /// </summary>
    [ObjectId("1336c2bb-5118-4744-a3a3-0a1f5c03231c")]
    internal class Default : _Plugins.Default
    {
        #region Private Constants
#if PLUGIN_COMMANDS
        //
        // HACK: If this environment variable is set [to anything], returning
        //       any script certificates (and other resource strings) will be
        //       disabled.
        //
        /// <summary>
        /// The name of the environment variable that, when set, disables
        /// returning any resource strings (including script certificates) from
        /// this plugin.
        /// </summary>
        public const string DisabledEnvVarName =
            "BadgePluginDisabled"; /* MAY NOT BE NULL */
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        #region GetString Data
#if PLUGIN_COMMANDS
        //
        // NOTE: This boolean is used to control whether or not the GetString
        //       interface method actually tries to lookup requested embeded
        //       resource names.  It defaults to true upon construction of an
        //       instance of this class.
        //
        /// <summary>
        /// Whether the <c>GetString</c> method actually looks up requested
        /// resource strings; defaults to true unless the disabling environment
        /// variable is set.
        /// </summary>
        private bool enabled;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This dictionary of strings is used to augment or override
        //       those used to service the GetString method (i.e. embedded
        //       within the assembly resources).
        //
        /// <summary>
        /// The named override strings that augment or replace the embedded
        /// resource strings used by <c>GetString</c>; an entry here takes
        /// precedence over a same-named resource, even when its value is null.
        /// </summary>
        private StringDictionary strings;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Certificate Data
#if LICENSING
        //
        // NOTE: The certificate file name currently in use.
        //
#if MONO_BUILD
#pragma warning disable 414
#endif
        /// <summary>
        /// The file name of the license certificate currently in use.
        /// </summary>
        private string certificateFileName;
#if MONO_BUILD
#pragma warning restore 414
#endif

        //
        // NOTE: The certificate currently in use.
        //
        /// <summary>
        /// The license certificate currently in use.
        /// </summary>
        private object certificate;
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Default" /> plugin
        /// class.  Resource-string lookups are enabled unless the disabling
        /// environment variable is set, and an empty override-string
        /// dictionary is created.
        /// </summary>
        /// <param name="pluginData">
        /// The data used to create and configure the plugin.
        /// </param>
        public Default(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

#if PLUGIN_COMMANDS
            //
            // HACK: Skip enabling the GetString method if the environment
            //       variable is set.
            //
            if (!Utility.DoesEnvironmentVariableExist(DisabledEnvVarName))
                this.enabled = true; /* COMPAT: Eagle beta. */

            //
            // NOTE: Initially, there are no strings in the dictionary of
            //       named, "non-resource" strings.  When present, a named
            //       string in this dictionary will override a string with
            //       the same name that exists as an embedded resource in
            //       the assembly itself, even if the value of that string
            //       is null.
            //
            this.strings = new StringDictionary();
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
#if LICENSING
        /// <summary>
        /// Gets the license certificate currently in use.
        /// </summary>
        /// <returns>
        /// The current certificate, or null when none is set.
        /// </returns>
        protected virtual object GetCertificate()
        {
            return this.certificate;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the current certificate file name and certificate, and sets
        /// or clears the licensed plugin flag accordingly.
        /// </summary>
        /// <param name="fileName">
        /// The certificate file name to store.
        /// </param>
        /// <param name="certificate">
        /// The certificate to store.
        /// </param>
        /// <param name="licensed">
        /// Non-zero to set the licensed flag; zero to clear it.
        /// </param>
        protected virtual void SetFlagAndData(
            string fileName,    /* in */
            object certificate, /* in */
            bool licensed       /* in */
            )
        {
            this.certificateFileName = fileName;
            this.certificate = certificate;

            if (licensed)
                this.Flags |= PluginFlags.Licensed;
            else
                this.Flags &= ~PluginFlags.Licensed;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Gets the simple (assembly) name of this plugin, used as a prefix
        /// for banner output.
        /// </summary>
        /// <returns>
        /// The simple assembly name, or null when it cannot be determined.
        /// </returns>
        protected virtual string GetSimpleName()
        {
            Type type = GetType();

            if (type == null)
                return null;

            _Assembly assembly = type.Assembly;

            if (assembly == null)
                return null;

            _AssemblyName assemblyName = assembly.GetName();

            if (assemblyName == null)
                return null;

            return assemblyName.Name;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if PLUGIN_COMMANDS
        /// <summary>
        /// Determines whether this plugin instance is the
        /// <see cref="Security.Certificates" /> variant.
        /// </summary>
        /// <returns>
        /// Non-zero when this is the certificates variant; otherwise, zero.
        /// </returns>
        private bool IsSecurityCertificates()
        {
            return Object.ReferenceEquals(
                GetType(), typeof(Security.Certificates));
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IState Members
        /// <summary>
        /// Initializes the plugin.  When licensing is enabled, the plugin's
        /// certificate is verified and stored and the licensed flag is set;
        /// the base initialization then runs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the plugin is being initialized in.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Initialize(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
#if LICENSING
            LicenseOps.SetupWellKnownConfigurationData(this.AppDomain);

            ReturnCode code;
            string fileName = null;
            object certificate = null;

            code = LicenseOps.VerifyCertificate(
                interpreter, this.Assembly, null, this, null, null, null,
                null, null, null, null, null, null, null, null, true, false,
                true, true, LicenseOps.UseIsolated(GetType()), null,
                null, new AnyClientData(clientData, false), ref fileName,
                ref certificate, ref result);

            if (code != ReturnCode.Ok)
                return code;

            SetFlagAndData(fileName, certificate, true);
#endif

            return base.Initialize(interpreter, clientData, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Terminates the plugin, clearing the certificate state and the
        /// licensed flag, then running the base termination.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the plugin is being terminated in.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Terminate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            SetFlagAndData(null, null, false);

            return base.Terminate(interpreter, clientData, ref result);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecuteRequest Members
#if PLUGIN_COMMANDS
        /// <summary>
        /// Handles an inter-plugin request.  When the request is a string
        /// array naming one of the supported override-string operations
        /// (enable, clearstrings, getstring, liststrings, removestring,
        /// nullstring/setstring, renullstring/resetstring), it is serviced
        /// here; any unrecognized request is forwarded to the base plugin.
        /// These operations back the corresponding <c>badge</c> sub-commands.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller; not used by this method.
        /// </param>
        /// <param name="request">
        /// The request to handle, typically a string array describing the
        /// operation and its arguments.
        /// </param>
        /// <param name="response">
        /// Upon success, receives the response value for the operation.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Execute(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in: NOT USED */
            object request,          /* in */
            ref object response,     /* out */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: This method is not supposed to raise an error under
            //       normal conditions when faced with an unrecognized
            //       request.  It simply does nothing and lets the base
            //       plugin handle it.
            //
            if (request is string[])
            {
                string[] operation = (string[])request;
                int length = operation.Length;

                if ((length >= 1) && (length <= 2) &&
                    Utility.SystemStringEquals(operation[0], "enable"))
                {
                    if (length >= 2)
                    {
                        CultureInfo cultureInfo = (interpreter != null) ?
                            interpreter.CultureInfo : null;

                        bool boolValue = false;

                        if (Value.GetBoolean2(
                                operation[1], ValueFlags.AnyBoolean,
                                cultureInfo, ref boolValue,
                                ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        enabled = boolValue;
                    }

                    response = enabled;
                    return ReturnCode.Ok;
                }

                if ((length == 1) &&
                    Utility.SystemStringEquals(operation[0], "clearstrings"))
                {
                    int count = 0;

                    if (!DictionaryOps<string, string>.TryClear(
                            strings, ref count, ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = count;
                    return ReturnCode.Ok;
                }

                if ((length == 2) &&
                    Utility.SystemStringEquals(operation[0], "getstring"))
                {
                    string value;

                    if (!DictionaryOps<string, string>.TryGetValue(
                            strings, operation[1], out value, ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = value;
                    return ReturnCode.Ok;
                }

                if ((length == 1) &&
                    Utility.SystemStringEquals(operation[0], "liststrings"))
                {
                    List<string> list = null;

                    if (!DictionaryOps<string, string>.TryListKeys(
                            strings, out list, ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = new StringList(list);
                    return ReturnCode.Ok;
                }

                if ((length == 2) &&
                    Utility.SystemStringEquals(operation[0], "removestring"))
                {
                    if (!DictionaryOps<string, string>.TryRemoveValue(
                            strings, operation[1], ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = String.Empty;
                    return ReturnCode.Ok;
                }

                if ((length == 3) &&
                    (Utility.SystemStringEquals(operation[0], "nullstring") ||
                    Utility.SystemStringEquals(operation[0], "setstring")))
                {
                    bool added = false;

                    if (!DictionaryOps<string, string>.TrySetValue(
                            strings, operation[1], operation[2], true,
                            ref added, ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = added;
                    return ReturnCode.Ok;
                }

                if ((length == 3) &&
                    (Utility.SystemStringEquals(operation[0], "renullstring") ||
                    Utility.SystemStringEquals(operation[0], "resetstring")))
                {
                    bool added = false;

                    if (!DictionaryOps<string, string>.TrySetValue(
                            strings, operation[1], operation[2], false,
                            ref added, ref error))
                    {
                        return ReturnCode.Error;
                    }

                    response = added;
                    return ReturnCode.Ok;
                }
            }

            //
            // NOTE: Call the base plugin and let it handle the request.
            //
            return base.Execute(
                interpreter, clientData, request, ref response, ref error);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
        /// <summary>
        /// Gets a resource string by name.  When plugin strings are enabled,
        /// an override string is returned first if present; otherwise the
        /// embedded resource is looked up verbatim and then by its
        /// package-relative form.  When strings are disabled, an error is
        /// returned.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the string.
        /// </param>
        /// <param name="name">
        /// The name of the resource string to retrieve.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to select the resource string.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the accumulated errors describing why the
        /// string could not be found.
        /// </param>
        /// <returns>
        /// The resource string, or null when it could not be found.
        /// </returns>
        public override string GetString(
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            //
            // TODO: Secondly, consider changing this method to include a
            //       search of the ISnippetManager (within the specified
            //       interpreter).
            //
            ResultList errors = null;
            string value; /* REUSED */
            string localName; /* REUSED */
            Result localError; /* REUSED */

            ///////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
            if (!enabled)
            {
                error = "plugin strings not enabled";
                return null;
            }

            localError = null;

            if (DictionaryOps<string, string>.TryGetValue(
                    strings, name, out value, ref localError))
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(String.Format(
                    "GetString: plugin override string {0}",
                    Utility.FormatWrapOrNull(name)),
                    typeof(Default).Name, TracePriority.Lower |
                        TracePriority.FromPlugin);
#endif

                return value;
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }
#endif

            ///////////////////////////////////////////////////////////////////

            localError = null;

            value = Utility.GetAnyString(
                interpreter, this, ResourceManager, name,
                cultureInfo, ref localError);

            if (value != null)
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(String.Format(
                    "GetString: verbatim resource string {0}",
                    Utility.FormatWrapOrNull(name)),
                    typeof(Default).Name, TracePriority.Lower |
                        TracePriority.FromPlugin);
#endif

                return value;
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            ///////////////////////////////////////////////////////////////////

            localError = null;

            localName = Utility.GetPackageRelativeFileName(
                name, true, false, ref localError);

            if (localName != null)
            {
                localName = Utility.TranslatePath(
                    localName, PathTranslationType.Unix);

                localError = null;

                value = Utility.GetAnyString(
                    interpreter, this, ResourceManager, localName,
                    cultureInfo, ref localError);

                if (value != null)
                {
#if DEBUG || FORCE_TRACE
                    Utility.DebugTrace(String.Format(
                        "GetString: relative resource string {0}",
                        Utility.FormatWrapOrNull(localName)),
                        typeof(Default).Name, TracePriority.Lower |
                            TracePriority.FromPlugin);
#endif

                    return value;
                }
                else if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            Utility.DebugTrace(String.Format(
                "GetString: failed to find string {0} or {1}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(localName)),
                typeof(Default).Name, TracePriority.Low |
                    TracePriority.FromPlugin);
#endif

            error = errors;
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Gets the file name of a license certificate.  When a name is
        /// supplied, the plugin-relative file name for that certificate type
        /// is returned; otherwise the file name of the certificate currently
        /// in use is returned.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate file name.
        /// </param>
        /// <param name="name">
        /// The certificate type name, or null/empty for the certificate
        /// currently in use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate file name, or null on failure.
        /// </returns>
        public override string GetCertificateFileName(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            string fileName;

            if (!String.IsNullOrEmpty(name))
            {
                fileName = Utility.GetPluginRelativeFileName(
                    this, null, name);

                if (fileName == null)
                    error = "unsupported certificate type";
            }
            else
            {
                fileName = certificateFileName;

                if (fileName == null)
                    error = "invalid file name";
            }

            return fileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the license certificate currently in use as an identifier.  A
        /// non-empty name is rejected as an unsupported certificate type, and
        /// the certificate is unavailable when the plugin is isolated in a
        /// different application domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate.
        /// </param>
        /// <param name="name">
        /// Must be null or empty; a non-empty value is unsupported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate identifier, or null on failure.
        /// </returns>
        public override IIdentifier GetCertificate(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (!String.IsNullOrEmpty(name))
            {
                error = "unsupported certificate type";
                return null;
            }

            if (Utility.IsCrossAppDomain(interpreter, this))
            {
                error = "unsupported when plugin is isolated";
                return null;
            }

            IIdentifier identifier = certificate as IIdentifier;

            if (identifier == null)
                error = "invalid certificate";

            return identifier;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Writes the plugin's startup banner.  For the certificates variant,
        /// when strings are enabled, a status line reporting the number of
        /// override strings is written to the interpreter host (using the
        /// host's configured colors when available); other variants do
        /// nothing.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose host receives the banner.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Banner(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            if (!IsSecurityCertificates())
                return ReturnCode.Ok;

            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            IHost host = interpreter.Host;

            if (host == null)
            {
                result = "interpreter host not available";
                return ReturnCode.Error;
            }

            ConsoleColor foregroundColor = _ConsoleColor.None;
            ConsoleColor backgroundColor = _ConsoleColor.None;

            if (Utility.HasFlags(
                    host.GetHostFlags(), HostFlags.AllColors, false))
            {
                Result error = null;

                /* IGNORED */
                host.GetColors(null,
                    ColorName.Enabled, true, true, ref foregroundColor,
                    ref backgroundColor, ref error);
            }

            if (enabled)
            {
                //
                // NOTE: Emit a blank line to separate the status lines
                //       emitted by this plugin from those emitted by
                //       the core (or other plugins).
                //
                host.WriteLine();

                //
                // NOTE: Do we have colors configured for this output?  If
                //       so, use them; otherwise, use the method without
                //       any color output.
                //
                string prefix = GetSimpleName();

                string value = String.Format(
                    "{0}: Certificates are enabled with {1} overrides.",
                    prefix, (strings != null) ? strings.Count : 0);

                if ((foregroundColor != _ConsoleColor.None) ||
                    (backgroundColor != _ConsoleColor.None))
                {
                    host.WriteLine(value, foregroundColor, backgroundColor);
                }
                else
                {
                    host.WriteLine(value);
                }
            }

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Produces the plugin's "about" information, including the license
        /// certificate details.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the about information.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the about information or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode About(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            return LicenseOps.AboutCertificate(interpreter, this,
                GetCertificate(), LicenseOps.UseIsolated(GetType()),
                ref result);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces the list of conditional compilation options that were
        /// active when the plugin was built.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the options.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the list of build options.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Options(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            result = new StringList(DefineConstants.OptionList, false);
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Produces a short status line describing the plugin and whether its
        /// string lookups are enabled.  In a safe interpreter, only
        /// "Present" is reported.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the status.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the status string.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Status(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            if ((interpreter != null) && interpreter.IsSafe())
            {
                result = "Present";
                return ReturnCode.Ok;
            }

            string typeName = this.TypeName;

            result = String.Format(
                "{0} Plugin {1}", (typeName != null) ? typeName :
                "<Unknown>", enabled ? "Enabled" : "DISABLED");

            return ReturnCode.Ok;
        }
#endif
        #endregion
    }
}
