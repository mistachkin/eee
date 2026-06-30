/*
 * StorageManager.cs --
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
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Public;
using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Provides a unified facade for reading, writing, deleting, and listing
    /// licensing storage values, dispatching each operation to the
    /// appropriate backing store (e.g. the Windows registry or an
    /// <see cref="Interpreter" />) selected by the effective
    /// <see cref="StorageType" />.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("8c1dd343-b039-406c-b5d1-0843cc63b1a5")]
    public sealed class StorageManager :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IStorageManager
    {
        #region Private Data
        /// <summary>
        /// The <see cref="Interpreter" /> associated with this storage
        /// manager, if any. This may be null.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// The explicitly configured <see cref="StorageType" /> to use, or
        /// null to fall back to the global or default storage type.
        /// </summary>
        private StorageType? storageType;
        /// <summary>
        /// Non-null to override whether security must be enabled before
        /// interpreter-backed storage operations are permitted; null to use
        /// the global or default setting.
        /// </summary>
        private bool? mustHaveSecurity;

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// The <see cref="IRegistryManager" /> used to perform
        /// registry-backed storage operations.
        /// </summary>
        private IRegistryManager registryManager;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs an instance of the <see cref="StorageManager" /> class.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> to associate with this storage
        /// manager, or null if none is available.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use, or null to defer to the
        /// global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        private StorageManager( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            StorageType? storageType, /* in */
            bool? mustHaveSecurity    /* in */
            )
        {
            this.interpreter = interpreter;
            this.storageType = storageType;
            this.mustHaveSecurity = mustHaveSecurity;
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Constructs an instance of the <see cref="StorageManager" /> class
        /// that uses the specified <see cref="IRegistryManager" /> for
        /// registry-backed storage operations.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> to associate with this storage
        /// manager, or null if none is available.
        /// </param>
        /// <param name="registryManager">
        /// The <see cref="IRegistryManager" /> to use for registry-backed
        /// storage operations.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use, or null to defer to the
        /// global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        private StorageManager( /* CORE */
            Interpreter interpreter,          /* in: OPTIONAL */
            IRegistryManager registryManager, /* in */
            StorageType? storageType,         /* in */
            bool? mustHaveSecurity            /* in */
            )
            : this(interpreter, storageType, mustHaveSecurity)
        {
            this.registryManager = registryManager;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Attempts to locate an existing <see cref="IStorageManager" /> that
        /// is associated with the specified plugin data.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> context to search within, or null
        /// if none is available.
        /// </param>
        /// <param name="pluginData">
        /// The <see cref="IPluginData" /> identifying the plugin whose
        /// storage manager is sought.
        /// </param>
        /// <returns>
        /// The matching <see cref="IStorageManager" />, or null if none was
        /// found.
        /// </returns>
        private static IStorageManager FindStorageManager( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in */
            )
        {
            return CertificateSharedOps.FindStorageManager(
                interpreter, pluginData, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new <see cref="StorageManager" /> using the specified
        /// storage type and security settings.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> to associate with the new storage
        /// manager, or null if none is available.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use, or null to defer to the
        /// global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        /// <returns>
        /// The newly created <see cref="IStorageManager" />.
        /// </returns>
        private static IStorageManager CreateStorageManager( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            StorageType? storageType, /* in */
            bool? mustHaveSecurity    /* in */
            )
        {
            return new StorageManager(
                interpreter, storageType, mustHaveSecurity);
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Creates a new <see cref="StorageManager" /> that uses the
        /// specified <see cref="IRegistryManager" /> for registry-backed
        /// storage operations.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> to associate with the new storage
        /// manager, or null if none is available.
        /// </param>
        /// <param name="registryManager">
        /// The <see cref="IRegistryManager" /> to use for registry-backed
        /// storage operations.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use, or null to defer to the
        /// global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        /// <returns>
        /// The newly created <see cref="IStorageManager" />.
        /// </returns>
        private static IStorageManager CreateStorageManager( /* CORE */
            Interpreter interpreter,          /* in: OPTIONAL */
            IRegistryManager registryManager, /* in */
            StorageType? storageType,         /* in */
            bool? mustHaveSecurity            /* in */
            )
        {
            return new StorageManager(
                interpreter, registryManager, storageType, mustHaveSecurity);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines the effective <see cref="StorageType" /> for this
        /// storage manager, preferring the configured value, then the global
        /// value, and finally a platform-dependent default.
        /// </summary>
        /// <returns>
        /// The effective <see cref="StorageType" /> to use.
        /// </returns>
        private StorageType GetStorageType() /* CORE */
        {
            if (storageType != null)
                return (StorageType)storageType;

            StorageType? localStorageType =
                CertificateGlobalState.GetStorageType();

            if (localStorageType != null)
                return (StorageType)localStorageType;

#if !NET_STANDARD_20
            return StorageType.Registry;
#else
            return StorageType.Interpreter;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether security must be enabled before
        /// interpreter-backed storage operations are permitted, preferring
        /// the configured value, then the global value, and finally the
        /// default.
        /// </summary>
        /// <returns>
        /// Non-zero if security must be enabled; otherwise, zero.
        /// </returns>
        private bool GetMustHaveSecurity()
        {
            if (mustHaveSecurity != null)
                return (bool)mustHaveSecurity;

            bool? localMustHaveSecurity =
                CertificateGlobalState.GetMustHaveSecurity();

            if (localMustHaveSecurity != null)
                return (bool)localMustHaveSecurity;

            return Constants.DefaultMustHaveSecurity;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStorageManager Members
        /// <summary>
        /// Gets or sets a value indicating whether security must be enabled
        /// before interpreter-backed storage operations are permitted. A null
        /// value defers to the global or default setting.
        /// </summary>
        public bool? MustHaveSecurity
        {
            get { return mustHaveSecurity; }
            set { mustHaveSecurity = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value used to represent a missing or unset stored value
        /// for the effective <see cref="StorageType" />.
        /// </summary>
        /// <returns>
        /// The default value, which may be null.
        /// </returns>
        public object GetDefaultValue() /* CORE */
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.GetDefaultValue();
#else
                return null;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                return StorageOps.GetDefaultValue();
            }
            else
            {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified value is equal to the default
        /// value for the effective <see cref="StorageType" />.
        /// </summary>
        /// <param name="value">
        /// The value to test.
        /// </param>
        /// <returns>
        /// Non-zero if <paramref name="value" /> is the default value;
        /// otherwise, zero.
        /// </returns>
        public bool IsDefaultValue( /* CORE */
            object value /* in */
            )
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.IsDefaultValue(value);
#else
                return false;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                return StorageOps.IsDefaultValue(value);
            }
            else
            {
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a stored value by name from the effective storage backend.
        /// </summary>
        /// <param name="name">
        /// The name of the value to read.
        /// </param>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo" /> to use when reading the value, if
        /// applicable.
        /// </param>
        /// <param name="perMachine">
        /// Non-null to indicate whether the per-machine or per-user scope
        /// should be used; otherwise, null.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the value must exist; otherwise, zero.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the bytes of the value that was read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public ReturnCode ReadValue( /* CORE */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            bool mustHaveValue,      /* in */
            ref byte[] value,        /* out */
            ref Result error         /* out */
            )
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.ReadValue(
                    registryManager, name, perMachine,
                    mustHaveValue, ref value, ref error);
#else
                error = "not implemented";
                return ReturnCode.Error;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                if (!GetMustHaveSecurity() ||
                    CertificateSharedOps.HasSecurityEnabled(
                        interpreter, ref error) == ReturnCode.Ok)
                {
                    return StorageOps.ReadValue(
                        interpreter, name, cultureInfo, perMachine,
                        mustHaveValue, ref value, ref error);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                error = String.Format(
                    "unrecognized storage type {0}",
                    _Utility.FormatWrapOrNull(localStorageType));

                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a value by name to the effective storage backend.
        /// </summary>
        /// <param name="name">
        /// The name of the value to write.
        /// </param>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo" /> to use when writing the value, if
        /// applicable.
        /// </param>
        /// <param name="perMachine">
        /// Non-null to indicate whether the per-machine or per-user scope
        /// should be used; otherwise, null.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the value must exist; otherwise, zero.
        /// </param>
        /// <param name="value">
        /// The bytes of the value to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public ReturnCode WriteValue( /* CORE */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            bool mustHaveValue,      /* in */
            byte[] value,            /* in */
            ref Result error         /* out */
            )
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.WriteValue(
                    registryManager, name, perMachine,
                    mustHaveValue, value, ref error);
#else
                error = "not implemented";
                return ReturnCode.Error;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                if (!GetMustHaveSecurity() ||
                    CertificateSharedOps.HasSecurityEnabled(
                        interpreter, ref error) == ReturnCode.Ok)
                {
                    return StorageOps.WriteValue(
                        interpreter, name, cultureInfo, perMachine,
                        mustHaveValue, value, ref error);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                error = String.Format(
                    "unrecognized storage type {0}",
                    _Utility.FormatWrapOrNull(localStorageType));

                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes a stored value by name from the effective storage backend.
        /// </summary>
        /// <param name="name">
        /// The name of the value to delete.
        /// </param>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo" /> to use when deleting the value, if
        /// applicable.
        /// </param>
        /// <param name="perMachine">
        /// Non-null to indicate whether the per-machine or per-user scope
        /// should be used; otherwise, null.
        /// </param>
        /// <param name="errorOnMissingValue">
        /// Non-zero to treat a missing value as an error; otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public ReturnCode DeleteValue( /* CORE */
            string name,              /* in */
            CultureInfo cultureInfo,  /* in */
            bool? perMachine,         /* in */
            bool errorOnMissingValue, /* in */
            ref Result error          /* out */
            )
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.DeleteValue(
                    registryManager, name, perMachine,
                    errorOnMissingValue, ref error);
#else
                error = "not implemented";
                return ReturnCode.Error;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                if (!GetMustHaveSecurity() ||
                    CertificateSharedOps.HasSecurityEnabled(
                        interpreter, ref error) == ReturnCode.Ok)
                {
                    return StorageOps.DeleteValue(
                        interpreter, name, cultureInfo, perMachine,
                        errorOnMissingValue, ref error);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                error = String.Format(
                    "unrecognized storage type {0}",
                    _Utility.FormatWrapOrNull(localStorageType));

                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lists the names of the values present in the effective storage
        /// backend.
        /// </summary>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo" /> to use when listing the values, if
        /// applicable.
        /// </param>
        /// <param name="perMachine">
        /// Non-null to indicate whether the per-machine or per-user scope
        /// should be used; otherwise, null.
        /// </param>
        /// <param name="names">
        /// Upon success, receives the array of value names.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public ReturnCode ListValues( /* CORE */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            ref string[] names,      /* out */
            ref Result error         /* out */
            )
        {
            StorageType localStorageType = GetStorageType();

            if (localStorageType == StorageType.Registry)
            {
#if !NET_STANDARD_20
                return RegistryOps.ListValues(
                    registryManager, perMachine,
                    ref names, ref error);
#else
                error = "not implemented";
                return ReturnCode.Error;
#endif
            }
            else if (localStorageType == StorageType.Interpreter)
            {
                if (!GetMustHaveSecurity() ||
                    CertificateSharedOps.HasSecurityEnabled(
                        interpreter, ref error) == ReturnCode.Ok)
                {
                    return StorageOps.ListValues(
                        interpreter, cultureInfo, perMachine,
                        ref names, ref error);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                error = String.Format(
                    "unrecognized storage type {0}",
                    _Utility.FormatWrapOrNull(localStorageType));

                return ReturnCode.Error;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Gets an existing <see cref="IStorageManager" /> for the specified
        /// plugin, optionally creating a new one if none can be found.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> context to use, or null if none is
        /// available.
        /// </param>
        /// <param name="pluginData">
        /// The <see cref="IPluginData" /> identifying the plugin whose
        /// storage manager is sought.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use when creating a new storage
        /// manager, or null to defer to the global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        /// <param name="create">
        /// Non-zero to create a new storage manager when an existing one
        /// cannot be found; otherwise, zero.
        /// </param>
        /// <returns>
        /// The existing or newly created <see cref="IStorageManager" />, or
        /// null if none was found and creation was not requested.
        /// </returns>
        public static IStorageManager GetStorageManager( /* CORE */
            Interpreter interpreter,  /* in: OPTIONAL */
            IPluginData pluginData,   /* in */
            StorageType? storageType, /* in */
            bool? mustHaveSecurity,   /* in */
            bool create               /* in */
            )
        {
            IStorageManager storageManager = FindStorageManager(
                interpreter, pluginData);

            if ((storageManager == null) && create)
            {
                storageManager = CreateStorageManager(
                    interpreter, storageType, mustHaveSecurity);
            }

            return storageManager;
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Gets an existing <see cref="IStorageManager" /> for the specified
        /// plugin, optionally creating a new one that uses the specified
        /// <see cref="IRegistryManager" /> if none can be found.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> context to use, or null if none is
        /// available.
        /// </param>
        /// <param name="pluginData">
        /// The <see cref="IPluginData" /> identifying the plugin whose
        /// storage manager is sought.
        /// </param>
        /// <param name="registryManager">
        /// The <see cref="IRegistryManager" /> to use when creating a new
        /// storage manager for registry-backed storage operations.
        /// </param>
        /// <param name="storageType">
        /// The <see cref="StorageType" /> to use when creating a new storage
        /// manager, or null to defer to the global or default storage type.
        /// </param>
        /// <param name="mustHaveSecurity">
        /// Non-null to override whether security must be enabled for
        /// interpreter-backed storage operations; otherwise, null.
        /// </param>
        /// <param name="create">
        /// Non-zero to create a new storage manager when an existing one
        /// cannot be found; otherwise, zero.
        /// </param>
        /// <returns>
        /// The existing or newly created <see cref="IStorageManager" />, or
        /// null if none was found and creation was not requested.
        /// </returns>
        public static IStorageManager GetStorageManager( /* CORE */
            Interpreter interpreter,          /* in: OPTIONAL */
            IPluginData pluginData,           /* in */
            IRegistryManager registryManager, /* in */
            StorageType? storageType,         /* in */
            bool? mustHaveSecurity,           /* in */
            bool create                       /* in */
            )
        {
            IStorageManager storageManager = FindStorageManager(
                interpreter, pluginData);

            if ((storageManager == null) && create)
            {
                storageManager = CreateStorageManager(
                    interpreter, registryManager, storageType,
                    mustHaveSecurity);
            }

            return storageManager;
        }
#endif
        #endregion
    }
}
