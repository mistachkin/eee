/*
 * CertificateDataOps.cs --
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
using System.Text;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

using Int64PluginDictionary =
    System.Collections.Generic.Dictionary<long,
        Eagle._Interfaces.Public.IPlugin>;

using VersionRange = Eagle._Components.Public.Pair<System.Version>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper methods for parsing, formatting, and
    /// validating the certificate-related data used by the licensing
    /// subsystem.
    /// </summary>
    [ObjectId("e11e4d57-0f78-4881-9400-ffad3e1217b3")]
    internal static class CertificateDataOps
    {
        /// <summary>
        /// Gets the default text encoding used for certificate data.
        /// </summary>
        /// <returns>
        /// The default <see cref="Encoding" />, or null if it is not
        /// available.
        /// </returns>
        public static Encoding GetDefaultEncoding() /* CORE */
        {
            return Constants.DefaultEncoding;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the <see cref="CultureInfo" /> associated with the specified
        /// client data, if any.
        /// </summary>
        /// <param name="clientData">
        /// The client data that may carry an associated culture; may be
        /// null.
        /// </param>
        /// <returns>
        /// The associated <see cref="CultureInfo" />, or null if none is
        /// available.
        /// </returns>
        public static CultureInfo GetCultureInfo( /* CORE */
            IClientData clientData /* in */
            )
        {
            if (clientData == null)
                return null;

            IHaveCultureInfo haveCultureInfo =
                clientData as IHaveCultureInfo;

            if (haveCultureInfo == null)
                return null;

            return haveCultureInfo.CultureInfo;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: This method is designed to be called from the following
        //          list of "approved" methods:
        //
        //          1. CertificateSharedOps.ShouldTreatAsIsolated
        //
        //          No other methods should call into this method because
        //          they should already have a CultureInfo passed directly
        //          into them -OR- they should not need a CultureInfo.
        //
        /// <summary>
        /// Safely retrieves the <see cref="CultureInfo" /> from the
        /// specified interpreter, ignoring whether it has been disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to obtain the culture from; may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// Upon return, receives the interpreter culture, or null if it is
        /// not available.
        /// </param>
        public static void SafeGetCultureInfo( /* CORE */
            Interpreter interpreter,    /* in */
            out CultureInfo cultureInfo /* out */
            )
        {
            bool disposed; /* NOT USED */

            SafeGetCultureInfo(interpreter, out cultureInfo, out disposed);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: This method is designed to be called from the following
        //          list of "approved" methods, which are all primary entry
        //          points into their respective "subsystems":
        //
        //          1. Configuration.MaybeLoadFor
        //          2. CertificateVerifyOps.LoadAndProcess
        //          3. CertificateRenewalOps.Process
        //          4. Licensing.Plugins.Default.Execute
        //          5. CertificatePolicyOps.ScriptCallback
        //          6. CertificatePolicyOps.FileCallback
        //
        //          No other methods should call into this method because
        //          they should already have a CultureInfo passed directly
        //          into them -OR- they should not need a CultureInfo.
        //
        /// <summary>
        /// Safely retrieves the <see cref="CultureInfo" /> from the
        /// specified interpreter while honoring its disposed state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to obtain the culture from; may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// Upon return, receives the interpreter culture, or null if it is
        /// not available.
        /// </param>
        /// <param name="disposed">
        /// Upon return, indicates whether the interpreter was found to be
        /// disposed.
        /// </param>
        public static void SafeGetCultureInfo( /* CORE */
            Interpreter interpreter,     /* in */
            out CultureInfo cultureInfo, /* out */
            out bool disposed            /* out */
            )
        {
            cultureInfo = null;
            disposed = false;

            if (interpreter == null)
                return;

            bool locked = false;

            try
            {
                interpreter.TryLockNoThrow(ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    if (interpreter.Disposed)
                        disposed = true;
                    else
                        cultureInfo = interpreter.CultureInfo;
                }
            }
            finally
            {
                interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a copy of the specified string with its first character
        /// converted to lower case.
        /// </summary>
        /// <param name="value">
        /// The string to transform; may be null or empty.
        /// </param>
        /// <returns>
        /// The transformed string, or the original value when it is null,
        /// empty, or a single character.
        /// </returns>
        private static string InitialLower( /* CORE */
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            if (value.Length == 1)
                return value;

            return String.Format(
                "{0}{1}", Char.ToLower(value[0]), value.Substring(1));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified <see cref="UriType" /> into its lower
        /// camel-case name string.
        /// </summary>
        /// <param name="type">
        /// The URI type value to convert.
        /// </param>
        /// <returns>
        /// The formatted name string for the type.
        /// </returns>
        public static string ToNameString( /* CORE */
            UriType type /* in */
            )
        {
            return InitialLower(Utility.FixupEnumString(
                (type & UriType.TypeMask).ToString()));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether two strings are equal using the system
        /// comparison rules.
        /// </summary>
        /// <param name="a">
        /// The first string to compare; may be null.
        /// </param>
        /// <param name="b">
        /// The second string to compare; may be null.
        /// </param>
        /// <returns>
        /// True if the strings are equal; otherwise, false.
        /// </returns>
        public static bool StringEquals( /* CORE */
            string a, /* in */
            string b  /* in */
            )
        {
            return Utility.SystemStringEquals(a, b);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether two strings are equal using the system
        /// comparison rules, ignoring case.
        /// </summary>
        /// <param name="a">
        /// The first string to compare; may be null.
        /// </param>
        /// <param name="b">
        /// The second string to compare; may be null.
        /// </param>
        /// <returns>
        /// True if the strings are equal ignoring case; otherwise, false.
        /// </returns>
        public static bool StringEqualsNoCase( /* CORE */
            string a, /* in */
            string b  /* in */
            )
        {
            return Utility.SystemStringEquals(a, b, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether string <paramref name="a" /> starts with
        /// string <paramref name="b" /> using the system comparison rules.
        /// </summary>
        /// <param name="a">
        /// The string to test; may be null.
        /// </param>
        /// <param name="b">
        /// The prefix string to look for.
        /// </param>
        /// <returns>
        /// True if <paramref name="a" /> starts with
        /// <paramref name="b" />; otherwise, false.
        /// </returns>
        public static bool StringStartsWith( /* CORE */
            string a, /* in */
            string b  /* in */
            )
        {
            if (a == null)
                return false;

            return a.StartsWith(b, Utility.GetSystemComparisonType(false));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether string <paramref name="a" /> ends with string
        /// <paramref name="b" /> using the system comparison rules.
        /// </summary>
        /// <param name="a">
        /// The string to test; may be null.
        /// </param>
        /// <param name="b">
        /// The suffix string to look for.
        /// </param>
        /// <returns>
        /// True if <paramref name="a" /> ends with <paramref name="b" />;
        /// otherwise, false.
        /// </returns>
        public static bool StringEndsWith( /* CORE */
            string a, /* in */
            string b  /* in */
            )
        {
            if (a == null)
                return false;

            return a.EndsWith(b, Utility.GetSystemComparisonType(false));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether two strings refer to the same path by
        /// comparing their path components.
        /// </summary>
        /// <param name="a">
        /// The first path to compare; may be null.
        /// </param>
        /// <param name="b">
        /// The second path to compare; may be null.
        /// </param>
        /// <returns>
        /// True if the paths are equal; otherwise, false.
        /// </returns>
        public static bool PathStringEquals( /* CORE */
            string a, /* in */
            string b  /* in */
            )
        {
            return Utility.ComparePathParts(a, b) == 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the first line of the specified string, excluding any
        /// line terminator.
        /// </summary>
        /// <param name="value">
        /// The string to extract the first line from; may be null or empty.
        /// </param>
        /// <returns>
        /// The first line of the string, or the original value when it is
        /// null, empty, or contains no line terminator.
        /// </returns>
        public static string FirstLine( /* CORE */
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return value;

            int index = value.IndexOfAny(Characters.LineTerminatorChars);

            if (index == Index.Invalid)
                return value;

            return value.Substring(0, index);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified time span represents a non-zero
        /// duration.
        /// </summary>
        /// <param name="timeSpan">
        /// The time span to check.
        /// </param>
        /// <returns>
        /// True if the time span is not zero; otherwise, false.
        /// </returns>
        public static bool IsNonZeroDuration( /* CORE */
            TimeSpan timeSpan /* in */
            )
        {
            return (timeSpan != TimeSpan.Zero);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified time span represents a limited
        /// (non-negative) duration.
        /// </summary>
        /// <param name="timeSpan">
        /// The time span to check.
        /// </param>
        /// <returns>
        /// True if the duration is limited; otherwise, false.
        /// </returns>
        public static bool IsLimitedDuration( /* CORE */
            TimeSpan timeSpan /* in */
            )
        {
            return (timeSpan.Ticks >= 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified time span represents an
        /// unlimited (negative) duration.
        /// </summary>
        /// <param name="timeSpan">
        /// The time span to check.
        /// </param>
        /// <returns>
        /// True if the duration is unlimited; otherwise, false.
        /// </returns>
        public static bool IsUnlimitedDuration( /* CORE */
            TimeSpan timeSpan /* in */
            )
        {
            return (timeSpan.Ticks < 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified nullable time span represents a
        /// limited (non-negative) duration.
        /// </summary>
        /// <param name="timeSpan">
        /// The time span to check; may be null.
        /// </param>
        /// <param name="default">
        /// The value to return when <paramref name="timeSpan" /> is null.
        /// </param>
        /// <returns>
        /// True if the duration is limited; otherwise, false.
        /// </returns>
        public static bool IsLimitedDuration( /* CORE */
            TimeSpan? timeSpan, /* in */
            bool @default       /* in */
            )
        {
            if (timeSpan == null)
                return @default;

            return IsLimitedDuration((TimeSpan)timeSpan);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a readable stream over the specified script text using
        /// the given encoding.
        /// </summary>
        /// <param name="text">
        /// The script text to wrap in a stream.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the text to bytes; when null, the
        /// default encoding is used.
        /// </param>
        /// <returns>
        /// A new stream over the script bytes, or null on failure.
        /// </returns>
        public static Stream GetScriptStream( /* CORE */
            string text,      /* in */
            Encoding encoding /* in: OPTIONAL */
            )
        {
            Result error = null;

            return GetScriptStream(text, encoding, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a readable stream over the specified script text using
        /// the given encoding, reporting any failure.
        /// </summary>
        /// <param name="text">
        /// The script text to wrap in a stream.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the text to bytes; when null, the
        /// default encoding is used.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// A new stream over the script bytes, or null on failure.
        /// </returns>
        public static Stream GetScriptStream( /* CORE */
            string text,       /* in */
            Encoding encoding, /* in: OPTIONAL */
            ref Result error   /* out */
            )
        {
            if (encoding == null)
                encoding = GetDefaultEncoding();

            if (encoding == null)
            {
                error = "script encoding unavailable";
                return null;
            }

            try
            {
                return new MemoryStream(
                    encoding.GetBytes(text)); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the raw (byte-preserving) encoding used for certificate
        /// data.
        /// </summary>
        /// <returns>
        /// The raw <see cref="Encoding" />.
        /// </returns>
        public static Encoding GetRawEncoding() /* CORE */
        {
            return Constants.RawEncoding;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified string to bytes using the raw encoding.
        /// </summary>
        /// <param name="value">
        /// The string to convert.
        /// </param>
        /// <returns>
        /// The raw bytes for the string.
        /// </returns>
        public static byte[] GetRawBytes( /* CORE */
            string value /* in */
            )
        {
            return GetRawEncoding().GetBytes(value);
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Converts the specified bytes to a string using the raw encoding.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to convert.
        /// </param>
        /// <returns>
        /// The decoded string.
        /// </returns>
        public static string GetRawString( /* CORE */
            byte[] bytes /* in */
            )
        {
            return GetRawEncoding().GetString(bytes);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified bytes begin with the encrypted
        /// data header.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to inspect; may be null or empty.
        /// </param>
        /// <returns>
        /// True if the encrypted data header is present; otherwise, false.
        /// </returns>
        public static bool HasEncryptedDataHeader( /* CORE */
            byte[] bytes /* in */
            )
        {
            if ((bytes == null) || (bytes.Length == 0))
                return false;

            byte[] headerBytes = Constants.EncryptedDataHeaderBytes;

            if (headerBytes == null)
                return false;

            //
            // TODO: Anywhere in document instead of just the start?
            //
            return Utility.ArrayEquals(
                bytes, headerBytes, headerBytes.Length);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified text begins with the encrypted
        /// data header.
        /// </summary>
        /// <param name="text">
        /// The text to inspect; may be null or empty.
        /// </param>
        /// <returns>
        /// True if the encrypted data header is present; otherwise, false.
        /// </returns>
        public static bool HasEncryptedDataHeader( /* CORE */
            string text /* in */
            )
        {
            if (String.IsNullOrEmpty(text))
                return false;

            string header = Constants.EncryptedDataHeader;

            if (header == null)
                return false;

            //
            // TODO: Anywhere in document instead of just the start?
            //
            return StringStartsWith(text, header);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current timestamp using the configured UTC preference.
        /// </summary>
        /// <returns>
        /// The current timestamp.
        /// </returns>
        public static DateTime GetTimeStamp() /* CORE */
        {
            return GetTimeStamp(Constants.IsTimeStampUtc);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current timestamp, optionally in coordinated universal
        /// time.
        /// </summary>
        /// <param name="utc">
        /// True to return the timestamp in UTC; otherwise, local time.
        /// </param>
        /// <returns>
        /// The current timestamp.
        /// </returns>
        private static DateTime GetTimeStamp( /* CORE */
            bool utc /* in */
            )
        {
            //
            // WARNING: Since the .NET Framework 2.0 does not support
            //          arbitrary transforms between times in different
            //          time zones, all timestamps must be in UTC;
            //          otherwise, verification will fail in all other
            //          time zones except the one where the certificate
            //          was signed.
            //
            return utc ? Utility.GetUtcNow() : Utility.GetNow();
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Gets the default format string used when formatting GUID values.
        /// </summary>
        /// <returns>
        /// The default GUID format string.
        /// </returns>
        public static string GetGuidFormat() /* UTILITY */
        {
            return Constants.DefaultGuidFormat;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default format string used when formatting timestamps.
        /// </summary>
        /// <returns>
        /// The default timestamp format string.
        /// </returns>
        public static string GetTimeStampFormat() /* UTILITY */
        {
            return Constants.DefaultTimeStampFormat;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new unique identifier, optionally forcing a real value
        /// even when the empty-identifier diagnostic override is in effect.
        /// </summary>
        /// <param name="withForce">
        /// True to always generate a new identifier; otherwise, the empty
        /// identifier may be returned when the diagnostic override is set.
        /// </param>
        /// <returns>
        /// The newly generated identifier.
        /// </returns>
        public static Guid GetNewId( /* CORE */
            bool withForce /* in */
            )
        {
#if DEBUG || EXTRA_DIAGNOSTICS
            if (!withForce && Configuration.DoesVariableExist(
                    Constants.UseEmptyIdEnvVarName))
            {
                return Guid.Empty;
            }
#endif

            return Guid.NewGuid();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a boolean value from an interpreter variable.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the variable from.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to interpret the variable value.
        /// </param>
        /// <param name="flags">
        /// The flags controlling how the variable is resolved.
        /// </param>
        /// <param name="name">
        /// The name of the variable to read.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the parsed boolean value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetBoolean( /* CORE */
            Interpreter interpreter, /* in */
            CultureInfo cultureInfo, /* in */
            VariableFlags flags,     /* in */
            string name,             /* in */
            ref bool value,          /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            Result localValue = null;

            if (interpreter.GetVariableValue(
                    flags, name, ref localValue,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool boolValue = false;

            if (Value.GetBoolean2(
                    localValue, ValueFlags.AnyBoolean,
                    cultureInfo, ref boolValue,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            value = boolValue;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Normalizes the specified version by trimming any trailing minimum
        /// build and revision components.
        /// </summary>
        /// <param name="version">
        /// The version to normalize; may be null.
        /// </param>
        /// <returns>
        /// The normalized version, or null when <paramref name="version" />
        /// is null.
        /// </returns>
        public static Version NormalizeVersion( /* CORE */
            Version version /* in */
            )
        {
            if (version == null)
                return null;

            int major = version.Major;
            int minor = version.Minor;
            int build = version.Build;

            if (build >= _Version.Minimum)
            {
                int revision = version.Revision;

                if (revision > _Version.Minimum)
                    return new Version(major, minor, build, revision);
                else if (build > _Version.Minimum)
                    return new Version(major, minor, build);
            }

            return new Version(major, minor);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified text into a version range.
        /// </summary>
        /// <param name="text">
        /// The text containing the version range to parse.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to interpret the text.
        /// </param>
        /// <param name="versionRange">
        /// Upon success, receives the parsed version range.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetVersionRange( /* CORE */
            string text,                   /* in */
            CultureInfo cultureInfo,       /* in */
            ref VersionRange versionRange, /* out */
            ref Result error               /* out */
            )
        {
            Version version1 = null;
            Version version2 = null;

            if (Value.GetVersionRange(
                    text, ValueFlags.AnyVersionRange,
                    cultureInfo, ref version1, ref version2,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            versionRange = new VersionRange(version1, version2);
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified version range as a string.
        /// </summary>
        /// <param name="versionRange">
        /// The version range to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted version range, or null when
        /// <paramref name="versionRange" /> is null.
        /// </returns>
        public static string FormatVersionRange( /* CORE */
            VersionRange versionRange /* in */
            )
        {
            if (versionRange == null)
                return null;

            char separator = Characters.MinusSign;
            Version version1 = versionRange.X;
            Version version2 = versionRange.Y;

            if (version1 != null)
            {
                if (version2 != null)
                {
                    //
                    // NOTE: From <version1> to <version2>.
                    //
                    return String.Format("{0}{1}{2}",
                        version1, separator, version2);
                }
                else
                {
                    //
                    // NOTE: From <version1> to <any>.
                    //
                    return String.Format("{0}{1}",
                        version1, separator);
                }
            }
            else
            {
                if (version2 != null)
                {
                    //
                    // NOTE: From <any> to <version2>.
                    //
                    return String.Format("{0}{1}",
                        separator, version2);
                }
                else
                {
                    //
                    // NOTE: Allows any valid version.
                    //
                    return separator.ToString();
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified version falls within the given
        /// version range.
        /// </summary>
        /// <param name="version">
        /// The version to test; may be null.
        /// </param>
        /// <param name="versionRange">
        /// The inclusive version range to test against; may be null.
        /// </param>
        /// <returns>
        /// True if the version is within the range; otherwise, false.
        /// </returns>
        public static bool IsVersionInRange( /* CORE */
            Version version,          /* in */
            VersionRange versionRange /* in */
            )
        {
            if ((version == null) || (versionRange == null))
                return false;

            Version version1 = versionRange.X;
            Version version2 = versionRange.Y;

            if ((version1 == null) && (version2 == null))
                return true;

            if ((version1 != null) &&
                (Utility.VersionCompare(version, version1) < 0))
            {
                return false;
            }

            if ((version2 != null) &&
                (Utility.VersionCompare(version, version2) > 0))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Retrieves an optional boolean value from an interpreter variable,
        /// leaving it null when the variable is empty.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the variable from.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to interpret the variable value.
        /// </param>
        /// <param name="flags">
        /// The flags controlling how the variable is resolved.
        /// </param>
        /// <param name="name">
        /// The name of the variable to read.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the parsed nullable boolean value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetNullableBoolean( /* CORE */
            Interpreter interpreter, /* in */
            CultureInfo cultureInfo, /* in */
            VariableFlags flags,     /* in */
            string name,             /* in */
            ref bool? value,         /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            Result localValue = null;

            if (interpreter.GetVariableValue(
                    flags, name, ref localValue,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool? boolValue = null;

            if (!String.IsNullOrEmpty(localValue))
            {
                if (Value.GetNullableBoolean2(
                        localValue, ValueFlags.AnyBoolean,
                        cultureInfo, ref boolValue,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            value = boolValue;
            return ReturnCode.Ok;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether two public key tokens are equal.
        /// </summary>
        /// <param name="publicKeyToken1">
        /// The first public key token to compare; may be null.
        /// </param>
        /// <param name="publicKeyToken2">
        /// The second public key token to compare; may be null.
        /// </param>
        /// <returns>
        /// True if the tokens are equal; otherwise, false.
        /// </returns>
        public static bool MatchPublicKeyToken( /* CORE */
            byte[] publicKeyToken1, /* in: OPTIONAL */
            byte[] publicKeyToken2  /* in: OPTIONAL */
            )
        {
            return Utility.ArrayEquals(publicKeyToken1, publicKeyToken2);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string as an unsigned number, honoring an
        /// optional hexadecimal prefix.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed number, or null when the value could not be parsed.
        /// </returns>
        public static ulong? ParseNumber( /* CORE */
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            string prefix = Constants.DefaultHexPrefix;

            if ((prefix != null) && value.StartsWith(
                    prefix, Utility.GetSystemComparisonType(false)))
            {
                value = value.Substring(prefix.Length);
            }

            if (value.Length > 0)
            {
                ulong ulongValue;

                if (ulong.TryParse(
                        value, Constants.DefaultNumberStyles,
                        null, out ulongValue))
                {
                    return ulongValue;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Parses the specified string as a signed quantity, honoring an
        /// optional hexadecimal prefix.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed quantity, or null when the value could not be parsed.
        /// </returns>
        public static long? ParseQuantity(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            string prefix = Constants.DefaultHexPrefix;

            if ((prefix != null) && value.StartsWith(
                    prefix, Utility.GetSystemComparisonType(false)))
            {
                value = value.Substring(prefix.Length);
            }

            if (value.Length > 0)
            {
                long longValue;

                if (long.TryParse(
                        value, Constants.DefaultNumberStyles,
                        null, out longValue))
                {
                    return longValue;
                }
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string as an annotation timestamp using the
        /// fixed annotation date and time format.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed timestamp, or null when the value could not be parsed.
        /// </returns>
        public static DateTime? ParseAnnotationTimeStamp( /* CORE */
            string value /* in */
            )
        {
            if (!String.IsNullOrEmpty(value))
            {
                DateTime dateTimeValue;

                if (DateTime.TryParseExact(
                        value, Constants.AnnotationDateTimeFormat,
                        null, Constants.DefaultDateTimeStyles,
                        out dateTimeValue))
                {
                    return dateTimeValue;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string as a timestamp.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed timestamp, or null when the value could not be parsed.
        /// </returns>
        private static DateTime? ParseTimeStamp( /* CORE */
            string value /* in */
            )
        {
            if (!String.IsNullOrEmpty(value))
            {
                DateTime dateTimeValue;

                if (DateTime.TryParse(value,
                        null, Constants.DefaultDateTimeStyles,
                        out dateTimeValue))
                {
                    return dateTimeValue;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string as a timestamp and converts the
        /// result to coordinated universal time.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed UTC timestamp, or null when the value could not be
        /// parsed.
        /// </returns>
        private static DateTime? ParseUniversalTimeStamp( /* CORE */
            string value /* in */
            )
        {
            //
            // WARNING: We must have UTC and the DateTime.Parse method will
            //          return local time if the string contains local time
            //          zone information.  Therefore, we may need to force
            //          the returned DateTime to UTC.
            //
            DateTime? timeStamp = ParseTimeStamp(value);

            if (timeStamp != null)
                return ((DateTime)timeStamp).ToUniversalTime();

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string as a duration.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed duration, or null when the value could not be parsed.
        /// </returns>
        public static TimeSpan? ParseDuration( /* CORE */
            string value /* in */
            )
        {
            if (!String.IsNullOrEmpty(value))
            {
                TimeSpan timeSpanValue;

                if (TimeSpan.TryParse(value, out timeSpanValue))
                    return timeSpanValue;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string into a public key token, honoring an
        /// optional hexadecimal prefix.
        /// </summary>
        /// <param name="value">
        /// The string to parse; may be null or empty.
        /// </param>
        /// <returns>
        /// The parsed public key token bytes, or null when the value could
        /// not be parsed.
        /// </returns>
        public static byte[] ParsePublicKeyToken( /* CORE */
            string value /* in */
            )
        {
            byte[] key = null;

            if (!String.IsNullOrEmpty(value))
            {
                string prefix = Constants.DefaultHexPrefix;

                if ((prefix != null) && value.StartsWith(
                        prefix, Utility.GetSystemComparisonType(false)))
                {
                    value = value.Substring(prefix.Length);
                }

                if (value.Length > 0)
                {
                    ulong ulongValue;

                    if (ulong.TryParse(
                            value, Constants.DefaultNumberStyles,
                            null, out ulongValue))
                    {
                        key = BitConverter.GetBytes(ulongValue);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(key);
                    }
                }
            }

            return key;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string into a public key token.
        /// </summary>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="publicKeyToken">
        /// Upon success, receives the parsed public key token bytes.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ParsePublicKeyToken( /* CORE */
            string value,             /* in */
            ref byte[] publicKeyToken /* out */
            )
        {
            Result error = null;

            return ParsePublicKeyToken(value, ref publicKeyToken, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified string into a public key token, reporting
        /// any failure.
        /// </summary>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="publicKeyToken">
        /// Upon success, receives the parsed public key token bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ParsePublicKeyToken( /* CORE */
            string value,              /* in */
            ref byte[] publicKeyToken, /* out */
            ref Result error           /* out */
            )
        {
            try
            {
                byte[] localPublicKeyToken = ParsePublicKeyToken(
                    value); /* throw */

                if (localPublicKeyToken != null)
                {
                    publicKeyToken = localPublicKeyToken;

                    return ReturnCode.Ok;
                }
                else
                {
                    error = String.Format(
                        "failed to parse public key token {0}",
                        Utility.FormatWrapOrNull(value));
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a GUID identifier.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="id">
        /// Upon success, receives the parsed identifier.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseId( /* CORE */
            string text,     /* in */
            ref Guid id,     /* out */
            ref Result error /* out */
            )
        {
            if (Value.GetGuid(
                    text, null, ref id,
                    ref error) == ReturnCode.Ok)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a version.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="version">
        /// Upon success, receives the parsed version.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseVersion( /* CORE */
            string text,         /* in */
            ref Version version, /* out */
            ref Result error     /* out */
            )
        {
            if (Value.GetVersion(
                    text, null, ref version,
                    ref error) == ReturnCode.Ok)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into an absolute URI.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="uri">
        /// Upon success, receives the parsed URI.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseAbsoluteUri( /* CORE */
            string text,     /* in */
            ref Uri uri,     /* out */
            ref Result error /* out */
            )
        {
            Uri localUri;

            if (Uri.TryCreate(
                    text, UriKind.Absolute, out localUri))
            {
                uri = localUri;
                return true;
            }
            else
            {
                error = "could not parse any uri";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a coordinated universal
        /// time timestamp.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="timeStamp">
        /// Upon success, receives the parsed UTC timestamp.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseUniversalTimeStamp( /* CORE */
            string text,            /* in */
            ref DateTime timeStamp, /* out */
            ref Result error        /* out */
            )
        {
            DateTime? localTimeStamp = ParseUniversalTimeStamp(text);

            if (localTimeStamp != null)
            {
                timeStamp = (DateTime)localTimeStamp;
                return true;
            }
            else
            {
                error = "could not parse universal time stamp";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a timestamp with the
        /// given date and time kind.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="kind">
        /// The date and time kind to assign to the parsed timestamp.
        /// </param>
        /// <param name="timeStamp">
        /// Upon success, receives the parsed timestamp.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseTimeStampWithKind( /* CORE */
            string text,             /* in */
            DateTimeKind kind,       /* in */
            ref DateTime? timeStamp, /* out */
            ref Result error         /* out */
            )
        {
            DateTime? localTimeStamp = ParseTimeStamp(text);

            if (localTimeStamp != null)
            {
                timeStamp = DateTime.SpecifyKind(
                    (DateTime)localTimeStamp, kind);

                return true;
            }
            else
            {
                error = "could not parse time stamp with kind";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a duration.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="duration">
        /// Upon success, receives the parsed duration.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseDuration( /* CORE */
            string text,           /* in */
            ref TimeSpan duration, /* out */
            ref Result error       /* out */
            )
        {
            TimeSpan? localDuration = ParseDuration(text);

            if (localDuration != null)
            {
                duration = (TimeSpan)localDuration;
                return true;
            }
            else
            {
                error = "could not parse duration";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a key (public key
        /// token).
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="key">
        /// Upon success, receives the parsed key bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseKey( /* CORE */
            string text,     /* in */
            ref byte[] key,  /* out */
            ref Result error /* out */
            )
        {
            byte[] localKey = ParsePublicKeyToken(text);

            if (localKey != null)
            {
                key = localKey;
                return true;
            }
            else
            {
                error = "could not parse key";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into an unsigned number.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="number">
        /// Upon success, receives the parsed number.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseNumber( /* CORE */
            string text,      /* in */
            ref ulong number, /* out */
            ref Result error  /* out */
            )
        {
            ulong? localNumber = ParseNumber(text);

            if (localNumber != null)
            {
                number = (ulong)localNumber;
                return true;
            }
            else
            {
                error = "could not parse number";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified text into a binary signature,
        /// optionally stripping surrounding blanks and comments.
        /// </summary>
        /// <param name="text">
        /// The text containing the base64-encoded signature.
        /// </param>
        /// <param name="extended">
        /// True to remove blank lines and comments before decoding.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the decoded signature bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the signature was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseSignature( /* CORE */
            string text,          /* in */
            bool extended,        /* in */
            ref byte[] signature, /* out */
            ref Result error      /* out */
            )
        {
            if (extended)
            {
                if (Utility.RemoveBlanksAndComments(true,
                        ref text, ref error) != ReturnCode.Ok)
                {
                    return false;
                }
            }

            try
            {
                signature = Convert.FromBase64String(text); /* throw */
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
        /// Attempts to read and parse a signature from the specified file or
        /// URI.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve and read the file; may be null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file; may be null.
        /// </param>
        /// <param name="fileName">
        /// The name of the file or URI to read the signature from.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, for remote reads; may be
        /// null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// True to permit reading from a remote URI.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the decoded signature bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the signature was read successfully; otherwise, false.
        /// </returns>
        public static bool TryReadSignatureFile( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            Encoding encoding,       /* in: OPTIONAL */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            bool allowRemoteUri,     /* in */
            ref byte[] signature,    /* out */
            ref Result error         /* out */
            )
        {
            string text;
            bool useResource = false;

            text = CertificateSharedOps.GetDataFromFile(
                interpreter, encoding, fileName, timeout,
                allowRemoteUri, false, false, ref useResource,
                ref error) as string;

            if (text == null)
                return false;

            return TryParseSignature(
                text, true, ref signature, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to read and parse a signature from an assembly resource
        /// stream.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the resource.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the resource; may be null.
        /// </param>
        /// <param name="resourceName">
        /// The name of the resource to read the signature from.
        /// </param>
        /// <param name="signature">
        /// Upon success, receives the decoded signature bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the signature was read successfully; otherwise, false.
        /// </returns>
        public static bool TryReadSignatureStream( /* CORE */
            Assembly assembly,    /* in */
            Encoding encoding,    /* in: OPTIONAL */
            string resourceName,  /* in */
            ref byte[] signature, /* out */
            ref Result error      /* out */
            )
        {
            string text;
            Result localError = null;

            text = Utility.GetResourceStreamData(
                assembly, resourceName, encoding,
                false, ref localError) as string;

            if (text == null)
            {
                if (localError != null)
                {
                    error = localError;
                }
                else
                {
                    error = String.Format(
                        "could not get resource stream {0} data",
                        Utility.FormatWrapOrNull(resourceName));
                }

                return false;
            }

            return TryParseSignature(
                text, true, ref signature, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Attempts to parse the specified text into a signed quantity.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="quantity">
        /// Upon success, receives the parsed quantity.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the text was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParseQuantity( /* CORE */
            string text,       /* in */
            ref long quantity, /* out */
            ref Result error   /* out */
            )
        {
            long? localQuantity = ParseQuantity(text);

            if (localQuantity != null)
            {
                quantity = (long)localQuantity;
                return true;
            }
            else
            {
                error = "could not parse quantity";
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && PLUGIN_COMMANDS
        /// <summary>
        /// Extracts the leading parameter lines from the specified block of
        /// text into a dictionary, returning the remaining text.
        /// </summary>
        /// <param name="value">
        /// The block of text to extract parameters from.
        /// </param>
        /// <param name="dictionary">
        /// Receives the extracted parameter name and value pairs; existing
        /// entries are preserved.
        /// </param>
        /// <param name="text">
        /// Upon return, receives the text remaining after the parameter
        /// lines.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ExtractParameters(
            string value,                    /* in */
            ref StringDictionary dictionary, /* out */
            ref string text,                 /* out */
            ref Result error                 /* out */
            )
        {
            if (value == null)
            {
                error = "invalid block";
                return ReturnCode.Error;
            }

            string[] lines = value.Split(new char[] {
                Characters.CarriageReturn, Characters.LineFeed
            }, StringSplitOptions.RemoveEmptyEntries);

            if (lines == null)
            {
                error = "could not split block";
                return ReturnCode.Error;
            }

            Regex regEx = Constants.ParameterRegEx;

            if (regEx == null) /* RARE */
            {
                error = "parameter extraction not available";
                return ReturnCode.Error;
            }

            StringDictionary localDictionary = null;
            int lineCount = lines.Length;
            int lineIndex = 0;

            for (; lineIndex < lineCount; lineIndex++)
            {
                string line = lines[lineIndex];

                if (String.IsNullOrEmpty(line))
                    continue;

                string trimLine = line.Trim();

                if (String.IsNullOrEmpty(trimLine))
                    continue;

                if (trimLine[0] == Characters.NumberSign)
                    continue;

                if (trimLine[0] != Constants.ParameterPrefix)
                    break;

                Match match = regEx.Match(trimLine);

                if ((match == null) || !match.Success)
                {
                    error = String.Format(
                        "bad block line #{0}: missing match",
                        lineIndex + 1);

                    return ReturnCode.Error;
                }

                //
                // NOTE: There should be at least two match
                //       groups.  The first is the overall
                //       input match.  The second would be
                //       for the parameter name.  The third
                //       group would contain the parameter
                //       value.  If there is no third group
                //       the parameter value will be null.
                //
                // HACK: By default, extra match groups are
                //       IGNORED.  This is to help with any
                //       possible future enhancements.
                //
                GroupCollection groups = match.Groups;

                if (groups == null)
                {
                    error = String.Format(
                        "bad block line #{0}: missing groups",
                        lineIndex + 1);

                    return ReturnCode.Error;
                }

                int count = groups.Count;

                if (count < 2)
                {
                    error = String.Format(
                        "bad block line #{0}: missing name",
                        lineIndex + 1);

                    return ReturnCode.Error;
                }

                string parameterName = groups[1].Value;

                if (parameterName == null)
                {
                    error = String.Format(
                        "bad block line #{0}: invalid name",
                        lineIndex + 1);

                    return ReturnCode.Error;
                }

                string parameterValue = null;

                if (count >= 3)
                    parameterValue = groups[2].Value;

                if (localDictionary == null)
                    localDictionary = new StringDictionary();

                localDictionary[parameterName] = parameterValue;
            }

            if (localDictionary != null)
            {
                if (dictionary != null)
                {
                    dictionary.AddKeysAndValues(
                        localDictionary, true);
                }
                else
                {
                    dictionary = localDictionary;
                }
            }

            if (lineIndex < lineCount)
            {
                text = String.Join(
                    Characters.NewLine.ToString(), lines, lineIndex,
                    lineCount - lineIndex);
            }

            return ReturnCode.Ok;
        }
#endif
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the identifier metadata from one identifier to another.
        /// </summary>
        /// <param name="source">
        /// The identifier to copy values from; may be null.
        /// </param>
        /// <param name="target">
        /// The identifier to copy values into; may be null.
        /// </param>
        /// <returns>
        /// True if the values were copied; otherwise, false.
        /// </returns>
        public static bool CopyIdentifier( /* CORE */
            IIdentifier source, /* in */
            IIdentifier target  /* out */
            )
        {
            if ((source == null) || (target == null))
                return false;

            target.Kind = source.Kind;
            target.Id = source.Id;
            target.Name = source.Name;
            target.Group = source.Group;
            target.Description = source.Description;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the key pair metadata from one instance to another.
        /// </summary>
        /// <param name="source">
        /// The metadata to copy values from; may be null.
        /// </param>
        /// <param name="target">
        /// The metadata to copy values into; may be null.
        /// </param>
        /// <returns>
        /// True if the values were copied; otherwise, false.
        /// </returns>
        public static bool CopyKeyPairMetadataBase( /* CORE */
            IKeyPairMetadataBase source, /* in */
            IKeyPairMetadataBase target  /* out */
            )
        {
            if ((source == null) || (target == null))
                return false;

            target.KeyUsage = source.KeyUsage;
            target.KeyExpiration = source.KeyExpiration;
            target.KeyDomains = source.KeyDomains;
            target.KeyGroups = source.KeyGroups;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ensures the specified identifier carries minimal key pair
        /// metadata, filling in the kind, identifier, and name as needed.
        /// </summary>
        /// <param name="identifierBase">
        /// The identifier to update; may be null.
        /// </param>
        /// <param name="name">
        /// The name to assign when the identifier has none; may be null.
        /// </param>
        /// <returns>
        /// True if any value was changed; otherwise, false.
        /// </returns>
        public static bool MaybeSetAsKeyPair( /* CORE */
            IIdentifierBase identifierBase, /* in, out */
            string name                     /* in */
            )
        {
            bool result = false;

            if (identifierBase != null)
            {
                //
                // NOTE: Fallback to at least making sure that there is some
                //       *minimal* identifier [metadata] about the key pair.
                //
                if (identifierBase.Kind == IdentifierKind.None)
                {
                    identifierBase.Kind = IdentifierKind.KeyPair;
                    result = true;
                }

                if (identifierBase.Id.Equals(Guid.Empty))
                {
                    identifierBase.Id = Utility.GetObjectId(identifierBase);
                    result = true;
                }

                if ((name != null) && (identifierBase.Name == null))
                {
                    identifierBase.Name = name;
                    result = true;
                }
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: The IIdentifier property names are hard-coded in this method
        //       and MUST be changed here when necessary.  Maybe they should
        //       be moved into constants?  Maybe the index offsets should be
        //       moved into constants too?
        //
        /// <summary>
        /// Parses the specified string into an identifier.
        /// </summary>
        /// <param name="value">
        /// The string containing the identifier fields to parse.
        /// </param>
        /// <param name="identifier">
        /// Upon success, receives the parsed identifier.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ParseIdentifier( /* CORE */
            string value,               /* in */
            ref IIdentifier identifier, /* out */
            ref Result error            /* out */
            )
        {
            StringDictionary dictionary = StringDictionary.FromString(
                value, false, false, ref error);

            if (dictionary == null)
                return ReturnCode.Error;

            string stringValue; /* REUSED */
            IdentifierKind kind = IdentifierKind.None;

            if (dictionary.TryGetValue(
                    "Kind", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                object enumValue = Utility.TryParseEnum(
                    typeof(IdentifierKind), stringValue,
                    true, true, ref error);

                if (enumValue is IdentifierKind)
                    kind = (IdentifierKind)enumValue;
                else
                    return ReturnCode.Error;
            }

            Guid id = Guid.Empty;

            if (dictionary.TryGetValue(
                    "Id", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                if (!TryParseId(
                        stringValue, ref id, ref error))
                {
                    return ReturnCode.Error;
                }
            }

            string name = null;

            if (dictionary.TryGetValue(
                    "Name", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                name = stringValue;
            }

            string group = null;

            if (dictionary.TryGetValue(
                    "Group", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                group = stringValue;
            }

            string description = null;

            if (dictionary.TryGetValue(
                    "Description", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                description = stringValue;
            }

            identifier = new Identifier(
                kind, id, name, group, description);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // BUGFIX: Hard-coding metadata name offsets into this method
        //         prevents later property name/value pairs from being
        //         recognized properly unless *ALL* previous property
        //         name/value pairs ahve been specified.
        //
        // TODO: Metadata property names are hard-coded in this method
        //       and MUST be changed here when necessary.  Maybe they
        //       should be moved into constants?
        //
        /// <summary>
        /// Parses the specified string into key pair metadata.
        /// </summary>
        /// <param name="value">
        /// The string containing the metadata fields to parse.
        /// </param>
        /// <param name="keyPairMetadataBase">
        /// Upon success, receives the parsed key pair metadata.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ParseKeyPairMetadata( /* CORE */
            string value,                                 /* in */
            ref IKeyPairMetadataBase keyPairMetadataBase, /* out */
            ref Result error                              /* out */
            )
        {
            StringDictionary dictionary = StringDictionary.FromString(
                value, false, false, ref error);

            if (dictionary == null)
                return ReturnCode.Error;

            string stringValue; /* REUSED */
            string keyUsage = null;

            if (dictionary.TryGetValue(
                    "KeyUsage", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                if (Utility.VerifyAttributeFlags(
                        stringValue, true, true, ref error))
                {
                    keyUsage = stringValue;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }

            DateTime? keyExpiration = null;

            if (dictionary.TryGetValue(
                    "KeyExpiration", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                DateTime dateTime = DateTime.MinValue;

                if (TryParseUniversalTimeStamp(
                        stringValue, ref dateTime,
                        ref error))
                {
                    keyExpiration = dateTime;
                }
                else
                {
                    return ReturnCode.Error;
                }
            }

            IList<string> keyDomains = null;

            if (dictionary.TryGetValue(
                    "KeyDomains", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                StringList subList = null;

                if (Parser.SplitList(null,
                        stringValue, 0, Length.Invalid, true,
                        ref subList, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                foreach (string element in subList)
                {
                    if (String.IsNullOrEmpty(element))
                        continue;

                    if (CheckHostName(
                            element, ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    if (keyDomains == null)
                        keyDomains = new List<string>();

                    keyDomains.Add(element);
                }
            }

            IList<byte[]> keyGroups = null;

            if (dictionary.TryGetValue(
                    "KeyGroups", out stringValue) &&
                !String.IsNullOrEmpty(stringValue))
            {
                StringList subList = null;

                if (Parser.SplitList(null,
                        stringValue, 0, Length.Invalid, true,
                        ref subList, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                foreach (string element in subList)
                {
                    if (String.IsNullOrEmpty(element))
                        continue;

                    byte[] publicKeyToken = null;

                    if (ParsePublicKeyToken(
                            element, ref publicKeyToken,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    if (keyGroups == null)
                        keyGroups = new List<byte[]>();

                    keyGroups.Add(publicKeyToken);
                }
            }

            keyPairMetadataBase = new KeyPairMetadataBase(
                keyUsage, keyExpiration, keyDomains, keyGroups);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Adds the specified named public key token to a string list,
        /// formatting the token before insertion.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token bytes to add.
        /// </param>
        /// <param name="list">
        /// The list to add the key pair to; created when null.
        /// </param>
        public static void AddKeyPairToList(
            string name,           /* in */
            byte[] publicKeyToken, /* in */
            ref IStringList list   /* in, out */
            )
        {
            AddKeyPairToList(name, FormatPublicKeyToken(
                publicKeyToken, false, false), ref list);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified public key token bytes to a list of tokens.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair; not stored in the list.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token bytes to add; ignored when null.
        /// </param>
        /// <param name="list">
        /// The list to add the token to; created when null.
        /// </param>
        public static void AddKeyPairToList(
            string name,           /* in */
            byte[] publicKeyToken, /* in */
            ref IList<byte[]> list /* in, out */
            )
        {
            if (publicKeyToken == null)
                return;

            if (list == null)
                list = new List<byte[]>();

            list.Add(publicKeyToken);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified named public key token string to a string list
        /// as a name and value pair.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair.
        /// </param>
        /// <param name="publicKeyToken">
        /// The formatted public key token to add; ignored when null.
        /// </param>
        /// <param name="list">
        /// The list to add the key pair to; created when null.
        /// </param>
        public static void AddKeyPairToList(
            string name,           /* in */
            string publicKeyToken, /* in */
            ref IStringList list   /* in, out */
            )
        {
            if (publicKeyToken == null)
                return;

            if (list == null)
                list = new StringList();

            //
            // NOTE: Sub-element #1, name of key (which is presumably
            //       from the originally imported key ring file).
            //
            list.Add(name);

            //
            // NOTE: Sub-element #2, the full public key token itself.
            //
            list.Add(publicKeyToken);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the specified key file data into a key pair.
        /// </summary>
        /// <param name="fileName">
        /// The file name to associate with the resulting key pair.
        /// </param>
        /// <param name="value">
        /// The key file data to parse.
        /// </param>
        /// <param name="pvk">
        /// True if the data is in the PVK key file format.
        /// </param>
        /// <param name="password">
        /// The optional password protecting the key file; may be null.
        /// </param>
        /// <param name="publicKey">
        /// True to load the public key portion.
        /// </param>
        /// <param name="privateKey">
        /// True to load the private key portion.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The parsed key pair, or null on failure.
        /// </returns>
        public static IKeyPair ParseKeyPairData( /* CORE? */
            string fileName, /* in */
            string value,    /* in */
            bool pvk,        /* in */
            string password, /* in: OPTIONAL */
            bool publicKey,  /* in */
            bool privateKey, /* in */
            ref Result error /* out */
            )
        {
            byte[] bytes = null;

            if (Utility.GetBytesFromString(
                    value, null, ref bytes, ref error) == ReturnCode.Ok)
            {
                KeyPairType? keyPairType;
                KeyFileFormat? format;
                Result scanError = null;

                keyPairType = KeyFile.ScanForKeyPairType(
                    bytes, out format, ref scanError);

                if (keyPairType == null)
                {
                    if (scanError != null)
                        error = scanError;
                    else
                        error = "could not detect key pair type";

                    return null;
                }

                if (format == null)
                {
                    if (scanError != null)
                        error = scanError;
                    else
                        error = "could not detect key file format";

                    return null;
                }

                using (MemoryStream memoryStream = new MemoryStream(
                        bytes))
                {
                    IKeyPair keyPair = null;
                    Result result = null;

                    if (KeyFile.Open(
                            memoryStream, KeyFile.GetReadCallback(
                                keyPair, keyPairType),
                            (KeyFileFormat)format, pvk, password,
                            publicKey, privateKey, ref keyPair,
                            ref result) == ReturnCode.Ok)
                    {
                        if (keyPair != null)
                            keyPair.FileName = fileName;

                        return keyPair;
                    }
                    else
                    {
                        error = result;
                    }
                }
            }

            return null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates the specified host name, allowing an optional leading
        /// wildcard prefix.
        /// </summary>
        /// <param name="value">
        /// The host name to validate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok when the host name is valid; otherwise,
        /// ReturnCode.Error.
        /// </returns>
        public static ReturnCode CheckHostName( /* CORE */
            string value,    /* in */
            ref Result error /* out */
            )
        {
            if (value == null)
            {
                error = "invalid host name";
                return ReturnCode.Error;
            }

            if (value.Length == 0) /* EXEMPT */
            {
                error = "empty host name";
                return ReturnCode.Error;
            }

            //
            // HACK: If the host name value starts with "*.", strip that
            //       prefix and then continue processing.  For now, no
            //       other wildcard constructs are allowed in the host
            //       name.
            //
            string prefix = Constants.HostNameWildcardPrefix;

            if ((prefix != null) && StringStartsWith(value, prefix))
                value = value.Substring(prefix.Length);

            //
            // NOTE: Use the .NET Framework method to check the host name
            //       type.  This is not supposed to throw any exceptions.
            //
            UriHostNameType hostNameType = Uri.CheckHostName(value);

            switch (hostNameType)
            {
                case UriHostNameType.Unknown:
                case UriHostNameType.Basic:
                    {
                        //
                        // NOTE: It is unclear exactly what the "Basic"
                        //       type here represents; however, to err
                        //       on the safe side, it will be treated
                        //       the same as "Unknown" type for now.
                        //
                        error = String.Format(
                            "unsupported host name type {0}",
                            Utility.FormatWrapOrNull(hostNameType));

                        return ReturnCode.Error;
                    }
                case UriHostNameType.Dns:
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    {
                        //
                        // NOTE: The value is either a valid DNS style host
                        //       name or an IP address.  These are always
                        //       allowed.
                        //
                        return ReturnCode.Ok;
                    }
                default:
                    {
                        //
                        // NOTE: No idea what this value is.  Just return
                        //       a suitable error message.
                        //
                        error = String.Format(
                            "unknown host name type {0}",
                            Utility.FormatWrapOrNull(hostNameType));

                        return ReturnCode.Error;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified string list for display, one element per
        /// line.
        /// </summary>
        /// <param name="list">
        /// The list to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted list, or a placeholder when the list is null or
        /// empty.
        /// </returns>
        public static object FormatList( /* CORE */
            StringList list /* in */
            )
        {
            if (list == null)
                return Constants.DisplayNull;

            if (list.Count == 0)
                return Constants.DisplayEmpty;

            string separator1 = Environment.NewLine;
            string separator2 = Characters.HorizontalTab.ToString();

            return String.Format("{0}{1}", separator1,
                list.ToRawString(separator1, separator2));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified GUID identifier as a string.
        /// </summary>
        /// <param name="id">
        /// The identifier to format.
        /// </param>
        /// <returns>
        /// The formatted identifier.
        /// </returns>
        public static string FormatId( /* CORE */
            Guid id /* in */
            )
        {
            return id.ToString(Constants.DefaultGuidFormat);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified value to a string builder, inserting a
        /// separating space when needed.
        /// </summary>
        /// <param name="builder">
        /// The string builder to append to; created when null.
        /// </param>
        /// <param name="value">
        /// The value to append; ignored when null.
        /// </param>
        /// <param name="nonEmpty">
        /// True to append only when the builder already contains text.
        /// </param>
        public static void AppendTo( /* CORE */
            ref StringBuilder builder, /* in, out: OPTIONAL */
            string value,              /* in: OPTIONAL */
            bool nonEmpty              /* in */
            )
        {
            if (builder == null)
                builder = new StringBuilder();

            if (value != null)
            {
                int length = builder.Length;

                if (!nonEmpty || (length > 0))
                {
                    if (length > 0)
                        builder.Append(Characters.Space);

                    builder.Append(value);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified object for diagnostic display, indicating
        /// when it is null, an empty string, or showing its length.
        /// </summary>
        /// <param name="object">
        /// The object to format; may be null.
        /// </param>
        /// <returns>
        /// A diagnostic string describing the object.
        /// </returns>
        public static string MaybeNullOrEmpty( /* CORE */
            object @object /* in */
            )
        {
            if (@object == null)
                return "<nullObject>";

            string stringValue = String.Format("{0}", @object);

            if (stringValue == null)
                return "<nullString>";

            int length = stringValue.Length;

            if (length == 0)
                return "<emptyString>";

            return String.Format("(length:{0}) {1}", length, stringValue);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the name of the specified plugin for display.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted plugin name.
        /// </returns>
        public static string FormatPluginName( /* CORE */
            IPluginData pluginData /* in: OPTIONAL */
            )
        {
            return Utility.FormatPluginName(pluginData, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified package name for display.
        /// </summary>
        /// <param name="packageName">
        /// The package name to format; may be null.
        /// </param>
        /// <returns>
        /// The package name, or a placeholder when it is null.
        /// </returns>
        public static string FormatPackageName( /* CORE */
            string packageName /* in: OPTIONAL */
            )
        {
            return (packageName != null) ? packageName : "<null>";
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified assembly for display, optionally including
        /// only its location.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to format; may be null.
        /// </param>
        /// <param name="locationOnly">
        /// True to include only the assembly location; otherwise, include
        /// the assembly name as well.
        /// </param>
        /// <returns>
        /// The formatted assembly description.
        /// </returns>
        public static string FormatAssembly( /* CORE */
            Assembly assembly, /* in: OPTIONAL */
            bool locationOnly  /* in */
            )
        {
            if (assembly == null)
                return "<unavailable>";

            string location = null;

            try
            {
                location = assembly.Location; /* throw */
            }
            catch
            {
                // do nothing.
            }

            if (locationOnly)
            {
                return String.Format("from location {0}",
                    Utility.FormatWrapOrNull(location));
            }
            else
            {
                return String.Format("{0} from location {1}",
                    FormatAssemblyName(assembly.GetName()),
                    Utility.FormatWrapOrNull(location));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified assembly name for display.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted assembly name.
        /// </returns>
        public static string FormatAssemblyName( /* CORE */
            AssemblyName assemblyName /* in: OPTIONAL */
            )
        {
            if (assemblyName == null)
                return "<unavailable>";

            string name = assemblyName.FullName;

            if (name != null)
            {
                name = Utility.FormatWrapOrNull(name);
            }
            else
            {
                name = assemblyName.Name;

                if (name != null)
                    name = Utility.FormatWrapOrNull(name);
                else
                    name = "<anonymous>";
            }

            return name;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the identifier of the specified application domain for
        /// display.
        /// </summary>
        /// <param name="appDomain">
        /// The application domain to format; may be null.
        /// </param>
        /// <param name="displayNull">
        /// True to return a placeholder when the application domain is null.
        /// </param>
        /// <param name="displayNonNull">
        /// True to wrap the formatted identifier for display.
        /// </param>
        /// <returns>
        /// The formatted application domain identifier, or null when not
        /// displayed.
        /// </returns>
        public static string FormatAppDomainId( /* CORE */
            AppDomain appDomain, /* in */
            bool displayNull,    /* in */
            bool displayNonNull  /* in */
            )
        {
            if (appDomain == null)
            {
                if (!displayNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "AppDomain:{0}", Constants.DisplayNull));
            }

            try
            {
                return String.Format("AppDomain:{0}", appDomain.Id);
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateDataOps).Name,
                    TracePriority.Highest);
#endif

                if (!displayNonNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "AppDomain:{0}", Constants.DisplayError));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the application domain identifier of the specified
        /// interpreter for display.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to format; may be null.
        /// </param>
        /// <param name="displayNull">
        /// True to return a placeholder when the interpreter is null.
        /// </param>
        /// <param name="displayNonNull">
        /// True to wrap the formatted identifier for display.
        /// </param>
        /// <returns>
        /// The formatted application domain identifier, or null when not
        /// displayed.
        /// </returns>
        public static string FormatAppDomainId( /* CORE */
            Interpreter interpreter, /* in */
            bool displayNull,        /* in */
            bool displayNonNull      /* in */
            )
        {
            if (interpreter == null)
            {
                if (!displayNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "AppDomain:{0}", Constants.DisplayNull));
            }

            try
            {
                string appDomainId = interpreter.FormatAppDomainId(
                    displayNonNull); /* throw */

                return displayNonNull ? Utility.FormatWrapOrNull(
                    appDomainId) : appDomainId;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateDataOps).Name,
                    TracePriority.Highest);
#endif

                if (!displayNonNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "AppDomain:{0}", Constants.DisplayError));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified interpreter, using its identifier, for
        /// display.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to format; may be null.
        /// </param>
        /// <param name="displayNull">
        /// True to return a placeholder when the interpreter is null.
        /// </param>
        /// <param name="displayNonNull">
        /// True to wrap the formatted identifier for display.
        /// </param>
        /// <returns>
        /// The formatted interpreter description, or null when not
        /// displayed.
        /// </returns>
        public static string FormatInterpreter( /* CORE */
            Interpreter interpreter, /* in */
            bool displayNull,        /* in */
            bool displayNonNull      /* in */
            )
        {
            if (interpreter == null)
            {
                if (!displayNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "Interpreter:{0}", Constants.DisplayNull));
            }

            long id = interpreter.IdNoThrow;

            if (!displayNonNull)
                return id.ToString();

            return Utility.FormatWrapOrNull(String.Format(
                "Interpreter:{0}", id));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified variable name for display.
        /// </summary>
        /// <param name="varName">
        /// The variable name to format; may be null.
        /// </param>
        /// <param name="displayNull">
        /// True to return a placeholder when the name is null.
        /// </param>
        /// <param name="displayNonNull">
        /// True to wrap the formatted name for display.
        /// </param>
        /// <returns>
        /// The formatted variable name, or null when not displayed.
        /// </returns>
        public static string FormatVarName( /* CORE */
            string varName,     /* in */
            bool displayNull,   /* in */
            bool displayNonNull /* in */
            )
        {
            if (varName == null)
            {
                if (!displayNull)
                    return null;

                return Utility.FormatWrapOrNull(String.Format(
                    "VarName:{0}", Constants.DisplayNull));
            }

            if (!displayNonNull)
                return varName;

            return Utility.FormatWrapOrNull(String.Format(
                "VarName:{0}", (varName.Length > 0) ? varName :
                Constants.DisplayEmpty));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified numeric identifier for display.
        /// </summary>
        /// <param name="id">
        /// The identifier to format.
        /// </param>
        /// <param name="display">
        /// True to wrap the formatted identifier for display.
        /// </param>
        /// <returns>
        /// The formatted identifier.
        /// </returns>
        public static string FormatId( /* CORE */
            long id,     /* in */
            bool display /* in */
            )
        {
            return display ?
                Utility.FormatWrapOrNull(String.Format("Id:{0}", id)) :
                id.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a fully qualified value name from the specified
        /// certificate identifier and value name.
        /// </summary>
        /// <param name="certificate">
        /// The certificate whose identifier is used; may be null.
        /// </param>
        /// <param name="name">
        /// The value name to append; when null, a default name is used.
        /// </param>
        /// <returns>
        /// The formatted value name.
        /// </returns>
        public static string FormatValueName( /* CORE */
            ICertificate certificate, /* in */
            string name               /* in */
            )
        {
            return String.Format(
                "{0}.{1}", FormatId((certificate != null) ?
                certificate.Id : Guid.Empty), (name != null) ?
                name : Constants.DefaultValueName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified integer value as a hexadecimal string,
        /// including the default prefix.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            int value /* in */
            )
        {
            return FormatHexadecimal(value, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified integer value as a hexadecimal string.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <param name="usePrefix">
        /// True to include the default hexadecimal prefix.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            int value,     /* in */
            bool usePrefix /* in */
            )
        {
            string prefix = Constants.DefaultHexPrefix;

            if (!usePrefix || (prefix == null))
                prefix = String.Empty;

            return prefix + value.ToString(Constants.DefaultIntFormat);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Formats the specified long value as a hexadecimal string,
        /// including the default prefix.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            long value /* in */
            )
        {
            return FormatHexadecimal(value, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified long value as a hexadecimal string.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <param name="usePrefix">
        /// True to include the default hexadecimal prefix.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            long value,    /* in */
            bool usePrefix /* in */
            )
        {
            string prefix = Constants.DefaultHexPrefix;

            if (!usePrefix || (prefix == null))
                prefix = String.Empty;

            return prefix + value.ToString(Constants.DefaultLongFormat);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified unsigned long value as a hexadecimal
        /// string, including the default prefix.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            ulong value /* in */
            )
        {
            return FormatHexadecimal(value, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified unsigned long value as a hexadecimal
        /// string.
        /// </summary>
        /// <param name="value">
        /// The value to format.
        /// </param>
        /// <param name="usePrefix">
        /// True to include the default hexadecimal prefix.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            ulong value,   /* in */
            bool usePrefix /* in */
            )
        {
            string prefix = Constants.DefaultHexPrefix;

            if (!usePrefix || (prefix == null))
                prefix = String.Empty;

            return prefix + value.ToString(Constants.DefaultLongFormat);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified public key token as a hexadecimal string,
        /// optionally tagging well-known tokens for display.
        /// </summary>
        /// <param name="value">
        /// The public key token bytes to format.
        /// </param>
        /// <param name="display">
        /// True to include a well-known token tag and a placeholder for an
        /// empty result.
        /// </param>
        /// <param name="usePrefix">
        /// True to include the default hexadecimal prefix.
        /// </param>
        /// <returns>
        /// The formatted public key token.
        /// </returns>
        public static string FormatPublicKeyToken( /* CORE */
            byte[] value,  /* in */
            bool display,  /* in */
            bool usePrefix /* in */
            )
        {
            StringBuilder builder = new StringBuilder();

            if (display)
            {
                string tag = null;

                if (CertificateSharedOps.IsWellKnownPublicKeyToken(
                        value, ref tag) && (tag != null))
                {
                    builder.Append(tag);

                    if (builder.Length > 0)
                        builder.Append(Characters.Colon);
                }
            }

            builder.Append(FormatHexadecimal(value, usePrefix));

            if (display && (builder.Length == 0))
                builder.Append("<none>");

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified bytes as a hexadecimal string, including
        /// the default prefix.
        /// </summary>
        /// <param name="value">
        /// The bytes to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string, or null when the value is
        /// null.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            byte[] value /* in */
            )
        {
            return FormatHexadecimal(value, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified bytes as a hexadecimal string.
        /// </summary>
        /// <param name="value">
        /// The bytes to format; may be null or empty.
        /// </param>
        /// <param name="usePrefix">
        /// True to include the default hexadecimal prefix.
        /// </param>
        /// <returns>
        /// The formatted hexadecimal string, null when the value is null, or
        /// an empty string when the value is empty.
        /// </returns>
        public static string FormatHexadecimal( /* CORE */
            byte[] value,  /* in */
            bool usePrefix /* in */
            )
        {
            if (value == null)
                return null;

            if (value.Length == 0)
                return String.Empty;

            string prefix = Constants.DefaultHexPrefix;

            if (!usePrefix || (prefix == null))
                prefix = String.Empty;

            return prefix + Utility.ToHexadecimalString(value);
        }

        ///////////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
        /// <summary>
        /// Formats the specified current timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatNow( /* CORE */
            DateTime value /* in */
            )
        {
            return Utility.FormatWrapOrNull(FormatTimeStamp(value));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified obsolescence timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatObsolete( /* CORE */
            DateTime value /* in */
            )
        {
            return Utility.FormatWrapOrNull(FormatTimeStamp(value));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified installation timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted timestamp, or a placeholder when it is null.
        /// </returns>
        public static string FormatInstalled( /* CORE */
            DateTime? value /* in */
            )
        {
            return (value != null) ?
                Utility.FormatWrapOrNull(FormatTimeStamp((DateTime)value)) :
                Constants.DisplayNever;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified creation timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted timestamp, or a placeholder when it is null.
        /// </returns>
        public static string FormatCreated( /* CORE */
            DateTime? value /* in */
            )
        {
            return (value != null) ?
                Utility.FormatWrapOrNull(FormatTimeStamp((DateTime)value)) :
                Constants.DisplayNever;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified expiration timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatExpired( /* CORE */
            DateTime value /* in */
            )
        {
            return Utility.FormatWrapOrNull(FormatTimeStamp(value));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified timestamp for display.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatTimeStamp( /* CORE */
            DateTime value /* in */
            )
        {
            return FormatTimeStamp(value, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified timestamp for display, optionally showing a
        /// placeholder for the minimum value.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <param name="never">
        /// True to display a placeholder when the value is the minimum
        /// timestamp.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatTimeStamp( /* CORE */
            DateTime value, /* in */
            bool never      /* in */
            )
        {
            return FormatTimeStamp(value, never, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified timestamp for display, optionally showing a
        /// placeholder for the minimum value.
        /// </summary>
        /// <param name="value">
        /// The timestamp to format.
        /// </param>
        /// <param name="never">
        /// True to display a placeholder when the value is the minimum
        /// timestamp.
        /// </param>
        /// <param name="always">
        /// True to use the "always" placeholder instead of the "never"
        /// placeholder for the minimum value.
        /// </param>
        /// <returns>
        /// The formatted timestamp.
        /// </returns>
        public static string FormatTimeStamp( /* CORE */
            DateTime value, /* in */
            bool never,     /* in */
            bool always     /* in */
            )
        {
            return (never && (value == DateTime.MinValue)) ?
                (always ? Constants.DisplayAlways : Constants.DisplayNever) :
                value.ToString(Constants.DefaultTimeStampFormat);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the specified signature as a single base64-encoded line.
        /// </summary>
        /// <param name="signature">
        /// The signature bytes to format; may be null.
        /// </param>
        /// <returns>
        /// The base64-encoded signature, or null when the signature is
        /// null.
        /// </returns>
        public static string FormatSignatureLine(
            byte[] signature /* in */
            )
        {
            if (signature == null)
                return null;

            return Convert.ToBase64String(
                signature, Base64FormattingOptions.None);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Formats the specified signature as an indented, line-wrapped
        /// base64-encoded block.
        /// </summary>
        /// <param name="signature">
        /// The signature bytes to format; may be null.
        /// </param>
        /// <returns>
        /// The formatted signature block, or null when the signature is
        /// null.
        /// </returns>
        public static string FormatSignatureBlock(
            byte[] signature /* in */
            )
        {
            if (signature == null)
                return null;

            return Convert.ToBase64String(signature,
                Base64FormattingOptions.InsertLineBreaks).Insert(0,
                Characters.DosNewLine).Replace(Characters.DosNewLine,
                String.Format("{0}{1}", Characters.DosNewLine,
                Characters.Indent));
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if (XML || (NETWORK && CERTIFICATE_RENEWAL)) && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Formats the certificate signature file name for the specified
        /// file name.
        /// </summary>
        /// <param name="fileName">
        /// The base file name; may be null.
        /// </param>
        /// <returns>
        /// The formatted file name, or null when <paramref name="fileName" />
        /// is null.
        /// </returns>
        public static string FormatFileName(
            string fileName /* in */
            )
        {
            if (fileName == null)
                return null;

            return PrivateFormatFileName(fileName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the certificate signature file name for the specified
        /// file name, supporting both local files and remote URIs.
        /// </summary>
        /// <param name="fileName">
        /// The base file name or URI; may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing a remote URI.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when combining a remote URI.
        /// </param>
        /// <param name="remoteUri">
        /// True if the file name should be treated as a remote URI.
        /// </param>
        /// <returns>
        /// The formatted file name, or null on failure.
        /// </returns>
        public static string FormatFileName(
            string fileName,         /* in */
            CultureInfo cultureInfo, /* in */
            Encoding encoding,       /* in */
            bool remoteUri           /* in */
            )
        {
            string fileNameOnly;

            return FormatFileName(
                fileName, cultureInfo, encoding,
                remoteUri, out fileNameOnly);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the certificate signature file name for the specified
        /// file name, supporting both local files and remote URIs, and
        /// returning the file name without any directory or URI prefix.
        /// </summary>
        /// <param name="fileName">
        /// The base file name or URI; may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing a remote URI.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when combining a remote URI.
        /// </param>
        /// <param name="remoteUri">
        /// True if the file name should be treated as a remote URI.
        /// </param>
        /// <param name="fileNameOnly">
        /// Upon return, receives the file name portion only, or null.
        /// </param>
        /// <returns>
        /// The formatted file name, or null on failure.
        /// </returns>
        public static string FormatFileName(
            string fileName,         /* in */
            CultureInfo cultureInfo, /* in */
            Encoding encoding,       /* in */
            bool remoteUri,          /* in */
            out string fileNameOnly  /* out */
            )
        {
            fileNameOnly = null;

            if (fileName == null)
                return null;

            if (remoteUri)
            {
                Uri baseUri = null;
                Result error = null;

                if (Value.GetUri(
                        fileName, UriKind.Absolute, cultureInfo,
                        ref baseUri, ref error) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "FormatFileName: create error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateDataOps).Name,
                        TracePriority.Medium);
#endif

                    return null;
                }

                Uri uri = Utility.TryCombineUris(
                    baseUri, FileExtension.Signature, encoding,
                    UriComponents.AbsoluteUri, UriFormat.Unescaped,
                    UriFlags.NoSeparators, ref error);

                if (uri == null)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "FormatFileName: combine error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateDataOps).Name,
                        TracePriority.Medium);
#endif

                    return null;
                }

                fileNameOnly = uri.GetComponents(
                    UriComponents.Path, UriFormat.Unescaped);

                return uri.ToString();
            }
            else
            {
                return PrivateFormatFileName(fileName);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a local certificate signature file name from the
        /// specified base file name.
        /// </summary>
        /// <param name="fileName">
        /// The base file name.
        /// </param>
        /// <returns>
        /// The formatted file name.
        /// </returns>
        private static string PrivateFormatFileName(
            string fileName /* in */
            )
        {
            return String.Format(
                Constants.CertificateFileNameFormat, fileName,
                FileExtension.Signature);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Formats a hash-based certificate signature file name for the
        /// specified file name, supporting both local files and remote URIs,
        /// and returning the file name without any directory or URI prefix.
        /// </summary>
        /// <param name="fileName">
        /// The base file name or URI; may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing a remote URI.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when combining a remote URI.
        /// </param>
        /// <param name="bytes">
        /// The hash bytes to embed in the file name; may not be empty.
        /// </param>
        /// <param name="remoteUri">
        /// True if the file name should be treated as a remote URI.
        /// </param>
        /// <param name="fileNameOnly">
        /// Upon return, receives the file name portion only, or null.
        /// </param>
        /// <returns>
        /// The formatted file name, or null on failure.
        /// </returns>
        public static string FormatHashFileName(
            string fileName,         /* in */
            CultureInfo cultureInfo, /* in */
            Encoding encoding,       /* in */
            byte[] bytes,            /* in */
            bool remoteUri,          /* in */
            out string fileNameOnly  /* out */
            )
        {
            fileNameOnly = null;

            if ((fileName == null) ||
                (bytes == null) || (bytes.Length == 0))
            {
                return null;
            }

            string path;
            char separator;

            if (remoteUri)
            {
                Uri baseUri = null;
                Result error = null;

                if (Value.GetUri(
                        fileName, UriKind.Absolute, cultureInfo,
                        ref baseUri, ref error) != ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "FormatHashFileName: create error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateDataOps).Name,
                        TracePriority.Medium);
#endif

                    return null;
                }

                Uri uri = Utility.TryCombineUris(
                    baseUri, FileExtension.Signature, encoding,
                    UriComponents.AbsoluteUri, UriFormat.Unescaped,
                    UriFlags.NoSeparators, ref error);

                if (uri == null)
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "FormatHashFileName: combine error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateDataOps).Name,
                        TracePriority.Medium);
#endif

                    return null;
                }

                string localFileNameOnly = uri.GetComponents(
                    UriComponents.Path, UriFormat.Unescaped);

                if (localFileNameOnly != null)
                {
                    int index = localFileNameOnly.LastIndexOfAny(new char[] {
                        Characters.DirectorySeparator,
                        Characters.AltDirectorySeparator
                    });

                    if (index != Index.Invalid)
                    {
                        path = localFileNameOnly.Substring(0, index);
                        separator = localFileNameOnly[index];

                        localFileNameOnly = PrivateFormatHashFileName(
                            path, separator, bytes);

                        uri = Utility.TryCombineUris(
                            uri, localFileNameOnly, encoding,
                            UriComponents.AbsoluteUri, UriFormat.Unescaped,
                            UriFlags.RelativePath, ref error);

                        if (uri == null)
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(String.Format(
                                "FormatHashFileName: recombine 1 error = {0}",
                                Utility.FormatWrapOrNull(error)),
                                typeof(CertificateDataOps).Name,
                                TracePriority.Medium);
#endif

                            return null;
                        }

                        fileNameOnly = localFileNameOnly;

                        return uri.ToString();
                    }
                    else
                    {
                        localFileNameOnly = FormatHashFileNameOnly(bytes);

                        uri = Utility.TryCombineUris(
                            uri, localFileNameOnly, encoding,
                            UriComponents.AbsoluteUri, UriFormat.Unescaped,
                            UriFlags.RelativePath, ref error);

                        if (uri == null)
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(String.Format(
                                "FormatHashFileName: recombine 2 error = {0}",
                                Utility.FormatWrapOrNull(error)),
                                typeof(CertificateDataOps).Name,
                                TracePriority.Medium);
#endif

                            return null;
                        }

                        fileNameOnly = localFileNameOnly;

                        return uri.ToString();
                    }
                }
                else
                {
                    path = null;
                    separator = Characters.AltDirectorySeparator;
                }
            }
            else
            {
                path = Path.GetDirectoryName(fileName);
                separator = Utility.GetFirstDirectorySeparator(fileName);
            }

            return PrivateFormatHashFileName(path, separator, bytes);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a hash-based certificate signature file name from the
        /// specified directory path, separator, and hash bytes.
        /// </summary>
        /// <param name="path">
        /// The directory path portion; may be null or empty.
        /// </param>
        /// <param name="separator">
        /// The directory separator character to use after the path.
        /// </param>
        /// <param name="bytes">
        /// The hash bytes to embed in the file name.
        /// </param>
        /// <returns>
        /// The formatted file name.
        /// </returns>
        private static string PrivateFormatHashFileName(
            string path,    /* in */
            char separator, /* in */
            byte[] bytes    /* in */
            )
        {
            return String.Format(
                Constants.HashCertificateFileNameFormat,
                path, !String.IsNullOrEmpty(path) ?
                    separator.ToString() : String.Empty,
                FormatHexadecimal(bytes), FileExtension.Signature);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN || LICENSE_MANAGER || (NETWORK && CERTIFICATE_RENEWAL)
        /* CANNOT RETURN NULL */
        /// <summary>
        /// Formats a hash-based certificate signature file name, without any
        /// directory portion, from the specified hash bytes.
        /// </summary>
        /// <param name="bytes">
        /// The hash bytes to embed in the file name.
        /// </param>
        /// <returns>
        /// The formatted file name.
        /// </returns>
        public static string FormatHashFileNameOnly(
            byte[] bytes /* in */
            )
        {
            return String.Format(
                Constants.HashCertificateFileNameOnlyFormat,
                FormatHexadecimal(bytes), FileExtension.Signature);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a string using one of two format strings, depending on
        /// whether plugin data is available.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to format; may be null.
        /// </param>
        /// <param name="format1">
        /// The format string used when plugin data is available; receives
        /// the plugin type name.
        /// </param>
        /// <param name="format2">
        /// The format string used when plugin data is not available.
        /// </param>
        /// <returns>
        /// The formatted string, or null when the selected format string is
        /// null.
        /// </returns>
        public static string FormatWithPluginData( /* CORE */
            IPluginData pluginData, /* in */
            string format1,         /* in */
            string format2          /* in */
            )
        {
            if (pluginData != null)
            {
                if (format1 == null)
                    return null;

                return String.Format(format1, pluginData.TypeName);
            }

            if (format2 == null)
                return null;

            return String.Format(format2, String.Empty);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the base64 signature file name for the specified file
        /// name.
        /// </summary>
        /// <param name="fileName">
        /// The base file name; may be null or empty.
        /// </param>
        /// <returns>
        /// The formatted signature file name, or null when
        /// <paramref name="fileName" /> is null or empty.
        /// </returns>
        public static string FormatSignatureFileName( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return null;

            return String.Format(
                "{0}{1}", fileName, FileExtension.Base64Signature);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the public key tokens of the specified key pairs for
        /// display.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs to format; may be null.
        /// </param>
        /// <param name="display">
        /// True to include placeholders for empty or null input.
        /// </param>
        /// <returns>
        /// The formatted key pairs.
        /// </returns>
        public static string FormatKeyPairs( /* CORE */
            IEnumerable<IKeyPair> keyPairs, /* in */
            bool display                    /* in */
            )
        {
            StringBuilder builder = new StringBuilder();

            if (keyPairs != null)
            {
                foreach (IKeyPair keyPair in keyPairs)
                {
                    if (keyPair == null)
                        continue;

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.Append(FormatPublicKeyToken(
                        keyPair.PublicKeyToken, display,
                        false));
                }

                if (display && (builder.Length == 0))
                    builder.Append("<none>");
            }
            else if (display)
            {
                builder.Append("<null>");
            }

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY && LICENSING
        /// <summary>
        /// Formats the specified pending plugins, keyed by interpreter
        /// identifier, for display.
        /// </summary>
        /// <param name="plugins">
        /// The pending plugins keyed by interpreter identifier; may be
        /// null.
        /// </param>
        /// <param name="display">
        /// True to include placeholders for empty or null input.
        /// </param>
        /// <returns>
        /// The formatted pending plugins.
        /// </returns>
        public static string FormatPendingPlugins( /* CORE? */
            Int64PluginDictionary plugins,
            bool display
            )
        {
            StringBuilder builder = new StringBuilder();

            if (plugins != null)
            {
                foreach (KeyValuePair<long, IPlugin> pair in plugins)
                {
                    IPlugin plugin = pair.Value;

                    if (plugin == null)
                        continue;

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat("{0}: {1}", pair.Key,
                        Utility.FormatPluginAbout(plugin, true));
                }

                if (display && (builder.Length == 0))
                    builder.Append("<none>");
            }
            else if (display)
            {
                builder.Append("<null>");
            }

            return builder.ToString();
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Joins the specified key names into a single name using the key
        /// name separator, trimming separators from each part.
        /// </summary>
        /// <param name="names">
        /// The key names to join; may be null or empty.
        /// </param>
        /// <returns>
        /// The joined key name, or null when no names are supplied.
        /// </returns>
        public static string JoinKeyNames( /* CORE */
            params string[] names /* in */
            )
        {
            if ((names == null) || (names.Length == 0))
                return null;

            StringBuilder builder = new StringBuilder();

            foreach (string name in names)
            {
                if (name == null)
                    continue;

                string newName = name.Trim(Constants.KeyNameSeparator);

                if (String.IsNullOrEmpty(newName))
                    continue;

                if (builder.Length > 0)
                    builder.Append(Constants.KeyNameSeparator);

                builder.Append(newName);
            }

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Splits the specified key name into its parts using the key name
        /// separators.
        /// </summary>
        /// <param name="keyName">
        /// The key name to split; may be null.
        /// </param>
        /// <returns>
        /// The key name parts, or null when <paramref name="keyName" /> is
        /// null.
        /// </returns>
        private static string[] SplitKeyName(
            string keyName /* in */
            )
        {
            if (keyName == null)
                return null;

            return keyName.Split(
                Constants.KeyNameSeparators,
                StringSplitOptions.RemoveEmptyEntries);
        }
#endif
        #endregion
    }
}
