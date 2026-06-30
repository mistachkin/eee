/*
 * RegistryOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Microsoft.Win32;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Interfaces.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods for reading, writing, deleting, and enumerating
    /// licensing-related values stored in the Windows registry.  Registry key
    /// locations are resolved through an <see cref="IRegistryManager" />.
    /// </summary>
    [ObjectId("735c5574-4bf7-4932-bc93-a4eb87a07e68")]
    internal static class RegistryOps
    {
        #region Public Registry Setting Value Abstraction Methods
        /// <summary>
        /// Gets the sentinel object used to represent the default (i.e. not
        /// set) registry value.
        /// </summary>
        /// <returns>
        /// The sentinel object representing the default registry value.
        /// </returns>
        public static object GetDefaultValue() /* CORE */
        {
            return Constants.DefaultValue;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified value is the same instance as the
        /// sentinel default value returned by <see cref="GetDefaultValue" />.
        /// </summary>
        /// <param name="value">
        /// The value to compare against the default value.
        /// </param>
        /// <returns>
        /// Non-zero if <paramref name="value" /> is the default value;
        /// otherwise, zero.
        /// </returns>
        public static bool IsDefaultValue( /* CORE */
            object value /* in */
            )
        {
            return Object.ReferenceEquals(value, GetDefaultValue());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a binary value with the specified name from the licensing
        /// registry key resolved by the provided registry manager.
        /// </summary>
        /// <param name="registryManager">
        /// The registry manager used to resolve the full registry key name.
        /// </param>
        /// <param name="name">
        /// The name of the registry value to read.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to read from the per-machine key, zero to read from the
        /// per-user key, or null to let the registry manager decide.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero to require that the value exists, is set, and is binary;
        /// otherwise, a missing or default value is tolerated.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the binary value that was read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ReadValue( /* CORE */
            IRegistryManager registryManager, /* in */
            string name,                      /* in */
            bool? perMachine,                 /* in */
            bool mustHaveValue,               /* in */
            ref byte[] value,                 /* out */
            ref Result error                  /* out */
            )
        {
            try
            {
                if (registryManager == null)
                {
                    error = "invalid registry manager";
                    return ReturnCode.Error;
                }

                string fullKeyName = registryManager.GetKeyName(
                    perMachine, true, ref error);

                if (fullKeyName == null)
                {
                    error = new ResultList(
                        "invalid full registry key name", error);

                    return ReturnCode.Error;
                }

                object localValue = Registry.GetValue(
                    fullKeyName, name, GetDefaultValue()); /* throw */

                if (mustHaveValue)
                {
                    if (localValue == null)
                    {
                        error = "key not found";
                        return ReturnCode.Error;
                    }

                    if (IsDefaultValue(localValue))
                    {
                        error = "value not set";
                        return ReturnCode.Error;
                    }

                    if (!(localValue is byte[]))
                    {
                        error = "value is not binary";
                        return ReturnCode.Error;
                    }
                }

                value = localValue as byte[];
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a binary value with the specified name to the licensing
        /// registry key resolved by the provided registry manager.
        /// </summary>
        /// <param name="registryManager">
        /// The registry manager used to resolve the full registry key name.
        /// </param>
        /// <param name="name">
        /// The name of the registry value to write.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to write to the per-machine key, zero to write to the
        /// per-user key, or null to let the registry manager decide.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero to require that a non-null, non-default value is supplied;
        /// otherwise, such values are rejected.
        /// </param>
        /// <param name="value">
        /// The binary value to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode WriteValue( /* CORE */
            IRegistryManager registryManager, /* in */
            string name,                      /* in */
            bool? perMachine,                 /* in */
            bool mustHaveValue,               /* in */
            byte[] value,                     /* in */
            ref Result error                  /* out */
            )
        {
            try
            {
                if (registryManager == null)
                {
                    error = "invalid registry manager";
                    return ReturnCode.Error;
                }

                if (mustHaveValue)
                {
                    if (value == null)
                    {
                        error = "no value specified";
                        return ReturnCode.Error;
                    }

                    if (IsDefaultValue(value))
                    {
                        error = "default value specified";
                        return ReturnCode.Error;
                    }
                }

                string fullKeyName = registryManager.GetKeyName(
                    perMachine, true, ref error);

                if (fullKeyName == null)
                {
                    error = new ResultList(
                        "invalid full registry key name", error);

                    return ReturnCode.Error;
                }

                Registry.SetValue(fullKeyName, name, value,
                    RegistryValueKind.Binary); /* throw */

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes the value with the specified name from the licensing
        /// registry key resolved by the provided registry manager.
        /// </summary>
        /// <param name="registryManager">
        /// The registry manager used to resolve the registry root key and key
        /// name.
        /// </param>
        /// <param name="name">
        /// The name of the registry value to delete.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to delete from the per-machine key, zero to delete from
        /// the per-user key, or null to let the registry manager decide.
        /// </param>
        /// <param name="errorOnMissingValue">
        /// Non-zero to raise an error if the value to delete does not exist;
        /// otherwise, a missing value is ignored.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode DeleteValue( /* CORE */
            IRegistryManager registryManager, /* in */
            string name,                      /* in */
            bool? perMachine,                 /* in */
            bool errorOnMissingValue,         /* in */
            ref Result error                  /* out */
            )
        {
            try
            {
                if (registryManager == null)
                {
                    error = "invalid registry manager";
                    return ReturnCode.Error;
                }

                RegistryKey rootKey = registryManager.GetRootKey(
                    perMachine, ref error) as RegistryKey;

                if (rootKey == null)
                {
                    error = new ResultList(
                        "invalid registry root key", error);

                    return ReturnCode.Error;
                }

                string keyName = registryManager.GetKeyName(
                    perMachine, false, ref error);

                if (keyName == null)
                {
                    error = new ResultList(
                        "invalid registry key name", error);

                    return ReturnCode.Error;
                }

                using (RegistryKey registryKey = rootKey.OpenSubKey(
                        keyName, true)) /* throw */
                {
                    if (registryKey == null)
                    {
                        error = "key not found";
                        return ReturnCode.Error;
                    }

                    registryKey.DeleteValue(
                        name, errorOnMissingValue); /* throw */
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enumerates the names of all values present in the licensing
        /// registry key resolved by the provided registry manager.
        /// </summary>
        /// <param name="registryManager">
        /// The registry manager used to resolve the registry root key and key
        /// name.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to enumerate the per-machine key, zero to enumerate the
        /// per-user key, or null to let the registry manager decide.
        /// </param>
        /// <param name="names">
        /// Upon success, receives the array of value names found in the key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ListValues( /* CORE */
            IRegistryManager registryManager, /* in */
            bool? perMachine,                 /* in */
            ref string[] names,               /* out */
            ref Result error                  /* out */
            )
        {
            try
            {
                if (registryManager == null)
                {
                    error = "invalid registry manager";
                    return ReturnCode.Error;
                }

                RegistryKey rootKey = registryManager.GetRootKey(
                    perMachine, ref error) as RegistryKey;

                if (rootKey == null)
                {
                    error = new ResultList(
                        "invalid registry root key", error);

                    return ReturnCode.Error;
                }

                string keyName = registryManager.GetKeyName(
                    perMachine, false, ref error);

                if (keyName == null)
                {
                    error = new ResultList(
                        "invalid registry key name", error);

                    return ReturnCode.Error;
                }

                using (RegistryKey registryKey = rootKey.OpenSubKey(
                        keyName, true)) /* throw */
                {
                    if (registryKey == null)
                    {
                        error = "key not found";
                        return ReturnCode.Error;
                    }

                    names = registryKey.GetValueNames(); /* throw */
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
        #endregion
    }
}
