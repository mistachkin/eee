/*
 * CommandOps.cs --
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
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;

using PropertyInfoDictionary =
    System.Collections.Generic.Dictionary<
        string, System.Reflection.PropertyInfo>;

using TypeDictionary =
    System.Collections.Generic.Dictionary<
        string, System.Type>;

#if !NET_STANDARD_20
using RSAProvider = System.Security.Cryptography.RSACryptoServiceProvider;
using DSAProvider = System.Security.Cryptography.DSACryptoServiceProvider;
#else
using RSAProvider = System.Security.Cryptography.RSA;
using DSAProvider = System.Security.Cryptography.DSA;
#endif

#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
using BigCrypto;
#endif

using Utility = Eagle._Components.Public.Utility;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper operations used by the licensing commands,
    /// including metadata property access, named object lookup from an
    /// interpreter, and the signing and verification of certificates and
    /// files.
    /// </summary>
    [ObjectId("7740f8b5-9322-4380-b5eb-fe359914a682")]
    internal static class CommandOps
    {
        /// <summary>
        /// Resolves a metadata value for the named property, converting a
        /// string value (or a named object handle) into the type expected
        /// by that property when necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up object handles and to perform
        /// value conversions; may be null.
        /// </param>
        /// <param name="type">
        /// The type that declares the metadata property.
        /// </param>
        /// <param name="object">
        /// The object instance from which an existing property value may be
        /// read.
        /// </param>
        /// <param name="name">
        /// The name of the metadata property.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing enumerated values; may be null.
        /// </param>
        /// <param name="value">
        /// On input, the candidate value; on output, receives the converted
        /// metadata value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetMetadataValue(
            Interpreter interpreter, /* in */
            Type type,               /* in */
            object @object,          /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref object value,        /* in, out */
            ref Result error         /* out */
            )
        {
            object localValue = (value is StringList) ?
                value.ToString() : value;

            if (localValue is string)
            {
                ResultList errors = null;
                Result localError; /* REUSED */
                string stringValue = (string)localValue;

                if (interpreter != null)
                {
                    IObject localObject = null;

                    localError = null;

                    if (interpreter.GetObject(stringValue,
                            LookupFlags.Default, ref localObject,
                            ref localError) == ReturnCode.Ok)
                    {
                        value = localObject.Value;
                        return ReturnCode.Ok;
                    }
                    else if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }
                }
                else
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add("invalid interpreter");
                }

                Type propertyType;

                if (TryGetMetadataPropertyType(
                        type, name, out propertyType))
                {
                    localError = null;

                    if (Utility.TryGetValueOfType(
                            interpreter, null, propertyType,
                            stringValue, ValueFlags.None,
                            Constants.DefaultTimeStampFormat,
                            DateTimeKind.Utc,
                            DateTimeStyles.RoundtripKind,
                            ref value,
                            ref localError) == ReturnCode.Ok)
                    {
                        return ReturnCode.Ok;
                    }
                    else if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    //
                    // TODO: Possibly update these IF STATEMENTS if
                    //       more properties are added to any of the
                    //       KeyPair related interfaces -OR- to the
                    //       ICertificate interface.
                    //
                    if ((propertyType == typeof(ProtocolType)) ||
                        (propertyType == typeof(EntityType)) ||
                        (propertyType == typeof(KeyPairType)) ||
                        (propertyType == typeof(KeyFileFormat)))
                    {
                        object oldEnumValue = null;

                        try
                        {
                            oldEnumValue = GetMetadataPropertyValue(
                                type, name, @object); /* throw */
                        }
                        catch (Exception e)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(e);
                        }

                        if (oldEnumValue != null)
                        {
                            object newEnumValue;

                            localError = null;

                            newEnumValue = Utility.TryParseFlagsEnum(
                                interpreter, propertyType,
                                oldEnumValue.ToString(), stringValue,
                                cultureInfo, true, true, true,
                                ref localError);

                            if (newEnumValue != null)
                            {
                                value = newEnumValue;
                                return ReturnCode.Ok;
                            }
                            else if (localError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(localError);
                            }
                        }
                    }
                    else if (propertyType == typeof(IKeyPairMetadata))
                    {
                        IKeyPair keyPair = null;

                        localError = null;

                        if (CertificateKeyPairOps.GetOne( /* OK */
                                null, PolicyType.Script, false,
                                CertificateAssemblyOps.GetObject(),
                                CertificateAssemblyOps.GetName(),
                                interpreter, stringValue, true,
                                true, ref keyPair,
                                ref localError) == ReturnCode.Ok)
                        {
                            value = keyPair;
                            return ReturnCode.Ok;
                        }
                        else if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }
                    }
                    else
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(String.Format(
                            "unsupported metadata value of type {0}",
                            Utility.FormatWrapOrNull(propertyType)));
                    }

                    if (errors != null)
                    {
                        error = errors;
                    }
                    else
                    {
                        error = String.Format(
                            "could not convert string {0} using object " +
                            "{1} of type {2} to metadata value of type {3}",
                            Utility.FormatWrapOrNull(stringValue),
                            Utility.FormatWrapOrNull(@object),
                            Utility.FormatWrapOrNull(type),
                            Utility.FormatWrapOrNull(propertyType));
                    }

                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a metadata return value into a script-friendly result,
        /// handling string lists, byte arrays, time stamps, and key pairs.
        /// </summary>
        /// <param name="returnValue">
        /// The raw metadata value to convert.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the converted result.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetMetadataResult(
            object returnValue, /* in */
            ref Result result   /* out */
            )
        {
            if (returnValue is IList<string>)
            {
                ResultList results = new ResultList();

                foreach (string item in (IEnumerable<string>)returnValue)
                    results.Add(item);

                result = results;
            }
            else if (returnValue is IList<byte[]>)
            {
                ResultList results = new ResultList();

                foreach (byte[] item in (IEnumerable<byte[]>)returnValue)
                    results.Add(new ByteList(item));

                result = results;
            }
            else if (returnValue is byte[])
            {
                result = new ByteList((byte[])returnValue);
            }
            else if (returnValue is DateTime)
            {
                result = CertificateDataOps.FormatTimeStamp(
                    (DateTime)returnValue);
            }
            else if (returnValue is IKeyPair)
            {
                result = CertificateDataOps.FormatPublicKeyToken(
                    ((IKeyPair)returnValue).PublicKeyToken, false,
                    true);
            }
            else
            {
                result = Utility.GetResultFromObject(returnValue);
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified hash algorithm name refers to
        /// the legacy hash algorithm.  An empty name is treated as legacy.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The hash algorithm name to test; may be null or empty.
        /// </param>
        /// <returns>
        /// Non-zero if the name refers to the legacy hash algorithm;
        /// otherwise, zero.
        /// </returns>
        public static bool IsLegacyHashAlgorithm(
            string hashAlgorithmName /* in: OPTIONAL */
            )
        {
            if (String.IsNullOrEmpty(hashAlgorithmName))
                return true;

            if (String.IsNullOrEmpty(
                    Constants.LegacyHashAlgorithmName))
            {
                return false;
            }

            if (CertificateDataOps.StringEqualsNoCase(hashAlgorithmName,
                    Constants.LegacyHashAlgorithmName))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the reflection binding flags used to query or update
        /// metadata properties, based on the property operation contained
        /// in the specified binding flags.
        /// </summary>
        /// <param name="bindingFlags">
        /// The binding flags whose property operation selects the metadata
        /// binding flags to return.
        /// </param>
        /// <returns>
        /// The binding flags used to get or set metadata properties, or
        /// zero when the operation is not recognized.
        /// </returns>
        private static BindingFlags GetMetadataBindingFlags(
            BindingFlags bindingFlags /* in */
            )
        {
            switch (bindingFlags & Constants.BindingFlagsPropertyMask)
            {
                case BindingFlags.GetProperty:
                    {
                        return Constants.GetMetadataBindingFlags;
                    }
                case BindingFlags.SetProperty:
                    {
                        return Constants.SetMetadataBindingFlags;
                    }
                default:
                    {
                        return (BindingFlags)0;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the metadata type associated with a key pair or a key
        /// pair type.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose type is used when a key pair type is not
        /// supplied; may be null.
        /// </param>
        /// <param name="keyPairType">
        /// The key pair type used to look up the metadata type; may be null.
        /// </param>
        /// <returns>
        /// The metadata type, or null when one cannot be determined.
        /// </returns>
        public static Type GetMetadataType(
            IKeyPair keyPair,        /* in: OPTIONAL */
            KeyPairType? keyPairType /* in: OPTIONAL */
            )
        {
            if (keyPairType != null)
            {
                Type type;

                if (KeyFile.TryGetType(keyPairType, out type))
                    return type;
            }

            if (keyPair != null)
                return keyPair.GetType();

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetMetadataType: unsupported key pair type {0}",
                Utility.FormatWrapOrNull(keyPairType)),
                typeof(CommandOps).Name,
                TracePriority.MediumHigh);
#endif

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the names of the metadata properties supported by the
        /// specified type.
        /// </summary>
        /// <param name="type">
        /// The type whose metadata property names are returned.
        /// </param>
        /// <returns>
        /// The list of metadata property names.
        /// </returns>
        public static StringList GetMetadataPropertyNames(
            Type type /* in */
            )
        {
            PropertyInfoDictionary properties = new PropertyInfoDictionary();

            /* NO RESULT */
            GetMetadataTypesAndProperties(
                type, BindingFlags.GetProperty, null, properties);

            StringList result = new StringList();

            foreach (KeyValuePair<string, PropertyInfo> pair in properties)
            {
                PropertyInfo property = pair.Value;

                if (property == null)
                    continue;

                result.Add(property.Name);
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the metadata properties supported by the specified type.
        /// </summary>
        /// <param name="type">
        /// The type whose metadata properties are returned.
        /// </param>
        /// <returns>
        /// An array of metadata properties, or null when none are found.
        /// </returns>
        public static PropertyInfo[] GetMetadataProperties(
            Type type /* in */
            )
        {
            PropertyInfoDictionary properties = new PropertyInfoDictionary();

            /* NO RESULT */
            GetMetadataTypesAndProperties(
                type, BindingFlags.GetProperty, null, properties);

            return (properties != null) ?
                new List<PropertyInfo>(properties.Values).ToArray() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Collects the declared metadata properties for the specified
        /// type, adding them to the optional type and property dictionaries.
        /// </summary>
        /// <param name="type">
        /// The type whose declared properties are collected.
        /// </param>
        /// <param name="bindingFlags">
        /// The binding flags selecting whether get or set properties are
        /// collected.
        /// </param>
        /// <param name="types">
        /// When not null, receives a mapping from property name to the type
        /// that declares it.
        /// </param>
        /// <param name="properties">
        /// When not null, receives a mapping from property name to the
        /// corresponding property.
        /// </param>
        private static void GetMetadataProperties(
            Type type,                        /* in */
            BindingFlags bindingFlags,        /* in */
            TypeDictionary types,             /* in, out: OPTIONAL */
            PropertyInfoDictionary properties /* in, out: OPTIONAL */
            )
        {
            //
            // NOTE: If there is no type, we cannot proceed.
            //
            if (type == null)
                return;

            //
            // NOTE: Grab the declared public properties for this type.
            //
            PropertyInfo[] propertyInfo = type.GetProperties(
                GetMetadataBindingFlags(bindingFlags));

            //
            // NOTE: Add the properties, if any, to the overall result
            //       list.
            //
            if (propertyInfo != null)
            {
                foreach (PropertyInfo localPropertyInfo in propertyInfo)
                {
                    if (localPropertyInfo == null)
                        continue;

                    string name = localPropertyInfo.Name;

                    if (name == null)
                        continue;

                    if ((types != null) &&
                        !types.ContainsKey(name))
                    {
                        types.Add(name, type);
                    }

                    if ((properties != null) &&
                        !properties.ContainsKey(name))
                    {
                        properties.Add(name, localPropertyInfo);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Walks the type hierarchy (and, for interfaces, the base
        /// interfaces) collecting metadata types and properties, stopping
        /// at <see cref="object" /> and <see cref="Identifier" />.
        /// </summary>
        /// <param name="type">
        /// The type at which to begin collecting metadata.
        /// </param>
        /// <param name="bindingFlags">
        /// The binding flags selecting whether get or set properties are
        /// collected.
        /// </param>
        /// <param name="types">
        /// When not null, receives a mapping from property name to the type
        /// that declares it.
        /// </param>
        /// <param name="properties">
        /// When not null, receives a mapping from property name to the
        /// corresponding property.
        /// </param>
        private static void GetMetadataTypesAndProperties(
            Type type,                        /* in */
            BindingFlags bindingFlags,        /* in */
            TypeDictionary types,             /* in, out: OPTIONAL */
            PropertyInfoDictionary properties /* in, out: OPTIONAL */
            )
        {
            //
            // NOTE: Garbage in, garbage out.
            //
            if (type == null)
                return;

            while (true)
            {
                //
                // NOTE: If the type is null, we cannot query properties.
                //
                if (type == null)
                    break;

                //
                // NOTE: If the type is System.Object, we want to stop.
                //
                if (type == typeof(object))
                    break;

                //
                // HACK: Also, if the type is Identifier, we want to stop.
                //
                if (type == typeof(Identifier))
                    break;

                //
                // NOTE: First, process properties for the current type.
                //
                GetMetadataProperties(type, bindingFlags, types, properties);

                //
                // NOTE: Next, for interfaces, a different means of getting
                //       the base type(s) is required.
                //
                if (type.IsInterface)
                {
                    //
                    // NOTE: Query the base interfaces for the current type,
                    //       which is actually an interface.
                    //
                    Type[] baseTypes = type.GetInterfaces();

                    if (baseTypes != null)
                    {
                        foreach (Type baseType in baseTypes)
                        {
                            //
                            // NOTE: Next, process properties for this base
                            //       type, which is a base interface for the
                            //       current type.
                            //
                            GetMetadataProperties(
                                baseType, bindingFlags, types, properties);
                        }
                    }
                }

                //
                // NOTE: Advance the current type to the next one up the
                //       type hierarchy.
                //
                type = type.BaseType;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to determine the type of the named metadata property.
        /// </summary>
        /// <param name="type">
        /// The type that declares the metadata property.
        /// </param>
        /// <param name="name">
        /// The name of the metadata property.
        /// </param>
        /// <param name="propertyType">
        /// Upon success, receives the type of the named property; otherwise,
        /// null.
        /// </param>
        /// <returns>
        /// Non-zero if the property type was determined; otherwise, zero.
        /// </returns>
        private static bool TryGetMetadataPropertyType(
            Type type,            /* in */
            string name,          /* in */
            out Type propertyType /* out */
            )
        {
            PropertyInfoDictionary properties = new PropertyInfoDictionary();

            /* NO RESULT */
            GetMetadataTypesAndProperties(
                type, BindingFlags.SetProperty, null, properties);

            PropertyInfo propertyInfo;

            if ((name != null) &&
                properties.TryGetValue(name, out propertyInfo))
            {
                propertyType = propertyInfo.PropertyType;
                return true;
            }

            propertyType = null;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the named metadata property from the specified
        /// object.
        /// </summary>
        /// <param name="type">
        /// The type used to resolve and read the property; used verbatim
        /// when no declaring type is mapped for the property name.
        /// </param>
        /// <param name="name">
        /// The name of the metadata property to read.
        /// </param>
        /// <param name="object">
        /// The object instance from which the property value is read.
        /// </param>
        /// <returns>
        /// The value of the named metadata property.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when no declaring type is mapped for the property name and
        /// <paramref name="type" /> is null.
        /// </exception>
        public static object GetMetadataPropertyValue(
            Type type,     /* in */
            string name,   /* in */
            object @object /* in */
            )
        {
            TypeDictionary types = new TypeDictionary();

            /* NO RESULT */
            GetMetadataTypesAndProperties(
                type, BindingFlags.GetProperty, types, null);

            //
            // NOTE: Grab the binding flags used to query metadata from
            //       the supported types.
            //
            BindingFlags bindingFlags = GetMetadataBindingFlags(
                BindingFlags.GetProperty);

            //
            // HACK: If no type (or a null type) for this property name
            //       within the mappings, use the type specified by the
            //       caller verbatim.
            //
            Type localType;

            if ((name == null) ||
                !types.TryGetValue(name, out localType) ||
                (localType == null))
            {
                if (type == null)
                    throw new ArgumentNullException("type");

                return type.InvokeMember(
                    name, bindingFlags, null, @object, null); /* throw */
            }

            return localType.InvokeMember(
                name, bindingFlags, null, @object, null); /* throw */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the value of the named metadata property on the specified
        /// object.
        /// </summary>
        /// <param name="type">
        /// The type used to resolve and write the property; used verbatim
        /// when no declaring type is mapped for the property name.
        /// </param>
        /// <param name="name">
        /// The name of the metadata property to write.
        /// </param>
        /// <param name="object">
        /// The object instance on which the property value is set.
        /// </param>
        /// <param name="value">
        /// The value to assign to the named property.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when no declaring type is mapped for the property name and
        /// <paramref name="type" /> is null.
        /// </exception>
        public static void SetMetadataPropertyValue(
            Type type,      /* in */
            string name,    /* in */
            object @object, /* in */
            object value    /* in */
            )
        {
            TypeDictionary types = new TypeDictionary();

            /* NO RESULT */
            GetMetadataTypesAndProperties(
                type, BindingFlags.SetProperty, types, null);

            //
            // NOTE: Grab the binding flags used to query metadata from
            //       the supported types.
            //
            BindingFlags bindingFlags = GetMetadataBindingFlags(
                BindingFlags.SetProperty);

            //
            // NOTE: Setup the reflection arguments array to use when
            //       setting the property.
            //
            object[] args = { value };

            //
            // HACK: If no type (or a null type) for this property name
            //       within the mappings, use the type specified by the
            //       caller verbatim.
            //
            Type localType;

            if ((name == null) ||
                !types.TryGetValue(name, out localType) ||
                (localType == null))
            {
                if (type == null)
                    throw new ArgumentNullException("type");

                type.InvokeMember(
                    name, bindingFlags, null, @object, args); /* throw */

                return;
            }

            localType.InvokeMember(
                name, bindingFlags, null, @object, args); /* throw */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the binder for the interpreter, unless the plugin has been
        /// loaded into an application domain different from that of the
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose binder is returned.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to detect a cross-application-domain
        /// condition.
        /// </param>
        /// <returns>
        /// The interpreter binder, or null when one is not available.
        /// </returns>
        public static IBinder GetBinder(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            //
            // BUGFIX: We cannot use the ScriptBinder if this plugin has
            //         been loaded into an AppDomain different from the
            //         interpreter -OR- there is no interpreter to obtain
            //         it from.
            //
            if (interpreter != null)
            {
                if (CertificateSharedOps.IsCrossAppDomain(
                        interpreter, pluginData))
                {
                    return null;
                }

                return interpreter.Binder;
            }
            else
            {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up a named object in the interpreter and returns the
        /// assembly that it wraps.  The object name "null" is permitted when
        /// <paramref name="validate" /> is zero.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the object that wraps the assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to treat a null assembly value as an error.
        /// </param>
        /// <param name="assembly">
        /// Upon success, receives the assembly; this may be null when
        /// <paramref name="validate" /> is zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetAssemblyObject(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            bool validate,           /* in */
            ref Assembly assembly,   /* out: may be NULL if Ok. */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }

            if (@object.Value == null)
            {
                if (validate)
                {
                    error = "invalid assembly";
                    return ReturnCode.Error;
                }
                else
                {
                    //
                    // NOTE: This permits the object name "null"
                    //       to be used, which is required by our
                    //       callers.
                    //
                    assembly = null;
                    return ReturnCode.Ok;
                }
            }

            Assembly localAssembly = (@object != null) ?
                @object.Value as Assembly : null;

            if (localAssembly == null)
            {
                error = "invalid assembly";
                return ReturnCode.Error;
            }

            assembly = localAssembly;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up a named object in the interpreter and returns the byte
        /// array that it wraps.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the object that wraps the byte array.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the byte array.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetByteArray(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            ref byte[] bytes,        /* out: may NOT be NULL if Ok. */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }

            byte[] localBytes = (@object != null) ?
                @object.Value as byte[] : null;

            if (localBytes == null)
            {
                error = "invalid byte array";
                return ReturnCode.Error;
            }

            bytes = localBytes;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up a named object in the interpreter and returns the stream
        /// that it wraps.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the object that wraps the stream.
        /// </param>
        /// <param name="stream">
        /// Upon success, receives the stream.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetStream(
            Interpreter interpreter, /* in */
            string objectName,       /* in */
            ref Stream stream,       /* out: may NOT be NULL if Ok. */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }

            Stream localStream = (@object != null) ?
                @object.Value as Stream : null;

            if (localStream == null)
            {
                error = "invalid stream";
                return ReturnCode.Error;
            }

            stream = localStream;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up a named object in the interpreter and returns the
        /// certificate that it wraps.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up the named object.
        /// </param>
        /// <param name="objectName">
        /// The name of the object that wraps the certificate.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the certificate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetObject(
            Interpreter interpreter,      /* in */
            string objectName,            /* in */
            ref ICertificate certificate, /* out: may NOT be NULL if Ok. */
            ref Result error              /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                error = localError;
                return ReturnCode.Error;
            }

            ICertificate localCertificate = (@object != null) ?
                @object.Value as ICertificate : null;

            if (localCertificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            certificate = localCertificate;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up the named certificate object and an associated key pair,
        /// preferring a key pair obtained from the key ring and optionally
        /// falling back to a named key pair object.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to search; may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type governing key pair retrieval.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero to require that the key ring name match.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the request.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name associated with the request.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to look up the named objects.
        /// </param>
        /// <param name="certificateObjectName">
        /// The name of the object that wraps the certificate.
        /// </param>
        /// <param name="keyPairObjectName">
        /// The name of the object that wraps the key pair.
        /// </param>
        /// <param name="allowObject">
        /// Non-zero to permit falling back to a named key pair object.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the certificate.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetObjectAndKeyPair(
            string keyRingName,           /* in: OPTIONAL */
            PolicyType policyType,        /* in */
            bool matchKeyRingName,        /* in */
            Assembly assembly,            /* in: OK */
            AssemblyName assemblyName,    /* in: OK */
            Interpreter interpreter,      /* in */
            string certificateObjectName, /* in */
            string keyPairObjectName,     /* in */
            bool allowObject,             /* in: OK */
            ref ICertificate certificate, /* out: may NOT be NULL if Ok. */
            ref IKeyPair keyPair,         /* out: may NOT be NULL if Ok. */
            ref Result error              /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ResultList errors = null;
            IObject @object = null;
            Result localError = null;

            if (interpreter.GetObject(
                    certificateObjectName, LookupFlags.Default,
                    ref @object, ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                error = errors;
                return ReturnCode.Error;
            }

            ICertificate localCertificate = (@object != null) ?
                @object.Value as ICertificate : null;

            if (localCertificate == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid certificate");

                error = errors;
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair;

#if CERTIFICATE_POLICY
            localKeyPair = null;
            localError = null;

            if (CertificateKeyPairOps.GetRing( /* OK */
                    keyRingName, policyType, matchKeyRingName, assembly,
                    assemblyName, interpreter, keyPairObjectName, false,
                    ref localKeyPair, ref localError) == ReturnCode.Ok)
            {
                if (localKeyPair != null)
                {
                    certificate = localCertificate;
                    keyPair = localKeyPair;

                    return ReturnCode.Ok;
                }
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }
#endif

            //
            // AUDIT: This has been audited to make sure that key pair
            //        objects returned from this method cannot be used
            //        in contexts that require only "full-trusted" key
            //        pairs.
            //
            if (!allowObject)
            {
                error = errors;
                return ReturnCode.Error;
            }

            @object = null;
            localError = null;

            if (interpreter.GetObject( /* AUDIT */
                    keyPairObjectName, LookupFlags.Default,
                    ref @object, ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                error = errors;
                return ReturnCode.Error;
            }

            localKeyPair = (@object != null) ?
                @object.Value as IKeyPair : null;

            if (localKeyPair == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid key pair");

                error = errors;
                return ReturnCode.Error;
            }

            certificate = localCertificate;
            keyPair = localKeyPair;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the additional object flags to apply, marking objects as
        /// safe when requested for a safe interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose safety is considered; may be null.
        /// </param>
        /// <param name="safe">
        /// Non-zero to request that objects be marked safe when the
        /// interpreter is safe.
        /// </param>
        /// <returns>
        /// <see cref="ObjectFlags.Safe" /> when applicable; otherwise,
        /// <see cref="ObjectFlags.None" />.
        /// </returns>
        public static ObjectFlags GetExtraObjectFlags(
            Interpreter interpreter, /* in */
            bool safe                /* in */
            )
        {
            if ((interpreter != null) && safe && interpreter.IsSafe())
                return ObjectFlags.Safe;

            return ObjectFlags.None;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a stream containing the warning text, reading from the
        /// specified file when present, or from the plugin assembly
        /// resources otherwise.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose assembly supplies the warning text when no
        /// file name is given.
        /// </param>
        /// <param name="fileName">
        /// The name of a file containing the warning text; may be null or
        /// empty.
        /// </param>
        /// <param name="raw">
        /// Non-zero to read the plain text resource; zero to read the XML
        /// resource.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The warning text stream, or null on failure.
        /// </returns>
        public static Stream GetWarningStream(
            IPluginData pluginData, /* in */
            string fileName,        /* in */
            bool raw,               /* in */
            ref Result error        /* out */
            )
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                if (File.Exists(fileName))
                {
                    try
                    {
                        return new FileStream(
                            fileName, FileMode.Open, FileAccess.Read);
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                }
                else
                {
                    error = String.Format(
                        "could not read {0}: no such file",
                        Utility.FormatWrapOrNull(fileName));
                }
            }
            else if (pluginData != null)
            {
                //
                // TODO: Why is this being used instead of the regular
                //       call (i.e. the one without the "NoIsolated"
                //       suffix)?
                //
                if (!Utility.IsCrossAppDomainNoIsolated(pluginData))
                {
                    return GetWarningStream(
                        pluginData.Assembly, raw, ref error);
                }
                else
                {
                    error = "wrong application domain";
                }
            }
            else
            {
                error = "invalid plugin data";
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a stream containing the warning text embedded as a resource
        /// in the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly that contains the warning text resource.
        /// </param>
        /// <param name="raw">
        /// Non-zero to read the plain text resource; zero to read the XML
        /// resource.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The warning text stream, or null on failure.
        /// </returns>
        private static Stream GetWarningStream(
            Assembly assembly, /* in */
            bool raw,          /* in */
            ref Result error   /* out */
            )
        {
            if (assembly != null)
            {
                string resourceName = raw ?
                    Constants.WarningTxtFileName :
                    Constants.WarningXmlFileName;

                if (resourceName == null)
                {
                    error = "invalid resource name";
                    return null;
                }

                return CertificateSharedOps.GetStream(
                    assembly, resourceName, ref error);
            }
            else
            {
                error = "invalid plugin assembly";
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs a precomputed hash using the RSA private key of the
        /// specified key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the hash.
        /// </param>
        /// <param name="bytes">
        /// The precomputed hash bytes to sign.
        /// </param>
        /// <param name="keyPair">
        /// The RSA key pair whose private key is used to sign.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the computed signature.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status value; otherwise, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode SignHashRsa(
            string hashAlgorithmName, /* in */
            byte[] bytes,             /* in */
            IKeyPair keyPair,         /* in */
            ref byte[] signature,     /* out */
            ref Result result         /* out */
            )
        {
            if (String.IsNullOrEmpty(hashAlgorithmName))
            {
                result = "invalid hash algorithm name";
                return ReturnCode.Error;
            }

            if (bytes == null)
            {
                result = "invalid byte array";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            if (!keyPair.HavePrivateKey)
            {
                result = "private key is not present";
                return ReturnCode.Error;
            }

            RsaKeyPair localKeyPair = keyPair as RsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an RSA key pair";
                return ReturnCode.Error;
            }

            RSAParameters parameters = localKeyPair.ToPrivateParameters(true);

#if DEBUG
            RsaKeyFile.MaybeDumpSignParameters(
                "SignHashRsa", parameters, TracePriority.Highest);
#endif

            Result localError = null;

            using (RSA rsa = CertificateSharedOps.CreateRsaProvider(
                    ref localError))
            {
                if (rsa != null)
                {
#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
                    BigRSACryptoServiceProvider bigRsa =
                        rsa as BigRSACryptoServiceProvider;

                    if (bigRsa != null)
                    {
                        bigRsa.ImportParameters(parameters);

                        signature = bigRsa.SignHash(
                            bytes, new HashAlgorithmName(hashAlgorithmName),
                            RSASignaturePadding.Pkcs1);

                        result = OperationStatus.SignedOk;
                        return ReturnCode.Ok;
                    }
#endif

                    RSAProvider provider = rsa as RSAProvider;

                    if (provider != null)
                    {
                        provider.ImportParameters(parameters);

#if !NET_STANDARD_20
                        signature = provider.SignHash(
                            bytes, CryptoConfig.MapNameToOID(hashAlgorithmName));
#else
                        //
                        // TODO: Sanity check the parameters used here.
                        //
                        signature = provider.SignHash(
                            bytes, new HashAlgorithmName(hashAlgorithmName),
                            RSASignaturePadding.Pkcs1);
#endif

                        result = OperationStatus.SignedOk;
                        return ReturnCode.Ok;
                    }

                    result = String.Format(
                        "RSA provider is not based on " +
                        "{0} -OR- its use is not enabled",
                        typeof(RSAProvider));

                    return ReturnCode.Error;
                }
                else if (localError != null)
                {
                    result = localError;
                    return ReturnCode.Error;
                }
                else
                {
                    result = String.Format(
                        "RSA provider is not based on {0}",
                        typeof(RSA));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs a precomputed hash using the DSA private key of the
        /// specified key pair.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the hash.
        /// </param>
        /// <param name="bytes">
        /// The precomputed hash bytes to sign.
        /// </param>
        /// <param name="keyPair">
        /// The DSA key pair whose private key is used to sign.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the computed signature.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status value; otherwise, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode SignHashDsa(
            string hashAlgorithmName, /* in */
            byte[] bytes,             /* in */
            IKeyPair keyPair,         /* in */
            ref byte[] signature,     /* out */
            ref Result result         /* out */
            )
        {
            if (String.IsNullOrEmpty(hashAlgorithmName))
            {
                result = "invalid hash algorithm name";
                return ReturnCode.Error;
            }

            if (bytes == null)
            {
                result = "invalid byte array";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid key pair";
                return ReturnCode.Error;
            }

            if (!keyPair.HavePrivateKey)
            {
                result = "private key is not present";
                return ReturnCode.Error;
            }

            DsaKeyPair localKeyPair = keyPair as DsaKeyPair;

            if (localKeyPair == null)
            {
                result = "not an DSA key pair";
                return ReturnCode.Error;
            }

            DSAParameters parameters = localKeyPair.ToPrivateParameters(true);

#if DEBUG
            DsaKeyFile.MaybeDumpSignParameters(
                "SignHashDsa", parameters, TracePriority.Highest);
#endif

            Result localError = null;

            using (DSA dsa = CertificateSharedOps.CreateDsaProvider(
                    ref localError))
            {
                if (dsa != null)
                {
                    DSAProvider provider = dsa as DSAProvider;

                    if (provider != null)
                    {
#if NET_STANDARD_20 || NET_STANDARD_21
                        //
                        // HACK: Apparently, if these DSAParameters fields are
                        //       not nulled out for .NET Core (Windows only?),
                        //       WindowsCryptographicException will be thrown
                        //       after NCryptImportKey fails from inside the
                        //       ImportKeyBlob method.
                        //
                        if (Utility.IsWindowsOperatingSystem())
                        {
                            parameters.Seed = null;
                            parameters.Counter = 0;
                        }
#endif

                        provider.ImportParameters(parameters);

#if NET_STANDARD_20 || NET_STANDARD_21
                        //
                        // BUGBUG: *SECURITY* This is really insecure because
                        //         it ignores the hash algorithm name used by
                        //         the caller and always uses SHA1, which is
                        //         fairly weak.
                        //
                        signature = provider.CreateSignature(bytes);
#else
                        //
                        // HACK: Apparently, Mono only supports the literal
                        //       string "SHA1" here.  Anything other string
                        //       will cause an exception.
                        //
                        if (Utility.IsMono())
                        {
                            signature = provider.SignHash(
                                bytes, hashAlgorithmName);
                        }
                        else
                        {
                            //
                            // TODO: Sanity check the parameters used here.
                            //
                            signature = provider.SignHash(
                                bytes, CryptoConfig.MapNameToOID(
                                hashAlgorithmName));
                        }
#endif

                        result = OperationStatus.SignedOk;
                        return ReturnCode.Ok;
                    }

                    result = String.Format(
                        "DSA provider is not based on " +
                        "{0} -OR- its use is not enabled",
                        typeof(DSAProvider));

                    return ReturnCode.Error;
                }
                else if (localError != null)
                {
                    result = localError;
                    return ReturnCode.Error;
                }
                else
                {
                    result = String.Format(
                        "DSA provider is not based on {0}",
                        typeof(DSA));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs a precomputed hash using the specified key pair,
        /// dispatching to the RSA or DSA implementation as appropriate.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the hash.
        /// </param>
        /// <param name="bytes">
        /// The precomputed hash bytes to sign.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the computed signature.
        /// </param>
        /// <param name="result">
        /// Upon success, receives a status value; otherwise, receives
        /// information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SignHash(
            string hashAlgorithmName, /* in */
            byte[] bytes,             /* in */
            IKeyPair keyPair,         /* in */
            ref byte[] signature,     /* out */
            ref Result result         /* out */
            )
        {
            if (keyPair is RsaKeyPair)
            {
                return SignHashRsa(
                    hashAlgorithmName, bytes, keyPair, ref signature,
                    ref result);
            }

            if (keyPair is DsaKeyPair)
            {
                return SignHashDsa(
                    hashAlgorithmName, bytes, keyPair, ref signature,
                    ref result);
            }

            result = "unsupported key pair type";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs a precomputed hash using the specified key pair and stores
        /// the resulting signature on the certificate.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to produce the hash.
        /// </param>
        /// <param name="bytes">
        /// The precomputed hash bytes to sign.
        /// </param>
        /// <param name="certificate">
        /// The certificate that receives the computed signature.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode SignHash(
            string hashAlgorithmName, /* in */
            byte[] bytes,             /* in */
            ICertificate certificate, /* in */
            IKeyPair keyPair,         /* in */
            ref Result result         /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            byte[] signature = null;

            if (SignHash(
                    hashAlgorithmName, bytes, keyPair,
                    ref signature, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            certificate.Signature = signature;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prepares, hashes, and signs the contents of a certificate
        /// together with an optional string value.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used for keyed hashing; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to prepare, hash, and sign.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags controlling how the certificate is hashed; may
        /// be null.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding used when hashing; may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="value">
        /// The optional string value to include in the hash; may be null.
        /// </param>
        /// <param name="setId">
        /// Non-zero to set the certificate identifier before signing.
        /// </param>
        /// <param name="setTimeStamp">
        /// Non-zero to set the certificate time stamp before signing.
        /// </param>
        /// <param name="setKey">
        /// Non-zero to set the certificate key information before signing.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SignString(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            string value,                               /* in: OPTIONAL */
            bool setId,                                 /* in */
            bool setTimeStamp,                          /* in */
            bool setKey,                                /* in */
            ref Result result                           /* out */
            )
        {
            if (certificate == null)
            {
                result = "invalid certificate";
                return ReturnCode.Error;
            }

            try
            {
                //
                // NOTE: Prepare the certificate to be signed.
                //
                Certificate.PrepareToSign(
                    hashAlgorithmName, certificate, keyPair,
                    ref setId, ref setTimeStamp, ref setKey);

                //
                // NOTE: Now. hash the contents of the certificate.
                //
                ReturnCode code;
                byte[] hashBytes = null;

                code = CertificateSharedOps.HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, value,
                    ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = SignHash(
                        hashAlgorithmName, hashBytes, certificate,
                        keyPair, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prepares, hashes, and signs the contents of a certificate
        /// together with the contents of the specified file.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used for keyed hashing; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to prepare, hash, and sign.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags controlling how the certificate is hashed; may
        /// be null.
        /// </param>
        /// <param name="encoding">
        /// The optional text encoding used when hashing; may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="fileName">
        /// The name of the file whose contents are included in the hash.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when reading the
        /// file; may be null.
        /// </param>
        /// <param name="setId">
        /// Non-zero to set the certificate identifier before signing.
        /// </param>
        /// <param name="setTimeStamp">
        /// Non-zero to set the certificate time stamp before signing.
        /// </param>
        /// <param name="setKey">
        /// Non-zero to set the certificate key information before signing.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SignFile(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IKeyPair keyPair,                           /* in */
            string fileName,                            /* in */
            int? timeout,                               /* in: OPTIONAL */
            bool setId,                                 /* in */
            bool setTimeStamp,                          /* in */
            bool setKey,                                /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                //
                // NOTE: Prepare the certificate to be signed.
                //
                Certificate.PrepareToSign(
                    hashAlgorithmName, certificate, keyPair,
                    ref setId, ref setTimeStamp, ref setKey);

                //
                // NOTE: Now. hash the contents of the certificate.
                //
                ReturnCode code;
                byte[] hashBytes = null;

                code = CertificateSharedOps.HashFile(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, fileName,
                    timeout, ref hashBytes, ref result);

                if (code == ReturnCode.Ok)
                {
                    code = SignHash(
                        hashAlgorithmName, hashBytes, certificate,
                        keyPair, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prepares, hashes, and signs the contents of a certificate.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used for keyed hashing; may be null.
        /// </param>
        /// <param name="certificate">
        /// The certificate to prepare, hash, and sign.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags controlling how the certificate is hashed; may
        /// be null.
        /// </param>
        /// <param name="encoding">
        /// The text encoding used when hashing.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="setId">
        /// Non-zero to set the certificate identifier before signing.
        /// </param>
        /// <param name="setTimeStamp">
        /// Non-zero to set the certificate time stamp before signing.
        /// </param>
        /// <param name="setKey">
        /// Non-zero to set the certificate key information before signing.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Sign(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in */
            IKeyPair keyPair,                           /* in */
            bool setId,                                 /* in */
            bool setTimeStamp,                          /* in */
            bool setKey,                                /* in */
            ref Result result                           /* out */
            )
        {
            try
            {
                //
                // NOTE: Prepare the certificate to be signed.
                //
                Certificate.PrepareToSign(
                    hashAlgorithmName, certificate, keyPair,
                    ref setId, ref setTimeStamp, ref setKey);

                //
                // NOTE: Now. hash the contents of the certificate.
                //
                ReturnCode code;
                byte[] hashBytes = null;

                code = CertificateSharedOps.Hash(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, ref hashBytes,
                    ref result);

                if (code == ReturnCode.Ok)
                {
                    code = SignHash(
                        hashAlgorithmName, hashBytes, certificate,
                        keyPair, ref result);
                }

                return code;
            }
            catch (Exception e)
            {
                result = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs the contents of a file and writes a detached signature file
        /// alongside it, optionally prefixed with warning text.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to sign.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose private key is used to sign.
        /// </param>
        /// <param name="noWarning">
        /// Non-zero to omit the warning text from the signature file.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SignFile(
            string fileName,  /* in */
            IKeyPair keyPair, /* in */
            bool noWarning,   /* in */
            ref Result error  /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid file name";
                return ReturnCode.Error;
            }

            string signatureFileName =
                CertificateDataOps.FormatSignatureFileName(fileName);

            if (File.Exists(signatureFileName))
            {
                error = "signature file already exists";
                return ReturnCode.Error;
            }

            try
            {
                string warningTxt = null;

                if (!noWarning)
                {
                    using (Stream stream = GetWarningStream(
                            CertificateAssemblyOps.GetObject(), true,
                            ref error))
                    {
                        if (stream == null)
                            return ReturnCode.Error;

                        using (StreamReader reader = new StreamReader(
                                stream))
                        {
                            warningTxt = reader.ReadToEnd();
                        }

                        if (String.IsNullOrEmpty(warningTxt))
                        {
                            error = "invalid warning text";
                            return ReturnCode.Error;
                        }
                    }
                }

                IEnumerable<IKeyPair> keyPairs = new IKeyPair[] {
                    keyPair
                };

                byte[] signature = null;

                if (CryptographyOps.Sign(
                        CertificateSharedOps.GetHashAlgorithm(null,
                        keyPairs, null, HashAlgorithmType.CommandUse),
                        null, File.ReadAllBytes(fileName), keyPair,
                        ref signature, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                byte[] publicKeyToken = (keyPair != null) ?
                    keyPair.PublicKeyToken : null;

                File.WriteAllText(
                    signatureFileName, String.Format("{0}{1}{2}",
                    String.Format(warningTxt, Path.GetFileName(
                        signatureFileName),
                    CertificateDataOps.FormatPublicKeyToken(
                        publicKeyToken, false, false)),
                    CertificateDataOps.FormatSignatureBlock(
                        signature), Characters.DosNewLine));

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the contents of a file against its detached signature
        /// file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to verify.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose public key is used to verify the signature.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when reading the
        /// signature file; may be null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode VerifyFile(
            string fileName,  /* in */
            IKeyPair keyPair, /* in */
            int? timeout,     /* in */
            ref Result error  /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid file name";
                return ReturnCode.Error;
            }

            string signatureFileName =
                CertificateDataOps.FormatSignatureFileName(fileName);

            if (!File.Exists(signatureFileName))
            {
                error = "signature file does not exist";
                return ReturnCode.Error;
            }

            try
            {
                byte[] signature = null;

                if (!CertificateDataOps.TryReadSignatureFile(
                        null, null, signatureFileName, timeout,
                        false, ref signature, ref error))
                {
                    return ReturnCode.Error;
                }

                IEnumerable<IKeyPair> keyPairs = new IKeyPair[] {
                    keyPair
                };

                if (CryptographyOps.Verify(
                        CertificateSharedOps.GetHashAlgorithm(null,
                        keyPairs, null, HashAlgorithmType.CommandUse),
                        null, File.ReadAllBytes(fileName), keyPair,
                        signature, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }
    }
}
