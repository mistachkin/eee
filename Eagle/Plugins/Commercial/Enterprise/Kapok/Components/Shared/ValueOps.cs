/*
 * ValueOps.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if KAPOK
using System.Collections.Generic;
#endif

using System.Globalization;

#if KAPOK
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;

using EnvironmentPair = Eagle._Interfaces.Public.IAnyPair<
    string, Kapok.Components.Shared.SettingDataType>;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This static class provides a set of methods for converting a string
    /// to another data type that may be needed by the calling page when
    /// processing the current request and/or rendering its response.
    /// </summary>
#if KAPOK
    [ObjectId("e85a3c20-13a0-4420-84f6-04f3d30d9379")]
#else
    [Guid("e85a3c20-13a0-4420-84f6-04f3d30d9379")]
#endif
    internal static class ValueOps
    {
        #region Type Conversion Support Methods
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="System.Boolean" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  If this value cannot be
        /// converted, the value of <paramref name="default" /> will be
        /// returned instead.
        /// </param>
        /// <param name="default">
        /// This is the value that should be returned if there is an error
        /// converting the string.
        /// </param>
        /// <returns>
        /// Either the <see cref="System.Boolean" /> value represented by
        /// the <paramref name="value" /> -OR- the value of
        /// <paramref name="default" /> if a conversion error is seen.
        /// </returns>
        public static bool TryParseBoolean(
            string value, /* in */
            bool @default /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return @default;

            bool result;

            if (bool.TryParse(value, out result))
                return result;

            return @default;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="Int32" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  If this value cannot be
        /// converted, the value of <paramref name="default" /> will be
        /// returned instead.
        /// </param>
        /// <param name="default">
        /// This is the value that should be returned if there is an error
        /// converting the string.
        /// </param>
        /// <returns>
        /// Either the <see cref="Int32" /> value represented by the
        /// <paramref name="value" /> -OR- the value of
        /// <paramref name="default" /> if a conversion error is seen.
        /// </returns>
        public static int TryParseInteger(
            string value, /* in */
            int @default  /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return @default;

            int result;

            if (int.TryParse(value, out result))
                return result;

            return @default;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="Int64" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  If this value cannot be
        /// converted, the value of <paramref name="default" /> will be
        /// returned instead.
        /// </param>
        /// <param name="default">
        /// This is the value that should be returned if there is an error
        /// converting the string.
        /// </param>
        /// <returns>
        /// Either the <see cref="Int64" /> value represented by the
        /// <paramref name="value" /> -OR- the value of
        /// <paramref name="default" /> if a conversion error is seen.
        /// </returns>
        public static long TryParseWideInteger(
            string value, /* in */
            long @default  /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return @default;

            long result;

            if (long.TryParse(value, out result))
                return result;

            return @default;
        }

        ///////////////////////////////////////////////////////////////////////

#if KAPOK
        /// <summary>
        /// Attempts to convert the specified string to a value of that will
        /// work for the specified flags <see cref="Enum" /> derived type.
        /// </summary>
        /// <param name="oldValue">
        /// The current flags <see cref="Enum" /> value, converted to string.
        /// This parameter may be null.
        /// </param>
        /// <param name="newValue">
        /// The list of new flags <see cref="Enum" /> values delimited by
        /// spaces or commas.  Each value may have an optional prefix, a '+'
        /// or '-' sign.  If the prefix is a '+', the value is added to the
        /// original <see cref="Enum" /> value.  If the prefix is a '-', the
        /// value is removed from the original <see cref="Enum" /> value.
        /// Other prefix values may be supported in the future.  This parameter
        /// may be null.
        /// </param>
        /// <param name="enumType">
        /// The flags <see cref="Enum" /> type to use when converting strings.
        /// </param>
        /// <param name="allowInteger">
        /// Non-zero to allow integers to be used instead of only allowing only
        /// names defined within the flags <see cref="Enum" /> derived type.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to ignore the case when consiering if a string value can
        /// be converted to one of the values defined within the flags
        /// <see cref="Enum" /> derived type.
        /// </param>
        /// <returns>
        /// Either the <see cref="Enum" /> value represented by combining the
        /// <paramref name="oldValue" /> and <paramref name="newValue" /> flags
        /// values -OR- null if an error occurs.
        /// </returns>
        public static object TryParseFlagsEnum(
            string oldValue,   /* in */
            string newValue,   /* in */
            Type enumType,     /* in */
            bool allowInteger, /* in */
            bool noCase        /* in */
            )
        {
            Result error = null;

            return Utility.TryParseFlagsEnum(
                null, enumType, oldValue, newValue, null, allowInteger,
                false, noCase, ref error);
        }
#endif

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="Guid" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  This parameter may not be null.
        /// </param>
        /// <returns>
        /// Either the newly created <see cref="Guid" /> -OR- null if the
        /// string could not be converted.
        /// </returns>
        public static Guid? TryParseGuid(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            try
            {
                return new Guid(value);
            }
            catch
            {
                // do nothing.
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="DateTime" /> with a <see cref="DateTimeKind"/> of
        /// <see cref="DateTimeKind.Utc" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  This parameter may not be null.
        /// </param>
        /// <param name="format">
        /// The exact <see cref="DateTime" /> format string to be used when
        /// converting the string.  This parameter may not be null.
        /// </param>
        /// <returns>
        /// Either the newly created <see cref="DateTime" /> -OR- null if the
        /// string could not be converted.
        /// </returns>
        public static DateTime? TryParseUtcDateTime(
            string value, /* in */
            string format /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            try
            {
                DateTime result;

                if (DateTime.TryParseExact(value, format, null,
                        DateTimeStyles.AdjustToUniversal, out result))
                {
                    return result;
                }
            }
            catch
            {
                // do nothing.
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified string to a value of type
        /// <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  This parameter may not be null.
        /// </param>
        /// <returns>
        /// Either the newly created <see cref="TimeSpan" /> -OR- null if the
        /// string could not be converted.
        /// </returns>
        public static TimeSpan? TryParseTimeSpan(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            try
            {
                TimeSpan result;

                if (TimeSpan.TryParse(value, out result))
                    return result;
            }
            catch
            {
                // do nothing.
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if KAPOK
        /// <summary>
        /// Attempts to convert the specified string to a collection of name /
        /// value pairs for use with the process environment.
        /// </summary>
        /// <param name="value">
        /// The string value to be converted.  This parameter may not be null.
        /// </param>
        /// <param name="dataType">
        /// The <see cref="SettingDataType" /> flags values that govern some
        /// data type handling semantics that should be used when attempting
        /// the conversion.
        /// </param>
        /// <returns>
        /// The logical list of name / value pairs -OR- null if the string
        /// could not be converted.
        /// </returns>
        public static IEnumerable<EnvironmentPair> TryParseEnvironment(
            string value,            /* in */
            SettingDataType dataType /* in */
            )
        {
            StringList list = null;
            Result error = null;

            if (Parser.SplitList(
                    null, value, 0, Length.Invalid, true,
                    ref list, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            int count = list.Count;

            if ((count % 2) != 0)
                return null;

            IList<EnvironmentPair> environment =
                new List<EnvironmentPair>();

            for (int index = 0; index < count; index += 2)
            {
                object enumValue = TryParseFlagsEnum(
                    dataType.ToString(), list[index + 1],
                    typeof(SettingDataType), true, true);

                if (!(enumValue is SettingDataType))
                    return null;

                environment.Add(
                    new AnyPair<string, SettingDataType>(
                    list[index], (SettingDataType)enumValue));
            }

            return environment;
        }
#endif
        #endregion
    }
}
