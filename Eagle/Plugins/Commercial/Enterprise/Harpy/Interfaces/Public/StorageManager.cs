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

using System.Globalization;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Defines the interface used to read, write, delete, and enumerate
    /// named values within an underlying storage backend.
    /// </summary>
    [ObjectId("6d70c210-2514-484e-b17e-82888fcf6041")]
    public interface IStorageManager /* CORE */
    {
        /// <summary>
        /// Gets or sets a value indicating whether security must be present
        /// when accessing the underlying storage.  A null value indicates
        /// that the requirement is unspecified.
        /// </summary>
        bool? MustHaveSecurity { get; set; }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value that represents the default for stored values.
        /// </summary>
        /// <returns>
        /// The object that represents the default stored value.
        /// </returns>
        object GetDefaultValue();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified value is equal to the default
        /// stored value.
        /// </summary>
        /// <param name="value">
        /// The value to compare against the default stored value.
        /// </param>
        /// <returns>
        /// Non-zero if <paramref name="value" /> is the default value;
        /// otherwise, zero.
        /// </returns>
        bool IsDefaultValue(
            object value
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the named value from the underlying storage.
        /// </summary>
        /// <param name="name">
        /// The name of the value to read.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when reading the value, or null to use the
        /// default culture.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to read the value from per-machine storage, zero to read
        /// it from per-user storage, or null to use the default scope.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the named value must exist; otherwise, zero.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the bytes of the value that was read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        ReturnCode ReadValue(
            string name,
            CultureInfo cultureInfo,
            bool? perMachine,
            bool mustHaveValue,
            ref byte[] value,
            ref Result error
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the named value to the underlying storage.
        /// </summary>
        /// <param name="name">
        /// The name of the value to write.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when writing the value, or null to use the
        /// default culture.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to write the value to per-machine storage, zero to write
        /// it to per-user storage, or null to use the default scope.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the value to write must be present; otherwise, zero.
        /// </param>
        /// <param name="value">
        /// The bytes of the value to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        ReturnCode WriteValue(
            string name,
            CultureInfo cultureInfo,
            bool? perMachine,
            bool mustHaveValue,
            byte[] value,
            ref Result error
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes the named value from the underlying storage.
        /// </summary>
        /// <param name="name">
        /// The name of the value to delete.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when deleting the value, or null to use the
        /// default culture.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to delete the value from per-machine storage, zero to
        /// delete it from per-user storage, or null to use the default scope.
        /// </param>
        /// <param name="errorOnMissingValue">
        /// Non-zero to treat a missing value as an error; otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        ReturnCode DeleteValue(
            string name,
            CultureInfo cultureInfo,
            bool? perMachine,
            bool errorOnMissingValue,
            ref Result error
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enumerates the names of the values present in the underlying
        /// storage.
        /// </summary>
        /// <param name="cultureInfo">
        /// The culture to use when listing the values, or null to use the
        /// default culture.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to list values from per-machine storage, zero to list
        /// them from per-user storage, or null to use the default scope.
        /// </param>
        /// <param name="names">
        /// Upon success, receives the names of the values that were found.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        ReturnCode ListValues(
            CultureInfo cultureInfo,
            bool? perMachine,
            ref string[] names,
            ref Result error
        );
    }
}
