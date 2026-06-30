/*
 * CertificateIsolatedOps.cs --
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
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using DataOps = Licensing.Components.Private.CertificateDataOps;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods used to support certificate operations that
    /// may execute across application domain boundaries, e.g. when a plugin
    /// is loaded in isolated mode.
    /// </summary>
    [ObjectId("3fef745b-f6dd-4fa0-9342-348e856997e3")]
    internal static class CertificateIsolatedOps
    {
        #region Isolated Plugin Support
#if ISOLATED_PLUGINS || CERTIFICATE_POLICY || PLUGIN_COMMANDS
        /// <summary>
        /// Determines whether the specified plugin should be treated as
        /// isolated, e.g. because it was loaded into an application domain
        /// that differs from that of its parent interpreter or because the
        /// parent interpreter was created within a non-default application
        /// domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the plugin.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to check for cross-application-domain isolation.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin should be treated as isolated; otherwise,
        /// zero.
        /// </returns>
        private static bool ShouldTreatAsIsolated(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            //
            // NOTE: If this plugin has been loaded into an AppDomain that is
            //       different from its parent interpreter then treat ourself
            //       as isolated.
            //
            if (CertificateSharedOps.IsCrossAppDomain(
                    interpreter, pluginData))
            {
                return true;
            }

            //
            // HACK: Otherwise, if the parent interpreter was created within
            //       a non-default AppDomain, we must assume this plugin may
            //       be isolated from the perspective of callers that may be
            //       the eventual destination of Result instances, e.g. when
            //       the [test2] command creates a brand new isolated parent
            //       interpreter for use when evaluating a test body to load
            //       this plugin.  This may result in false positives, which
            //       are harmless, if no interpreters actually exist in the
            //       default AppDomain.
            //
            AppDomain appDomain = interpreter.GetAppDomain();

            if ((appDomain == null) || !appDomain.IsDefaultAppDomain())
                return true;

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Examines the specified result and, when it contains a list of
        /// results whose error messages are all identical, collapses them
        /// into a single message.  When that message indicates a public key
        /// token mismatch, it is replaced with a "public key not trusted"
        /// message instead.
        /// </summary>
        /// <param name="result">
        /// The result to examine and possibly modify in place.
        /// </param>
        /// <returns>
        /// The number of result elements that were normalized.
        /// </returns>
        private static int MaybeNormalizeErrors(
            Result result /* in, out */
            )
        {
            int count = 0;

            if (result == null)
                return count;

            object value = result.Value;

            if (value == null)
                return count;

            IList<Result> list = value as IList<Result>;

            if ((list == null) || (list.Count <= 1))
                return count;

            Result firstElement = list[0];

            if (firstElement == null)
                return count;

            string firstString = firstElement.Value as string;

            if (firstString == null)
                return count;

            foreach (Result element in list)
            {
                if (element == null)
                    return count;

                string stringValue = element.Value as string;

                if (stringValue == null)
                    return count;

                if (!DataOps.StringEquals(stringValue, firstString))
                    return count;
            }

            if (DataOps.StringEquals(firstString,
                    Constants.PublicKeyTokenMismatchError) ||
                Parser.StringMatch(null, firstString, 0,
                    Constants.PublicKeyTokenMismatchPattern, 0, false))
            {
                result.Value = Constants.PublicKeyUntrustedError;
            }
            else
            {
                result.Value = firstString;
            }

            count += list.Count + 1;

            return count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts values contained in the specified result into types that
        /// can be safely marshalled to another application domain, e.g. by
        /// converting enumeration values to strings, formatting timestamps,
        /// and wrapping byte arrays.  Nested result collections are processed
        /// recursively.
        /// </summary>
        /// <param name="result">
        /// The result whose value should be fixed up in place.
        /// </param>
        /// <returns>
        /// The number of values that were converted.
        /// </returns>
        private static int MaybeFixupResult(
            Result result /* in, out */
            )
        {
            int count = 0;

            if (result == null)
                return count;

            object value = result.Value;

            if (value == null)
                return count;

            IEnumerable<Result> collection = value as IEnumerable<Result>;

            if (collection != null) /* NOTE: *REQD* Maybe assembly? */
            {
                foreach (Result item in collection)
                    count += MaybeFixupResult(item); /* RECURSIVE */

                return count;
            }

            if ((value is OperationStatus) ||  /* NOTE: *REQD* This assembly. */
                (value is ProtocolType) ||     /* NOTE: *REQD* This assembly. */
                (value is EntityType) ||       /* NOTE: *REQD* This assembly. */
                (value is PolicyTraceFlags) || /* NOTE: *REQD* This assembly. */
                (value is StorageType) ||      /* NOTE: *REQD* This assembly. */
                (value is AssemblyKeyType))    /* NOTE: *REQD* This assembly. */
            {
                result.Value = value.ToString();
                count++;
            }
            else if (value is DateTime)       /* NOTE: *REQD* Round-tripping. */
            {
                result.Value = DataOps.FormatTimeStamp((DateTime)value);
                count++;
            }
            else if (value is byte[])         /* NOTE: *REQD* Bad sub-type. */
            {
                result.Value = new ByteList((byte[])value);
                count++;
            }

            return count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Normalizes and fixes up the specified result so that it can be
        /// safely returned across application domain boundaries when the
        /// plugin is treated as isolated.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the plugin.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to determine whether isolation handling is
        /// required.
        /// </param>
        /// <param name="result">
        /// The result to normalize and fix up in place.
        /// </param>
        /// <returns>
        /// The total number of result values that were normalized or
        /// converted.
        /// </returns>
        public static int MaybeFixupResult(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            Result result            /* in, out */
            )
        {
            //
            // HACK: If the result is really a list-of-results (i.e. ResultList
            //       object), check and see if all the error messages are the
            //       same.  If so, remove all duplicate messages.  Additionally,
            //       if all the error messages are "public key token mismatch",
            //       create one error message "public key not trusted" instead.
            //
            // TODO: Add (more) information here about why this error message
            //       transformation is useful.
            //
            // TODO: Consider changing final error message to be more useful
            //       when troubleshooting.
            //
            // NOTE: It is now possible to disable this behavior, e.g. just in
            //       case the original error message(s) contain more details,
            //       etc.
            //
            int count = 0;

            if (CertificateIsolatedState.GetNormalizeErrors())
                count += MaybeNormalizeErrors(result);

            //
            // NOTE: Otherwise, if this plugin was not loaded into an isolated
            //       application domain, do nothing.
            //
            if (!ShouldTreatAsIsolated(interpreter, pluginData))
                goto done;

            //
            // BUGFIX: We must convert any of our enumeration values contained
            //         in the result, if any, to strings because the referenced
            //         enumerated types in this assembly most likely cannot be
            //         marshalled back to the interpreter AppDomain -OR- to the
            //         parent interpreter AppDomain if this interpreter is not
            //         the parent.
            //
            count += MaybeFixupResult(result);

            ///////////////////////////////////////////////////////////////////

        done:

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "MaybeFixupResult: count = {0}, result = {1}",
                count, Utility.FormatWrapOrNull(result)),
                typeof(CertificateIsolatedOps).Name,
                TracePriority.Lowest);
#endif

            ///////////////////////////////////////////////////////////////////

            return count;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region License Manager Parameter Support
#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// Supplies default values for any optional license manager
        /// parameters that were not provided by the caller.  This overload
        /// omits the renew callback parameter.
        /// </summary>
        /// <param name="skipOptional">
        /// Non-zero to skip supplying defaults for optional parameters when
        /// the plugin is not isolated.
        /// </param>
        /// <param name="assembly">
        /// The assembly to use; when null, a default value may be supplied.
        /// </param>
        /// <param name="plugin">
        /// The plugin used to determine whether the call is isolated.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; when null, a default value is supplied.
        /// </param>
        public static void MaybeFixupParameters(
            bool skipOptional,     /* in */
            ref Assembly assembly, /* in, out */
            ref IPlugin plugin,    /* in */
            ref Encoding encoding  /* in, out */
            )
        {
            RenewCallback renewCallback = null;

            MaybeFixupParameters(
                skipOptional, ref assembly, ref plugin, ref encoding,
                ref renewCallback);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if ISOLATED_PLUGINS || (NETWORK && CERTIFICATE_RENEWAL)
        /// <summary>
        /// Supplies default values for any optional license manager
        /// parameters that were not provided by the caller, including the
        /// renew callback used when renewing certificates.
        /// </summary>
        /// <param name="skipOptional">
        /// Non-zero to skip supplying defaults for optional parameters when
        /// the plugin is not isolated.
        /// </param>
        /// <param name="assembly">
        /// The assembly to use; when null, a default value may be supplied.
        /// </param>
        /// <param name="plugin">
        /// The plugin used to determine whether the call is isolated.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use; when null, a default value is supplied.
        /// </param>
        /// <param name="renewCallback">
        /// The renew callback to use; when null, a default value may be
        /// supplied.
        /// </param>
        public static void MaybeFixupParameters(
            bool skipOptional,              /* in */
            ref Assembly assembly,          /* in, out */
            ref IPlugin plugin,             /* in */
            ref Encoding encoding,          /* in, out */
            ref RenewCallback renewCallback /* in, out */
            )
        {
            bool isolated = Utility.IsCrossAppDomain(plugin);

            if ((assembly == null) && (!skipOptional || isolated))
                assembly = CertificateAssemblyOps.GetObject();

            if (encoding == null)
                encoding = DataOps.GetDefaultEncoding();

#if NETWORK && CERTIFICATE_RENEWAL
            if ((renewCallback == null) && (!skipOptional || isolated))
                renewCallback = CertificateRenewalOps.DefaultRenewCallback;
#endif
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Isolated License Manager Support
#if ISOLATED_PLUGINS
        //
        // TODO: This method contains hard-coded array indexes for the
        //       arguments marshalled from the other application domain.
        //       Keep updated.
        //
        /// <summary>
        /// Fixes up the optional parameters contained at well-known indexes
        /// within the specified marshalled argument array, supplying default
        /// values where necessary.
        /// </summary>
        /// <param name="args">
        /// The marshalled argument array to fix up in place.
        /// </param>
        /// <param name="skipOptional">
        /// Non-zero to skip supplying defaults for optional parameters when
        /// the plugin is not isolated.
        /// </param>
        private static void MaybeFixupRequestArgs(
            object[] args,    /* in, out */
            bool skipOptional /* in */
            )
        {
            Assembly assembly = args[1] as Assembly;
            IPlugin plugin = args[3] as IPlugin;
            Encoding encoding = args[6] as Encoding;
            RenewCallback renewCallback = args[18] as RenewCallback;

            MaybeFixupParameters(
                skipOptional, ref assembly, ref plugin, ref encoding,
                ref renewCallback);

            args[1] = assembly;
            args[3] = plugin;
            args[6] = encoding;
            args[18] = renewCallback;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a value of the specified type is permitted to
        /// be null, i.e. because it is a reference type or a nullable value
        /// type.
        /// </summary>
        /// <param name="type">
        /// The type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a value of the specified type may be null; otherwise,
        /// zero.
        /// </returns>
        private static bool TypeCanHaveNullValue(
            Type type /* in */
            )
        {
            //
            // NOTE: Is the type itself invalid?  If so, we cannot check it
            //       further.  An invalid type cannot have a null value.
            //
            if (type == null)
                return false;

            //
            // NOTE: If this is not a value type (of any kind), it must be
            //       a reference type and all reference types are allowed
            //       to have a value of null.
            //
            if (!type.IsValueType)
                return true;

            //
            // NOTE: If this is not a generic type, it must be a value type
            //       because it cannot be a nullable value type; therefore,
            //       it cannot have a value of null.
            //
            if (!type.IsGenericType)
                return false;

            //
            // NOTE: If the generic type definition is Nullable<>, then it
            //       really should be treated as a reference type and can
            //       have a value of null; otherwise, it cannot.
            //
            Type genericType = type.GetGenericTypeDefinition();

            return (genericType == typeof(Nullable<>));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the elements of the specified argument array so that they
        /// match the expected parameter types, handling values that were
        /// marshalled from another application domain and constructing
        /// certificate instances from dictionaries or serialized data as
        /// needed.
        /// </summary>
        /// <param name="types">
        /// The expected types for the leading elements of the argument array.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture information used when decrypting serialized
        /// certificate data.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when decrypting
        /// serialized certificate data.
        /// </param>
        /// <param name="args">
        /// The argument array to convert in place.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode MarshalArguments(
            Type[] types,            /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            int? timeout,            /* in: OPTIONAL */
            ref object[] args,       /* in, out */
            ref Result error         /* out */
            )
        {
            if (types == null)
            {
                error = "invalid type array";
                return ReturnCode.Error;
            }

            if (args == null)
            {
                error = "invalid argument array";
                return ReturnCode.Error;
            }

            int typesLength = types.Length;
            int argsLength = args.Length;

            if (argsLength < typesLength)
            {
                error = String.Format(
                    "malformed request: have {0} elements, need at least {1}",
                    argsLength, typesLength);

                return ReturnCode.Error;
            }

            Interpreter interpreter = null; /* NOTE: Found value, if any. */
            IPlugin plugin = null;          /* NOTE: Found value, if any. */
            object[] newArgs = new object[argsLength];

            for (int index = 0; index < typesLength; index++)
            {
                Type type = types[index];

                if (type == null)
                {
                    newArgs[index] = null;
                    continue;
                }

                object arg = args[index];

                if (arg == null)
                {
                    if (!TypeCanHaveNullValue(type))
                    {
                        error = String.Format(
                            "malformed request ({0}): have type {1}, need type {2}",
                            index, typeof(object), type);

                        return ReturnCode.Error;
                    }

                    newArgs[index] = arg;
                    continue;
                }

                Type argType = arg.GetType();

                if (argType == null)
                {
                    error = String.Format(
                        "malformed request ({0}): have invalid type, need type {1}",
                        index, type);

                    return ReturnCode.Error;
                }

                if ((argType == type) || type.IsAssignableFrom(argType))
                {
                    newArgs[index] = arg;
                    continue;
                }

                //
                // NOTE: Handle the "well-known" target types that we know may
                //       derive from MarshalByRefObject when they come from
                //       another application domain.  For now, this includes
                //       the Interpreter class type as well as the IPlugin and
                //       IAnyClientData interface types.
                //
                if (argType.IsAssignableFrom(typeof(MarshalByRefObject)))
                {
                    if (type == typeof(Interpreter))
                    {
                        if (interpreter == null)
                            interpreter = (Interpreter)arg;

                        newArgs[index] = arg;
                        continue;
                    }
                    else if (type == typeof(IPlugin))
                    {
                        if (plugin == null)
                            plugin = (IPlugin)arg;

                        newArgs[index] = arg;
                        continue;
                    }
                    else if (type == typeof(IAnyClientData))
                    {
                        newArgs[index] = arg;
                        continue;
                    }
                }

                //
                // NOTE: If the target type is "Certificate" and we did not
                //       already handle this argument via an exact type match,
                //       check if the argument type is a string dictionary and
                //       then create a new "Certificate" instance from that,
                //       using the CreateFromDictionary static factory method.
                //
                if (type == typeof(ICertificate))
                {
                    ICertificate certificate;

                    if (arg is StringDictionary)
                    {
                        certificate = Certificate.CreateFromDictionary(
                            (StringDictionary)arg, ref error);

                        if (certificate == null)
                            return ReturnCode.Error;
                    }
#if XML && SERIALIZATION && NETWORK && CERTIFICATE_RENEWAL
                    else if (arg is byte[])
                    {
                        byte[] newData = (byte[])arg;

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                        if (DataOps.HasEncryptedDataHeader(newData))
                        {
                            Encoding encoding = DataOps.GetDefaultEncoding();

                            if (encoding != null)
                            {
                                string text = null;

                                try
                                {
                                    text = encoding.GetString(
                                        newData); /* throw */
                                }
                                catch (Exception e)
                                {
                                    error = e;
                                    return ReturnCode.Error;
                                }

                                if (CryptographyOps.ObtainParametersAndDecrypt(
                                        interpreter, plugin, encoding,
                                        null, text, cultureInfo, timeout,
                                        Constants.DefaultTraceOnError,
                                        ref newData, ref error) != ReturnCode.Ok)
                                {
                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                error = "default encoding unavailable";
                                return ReturnCode.Error;
                            }
                        }
#endif

                        certificate = null;

                        Result localError = null;

                        if (CertificateXmlOps.Import(
                                null, newData, true, ref certificate,
                                ref localError) != ReturnCode.Ok)
                        {
                            error = localError;
                            return ReturnCode.Error;
                        }
                    }
#endif
                    else
                    {
                        error = String.Format(
                            "malformed request ({0}): have type {1}, need type {2}",
                            index, Utility.FormatWrapOrNull(argType),
                            typeof(StringDictionary));

                        return ReturnCode.Error;
                    }

                    newArgs[index] = certificate;
                    continue;
                }

                error = String.Format(
                    "malformed request ({0}): have type {1}, need type {2}",
                    index, Utility.FormatWrapOrNull(argType), type);

                return ReturnCode.Error;
            }

            args = newArgs;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: This method contains hard-coded array indexes for the
        //       arguments marshalled from the other application domain.
        //
        /// <summary>
        /// Executes a single marshalled license manager request, dispatching
        /// to the appropriate operation based on the request name carried by
        /// the client data and returning the marshalled response.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request.  This parameter is
        /// not currently used.
        /// </param>
        /// <param name="licenseManager">
        /// The license manager used to service the request, if any.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying the request name and any associated
        /// options.
        /// </param>
        /// <param name="request">
        /// The marshalled request, expected to be an array of arguments.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture information used when marshalling arguments.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when marshalling
        /// arguments.
        /// </param>
        /// <param name="response">
        /// Upon success, receives the marshalled response value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public static ReturnCode ExecuteRequest(
            Interpreter interpreter,        /* in: NOT USED */
            ILicenseManager licenseManager, /* in */
            IClientData clientData,         /* in */
            object request,                 /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            int? timeout,                   /* in: OPTIONAL */
            ref object response,            /* out */
            ref Result error                /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return ReturnCode.Error;
            }

            if (!(clientData.Data is string))
            {
                error = String.Format(
                    "malformed request: clientData type {0}, need type {1}",
                    (clientData.Data != null) ? clientData.Data.GetType() :
                    typeof(object), typeof(string));

                return ReturnCode.Error;
            }

            if (!(request is object[]))
            {
                error = String.Format(
                    "malformed request: have type {0}, need type {1}",
                    (request != null) ? request.GetType() : typeof(object),
                    typeof(object[]));

                return ReturnCode.Error;
            }

            try
            {
                string name = (string)clientData.Data;
                object[] args = (object[])request;

                switch (name)
                {
                    case "AboutCertificate":
                        {
                            Type[] types = {
                                typeof(Interpreter), typeof(IPlugin),
                                typeof(ICertificate), typeof(Result)
                            };

                            if (MarshalArguments(
                                    types, cultureInfo, timeout, ref args,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            IAnyClientData anyClientData =
                                clientData as IAnyClientData;

                            Result result;
                            bool asDictionary;

                            if ((anyClientData != null) &&
                                anyClientData.TryGetBoolean(
                                    DataNames.AsDictionary, false,
                                    out asDictionary, ref error) &&
                                asDictionary)
                            {
                                result = Certificate.ToDictionaryString(
                                    (ICertificate)args[2]);
                            }
                            else
                            {
                                result = (Result)args[3];

                                if (licenseManager != null)
                                {
                                    if (licenseManager.AboutCertificate(
                                            (Interpreter)args[0],
                                            (IPlugin)args[1],
                                            (ICertificate)args[2],
                                            ref result) != ReturnCode.Ok)
                                    {
                                        error = result;
                                        return ReturnCode.Error;
                                    }
                                }
                                else
                                {
                                    if (CertificatePluginOps.About(
                                            (Interpreter)args[0],
                                            (IPlugin)args[1],
                                            (ICertificate)args[2],
                                            ref result) != ReturnCode.Ok)
                                    {
                                        error = result;
                                        return ReturnCode.Error;
                                    }
                                }
                            }

                            response = result;
                            return ReturnCode.Ok;
                        }
                    case "EvaluateFile":
                        {
                            Type[] types = {
                                typeof(Interpreter), typeof(IPlugin),
                                typeof(string), typeof(IAnyClientData)
                            };

                            if (MarshalArguments(
                                    types, cultureInfo, timeout, ref args,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            Result result = null;

                            if (licenseManager != null)
                            {
                                if (licenseManager.EvaluateFile(
                                        (Interpreter)args[0],
                                        (IPlugin)args[1],
                                        (string)args[2],
                                        (IAnyClientData)args[3],
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                using (EvaluateClientData evaluateClientData =
                                        EvaluateClientData.CreateFrom(
                                            (Interpreter)args[0],
                                            (IPlugin)args[1],
                                            (string)args[2],
                                            (IAnyClientData)args[3],
                                            ref result))
                                {
                                    if (evaluateClientData == null)
                                    {
                                        error = result;
                                        return ReturnCode.Error;
                                    }

                                    /* IGNORED */
                                    evaluateClientData.MaybeSetConfigurationPhase(
                                        ConfigurationPhase.Demand |
                                        ConfigurationPhase.Isolated);

                                    /* IGNORED */
                                    evaluateClientData.AttachTo(
                                        (IAnyClientData)args[3]);

                                    if (CertificateScriptOps.EvaluateFile(
                                            evaluateClientData,
                                            ref result) != ReturnCode.Ok)
                                    {
                                        error = result;
                                        return ReturnCode.Error;
                                    }
                                }
                            }

                            response = (result != null) ?
                                result.ToString() : null;

                            return ReturnCode.Ok;
                        }
                    case "GetCertificate":
                        {
                            Type[] types = {
                                typeof(Interpreter), typeof(IPlugin),
                                typeof(Guid), typeof(ICertificate),
                                typeof(Result)
                            };

                            if (MarshalArguments(
                                    types, cultureInfo, timeout, ref args,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            ICertificate certificate = null;
                            Result result = (Result)args[4];

                            if (licenseManager != null)
                            {
                                if (licenseManager.GetCertificate(
                                        (Interpreter)args[0],
                                        (IPlugin)args[1],
                                        (Guid)args[2],
                                        ref certificate,
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                if (!Helpers.GetLicenseCertificate(
                                        (Guid)args[2],
                                        ref certificate,
                                        ref result))
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }

                            response = new object[] {
                                (certificate != null) ?
                                    Certificate.ToDictionary(certificate) : null,
                                (result != null) ? result.ToString() :
                                    null
                            };

                            return ReturnCode.Ok;
                        }
                    case "MatchCertificateFlags":
                        {
                            Type[] types = {
                                typeof(IPlugin), typeof(ICertificate),
                                typeof(int), typeof(long),
                                typeof(string), typeof(string),
                                typeof(bool), typeof(bool),
                                typeof(bool)
                            };

                            if (MarshalArguments(
                                    types, cultureInfo, timeout, ref args,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            Result result = null;

                            if (licenseManager != null)
                            {
                                if (licenseManager.MatchCertificateFlags(
                                        (IPlugin)args[0],
                                        (ICertificate)args[1],
                                        (int)args[2],
                                        (long)args[3],
                                        (string)args[4],
                                        (string)args[5],
                                        (bool)args[6],
                                        (bool)args[7],
                                        (bool)args[8],
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                if (CertificateSharedOps.MatchFlags(
                                        (ICertificate)args[1],
                                        (FlagType)args[2],
                                        (long)args[3],
                                        (string)args[4],
                                        (string)args[5],
                                        (bool)args[6],
                                        (bool)args[7],
                                        (bool)args[8],
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }

                            response = (result != null) ?
                                result.ToString() : null;

                            return ReturnCode.Ok;
                        }
                    case "VerifyCertificate":
                        {
                            Type[] types = {
                                typeof(Interpreter), typeof(Assembly),
                                typeof(AssemblyName), typeof(IPlugin),
                                typeof(string), typeof(byte[]),
                                typeof(Encoding), typeof(IEnumerable<IKeyPair>),
                                typeof(string), typeof(string), typeof(int?),
                                typeof(ExecutionPolicy?), typeof(string),
                                typeof(string), typeof(bool), typeof(bool),
                                typeof(bool), typeof(ElementSelectionCallback),
                                typeof(RenewCallback), typeof(IAnyClientData),
                                typeof(string)
                            };

                            if (MarshalArguments(
                                    types, cultureInfo, timeout, ref args,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            string fileName = (string)args[20];
                            ICertificate certificate = null;
                            Result result = null;

                            if (licenseManager != null)
                            {
                                if (licenseManager.VerifyCertificate(
                                        (Interpreter)args[0],
                                        (Assembly)args[1],
                                        (AssemblyName)args[2],
                                        (IPlugin)args[3],
                                        (string)args[4],
                                        (byte[])args[5],
                                        (Encoding)args[6],
                                        (IEnumerable<IKeyPair>)args[7],
                                        (string)args[8],
                                        (string)args[9],
                                        (ExecutionPolicy?)args[10],
                                        (string)args[11],
                                        (string)args[12],
                                        (int?)args[13],
                                        (bool)args[14],
                                        (bool)args[15],
                                        (bool)args[16],
                                        (ElementSelectionCallback)args[17],
                                        (RenewCallback)args[18],
                                        (IAnyClientData)args[19],
                                        ref fileName,
                                        ref certificate,
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }
                            else
                            {
                                //
                                // HACK: Always disable the "strict" license
                                //       manager parameter handling here
                                //       because it is being called late-bound
                                //       via the SDK; otherwise, attempting to
                                //       use the isolated mode for a
                                //       non-isolated plugin will fail.
                                //
                                MaybeFixupRequestArgs(args, false);

                                if (CertificateVerifyOps.LoadAndProcess(
                                        (Interpreter)args[0],
                                        (Assembly)args[1],
                                        (AssemblyName)args[2],
                                        (IPlugin)args[3],
                                        (string)args[4],
                                        (byte[])args[5],
                                        (Encoding)args[6],
                                        (IEnumerable<IKeyPair>)args[7],
                                        (string)args[8],
                                        (string)args[9],
                                        (ExecutionPolicy?)args[10],
                                        (string)args[11],
                                        (string)args[12],
                                        (int?)args[13],
                                        (bool)args[14],
                                        (bool)args[15],
                                        (bool)args[16],
                                        (ElementSelectionCallback)args[17],
                                        (RenewCallback)args[18],
                                        (IAnyClientData)args[19],
                                        ref fileName,
                                        ref certificate,
                                        ref result) != ReturnCode.Ok)
                                {
                                    error = result;
                                    return ReturnCode.Error;
                                }
                            }

                            response = new object[] {
                                fileName, (certificate != null) ?
                                    Certificate.ToDictionary(certificate) : null,
                                (result != null) ? result.ToString() :
                                    null
                            };

                            return ReturnCode.Ok;
                        }
                    default:
                        {
                            error = String.Format(
                                "unsupported request {0}",
                                Utility.FormatWrapOrNull(name));

                            break;
                        }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
#endif
        #endregion
    }
}
