/*
 * CommonOps.cs --
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
using System.Text;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Registered = Zeus.Procedures.Registered;
using Obfuscated = Zeus.Procedures.Obfuscated;

namespace Zeus.Components.Private
{
    /// <summary>
    /// Provides the common helper methods shared across the Zeus plugin that
    /// support its registered and obfuscated procedure features.  This
    /// includes computing the SHA-512 registration name from a procedure's
    /// flags, arguments, and body; validating and parsing registration
    /// tokens; and verifying that a procedure is a genuine, unmodified
    /// registered procedure.
    /// </summary>
    [ObjectId("3382f1bc-90d5-4d53-abbd-3d5cc5d0ef95")]
    internal static class CommonOps
    {
        #region Private Constants
        //
        // NOTE: This is the culture that will be returned when there is no
        //       interpreter available.
        //
        /// <summary>
        /// The culture used when no interpreter is available to supply one.
        /// </summary>
        private static readonly CultureInfo DefaultCultureInfo =
            CultureInfo.InvariantCulture;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The regular expression used to validate a registered procedure
        /// name, which must be exactly 128 lowercase hexadecimal digits (a
        /// SHA-512 hash).
        /// </summary>
        private static readonly Regex registrationNameRegEx = new Regex(
            "^[0-9a-f]{128}$", RegexOptions.Compiled);

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The mask of procedure flags that participate in the registration
        /// name hash.
        /// </summary>
        private static readonly ProcedureFlags registrationFlagsMask =
            ProcedureFlags.TypeOnlyMask;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The regular expression used to strip the useless "None" value from
        /// a formatted procedure flags string.
        /// </summary>
        private static readonly Regex normalizeFlagsRegEx = new Regex(
            "(?:^|, )None(?:, |$)", RegexOptions.Compiled);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Extracts the body, saved arguments, and saved procedure flags from
        /// the supplied procedure.  When the procedure is a registered
        /// procedure, its saved arguments and flags and its inner procedure
        /// are used; when it is an obfuscated procedure, the body is decrypted
        /// before being returned.
        /// </summary>
        /// <param name="procedure">
        /// The procedure to extract data from.
        /// </param>
        /// <param name="body">
        /// Upon success, receives the procedure body (decrypted when the
        /// procedure is obfuscated).
        /// </param>
        /// <param name="arguments">
        /// Upon success, receives the saved arguments, if any.
        /// </param>
        /// <param name="procedureFlags">
        /// Upon success, receives the saved procedure flags, if any.
        /// </param>
        /// <returns>
        /// Non-zero on success; zero when the procedure is null.
        /// </returns>
        private static bool TryGetFromProcedure(
            IProcedure procedure,              /* in */
            ref string body,                   /* in: OPTIONAL */
            ref string arguments,              /* in: OPTIONAL */
            ref ProcedureFlags? procedureFlags /* in */
            )
        {
            if (procedure == null)
                return false;

            Registered registered = procedure as Registered;
            Obfuscated obfuscated = procedure as Obfuscated;

            string localArguments = null;
            ProcedureFlags? localProcedureFlags = null;

            if (registered != null)
            {
                localArguments = registered.SavedArguments;
                localProcedureFlags = registered.SavedProcedureFlags;

                procedure = registered.GetProcedure();
            }

            string localBody = procedure.Body;

            if (obfuscated != null)
                obfuscated.Transform(false, ref localBody);

            body = localBody;
            arguments = localArguments;
            procedureFlags = localProcedureFlags;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the text whose hash forms a registered procedure name from
        /// the supplied argument list.  The argument layout (with or without
        /// a separate arguments element preceding the body) is determined from
        /// the argument count, and the resolved body is returned to the
        /// caller.
        /// </summary>
        /// <param name="arguments">
        /// The full argument list of the registering command.
        /// </param>
        /// <param name="argumentIndex">
        /// The index of the first non-option argument (the arguments or body
        /// element).
        /// </param>
        /// <param name="procedureFlags">
        /// The procedure flags contributing to the hash text, if any.
        /// </param>
        /// <param name="body">
        /// Upon success, receives the resolved procedure body.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The hash text, or null on failure.
        /// </returns>
        private static string GetHashTextForRegisteredProcedureName(
            ArgumentList arguments,         /* in */
            int argumentIndex,              /* in */
            ProcedureFlags? procedureFlags, /* in */
            ref IScriptLocation body,       /* out */
            ref Result error                /* out */
            )
        {
            if (arguments == null)
            {
                error = "invalid argument list";
                return null;
            }

            int argumentCount = arguments.Count;
            IScriptLocation localBody;
            string localArguments;

            if ((argumentIndex + 2) == argumentCount)
            {
                //
                // NOTE: This branch handles the syntax:
                //
                //       <cmd> <subCmd> ?options? <arguments> <body>
                //
                //       This branch actually handles two different
                //       argument counts, i.e. with / without flags.
                //
                localBody = arguments[argumentIndex + 1];
                localArguments = arguments[argumentIndex];
            }
            else if ((argumentIndex + 1) == argumentCount)
            {
                //
                // NOTE: This branch handles the syntax:
                //
                //       <cmd> <subCmd> ?options? <body>
                //
                localBody = arguments[argumentIndex];
                localArguments = null;
            }
            else
            {
                error = "syntax is \"?options? ?arguments? body\"";
                return null;
            }

            StringBuilder builder = GetHashBuilderForRegisteredProcedure(
                localBody as Argument, localArguments, procedureFlags);

            body = localBody;

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied value has the form of a registered
        /// procedure name (exactly 128 lowercase hexadecimal digits).
        /// </summary>
        /// <param name="value">
        /// The candidate registration name to validate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero if the value is a well-formed registration name;
        /// otherwise, zero.
        /// </returns>
        private static bool LooksLikeRegisteredProcedureName(
            string value,    /* in */
            ref Result error /* out */
            )
        {
            Regex regEx = registrationNameRegEx;

            if (regEx == null)
            {
                error = "cannot validate registration name";
                return false;
            }

            if (!regEx.IsMatch(value))
            {
                error = "invalid or malformed registration name";
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the supplied procedure flags into the normalized string
        /// used when computing a registration hash, removing the useless
        /// "None" value.  Returns null when no flags are supplied and an
        /// empty string when the flags are exactly "None".
        /// </summary>
        /// <param name="procedureFlags">
        /// The procedure flags to format, or null.
        /// </param>
        /// <returns>
        /// The normalized flags string, an empty string, or null.
        /// </returns>
        private static string RegisteredProcedureFlagsToString(
            ProcedureFlags? procedureFlags /* in */
            )
        {
            if (procedureFlags == null)
                return null;

            ProcedureFlags localProcedureFlags =
                (ProcedureFlags)procedureFlags;

            if (localProcedureFlags == ProcedureFlags.None)
                return String.Empty;

            string stringValue = localProcedureFlags.ToString();

            //
            // HACK: Normalize the flags string by removing the "None"
            //       value from it, because it is useless for our code.
            //
            Regex regEx = normalizeFlagsRegEx;

            if (regEx != null)
                stringValue = regEx.Replace(stringValue, String.Empty);

            return stringValue;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Gets the culture associated with the supplied interpreter, or the
        /// default (invariant) culture when no interpreter is available.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose culture is requested, or null.
        /// </param>
        /// <returns>
        /// The interpreter's culture, or the default culture.
        /// </returns>
        public static CultureInfo GetCultureInfo(
            Interpreter interpreter /* in */
            )
        {
            if (interpreter == null)
                return DefaultCultureInfo;

            return interpreter.CultureInfo;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Normalizes the supplied procedure flags for a registered
        /// procedure.  The flags are reduced to the registration mask, the
        /// argument style is resolved to exactly one of named, positional, or
        /// default arguments, and the read-only and registered flags are
        /// added.
        /// </summary>
        /// <param name="procedureFlags">
        /// On input, the candidate procedure flags; on output, the normalized
        /// registration flags.
        /// </param>
        public static void MaskRegisteredFlags(
            ref ProcedureFlags procedureFlags /* in, out */
            )
        {
            procedureFlags &= registrationFlagsMask;

            if (Utility.HasFlags(procedureFlags,
                    ProcedureFlags.NamedArguments, true))
            {
                procedureFlags &= ~ProcedureFlags.PositionalArguments;
            }
            else if (Utility.HasFlags(procedureFlags,
                    ProcedureFlags.PositionalArguments, true))
            {
                procedureFlags &= ~ProcedureFlags.NamedArguments;
            }
            else
            {
                procedureFlags |= ProcedureFlags.DefaultArguments;
            }

            procedureFlags |= ProcedureFlags.ReadOnly;
            procedureFlags |= ProcedureFlags.Registered;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the canonical hash input for a registered procedure by
        /// concatenating its normalized flags, saved arguments, and body,
        /// each separated by a horizontal tab.  This is the exact text whose
        /// SHA-512 hash forms the registered procedure name.
        /// </summary>
        /// <param name="body">
        /// The procedure body, if any.
        /// </param>
        /// <param name="arguments">
        /// The saved arguments, if any.
        /// </param>
        /// <param name="procedureFlags">
        /// The procedure flags contributing to the hash, if any.
        /// </param>
        /// <returns>
        /// A string builder containing the hash input; never null.
        /// </returns>
        public static StringBuilder GetHashBuilderForRegisteredProcedure(
            string body,                   /* in: OPTIONAL */
            string arguments,              /* in: OPTIONAL */
            ProcedureFlags? procedureFlags /* in */
            ) /* CANNOT RETURN NULL */
        {
            StringBuilder builder = new StringBuilder();

            if (procedureFlags != null)
            {
                builder.Append(RegisteredProcedureFlagsToString(
                    procedureFlags));
            }

            if (arguments != null)
            {
                builder.Append(Characters.HorizontalTab);
                builder.Append(arguments);
            }

            if (body != null)
            {
                builder.Append(Characters.HorizontalTab);
                builder.Append(body);
            }

            return builder;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the name for a registered procedure by hashing its
        /// canonical flags, arguments, and body text with the registration
        /// hash algorithm and formatting the result as a hexadecimal string.
        /// The resolved body is also returned to the caller.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to compute the hash.
        /// </param>
        /// <param name="arguments">
        /// The full argument list of the registering command.
        /// </param>
        /// <param name="argumentIndex">
        /// The index of the first non-option argument.
        /// </param>
        /// <param name="procedureFlags">
        /// The procedure flags contributing to the name, if any.
        /// </param>
        /// <param name="name">
        /// Upon success, receives the computed registered procedure name.
        /// </param>
        /// <param name="body">
        /// Upon success, receives the resolved procedure body.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode CreateNameForRegisteredProcedure(
            Interpreter interpreter,        /* in */
            ArgumentList arguments,         /* in */
            int argumentIndex,              /* in */
            ProcedureFlags? procedureFlags, /* in */
            ref string name,                /* out */
            ref IScriptLocation body,       /* out */
            ref Result error                /* out */
            )
        {
            string text;
            IScriptLocation localBody = null;

            text = GetHashTextForRegisteredProcedureName(
                arguments, argumentIndex, procedureFlags, ref localBody,
                ref error);

            if (text == null)
                return ReturnCode.Error;

            byte[] hashValue = Utility.HashString(
                interpreter, Registered.HashAlgorithmName, text,
                EncodingType.Script, ref error);

            if (hashValue == null)
                return ReturnCode.Error;

            string localName = Utility.ToHexadecimalString(hashValue);

            if (localName == null)
            {
                error = "could not format registered procedure name";
                return ReturnCode.Error;
            }

            name = localName;
            body = localBody;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a registration value, which must be a two-element list of a
        /// procedure token and a registered procedure name.  The token is
        /// parsed as a wide integer and the name is validated as a
        /// well-formed registration name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to split and parse the registration value.
        /// </param>
        /// <param name="registration">
        /// The registration value to parse.
        /// </param>
        /// <param name="token">
        /// Upon success, receives the parsed procedure token.
        /// </param>
        /// <param name="name">
        /// Upon success, receives the registered procedure name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode ParseRegistration(
            Interpreter interpreter, /* in */
            string registration,     /* in */
            ref long token,          /* out */
            ref string name,         /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            StringList list = null;

            if (Parser.SplitList(
                    interpreter, registration, 0, Length.Invalid,
                    true, ref list, ref error) != ReturnCode.Ok)
            {
                error = "malformed registration: must be valid list";
                return ReturnCode.Error;
            }

            if (list.Count != 2)
            {
                error = "malformed registration: need two elements";
                return ReturnCode.Error;
            }

            long localToken = 0;

            if (Value.GetWideInteger2(
                    list[0], ValueFlags.AnyWideInteger,
                    interpreter.CultureInfo, ref localToken,
                    ref error) != ReturnCode.Ok)
            {
                error = String.Format(
                    "bad registration token: {0}", error);

                return ReturnCode.Error;
            }

            string localName = list[1];

            if (!LooksLikeRegisteredProcedureName(
                    Utility.TailOnly(localName), ref error))
            {
                return ReturnCode.Error;
            }

            token = localToken;
            name = localName;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the supplied procedure is a genuine, unmodified
        /// registered procedure with the given registration name.  This
        /// overload discards the procedure flags determined during
        /// verification.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to recompute the verification hash.
        /// </param>
        /// <param name="registrationName">
        /// The expected registered procedure name.
        /// </param>
        /// <param name="procedureName">
        /// The actual name of the procedure being verified.
        /// </param>
        /// <param name="procedure">
        /// The procedure to verify.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> when the procedure is a valid
        /// registered procedure; otherwise, another <see cref="ReturnCode" />
        /// value that indicates the type of failure.
        /// </returns>
        public static ReturnCode IsRegisteredProcedure(
            Interpreter interpreter,           /* in */
            string registrationName,           /* in */
            string procedureName,              /* in */
            IProcedure procedure,              /* in */
            ref Result error                   /* out */
            )
        {
            ProcedureFlags procedureFlags = ProcedureFlags.None; /* NOT USED */

            return IsRegisteredProcedure(
                interpreter, registrationName, procedureName,
                procedure, ref procedureFlags, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the supplied procedure is a genuine, unmodified
        /// registered procedure with the given registration name.  The
        /// procedure must be a registered procedure marked as registered, its
        /// name must match, and the SHA-512 hash recomputed from its current
        /// flags, arguments, and body must match the registration name;
        /// otherwise verification fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to recompute the verification hash.
        /// </param>
        /// <param name="registrationName">
        /// The expected registered procedure name.
        /// </param>
        /// <param name="procedureName">
        /// The actual name of the procedure being verified.
        /// </param>
        /// <param name="procedure">
        /// The procedure to verify.
        /// </param>
        /// <param name="procedureFlags">
        /// Upon success, receives the procedure flags determined during
        /// verification.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> when the procedure is a valid
        /// registered procedure; otherwise, another <see cref="ReturnCode" />
        /// value that indicates the type of failure.
        /// </returns>
        public static ReturnCode IsRegisteredProcedure(
            Interpreter interpreter,           /* in */
            string registrationName,           /* in */
            string procedureName,              /* in */
            IProcedure procedure,              /* in */
            ref ProcedureFlags procedureFlags, /* out */
            ref Result error                   /* out */
            )
        {
            if (procedure == null)
            {
                error = String.Format(
                    "invalid registered procedure {0}",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            if (!(procedure is Registered))
            {
                error = String.Format(
                    "procedure {0} does not support registration",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            ProcedureFlags? localProcedureFlags = procedure.Flags;

            if ((localProcedureFlags == null) || !Utility.HasFlags(
                    (ProcedureFlags)localProcedureFlags,
                    ProcedureFlags.Registered, true))
            {
                error = String.Format(
                    "procedure {0} not marked as registered",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            if (!Utility.SystemStringEquals(registrationName, procedureName))
            {
                error = String.Format(
                    "registration name does not match procedure {0}",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            string localBody = null;
            string localArguments = null;

            if (!TryGetFromProcedure(
                    procedure, ref localBody, ref localArguments,
                    ref localProcedureFlags))
            {
                error = String.Format(
                    "failed to get registered procedure {0} body",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            byte[] hashValue = Utility.HashString(
                interpreter, Registered.HashAlgorithmName,
                GetHashBuilderForRegisteredProcedure(localBody,
                localArguments, localProcedureFlags).ToString(),
                EncodingType.Script, ref error);

            if (hashValue == null)
            {
                error = String.Format(
                    "failed to hash registered procedure {0} body: {1}",
                    Utility.FormatWrapOrNull(procedureName), error);

                return ReturnCode.Error;
            }

            string hashString = Utility.ToHexadecimalString(hashValue);

            if (!Utility.SystemStringEquals(
                    hashString, Utility.TailOnly(registrationName)))
            {
                error = String.Format(
                    "mismatched registration name hash for procedure {0}",
                    Utility.FormatWrapOrNull(procedureName));

                return ReturnCode.Error;
            }

            procedureFlags = (ProcedureFlags)localProcedureFlags;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// When the supplied procedure is a registered procedure, records its
        /// saved arguments and saved procedure flags (used during
        /// pre-execution verification) and reports that it is a registered
        /// procedure.
        /// </summary>
        /// <param name="procedure">
        /// The procedure that may have its saved data set.
        /// </param>
        /// <param name="arguments">
        /// The saved arguments to record.
        /// </param>
        /// <param name="procedureFlags">
        /// The saved procedure flags to record.
        /// </param>
        /// <returns>
        /// Non-zero when the procedure is a registered procedure (and its
        /// saved data was set); otherwise, zero.
        /// </returns>
        public static bool MaybeSetSavedArgumentsAndProcedureFlags(
            IProcedure procedure,          /* in */
            string arguments,              /* in */
            ProcedureFlags? procedureFlags /* in */
            )
        {
            Registered registered = procedure as Registered;

            if (registered != null)
            {
                /* IGNORED */
                registered.MaybeSetSavedArguments(arguments);

                /* IGNORED */
                registered.MaybeSetSavedProcedureFlags(procedureFlags);

                /* THIS IS A REGISTERED PROCEDURE */
                return true;
            }

            /* THIS IS NOT A REGISTERED PROCEDURE */
            return false;
        }
        #endregion
    }
}
