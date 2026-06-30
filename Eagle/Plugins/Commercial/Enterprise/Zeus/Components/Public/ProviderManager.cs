/*
 * ProviderManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Zeus.Components.Private;

namespace Zeus.Components.Public
{
    /// <summary>
    /// Provides the factory used to create RFC 2898 (PBKDF2) data providers
    /// from an assembly name and/or type name.  It recognizes the built-in
    /// provider types by name (the RFC 2898 data manager and the core, test,
    /// remote, and script providers) and otherwise resolves and instantiates
    /// an arbitrary provider type, optionally from a named assembly.
    /// </summary>
    [ObjectId("356ae94d-be26-4695-a5ff-cf778b8a8d76")]
    public static class ProviderManager
    {
        //
        // HACK: This class and all of its callers always assume that all the
        //       "providers" to be created implement the IRfc2898DataProvider
        //       interface.
        //
        /// <summary>
        /// Creates an RFC 2898 data provider selected by the supplied
        /// assembly and type names.  When neither name is supplied the call
        /// succeeds with a null result; a recognized built-in type name (with
        /// no assembly name) yields the corresponding built-in provider; an
        /// unrecognized type name is resolved within the appropriate
        /// application domain and instantiated; and supplying both an assembly
        /// name and a type name loads the provider from that assembly.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the provider will be associated with.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to determine the application domain in which
        /// types are resolved and providers are created.
        /// </param>
        /// <param name="clientData">
        /// The extra data passed to the created provider, if any.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly containing the provider type, or null to
        /// resolve the type without a specific assembly.
        /// </param>
        /// <param name="typeName">
        /// The name of the provider type to create, or null when no type is
        /// specified.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created provider object, or null when no type was specified or
        /// the provider could not be created.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the internal control flow is reached with a null
        /// assembly name or type name that was expected to be non-null.
        /// </exception>
        /// <exception cref="NotImplementedException">
        /// Thrown when an unhandled combination of arguments is encountered.
        /// </exception>
        [Throw(true)]
        public static object Create(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            IClientData clientData,  /* in */
            string assemblyName,     /* in */
            string typeName,         /* in */
            ref Result error         /* out */
            )
        {
            if ((assemblyName == null) && (typeName == null))
            {
                error = "no assembly name or type name specified";
                return null; /* "success" */
            }
            else if ((assemblyName == null) &&
                Utility.SystemStringEquals(typeName,
                    typeof(Rfc2898Data).Name))
            {
                return Rfc2898Data.IsEnabled() ?
                    new Rfc2898Data() : null;
            }
            else if ((assemblyName == null) &&
                (Utility.SystemStringEquals(typeName,
                    typeof(Providers.Core).Name) ||
                Utility.SystemStringEquals(typeName,
                    typeof(Providers.Core).FullName)))
            {
                return new Providers.Core(
                    interpreter, clientData);
            }
            else if ((assemblyName == null) &&
                (Utility.SystemStringEquals(typeName,
                    typeof(Providers.Test).Name) ||
                Utility.SystemStringEquals(typeName,
                    typeof(Providers.Test).FullName)))
            {
                return new Providers.Test(
                    interpreter, clientData);
            }
            else if ((assemblyName == null) &&
                (Utility.SystemStringEquals(typeName,
                    typeof(Providers.Remote).Name) ||
                Utility.SystemStringEquals(typeName,
                    typeof(Providers.Remote).FullName)))
            {
                return new Providers.Remote(
                    interpreter, clientData);
            }
            else if ((assemblyName == null) &&
                (Utility.SystemStringEquals(typeName,
                    typeof(Providers.Script).Name) ||
                Utility.SystemStringEquals(typeName,
                    typeof(Providers.Script).FullName)))
            {
                return new Providers.Script(
                    interpreter, clientData);
            }
            else if ((assemblyName == null) &&
                (typeName != null))
            {
                Type type = null;
                ResultList errors = null;

                if (Value.GetAnyType(
                        interpreter, typeName, null,
                        IsolatedOps.GetAppDomainForGetType(
                            interpreter, pluginData),
                        ValueFlags.None,
                        CommonOps.GetCultureInfo(interpreter),
                        ref type, ref errors) == ReturnCode.Ok)
                {
                    //
                    // NOTE: Apparently, we succeeded in looking
                    //       up the type, attempt to create an 
                    //       instance of it.  This may throw an
                    //       exception.
                    //
                    return Rfc2898Ops.CreateBuiltInDataProvider(
                        interpreter, clientData, assemblyName,
                        typeName, type, ref error);
                }
                else
                {
                    //
                    // NOTE: Skip throwing an exception here.
                    //       The list of errors will later be
                    //       used to throw a catchable script
                    //       error.
                    //
                    error = errors;
                    return null;
                }
            }
            else if ((assemblyName != null) && (typeName != null))
            {
                return Rfc2898Ops.CreateOtherDataProvider(
                    interpreter, clientData, assemblyName,
                    typeName, IsolatedOps.GetAppDomainForCreate(
                    interpreter, pluginData), ref error);
            }
            else
            {
                if (assemblyName == null)
                    throw new ArgumentNullException("assemblyName");

                if (typeName == null)
                    throw new ArgumentNullException("typeName");

                throw new NotImplementedException();
            }
        }
    }
}
