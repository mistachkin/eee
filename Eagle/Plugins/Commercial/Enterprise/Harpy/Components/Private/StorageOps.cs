/*
 * StorageOps.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods used to read, write, delete, and enumerate
    /// persistent storage setting values by dispatching to the script-based
    /// storage manager procedure.
    /// </summary>
    [ObjectId("ca51425d-80c7-4e7d-9947-c2d2265a8650")]
    internal static class StorageOps
    {
        #region Private Constants
        /// <summary>
        /// The fully qualified name of the script procedure used to manage
        /// persistent storage setting values.
        /// </summary>
        private const string procedureName = "::storageManager";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Converts the specified storage action into its lower-case string
        /// representation for use within the storage manager script.
        /// </summary>
        /// <param name="storageAction">
        /// The storage action to convert.
        /// </param>
        /// <returns>
        /// The lower-case string representation of
        /// <paramref name="storageAction" />.
        /// </returns>
        private static string GetAction( /* CORE */
            StorageAction storageAction /* in */
            )
        {
            return storageAction.ToString().ToLowerInvariant();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the storage manager script command, appending the standard
        /// arguments along with the machine identifier (when available) used
        /// to scope the storage operation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve the machine identifier.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive operations.
        /// </param>
        /// <param name="args">
        /// The additional arguments to include in the generated script.
        /// </param>
        /// <returns>
        /// The generated storage manager script command.
        /// </returns>
        private static string GetScript( /* CORE */
            Interpreter interpreter, /* in */
            CultureInfo cultureInfo, /* in */
            params object[] args     /* in */
            )
        {
            StringList list = new StringList();

            list.Add(procedureName);
            list.AddObjectOrObjects(args);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            list.Add("machineId");

            Guid? machineId = CertificatePolicyOps.GetMachineId(
                interpreter, null, cultureInfo);

            if (machineId != null)
            {
                list.Add(machineId.ToString());
            }
            else
#endif
            {
                list.Add("unknown");
            }

            return list.ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Storage Setting Value Abstraction Methods
        /// <summary>
        /// Gets the sentinel object used to represent the default storage
        /// setting value.
        /// </summary>
        /// <returns>
        /// The default storage setting value.
        /// </returns>
        public static object GetDefaultValue() /* CORE */
        {
            return Constants.DefaultValue;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified value is the sentinel object
        /// that represents the default storage setting value.
        /// </summary>
        /// <param name="value">
        /// The value to test.
        /// </param>
        /// <returns>
        /// Non-zero if <paramref name="value" /> is the default storage
        /// setting value; otherwise, zero.
        /// </returns>
        public static bool IsDefaultValue( /* CORE */
            object value /* in */
            )
        {
            return Object.ReferenceEquals(value, GetDefaultValue());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the value of a named persistent storage setting by
        /// evaluating the storage manager script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the storage manager script.
        /// </param>
        /// <param name="name">
        /// The name of the storage setting to read.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when converting the resulting value.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to read the per-machine value, zero to read the per-user
        /// value, or null to use the default scope.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the storage setting is required to have a value.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the bytes of the storage setting value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode ReadValue( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            bool mustHaveValue,      /* in */
            ref byte[] value,        /* out */
            ref Result error         /* out */
            )
        {
            string text = GetScript(
                interpreter, cultureInfo, "method",
                GetAction(StorageAction.Read),
                "name", name, "perMachine", perMachine,
                "mustHaveValue", mustHaveValue);

            Result result = null;

            if (interpreter.EvaluateScript(
                    text, ref result) == ReturnCode.Ok)
            {
                return Utility.GetBytesFromString(
                    result, cultureInfo, ref value, ref error);
            }
            else
            {
                error = result;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes the value of a named persistent storage setting by
        /// evaluating the storage manager script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the storage manager script.
        /// </param>
        /// <param name="name">
        /// The name of the storage setting to write.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive operations.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to write the per-machine value, zero to write the
        /// per-user value, or null to use the default scope.
        /// </param>
        /// <param name="mustHaveValue">
        /// Non-zero if the storage setting is required to have a value.
        /// </param>
        /// <param name="value">
        /// The bytes of the storage setting value to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode WriteValue( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            bool mustHaveValue,      /* in */
            byte[] value,            /* in */
            ref Result error         /* out */
            )
        {
            string text = GetScript(
                interpreter, cultureInfo, "method",
                GetAction(StorageAction.Write),
                "name", name, "perMachine", perMachine,
                "mustHaveValue", mustHaveValue,
                "value", Convert.ToBase64String(value));

            Result result = null;

            if (interpreter.EvaluateScript(
                    text, ref result) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }
            else
            {
                error = result;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes a named persistent storage setting by evaluating the
        /// storage manager script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the storage manager script.
        /// </param>
        /// <param name="name">
        /// The name of the storage setting to delete.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive operations.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to delete the per-machine value, zero to delete the
        /// per-user value, or null to use the default scope.
        /// </param>
        /// <param name="errorOnMissingValue">
        /// Non-zero to treat a missing storage setting as an error.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode DeleteValue( /* CORE */
            Interpreter interpreter,  /* in */
            string name,              /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,         /* in */
            bool errorOnMissingValue, /* in */
            ref Result error          /* out */
            )
        {
            string text = GetScript(
                interpreter, cultureInfo, "method",
                GetAction(StorageAction.Delete),
                "name", name, "perMachine", perMachine,
                "errorOnMissingValue", errorOnMissingValue);

            Result result = null;

            if (interpreter.EvaluateScript(
                    text, ref result) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }
            else
            {
                error = result;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enumerates the names of the available persistent storage settings
        /// by evaluating the storage manager script.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the storage manager script.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive operations.
        /// </param>
        /// <param name="perMachine">
        /// Non-zero to list the per-machine values, zero to list the
        /// per-user values, or null to use the default scope.
        /// </param>
        /// <param name="names">
        /// Upon success, receives the names of the available storage
        /// settings.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode ListValues( /* CORE */
            Interpreter interpreter, /* in */
            CultureInfo cultureInfo, /* in */
            bool? perMachine,        /* in */
            ref string[] names,      /* out */
            ref Result error         /* out */
            )
        {
            string text = GetScript(
                interpreter, cultureInfo, "method",
                GetAction(StorageAction.List),
                "perMachine", perMachine);

            Result result = null;

            if (interpreter.EvaluateScript(
                    text, ref result) == ReturnCode.Ok)
            {
                StringList list = StringList.FromString(
                    result, ref error);

                if (list != null)
                {
                    names = list.ToArray();
                    return ReturnCode.Ok;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                error = result;
                return ReturnCode.Error;
            }
        }
        #endregion
    }
}
