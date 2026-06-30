/*
 * RegistryManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Microsoft.Win32;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Public;
using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Provides the default implementation of
    /// <see cref="IRegistryManager" />, used to locate the registry root key
    /// and key name associated with a particular plugin and interpreter.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("c5d1b4d9-58b1-4808-9f55-0445749c5e29")]
    public sealed class RegistryManager :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IRegistryManager
    {
        #region Private Data
        /// <summary>
        /// The interpreter associated with this registry manager, if any.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// The plugin data used to determine the registry key name.
        /// </summary>
        private IPluginData pluginData;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="RegistryManager" />
        /// class.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to associate with this registry manager, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to determine the registry key name.
        /// </param>
        private RegistryManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in */
            )
        {
            this.interpreter = interpreter;
            this.pluginData = pluginData;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to locate an existing registry manager for the specified
        /// interpreter and plugin data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when searching for the registry manager, if
        /// any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to identify the registry manager.
        /// </param>
        /// <returns>
        /// The matching <see cref="IRegistryManager" /> instance, or null if
        /// one could not be found.
        /// </returns>
        private static IRegistryManager FindRegistryManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in */
            )
        {
            return CertificateSharedOps.FindRegistryManager(
                interpreter, pluginData, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new registry manager for the specified interpreter and
        /// plugin data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to associate with the new registry manager, if
        /// any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to determine the registry key name.
        /// </param>
        /// <returns>
        /// The newly created <see cref="IRegistryManager" /> instance.
        /// </returns>
        private static IRegistryManager CreateRegistryManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in */
            )
        {
            return new RegistryManager(interpreter, pluginData);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the name and version from the specified assembly name,
        /// optionally falling back to default values when they are not
        /// available.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name from which to obtain the name and version, if
        /// any.
        /// </param>
        /// <param name="allowFallback">
        /// Non-zero to use fallback values when the name or version could not
        /// be obtained from <paramref name="assemblyName" />.
        /// </param>
        /// <param name="name">
        /// Upon return, receives the assembly name, or a fallback value when
        /// <paramref name="allowFallback" /> is non-zero.
        /// </param>
        /// <param name="version">
        /// Upon return, receives the assembly version, or a fallback value
        /// when <paramref name="allowFallback" /> is non-zero.
        /// </param>
        private static void GetNameAndVersion( /* CORE */
            AssemblyName assemblyName, /* in */
            bool allowFallback,        /* in */
            out string name,           /* out */
            out Version version        /* out */
            )
        {
            name = null;
            version = null;

            if (assemblyName != null)
            {
                name = assemblyName.Name;
                version = assemblyName.Version;
            }

            if (allowFallback)
            {
                if (name == null)
                    name = typeof(RegistryManager).Name;

                if (version == null)
                    version = new Version();
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRegistryManager Members
        /// <summary>
        /// Gets the registry root key (either the per-machine or per-user
        /// hive) that should be used based on the specified preference.
        /// </summary>
        /// <param name="perMachine">
        /// Non-zero to prefer the per-machine registry hive, zero to prefer
        /// the per-user registry hive, or null to use the default preference.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The registry root key to use, or null if it could not be
        /// determined.
        /// </returns>
        public object GetRootKey( /* CORE */
            bool? perMachine, /* in */
            ref Result error  /* out */
            )
        {
            return CertificateSharedOps.ShouldUsePerMachine(perMachine) ?
                Registry.LocalMachine : Registry.CurrentUser;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the registry key name associated with the plugin assembly,
        /// optionally including the full path that incorporates the registry
        /// root key.
        /// </summary>
        /// <param name="perMachine">
        /// Non-zero to use the per-machine registry hive, zero to use the
        /// per-user registry hive, or null to use the default preference.
        /// This is only used when <paramref name="full" /> is non-zero.
        /// </param>
        /// <param name="full">
        /// Non-zero to return the full registry key name, including the
        /// registry root key; otherwise, the key name relative to the root
        /// key is returned.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The registry key name, or null if it could not be determined.
        /// </returns>
        public string GetKeyName( /* CORE */
            bool? perMachine, /* in */
            bool full,        /* in */
            ref Result error  /* out */
            )
        {
            string name;
            Version version;

            /* NO RESULT */
            GetNameAndVersion(
                CertificateSharedOps.GetAssemblyName(pluginData),
                CertificateAssemblyOps.MatchObjectOrName(
                pluginData), out name, out version);

            if (name == null)
            {
                error = "invalid name from plugin assembly";
                return null;
            }

            if (name.Length == 0)
            {
                error = "empty name from plugin assembly";
                return null;
            }

            if (version == null)
            {
                error = "invalid version from plugin assembly";
                return null;
            }

            string versionString = version.ToString(
                Constants.VersionComponentsForKeyName);

            if (versionString == null)
            {
                error = "invalid version string from plugin assembly";
                return null;
            }

            if (versionString.Length == 0)
            {
                error = "empty version string from plugin assembly";
                return null;
            }

            string baseKeyName = Constants.BaseKeyName;

            if (baseKeyName == null)
            {
                error = "invalid base registry key name";
                return null;
            }

            if (baseKeyName.Length == 0)
            {
                error = "empty base registry key name";
                return null;
            }

            string keyName = CertificateDataOps.JoinKeyNames(
                baseKeyName, name, versionString);

            if (keyName == null)
            {
                error = "failed to build registry key name";
                return null;
            }

            if (full)
            {
                RegistryKey rootKey = GetRootKey(
                    perMachine, ref error) as RegistryKey;

                if (rootKey == null)
                {
                    error = new ResultList(
                        "invalid registry root key", error);

                    return null;
                }

                string fullKeyName = CertificateDataOps.JoinKeyNames(
                    rootKey.ToString(), keyName);

                if (fullKeyName == null)
                {
                    error = "failed to build full registry key name";
                    return null;
                }

                return fullKeyName;
            }
            else
            {
                return keyName;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Gets the registry manager for the specified interpreter and plugin
        /// data, optionally creating a new one when an existing one cannot be
        /// found.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when locating or creating the registry
        /// manager, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to identify the registry manager.
        /// </param>
        /// <param name="create">
        /// Non-zero to create a new registry manager when an existing one
        /// cannot be found.
        /// </param>
        /// <returns>
        /// The matching <see cref="IRegistryManager" /> instance, or null if
        /// one could not be found or created.
        /// </returns>
        public static IRegistryManager GetRegistryManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in */
            bool create              /* in */
            )
        {
            IRegistryManager registryManager = FindRegistryManager(
                interpreter, pluginData);

            if ((registryManager == null) && create)
            {
                registryManager = CreateRegistryManager(
                    interpreter, pluginData);
            }

            return registryManager;
        }
        #endregion
    }
}
