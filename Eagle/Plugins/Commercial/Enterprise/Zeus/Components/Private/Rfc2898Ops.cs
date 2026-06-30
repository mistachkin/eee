/*
 * Rfc2898Ops.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if NETWORK
using System.Globalization;
using System.IO;
#endif

#if NETWORK && XML
using System.Reflection;
#endif

using System.Text;

#if NETWORK && XML
using System.Xml;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;

#if NETWORK
using Eagle._Containers.Public;
#endif

using Eagle._Interfaces.Public;
using Zeus.Components.Public;
using Rfc2898Data = Zeus.Components.Public.Rfc2898Data;

namespace Zeus.Components.Private
{
    /// <summary>
    /// Provides the operations that create and use RFC 2898 (PBKDF2) data
    /// providers for the Zeus plugin.  This includes instantiating built-in
    /// and external provider types (locally or across application domains),
    /// fetching key-derivation parameters from a remote URI (parsed from
    /// either an XML document or a settings script), and getting or setting
    /// the data or provider held by a plugin's RFC 2898 data manager.
    /// </summary>
    [ObjectId("3f1f9dfe-5b04-4d6c-9881-329ff3330411")]
    internal static class Rfc2898Ops
    {
        #region Private Constants
        /// <summary>
        /// The placeholder text used in messages when a type name cannot be
        /// determined.
        /// </summary>
        private static readonly string DisplayBadTypeName = "<badTypeName>";

        ///////////////////////////////////////////////////////////////////////

        #region Assembly Constants
#if NETWORK && XML
        /// <summary>
        /// The assembly containing this code, used to locate its embedded XML
        /// schema.
        /// </summary>
        private static readonly Assembly thisAssembly =
            Assembly.GetExecutingAssembly();
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region XML Schema Constants
#if NETWORK && XML
        /// <summary>
        /// The name of the embedded resource containing the XML schema used to
        /// validate remote RFC 2898 data documents.
        /// </summary>
        private static readonly string SchemaResourceName = "Zeus.xsd";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The XML namespace prefix used when querying remote RFC 2898 data
        /// documents.
        /// </summary>
        private static readonly string SchemaNamespaceName = "zeus";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The XML namespace URI associated with the RFC 2898 data schema.
        /// </summary>
        private static readonly Uri SchemaNamespaceUri =
            Utility.GetAssemblyXmlSchemaUri(thisAssembly);
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Provider Creation Methods
        /// <summary>
        /// Formats an assembly name and type name into a human-readable type
        /// description for use in messages, falling back to a placeholder when
        /// neither is available.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name, if any.
        /// </param>
        /// <param name="typeName">
        /// The type name, if any.
        /// </param>
        /// <returns>
        /// A formatted type description.
        /// </returns>
        private static string FormatTypeName(
            string assemblyName, /* in */
            string typeName      /* in */
            )
        {
            if (!String.IsNullOrEmpty(assemblyName))
            {
                if (!String.IsNullOrEmpty(typeName))
                    return String.Format("{0}, {1}", typeName, assemblyName);
                else
                    return assemblyName;
            }
            else
            {
                if (!String.IsNullOrEmpty(typeName))
                    return typeName;
                else
                    return DisplayBadTypeName;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the interpreter and caller data on a newly created provider.
        /// Because providers are constructed parameterlessly (so they can be
        /// created across application domains), these values must be assigned
        /// through their settable interfaces afterward.
        /// </summary>
        /// <param name="provider">
        /// The provider to configure.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter to assign to the provider.
        /// </param>
        /// <param name="clientData">
        /// The caller data to assign to the provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; zero when the provider does not support
        /// setting the interpreter or caller data, or an exception occurs.
        /// </returns>
        private static bool TrySetInterpreterAndClientData(
            IRfc2898DataProvider provider, /* in */
            Interpreter interpreter,       /* in */
            IClientData clientData,        /* in */
            ref Result error               /* out */
            )
        {
            ISetInterpreter setInterpreter = provider as ISetInterpreter;

            if (setInterpreter == null)
            {
                error = "cannot set interpreter for RFC 2898 data provider";
                return false;
            }

            ISetClientData setClientData = provider as ISetClientData;

            if (setClientData == null)
            {
                error = "cannot set clientData for RFC 2898 data provider";
                return false;
            }

            try
            {
                setInterpreter.Interpreter = interpreter; /* throw */
                setClientData.ClientData = clientData; /* throw */

                return true;
            }
            catch (Exception e)
            {
                error = e;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates that a newly created object is a non-null RFC 2898 data
        /// provider and configures it with the interpreter and caller data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to assign to the provider.
        /// </param>
        /// <param name="clientData">
        /// The caller data to assign to the provider.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used in error messages.
        /// </param>
        /// <param name="typeName">
        /// The type name used in error messages.
        /// </param>
        /// <param name="object">
        /// The created object to validate and configure.
        /// </param>
        /// <param name="provider">
        /// Upon success, receives the validated, configured provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode SetupDataProvider(
            Interpreter interpreter,           /* in */
            IClientData clientData,            /* in */
            string assemblyName,               /* in */
            string typeName,                   /* in */
            object @object,                    /* in */
            ref IRfc2898DataProvider provider, /* in, out */
            ref Result error                   /* out */
            )
        {
            if (@object == null)
            {
                error = String.Format(
                    "instance of type {0} could not be created",
                    FormatTypeName(assemblyName, typeName));

                return ReturnCode.Error;
            }

            IRfc2898DataProvider localProvider =
                @object as IRfc2898DataProvider;

            if (localProvider == null)
            {
                error = String.Format(
                    "type {0} is not RFC 2898 data provider",
                    FormatTypeName(assemblyName, typeName));

                return ReturnCode.Error;
            }

            //
            // NOTE: Pass the interpreter and client data into the newly
            //       created instance now.  This must be done via public
            //       properties because the constructor itself has to be
            //       parameterless for use with CreateInstanceAndUnwrap.
            //
            if (!TrySetInterpreterAndClientData(
                    localProvider, interpreter, clientData, ref error))
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: Everything succeeded and the object implements the
            //       necessary interface, return it.
            //
            provider = localProvider;
            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Provider Data Handling Methods
#if NETWORK
#if XML
        /// <summary>
        /// Creates an XML namespace manager for the supplied document with the
        /// RFC 2898 data schema namespace registered, so that schema-qualified
        /// queries can be performed against it.
        /// </summary>
        /// <param name="document">
        /// The XML document the namespace manager will be used with.
        /// </param>
        /// <returns>
        /// A namespace manager with the schema namespace registered.
        /// </returns>
        private static XmlNamespaceManager GetNamespaceManager(
            XmlDocument document /* in */
            )
        {
            XmlNamespaceManager namespaceManager =
                new XmlNamespaceManager(document.NameTable);

            namespaceManager.AddNamespace(SchemaNamespaceName,
                SchemaNamespaceUri.ToString());

            return namespaceManager;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the RFC 2898 key-derivation parameters from an XML
        /// document by selecting the schema-qualified password, salt,
        /// iteration count, hash algorithm name, and signature elements.  Only
        /// elements that are present and non-empty overwrite the corresponding
        /// reference arguments.
        /// </summary>
        /// <param name="document">
        /// The XML document to read the parameters from.
        /// </param>
        /// <param name="password">
        /// On output, may receive the password from the document.
        /// </param>
        /// <param name="salt">
        /// On output, may receive the salt from the document.
        /// </param>
        /// <param name="iterationCount">
        /// On output, may receive the iteration count from the document.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On output, may receive the hash algorithm name from the document.
        /// </param>
        /// <param name="signature">
        /// On output, may receive the signature from the document.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode DataFromDocument(
            XmlDocument document,         /* in */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (document == null)
            {
                error = "invalid xml document";
                return ReturnCode.Error;
            }

            XmlElement documentElement = document.DocumentElement;

            if (documentElement == null)
            {
                error = "invalid xml document element";
                return ReturnCode.Error;
            }

            XmlNamespaceManager namespaceManager = GetNamespaceManager(
                document);

            if (namespaceManager == null)
            {
                error = "invalid xml namespace manager";
                return ReturnCode.Error;
            }

            XmlNode node;

            node = documentElement.SelectSingleNode(
                String.Format("{0}:password", SchemaNamespaceName),
                namespaceManager);

            if ((node != null) && !String.IsNullOrEmpty(node.InnerText))
                password = node.InnerText;

            node = documentElement.SelectSingleNode(
                String.Format("{0}:salt", SchemaNamespaceName),
                namespaceManager);

            if ((node != null) && !String.IsNullOrEmpty(node.InnerText))
                salt = node.InnerText;

            node = documentElement.SelectSingleNode(
                String.Format("{0}:iterationCount", SchemaNamespaceName),
                namespaceManager);

            if ((node != null) && !String.IsNullOrEmpty(node.InnerText))
                iterationCount = int.Parse(node.InnerText); /* throw */

            node = documentElement.SelectSingleNode(
                String.Format("{0}:hashAlgorithmName", SchemaNamespaceName),
                namespaceManager);

            if ((node != null) && !String.IsNullOrEmpty(node.InnerText))
                hashAlgorithmName = node.InnerText;

            node = documentElement.SelectSingleNode(
                String.Format("{0}:signature", SchemaNamespaceName),
                namespaceManager);

            if ((node != null) && !String.IsNullOrEmpty(node.InnerText))
                signature = node.InnerText;

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the RFC 2898 key-derivation parameters from a settings
        /// dictionary (as produced by evaluating a settings script), parsing
        /// the iteration count as an integer.  Only values that are present
        /// and non-empty overwrite the corresponding reference arguments.
        /// </summary>
        /// <param name="settings">
        /// The settings dictionary to read the parameters from.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse the iteration count.
        /// </param>
        /// <param name="password">
        /// On output, may receive the password from the settings.
        /// </param>
        /// <param name="salt">
        /// On output, may receive the salt from the settings.
        /// </param>
        /// <param name="iterationCount">
        /// On output, may receive the iteration count from the settings.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On output, may receive the hash algorithm name from the settings.
        /// </param>
        /// <param name="signature">
        /// On output, may receive the signature from the settings.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode DataFromSettings(
            StringDictionary settings,    /* in */
            CultureInfo cultureInfo,      /* in */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
            if (settings == null)
            {
                error = "invalid settings";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            string localPassword;

            /* IGNORED */
            settings.TryGetValue("password", out localPassword);

            ///////////////////////////////////////////////////////////////////

            string localSalt;

            /* IGNORED */
            settings.TryGetValue("salt", out localSalt);

            ///////////////////////////////////////////////////////////////////

            int? localiterationCount = null;
            string stringValue;

            if (settings.TryGetValue("iterationCount", out stringValue))
            {
                int intValue = 0;

                if (Value.GetInteger2(
                        stringValue, ValueFlags.AnyInteger, cultureInfo,
                        ref intValue, ref error) == ReturnCode.Ok)
                {
                    localiterationCount = intValue;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string localHashAlgorithmName;

            /* IGNORED */
            settings.TryGetValue(
                "hashAlgorithmName", out localHashAlgorithmName);

            ///////////////////////////////////////////////////////////////////

            string localSignature;

            /* IGNORED */
            settings.TryGetValue("signature", out localSignature);

            ///////////////////////////////////////////////////////////////////

            if (!String.IsNullOrEmpty(localPassword))
                password = localPassword;

            if (!String.IsNullOrEmpty(localSalt))
                salt = localSalt;

            if (localiterationCount != null)
                iterationCount = (int)localiterationCount;

            if (!String.IsNullOrEmpty(localHashAlgorithmName))
                hashAlgorithmName = localHashAlgorithmName;

            if (!String.IsNullOrEmpty(localSignature))
                signature = localSignature;

            return ReturnCode.Ok;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Provider Creation Methods
        /// <summary>
        /// Creates an instance of a built-in (already-resolved) provider type
        /// in the current application domain and configures it with the
        /// interpreter and caller data.  On failure, the partially created
        /// object is disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to assign to the provider.
        /// </param>
        /// <param name="clientData">
        /// The caller data to assign to the provider.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used in error messages.
        /// </param>
        /// <param name="typeName">
        /// The type name used in error messages.
        /// </param>
        /// <param name="type">
        /// The already-resolved provider type to instantiate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created provider, or null on failure.
        /// </returns>
        public static IRfc2898DataProvider CreateBuiltInDataProvider(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            string assemblyName,     /* in */
            string typeName,         /* in */
            Type type,               /* in */
            ref Result error         /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;
            object @object = null; /* finally */

            try
            {
                @object = Activator.CreateInstance(type); /* throw */

                IRfc2898DataProvider provider = null;

                if (SetupDataProvider(
                        interpreter, clientData, assemblyName,
                        typeName, @object, ref provider,
                        ref error) == ReturnCode.Ok)
                {
                    return provider;
                }

                code = ReturnCode.Error;
            }
            catch (Exception e)
            {
                error = e;
                code = ReturnCode.Error;
            }
            finally
            {
                if (code != ReturnCode.Ok)
                {
                    ReturnCode disposeCode;
                    Result disposeError = null;

                    disposeCode = Utility.TryDisposeObject<object>(
                        ref @object, ref disposeError);

                    if (disposeCode != ReturnCode.Ok)
                    {
                        Utility.Complain(
                            interpreter, disposeCode, disposeError);
                    }
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates an instance of an external provider type identified by
        /// assembly and type name.  On most frameworks the instance is created
        /// and unwrapped in the specified application domain; on .NET Standard
        /// 2.0 the assembly is loaded and the instance created in the current
        /// domain.  The new provider is then configured with the interpreter
        /// and caller data, and disposed on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to assign to the provider.
        /// </param>
        /// <param name="clientData">
        /// The caller data to assign to the provider.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly containing the provider type.
        /// </param>
        /// <param name="typeName">
        /// The name of the provider type to create.
        /// </param>
        /// <param name="appDomain">
        /// The application domain in which to create the provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created provider, or null on failure.
        /// </returns>
        public static IRfc2898DataProvider CreateOtherDataProvider(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            string assemblyName,     /* in */
            string typeName,         /* in */
            AppDomain appDomain,     /* in */
            ref Result error         /* out */
            )
        {
            if (appDomain == null)
            {
                error = "invalid application domain";
                return null;
            }

            ReturnCode code = ReturnCode.Ok;
            object @object = null; /* finally */

            try
            {
#if !NET_STANDARD_20
                @object = appDomain.CreateInstanceAndUnwrap(
                    assemblyName, typeName); /* throw */
#else
                Assembly assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(typeName);

                @object = Activator.CreateInstance(type);
#endif

                IRfc2898DataProvider provider = null;

                if (SetupDataProvider(
                        interpreter, clientData, assemblyName,
                        typeName, @object, ref provider,
                        ref error) == ReturnCode.Ok)
                {
                    return provider;
                }

                code = ReturnCode.Error;
            }
            catch (Exception e)
            {
                error = e;
                code = ReturnCode.Error;
            }
            finally
            {
                if (code != ReturnCode.Ok)
                {
                    ReturnCode disposeCode;
                    Result disposeError = null;

                    disposeCode = Utility.TryDisposeObject<object>(
                        ref @object, ref disposeError);

                    if (disposeCode != ReturnCode.Ok)
                    {
                        Utility.Complain(
                            interpreter, disposeCode, disposeError);
                    }
                }
            }

            return null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Provider Data Handling Methods
        /// <summary>
        /// Downloads RFC 2898 key-derivation parameters from a remote URI.
        /// The downloaded content is interpreted as an XML document (validated
        /// against the schema when requested) or, otherwise, as a settings
        /// script evaluated through a temporary file; the resulting parameters
        /// overwrite the reference arguments.  Available only when compiled
        /// with network support.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to download and parse the remote data.
        /// </param>
        /// <param name="encodingName">
        /// The name of the encoding used to decode the downloaded bytes.
        /// </param>
        /// <param name="uri">
        /// The remote location to download the data from.
        /// </param>
        /// <param name="trusted">
        /// Whether the remote location is to be trusted during download.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the XML against the schema (and to enforce
        /// security when loading a settings script); zero to skip validation.
        /// </param>
        /// <param name="password">
        /// On output, may receive the downloaded password.
        /// </param>
        /// <param name="salt">
        /// On output, may receive the downloaded salt.
        /// </param>
        /// <param name="iterationCount">
        /// On output, may receive the downloaded iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On output, may receive the downloaded hash algorithm name.
        /// </param>
        /// <param name="signature">
        /// On output, may receive the downloaded signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode GetRemoteData(
            Interpreter interpreter,      /* in */
            string encodingName,          /* in */
            Uri uri,                      /* in */
            bool? trusted,                /* in */
            bool validate,                /* in */
            ref string password,          /* out */
            ref string salt,              /* out */
            ref int iterationCount,       /* out */
            ref string hashAlgorithmName, /* out */
            ref string signature,         /* out */
            ref Result error              /* out */
            )
        {
#if NETWORK
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ReturnCode code;
            Encoding encoding = null;

            code = interpreter.GetEncodingOrDefault(
                encodingName, LookupFlags.Default, ref encoding,
                ref error);

            if (code != ReturnCode.Ok)
                return code;

#if TEST
            code = Utility.SetWebSecurityProtocol(
                false, ref error);

            if (code != ReturnCode.Ok)
                return code;
#endif

            byte[] bytes = null;

            code = Utility.DownloadData(
                interpreter, null, uri, null, null, trusted, ref bytes,
                ref error);

            if (code == ReturnCode.Ok)
            {
                string temporaryFileName = null;

                try
                {
                    string text = encoding.GetString(bytes);

#if XML
                    if (Utility.LooksLikeXmlDocument(text))
                    {
                        XmlDocument document = new XmlDocument();
                        document.LoadXml(text); /* throw */

                        if (validate)
                        {
                            code = Utility.Validate(
                                thisAssembly, SchemaResourceName,
                                document, ref error);
                        }

                        if (code == ReturnCode.Ok)
                        {
                            code = DataFromDocument(
                                document, ref password, ref salt,
                                ref iterationCount, ref hashAlgorithmName,
                                ref signature, ref error);
                        }
                    }
                    else
#endif
                    {
                        code = Utility.CreateTemporaryScriptFile(
                            interpreter, text, encoding, ref temporaryFileName,
                            ref error);

                        if (code == ReturnCode.Ok)
                        {
                            ScriptDataFlags flags = ScriptDataFlags.High;

                            if (!validate)
                                flags |= ScriptDataFlags.DisableSecurity;

                            StringDictionary settings = null;

                            code = Utility.LoadSettingsViaScriptFile(
                                interpreter, null, null, temporaryFileName,
                                ref flags, ref settings, ref error);

                            if (code == ReturnCode.Ok)
                            {
                                code = DataFromSettings(
                                    settings, interpreter.CultureInfo,
                                    ref password, ref salt, ref iterationCount,
                                    ref hashAlgorithmName, ref signature,
                                    ref error);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    error = e;
                    code = ReturnCode.Error;
                }
                finally
                {
                    //
                    // NOTE: If we created a temporary file, always delete it
                    //       prior to returning from this method.
                    //
                    if (temporaryFileName != null)
                    {
                        try
                        {
                            File.Delete(temporaryFileName); /* throw */
                        }
                        catch (Exception e)
                        {
                            Utility.DebugTrace(
                                e, typeof(Rfc2898Ops).Name,
                                TracePriority.MediumHigh |
                                    TracePriority.FromPlugin);
                        }

                        temporaryFileName = null;
                    }
                }
            }

            return code;
#else
            error = "not implemented";
            return ReturnCode.Error;
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Procedure Helper Methods
        /// <summary>
        /// Gets the RFC 2898 data or data provider currently held by the
        /// specified plugin's data manager, preferring the data when both are
        /// present.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose data manager is queried.
        /// </param>
        /// <param name="object">
        /// Upon success, receives the data or provider object.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when a data or provider object was found; otherwise, zero.
        /// </returns>
        private static bool GetDataOrProvider(
            IPlugin plugin,     /* in */
            ref object @object, /* in, out */
            ref Result error    /* out */
            )
        {
            if (plugin == null)
            {
                error = "invalid plugin";
                return false;
            }

            IRfc2898DataManager manager = plugin as IRfc2898DataManager;

            if (manager == null)
            {
                error = "plugin type mismatch";
                return false;
            }

            object localObject = manager.Rfc2898Data;

            if (localObject != null)
            {
                @object = localObject;
                return true;
            }

            localObject = manager.Rfc2898DataProvider;

            if (localObject != null)
            {
                @object = localObject;
                return true;
            }

            error = "no data or provider available";
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the standard error message describing the object types that
        /// are acceptable as RFC 2898 data or a data provider, namely null, an
        /// <see cref="IRfc2898Data" />, or an
        /// <see cref="IRfc2898DataProvider" />.
        /// </summary>
        /// <returns>
        /// The formatted type-mismatch error message.
        /// </returns>
        private static string TypeMismatchErrorMessage()
        {
            return String.Format(
                "object type mismatch, must be: {0}, {1}, or {2}",
                Utility.FormatWrapOrNull(null),
                Utility.FormatWrapOrNull(typeof(IRfc2898Data)),
                Utility.FormatWrapOrNull(typeof(IRfc2898DataProvider)));
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Procedure Helper Methods
        /// <summary>
        /// Resolves the named opaque object and verifies that it is an RFC
        /// 2898 data provider.  This backs the <c>zeus clone</c> command,
        /// returning the object name when the data is enabled or the null
        /// object name otherwise.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the opaque object expected to be a data provider.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the object name or the null object name; on
        /// failure, receives an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode MaybeUseProvider(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject @object = null;
            Result error = null;

            if (interpreter.GetObject(objectName,
                    LookupFlags.Default, ref @object,
                    ref error) != ReturnCode.Ok)
            {
                result = error;
                return ReturnCode.Error;
            }

            object value = @object.Value;

            if (!(value is IRfc2898DataProvider))
            {
                result = "invalid data provider";
                return ReturnCode.Error;
            }

            result = Rfc2898Data.IsEnabled() ?
                objectName : Utility.NullObjectName();

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces a human-readable status report describing what RFC 2898
        /// data or data provider, if any, the specified plugin currently
        /// holds.  This backs the result of the <c>zeus proc</c> command.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose RFC 2898 status is reported.
        /// </param>
        /// <returns>
        /// A multi-line status description.
        /// </returns>
        public static Result GetStatus(
            IPlugin plugin /* in */
            )
        {
            StringBuilder builder = new StringBuilder();
            string typeName = Utility.FormatTypeNameOrFullName(plugin);
            object @object = null;
            Result error = null;

            if (!GetDataOrProvider(plugin, ref @object, ref error))
            {
                builder.AppendLine(String.Format(
                    "plugin {0} has data or provider error: {1}", typeName,
                    Utility.FormatWrapOrNull(error)));
            }

            if (@object == null)
            {
                builder.AppendLine(String.Format(
                    "plugin {0} has no data or provider", typeName));
            }

            if (@object is IRfc2898Data)
            {
                builder.AppendLine(String.Format(
                    "plugin {0} has some kind of data", typeName));
            }

            if (@object is Rfc2898Data)
            {
                builder.AppendLine(String.Format(
                    "plugin {0} has internal data", typeName));
            }

            if (@object is IRfc2898DataProvider)
            {
                builder.AppendLine(String.Format(
                    "plugin {0} has some kind of data provider", typeName));
            }

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the RFC 2898 key-derivation parameters for the specified
        /// plugin.  When the plugin holds data directly, its parameters are
        /// returned; when it holds a provider, the provider is asked to supply
        /// them.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose data or provider is used.
        /// </param>
        /// <param name="fileName">
        /// An optional file name passed to the provider, if one is used.
        /// </param>
        /// <param name="encodingName">
        /// An optional encoding name passed to the provider, if one is used.
        /// </param>
        /// <param name="password">
        /// On output, receives the resolved password.
        /// </param>
        /// <param name="salt">
        /// On output, receives the resolved salt.
        /// </param>
        /// <param name="iterationCount">
        /// On output, receives the resolved iteration count.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On output, receives the resolved hash algorithm name.
        /// </param>
        /// <param name="signature">
        /// On output, receives the resolved signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode GetData(
            IPlugin plugin,               /* in */
            string fileName,              /* in */
            string encodingName,          /* in */
            ref string password,          /* out */
            ref string salt,              /* out */
            ref int iterationCount,       /* out */
            ref string hashAlgorithmName, /* out */
            ref string signature,         /* out */
            ref Result error              /* out */
            )
        {
            object @object = null;

            if (!GetDataOrProvider(plugin, ref @object, ref error))
                return ReturnCode.Error;

            Rfc2898Data data = @object as Rfc2898Data;

            if (data != null)
            {
                password = data.Password;
                salt = data.Salt;
                iterationCount = data.IterationCount;
                hashAlgorithmName = data.HashAlgorithmName;
                signature = data.Signature;

                return ReturnCode.Ok;
            }

            IRfc2898DataProvider provider = @object as IRfc2898DataProvider;

            if (provider != null)
            {
                return provider.GetData(
                    fileName, encodingName, ref password, ref salt,
                    ref iterationCount, ref hashAlgorithmName,
                    ref signature, ref error);
            }

            error = "no data or provider available";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the named opaque object and verifies that it is suitable
        /// for use as RFC 2898 data or a data provider (that is, null, an
        /// <see cref="IRfc2898Data" />, or an
        /// <see cref="IRfc2898DataProvider" />).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the opaque object to resolve.
        /// </param>
        /// <param name="object">
        /// Upon success, receives the resolved data or provider value (which
        /// may be null).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode GetDataOrProvider(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            ref object @object,      /* in, out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject localObject = null;
            Result localError = null;

            if (interpreter.GetObject(objectName,
                    LookupFlags.Default, ref localObject,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }

            object value = localObject.Value;

            if ((value == null) || (value is IRfc2898Data) ||
                (value is IRfc2898DataProvider))
            {
                @object = value;
                return ReturnCode.Ok;
            }
            else
            {
                error = TypeMismatchErrorMessage();
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the RFC 2898 data or data provider held by the specified
        /// plugin's data manager.  A null object clears both; an
        /// <see cref="IRfc2898Data" /> sets the data and clears the provider;
        /// an <see cref="IRfc2898DataProvider" /> sets the provider and resets
        /// any data.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose data manager is updated.
        /// </param>
        /// <param name="object">
        /// The data or provider to set, or null to clear both.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; zero on failure.
        /// </returns>
        public static bool SetDataOrProvider(
            IPlugin plugin,  /* in */
            object @object,  /* in */
            ref Result error /* out */
            )
        {
            if (plugin == null)
            {
                error = "invalid plugin";
                return false;
            }

            IRfc2898DataManager manager = plugin as IRfc2898DataManager;

            if (manager == null)
            {
                error = "plugin type mismatch";
                return false;
            }

            if (@object == null)
            {
                /* IGNORED */
                Rfc2898Data.MaybeReset(manager);

                manager.Rfc2898DataProvider = null;

                return true;
            }
            else if (@object is IRfc2898Data)
            {
                manager.Rfc2898Data = (IRfc2898Data)@object;
                manager.Rfc2898DataProvider = null;

                return true;
            }
            else if (@object is IRfc2898DataProvider)
            {
                /* IGNORED */
                Rfc2898Data.MaybeReset(manager);

                manager.Rfc2898DataProvider = (IRfc2898DataProvider)@object;

                return true;
            }
            else
            {
                error = TypeMismatchErrorMessage();
                return false;
            }
        }
        #endregion
    }
}
