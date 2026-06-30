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
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Kapok.Interfaces.Private;

#if TEST
using _Helpers = Eagle._Tests.Default.Helpers;
#endif

using MethodStorageCommandDictionary = System.Collections.Generic.Dictionary<
    Kapok.Components.Private.VariableMethod,
    Kapok.Interfaces.Private.IStorageCommand>;

using MethodStorageFormatDictionary = System.Collections.Generic.Dictionary<
    Kapok.Components.Private.VariableMethod,
    Kapok.Interfaces.Private.IStorageFormat>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Implements the persistent variable-storage subsystem backed by SQLite,
    /// including a value-formatting layer, a tree of storage "commands"
    /// (logical and conditional expressions over variable operations), API-key
    /// access control, and the top-level processing entry point.
    /// </summary>
    [ObjectId("5d395717-ac19-4599-bce7-fca8a2ed6298")]
    internal static class StorageOps
    {
        #region DefaultFormat Helper Class
        /// <summary>
        /// Provides the default storage format, which encodes and decodes
        /// variable values for persistence according to configurable BLOB,
        /// date/time, number, and null-handling behaviors.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("29199484-c58c-4b7f-ab1c-972c75ee2554")]
        internal sealed class DefaultFormat :
                IStorageFormat, IGetInterpreter
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this format consumes.
            /// </summary>
            public const int ParameterCount = 17;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="DefaultFormat" /> instance.
            /// </summary>
            public DefaultFormat()
            {
                interpreter = null;
                cultureInfo = null;
                blobBehavior = BlobBehavior.None;
                dateTimeBehavior = DateTimeBehavior.None;
                dateTimeKind = DateTimeKind.Unspecified;
                dateTimeFormat = null;
                numberFormat = null;
                nullValue = null;
                dbNullValue = null;
                errorValue = null;
                limit = 0;
                nested = false;
                allowNull = false;
                pairs = false;
                names = false;
                noFixup = false;
                alias = false;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Constructs a new <see cref="DefaultFormat" /> with the
            /// specified encoding and decoding behaviors.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter used for value conversions.
            /// </param>
            /// <param name="cultureInfo">
            /// The culture used for value conversions.
            /// </param>
            /// <param name="blobBehavior">
            /// The BLOB handling behavior.
            /// </param>
            /// <param name="dateTimeBehavior">
            /// The date/time handling behavior.
            /// </param>
            /// <param name="dateTimeKind">
            /// The date/time kind.
            /// </param>
            /// <param name="dateTimeFormat">
            /// The date/time format string.
            /// </param>
            /// <param name="numberFormat">
            /// The number format string.
            /// </param>
            /// <param name="nullValue">
            /// The value used to represent null.
            /// </param>
            /// <param name="dbNullValue">
            /// The value used to represent database null.
            /// </param>
            /// <param name="errorValue">
            /// The value used to represent an error.
            /// </param>
            /// <param name="limit">
            /// The maximum number of rows to return.
            /// </param>
            /// <param name="nested">
            /// Non-zero to format nested values.
            /// </param>
            /// <param name="allowNull">
            /// Non-zero to allow null values.
            /// </param>
            /// <param name="pairs">
            /// Non-zero to format results as name/value pairs.
            /// </param>
            /// <param name="names">
            /// Non-zero to include names in the result.
            /// </param>
            /// <param name="noFixup">
            /// Non-zero to skip value fix-up.
            /// </param>
            /// <param name="alias">
            /// Non-zero to use an alias for opaque values.
            /// </param>
            public DefaultFormat(
                Interpreter interpreter,           /* in */
                CultureInfo cultureInfo,           /* in */
                BlobBehavior blobBehavior,         /* in */
                DateTimeBehavior dateTimeBehavior, /* in */
                DateTimeKind dateTimeKind,         /* in */
                string dateTimeFormat,             /* in */
                string numberFormat,               /* in */
                string nullValue,                  /* in */
                string dbNullValue,                /* in */
                string errorValue,                 /* in */
                int limit,                         /* in */
                bool nested,                       /* in */
                bool allowNull,                    /* in */
                bool pairs,                        /* in */
                bool names,                        /* in */
                bool noFixup,                      /* in */
                bool alias                         /* in */
                )
                : this()
            {
                this.interpreter = interpreter;
                this.cultureInfo = cultureInfo;
                this.blobBehavior = blobBehavior;
                this.dateTimeBehavior = dateTimeBehavior;
                this.dateTimeKind = dateTimeKind;
                this.dateTimeFormat = dateTimeFormat;
                this.numberFormat = numberFormat;
                this.nullValue = nullValue;
                this.dbNullValue = dbNullValue;
                this.errorValue = errorValue;
                this.limit = limit;
                this.nested = nested;
                this.allowNull = allowNull;
                this.pairs = pairs;
                this.names = names;
                this.noFixup = noFixup;
                this.alias = alias;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IGetInterpreter Members
            /// <summary>
            /// The backing field for the <see cref="Interpreter" /> property.
            /// </summary>
            private Interpreter interpreter;
            /// <summary>
            /// Gets or sets the interpreter used for value conversions.
            /// </summary>
            public Interpreter Interpreter
            {
                get { return interpreter; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IHaveCultureInfo Members
            /// <summary>
            /// The backing field for the <see cref="CultureInfo" /> property.
            /// </summary>
            private CultureInfo cultureInfo;
            /// <summary>
            /// Gets or sets the culture used for value conversions.
            /// </summary>
            public CultureInfo CultureInfo
            {
                get { return cultureInfo; }
                set { cultureInfo = value; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IFormatValue Members
            /// <summary>
            /// The backing field for the <see cref="BlobBehavior" /> property.
            /// </summary>
            private BlobBehavior blobBehavior;
            /// <summary>
            /// Gets or sets the BLOB handling behavior.
            /// </summary>
            public BlobBehavior BlobBehavior
            {
                get { return blobBehavior; }
                set { blobBehavior = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="DateTimeBehavior" />
            /// property.
            /// </summary>
            private DateTimeBehavior dateTimeBehavior;
            /// <summary>
            /// Gets or sets the date/time handling behavior.
            /// </summary>
            public DateTimeBehavior DateTimeBehavior
            {
                get { return dateTimeBehavior; }
                set { dateTimeBehavior = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="DateTimeKind" /> property.
            /// </summary>
            private DateTimeKind dateTimeKind;
            /// <summary>
            /// Gets or sets the date/time kind.
            /// </summary>
            public DateTimeKind DateTimeKind
            {
                get { return dateTimeKind; }
                set { dateTimeKind = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="DateTimeFormat" />
            /// property.
            /// </summary>
            private string dateTimeFormat;
            /// <summary>
            /// Gets or sets the date/time format string.
            /// </summary>
            public string DateTimeFormat
            {
                get { return dateTimeFormat; }
                set { dateTimeFormat = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NumberFormat" /> property.
            /// </summary>
            private string numberFormat;
            /// <summary>
            /// Gets or sets the number format string.
            /// </summary>
            public string NumberFormat
            {
                get { return numberFormat; }
                set { numberFormat = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NullValue" /> property.
            /// </summary>
            private string nullValue;
            /// <summary>
            /// Gets or sets the value used to represent null.
            /// </summary>
            public string NullValue
            {
                get { return nullValue; }
                set { nullValue = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="DbNullValue" /> property.
            /// </summary>
            private string dbNullValue;
            /// <summary>
            /// Gets or sets the value used to represent database null.
            /// </summary>
            public string DbNullValue
            {
                get { return dbNullValue; }
                set { dbNullValue = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="ErrorValue" /> property.
            /// </summary>
            private string errorValue;
            /// <summary>
            /// Gets or sets the value used to represent an error.
            /// </summary>
            public string ErrorValue
            {
                get { return errorValue; }
                set { errorValue = value; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IFormatDataValue Members
            /// <summary>
            /// The backing field for the <see cref="Limit" /> property.
            /// </summary>
            private int limit;
            /// <summary>
            /// Gets or sets the maximum number of rows to return.
            /// </summary>
            public int Limit
            {
                get { return limit; }
                set { limit = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Nested" /> property.
            /// </summary>
            private bool nested;
            /// <summary>
            /// Gets or sets a value indicating whether nested values are
            /// formatted.
            /// </summary>
            public bool Nested
            {
                get { return nested; }
                set { nested = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Clear" /> property.
            /// </summary>
            private bool clear;
            /// <summary>
            /// Gets or sets a value indicating whether the result is cleared
            /// first.
            /// </summary>
            public bool Clear /* NOT USED */
            {
                get { return clear; }
                set { clear = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="AllowNull" /> property.
            /// </summary>
            private bool allowNull;
            /// <summary>
            /// Gets or sets a value indicating whether null values are
            /// allowed.
            /// </summary>
            public bool AllowNull
            {
                get { return allowNull; }
                set { allowNull = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Pairs" /> property.
            /// </summary>
            private bool pairs;
            /// <summary>
            /// Gets or sets a value indicating whether results are formatted
            /// as name/value pairs.
            /// </summary>
            public bool Pairs
            {
                get { return pairs; }
                set { pairs = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Names" /> property.
            /// </summary>
            private bool names;
            /// <summary>
            /// Gets or sets a value indicating whether names are included in
            /// the result.
            /// </summary>
            public bool Names
            {
                get { return names; }
                set { names = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NoFixup" /> property.
            /// </summary>
            private bool noFixup;
            /// <summary>
            /// Gets or sets a value indicating whether value fix-up is
            /// skipped.
            /// </summary>
            public bool NoFixup
            {
                get { return noFixup; }
                set { noFixup = value; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Alias" /> property.
            /// </summary>
            private bool alias;
            /// <summary>
            /// Gets or sets a value indicating whether an alias is used for
            /// opaque values.
            /// </summary>
            public bool Alias
            {
                get { return alias; }
                set { alias = value; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageFormat Members
            /// <summary>
            /// Produces a list representation of this storage format.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this storage format.
            /// </returns>
            public IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (cultureInfo != null))
                {
                    list.Add("cultureInfo", (cultureInfo != null) ?
                        cultureInfo.ToString() : null);
                }

                if (full || (blobBehavior != BlobBehavior.None))
                    list.Add("blobBehavior", blobBehavior.ToString());

                if (full || (dateTimeBehavior != DateTimeBehavior.None))
                {
                    //
                    // HACK: Work around compilation errors when compiling for
                    //       the .NET Framework 2.0:
                    //
                    //       CS0176: Static member 'DateTimeBehavior.ToString'
                    //               cannot be accessed with an instance
                    //               reference; qualify it with a type name
                    //               instead
                    //
                    //       CS0118: 'DateTimeBehavior.ToString' is a 'field'
                    //               but is used like a 'method'
                    //
                    //       The above compilation errors were caused by the
                    //       following line of commented-out code, which seems
                    //       to confuse the 2005 C# (8.0?) compiler.  It thinks
                    //       the "ToString()" method call below actually refers
                    //       to the "DateTimeBehavior.ToString" value from the
                    //       enumeration itself.  These compilation errors are
                    //       not seen when using subsequent versions of the C#
                    //       compiler.  Either this was a compiler bug that was
                    //       fixed -OR- an enhancement that allowed it to avoid
                    //       this potential ambiguity.
                    //
                    // list.Add("dateTimeBehavior", dateTimeBehavior.ToString());
                    //
                    list.Add("dateTimeBehavior",
                        String.Format("{0}", dateTimeBehavior));
                }

                if (full || (dateTimeKind != DateTimeKind.Unspecified))
                    list.Add("dateTimeKind", dateTimeKind.ToString());

                if (full || (dateTimeFormat != null))
                    list.Add("dateTimeFormat", dateTimeFormat);

                if (full || (numberFormat != null))
                    list.Add("numberFormat", numberFormat);

                if (full || (nullValue != null))
                    list.Add("nullValue", nullValue);

                if (full || (dbNullValue != null))
                    list.Add("dbNullValue", dbNullValue);

                if (full || (errorValue != null))
                    list.Add("errorValue", errorValue);

                if (full || (limit != 0))
                    list.Add("limit", limit.ToString());

                if (full || nested)
                    list.Add("nested", nested.ToString());

                if (full || clear)
                    list.Add("clear", clear.ToString());

                if (full || allowNull)
                    list.Add("allowNull", allowNull.ToString());

                if (full || pairs)
                    list.Add("pairs", pairs.ToString());

                if (full || names)
                    list.Add("names", names.ToString());

                if (full || noFixup)
                    list.Add("noFixup", noFixup.ToString());

                if (full || alias)
                    list.Add("alias", alias.ToString());

                return list;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Returns a string that represents this storage format.
            /// </summary>
            /// <returns>
            /// A string that represents this storage format.
            /// </returns>
            public override string ToString()
            {
                return ToList(false).ToString();
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region BaseCommand Helper Class
        /// <summary>
        /// Provides the abstract base class for storage commands, which
        /// represent operations (and logical/conditional expressions over
        /// them) evaluated against the storage database.
        /// </summary>
        [ObjectId("64f3d472-b90e-4520-b03f-d139782f1c70")]
        internal abstract class BaseCommand :
                IStorageCommand, IGetInterpreter
        {
            #region IGetInterpreter Members
            /// <summary>
            /// Gets the interpreter associated with this command.
            /// </summary>
            public abstract Interpreter Interpreter { get; }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes this storage command against the database.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public abstract ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this storage command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this storage command.
            /// </returns>
            public abstract IStringList ToList(
                bool full /* in */
            );
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region InterpreterCommand Helper Class
        /// <summary>
        /// Provides the abstract base class for storage commands that carry an
        /// associated interpreter.
        /// </summary>
        [ObjectId("f7f47938-072a-46e6-8307-a67e649d63e1")]
        internal abstract class InterpreterCommand : BaseCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="InterpreterCommand" /> for the
            /// specified interpreter.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            public InterpreterCommand(
                Interpreter interpreter /* in */
                )
                : base()
            {
                this.interpreter = interpreter;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IGetInterpreter Members
            /// <summary>
            /// The backing field for the <see cref="Interpreter" /> property.
            /// </summary>
            private Interpreter interpreter;
            /// <summary>
            /// Gets the interpreter associated with this command.
            /// </summary>
            public override Interpreter Interpreter
            {
                get { return interpreter; }
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region TraceCommand Helper Class
        /// <summary>
        /// Provides the abstract base class for storage commands that emit
        /// diagnostic traces around their execution.
        /// </summary>
        [ObjectId("dd057f38-aa59-44df-81bb-710c19fbf803")]
        internal abstract class TraceCommand : InterpreterCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="TraceCommand" /> for the specified
            /// interpreter.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            public TraceCommand(
                Interpreter interpreter /* in */
                )
                : base(interpreter)
            {
                // do nothing.
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes this command with diagnostic tracing around the
            /// operation.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out: NOT USED */
                )
            {
                Utility.DebugTrace(null, String.Format(
                    "Execute: {0} - {1}, connection = {2}, " +
                    "parameterNames = {3}, parameterValues = {4}, " +
                    "method = {5}, noCase = {6}, errorOnNop = {7}",
                    Utility.FormatWrapOrNull(GetType()),
                    Utility.FormatWrapOrNull(ToString()),
                    Utility.FormatWrapOrNull(connection),
                    Utility.FormatWrapOrNull(parameterNames),
                    Utility.FormatWrapOrNull(parameterValues),
                    Utility.FormatWrapOrNull(method), noCase,
                    errorOnNop), typeof(TraceCommand).Name,
                    CommandPriority |
                        TracePriority.ViaWrapperFromPlugin, 1);

                return ReturnCode.Ok;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Returns a string that represents this command.
            /// </summary>
            /// <returns>
            /// A string that represents this command.
            /// </returns>
            public override string ToString()
            {
                return ToList(false).ToString();
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ScriptCommand Helper Class
#if TEST
        /// <summary>
        /// Implements a storage command that evaluates an Eagle script (in a
        /// child interpreter) to produce its result.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("f23c0177-9d56-4101-b5c6-b47c459d0231")]
        internal sealed class ScriptCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 3;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Static Data
            //
            // HACK: Making this field static prevents any instances of this
            //       class within the AppDomain from participating in mutual
            //       (infinite?) recursion from the Execute method, meaning
            //       that only one instance of this class (i.e. within with
            //       the AppDomain) may be executing the Execute method at a
            //       time.
            //
            /// <summary>
            /// The maximum script nesting depth allowed.
            /// </summary>
            private static int scriptLevels = 0;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The child interpreter used to evaluate the script.
            /// </summary>
            private Interpreter childInterpreter;
            /// <summary>
            /// The script text to evaluate.
            /// </summary>
            private string text;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="ScriptCommand" /> with the
            /// specified script and interpreters.
            /// </summary>
            /// <param name="parentInterpreter">
            /// The parent interpreter.
            /// </param>
            /// <param name="childInterpreter">
            /// The child interpreter used to evaluate the script.
            /// </param>
            /// <param name="text">
            /// The script text to evaluate.
            /// </param>
            public ScriptCommand(
                Interpreter parentInterpreter, /* in */
                Interpreter childInterpreter,  /* in */
                string text                    /* in */
                )
                : base(parentInterpreter)
            {
                this.childInterpreter = childInterpreter;
                this.text = text;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Methods
            /// <summary>
            /// Gets the parent interpreter for this command.
            /// </summary>
            /// <returns>
            /// The parent interpreter.
            /// </returns>
            private Interpreter GetParentInterpreter()
            {
                Interpreter parentInterpreter = base.Interpreter;

                if (parentInterpreter != null)
                {
                    if (parentInterpreter.IsSafe())
                        return parentInterpreter;

                    if (childInterpreter == null)
                        return parentInterpreter;

                    if (!childInterpreter.IsSafe())
                        return parentInterpreter;
                }

                return null;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the script and uses its result as the command result.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                int levels = Interlocked.Increment(ref scriptLevels);

                try
                {
                    if (levels == 1)
                    {
                        ObjectDictionary objects = new ObjectDictionary();

                        objects.Add("className", GetType().ToString());
                        objects.Add("methodName", "Execute");
                        objects.Add("parentInterpreter", GetParentInterpreter());
                        objects.Add("childInterpreter", childInterpreter);
                        objects.Add("scriptText", text);
                        objects.Add("connection", connection);
                        objects.Add("storageCommand", this);
                        objects.Add("storageFormat", format);
                        objects.Add("parameterNames", parameterNames);
                        objects.Add("parameterValues", parameterValues);
                        objects.Add("variableMethod", method);
                        objects.Add("noCase", noCase);
                        objects.Add("errorOnNop", errorOnNop);

                        return _Helpers.EvaluateScript(
                            childInterpreter, text, objects, ref result);
                    }
                    else
                    {
                        result = "cannot handle script, already pending";
                        return ReturnCode.Error;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref scriptLevels);
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this script command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this script command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (childInterpreter != null))
                {
                    list.Add("childInterpreter", (childInterpreter != null) ?
                        childInterpreter.IdNoThrow.ToString() : null);
                }

                if (full || (text != null))
                    list.Add("text", text);

                return list;
            }
            #endregion
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region UnaryCommand Helper Class
        /// <summary>
        /// Provides the base class for storage commands that operate on a
        /// single operand command.
        /// </summary>
        [ObjectId("4212bcf0-b8db-4434-b831-aef7ad645eca")]
        internal class UnaryCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 2;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The operand command.
            /// </summary>
            protected IStorageCommand valueCommand;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="UnaryCommand" /> wrapping the
            /// specified operand.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="valueCommand">
            /// The operand command.
            /// </param>
            public UnaryCommand(
                Interpreter interpreter,     /* in */
                IStorageCommand valueCommand /* in */
                )
                : base(interpreter)
            {
                this.valueCommand = valueCommand;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the operand command (the base unary behavior).
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                return base.Execute(connection, format,
                    parameterNames, parameterValues,
                    method, noCase, errorOnNop,
                    ref result);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this unary command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this unary command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (valueCommand != null))
                    list.Add("valueCommand", valueCommand.ToString());

                return list;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region NotCommand Helper Class
        /// <summary>
        /// Implements the logical NOT storage command, inverting the success
        /// of its operand command.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("605e1905-ca51-4465-a9f1-277d827e34e6")]
        internal sealed class NotCommand : UnaryCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="NotCommand" /> wrapping the
            /// specified operand.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="valueCommand">
            /// The operand command to negate.
            /// </param>
            public NotCommand(
                Interpreter interpreter,     /* in */
                IStorageCommand valueCommand /* in */
                )
                : base(interpreter, valueCommand)
            {
                // do nothing.
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the operand and inverts its success result.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? value = null; /* TODO: Good default? */
                Result localResult; /* REUSED */
                string text; /* REUSED */

                if (valueCommand != null)
                {
                    localResult = null;

                    if (valueCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    bool boolValue = false;

                    text = localResult;
                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    value = boolValue;
                }

                if (value != null)
                    result = !(bool)value;
                else
                    result = null;

                return ReturnCode.Ok;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region BinaryCommand Helper Class
        /// <summary>
        /// Provides the base class for storage commands that combine a left
        /// and right operand command.
        /// </summary>
        [ObjectId("13f82caa-d6c9-4856-b236-eadae1054e4d")]
        internal class BinaryCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 4;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// Non-zero to short-circuit evaluation of the right operand.
            /// </summary>
            protected bool shortCircuit;
            /// <summary>
            /// The left operand command.
            /// </summary>
            protected IStorageCommand leftCommand;
            /// <summary>
            /// The right operand command.
            /// </summary>
            protected IStorageCommand rightCommand;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="BinaryCommand" /> combining the
            /// specified operands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="shortCircuit">
            /// Non-zero to short-circuit evaluation.
            /// </param>
            /// <param name="leftCommand">
            /// The left operand command.
            /// </param>
            /// <param name="rightCommand">
            /// The right operand command.
            /// </param>
            public BinaryCommand(
                Interpreter interpreter,     /* in */
                bool shortCircuit,           /* in */
                IStorageCommand leftCommand, /* in */
                IStorageCommand rightCommand /* in */
                )
                : base(interpreter)
            {
                this.shortCircuit = shortCircuit;
                this.leftCommand = leftCommand;
                this.rightCommand = rightCommand;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the left and right operand commands (the base binary
            /// behavior).
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                return base.Execute(connection, format,
                    parameterNames, parameterValues,
                    method, noCase, errorOnNop,
                    ref result);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this binary command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this binary command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || shortCircuit)
                    list.Add("shortCircuit", shortCircuit.ToString());

                if (full || (leftCommand != null))
                    list.Add("leftCommand", leftCommand.ToString());

                if (full || (rightCommand != null))
                    list.Add("rightCommand", rightCommand.ToString());

                return list;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region AndCommand Helper Class
        /// <summary>
        /// Implements the logical AND storage command, succeeding only when
        /// both operand commands succeed.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("e5e16586-3bbc-4594-a884-5ad6c8f533e5")]
        internal sealed class AndCommand : BinaryCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="AndCommand" /> combining the
            /// specified operands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="shortCircuit">
            /// Non-zero to short-circuit when the left operand fails.
            /// </param>
            /// <param name="leftCommand">
            /// The left operand command.
            /// </param>
            /// <param name="rightCommand">
            /// The right operand command.
            /// </param>
            public AndCommand(
                Interpreter interpreter,     /* in */
                bool shortCircuit,           /* in */
                IStorageCommand leftCommand, /* in */
                IStorageCommand rightCommand /* in */
                )
                : base(interpreter, shortCircuit, leftCommand, rightCommand)
            {
                // do nothing.
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the operands with logical AND semantics, optionally
            /// short-circuiting when the left operand fails.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? leftValue = null; /* TODO: Good default? */
                bool boolValue; /* REUSED */
                Result localResult; /* REUSED */
                string text; /* REUSED */

                if (leftCommand != null)
                {
                    localResult = null;

                    if (leftCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    text = localResult;
                    boolValue = false;
                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    leftValue = boolValue;
                }

                if (shortCircuit &&
                    (leftValue != null) && !(bool)leftValue)
                {
                    result = leftValue;
                    return ReturnCode.Ok;
                }

                bool? rightValue = null; /* TODO: Good default? */

                if (rightCommand != null)
                {
                    localResult = null;

                    if (rightCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    text = localResult;
                    boolValue = false;
                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    rightValue = boolValue;
                }

                if ((leftValue != null) && (rightValue != null))
                    result = (bool)leftValue && (bool)rightValue;
                else
                    result = null;

                return ReturnCode.Ok;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region OrCommand Helper Class
        /// <summary>
        /// Implements the logical OR storage command, succeeding when either
        /// operand command succeeds.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("3e2d8b2c-7474-44c0-8ff9-789af63a6e44")]
        internal sealed class OrCommand : BinaryCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="OrCommand" /> combining the
            /// specified operands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="shortCircuit">
            /// Non-zero to short-circuit when the left operand succeeds.
            /// </param>
            /// <param name="leftCommand">
            /// The left operand command.
            /// </param>
            /// <param name="rightCommand">
            /// The right operand command.
            /// </param>
            public OrCommand(
                Interpreter interpreter,     /* in */
                bool shortCircuit,           /* in */
                IStorageCommand leftCommand, /* in */
                IStorageCommand rightCommand /* in */
                )
                : base(interpreter, shortCircuit, leftCommand, rightCommand)
            {
                // do nothing.
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the operands with logical OR semantics, optionally
            /// short-circuiting when the left operand succeeds.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? leftValue = null; /* TODO: Good default? */
                bool boolValue; /* REUSED */
                Result localResult; /* REUSED */
                string text; /* REUSED */

                if (leftCommand != null)
                {
                    localResult = null;

                    if (leftCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    text = localResult;
                    boolValue = false;
                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    leftValue = boolValue;
                }

                if (shortCircuit &&
                    (leftValue != null) && (bool)leftValue)
                {
                    result = leftValue;
                    return ReturnCode.Ok;
                }

                bool? rightValue = null; /* TODO: Good default? */

                if (rightCommand != null)
                {
                    localResult = null;

                    if (rightCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    text = localResult;
                    boolValue = false;
                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    rightValue = boolValue;
                }

                if ((leftValue != null) && (rightValue != null))
                    result = (bool)leftValue || (bool)rightValue;
                else
                    result = null;

                return ReturnCode.Ok;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region NopCommand Helper Class
        /// <summary>
        /// Implements a no-op storage command that returns a fixed return code
        /// and result without touching the database.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("72e44a6e-c9f5-45a5-a775-25f7c3832bad")]
        internal sealed class NopCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 3;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The fixed return code returned by this command.
            /// </summary>
            private ReturnCode returnCode;
            /// <summary>
            /// The fixed result returned by this command.
            /// </summary>
            private Result result;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="NopCommand" /> returning the
            /// specified fixed outcome.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="returnCode">
            /// The fixed return code to return.
            /// </param>
            /// <param name="result">
            /// The fixed result to return.
            /// </param>
            public NopCommand(
                Interpreter interpreter, /* in */
                ReturnCode returnCode,   /* in */
                Result result            /* in */
                )
                : base(interpreter)
            {
                this.returnCode = returnCode;
                this.result = result;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Returns the fixed return code and result without performing any
            /// database operation.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in: NOT USED */
                IStorageFormat format,    /* in: NOT USED */
                string[] parameterNames,  /* in: NOT USED */
                string[] parameterValues, /* in: NOT USED */
                VariableMethod method,    /* in: NOT USED */
                bool noCase,              /* in: NOT USED */
                bool errorOnNop,          /* in: NOT USED */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                result = this.result;
                return this.returnCode;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this no-op command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this no-op command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (returnCode != ReturnCode.Ok))
                    list.Add("returnCode", returnCode.ToString());

                if (full || (result != null))
                    list.Add("result", result);

                return list;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region MaybeValuesCommand Helper Class
        /// <summary>
        /// Implements a storage command that executes one of two sub-commands
        /// depending on whether parameter values are present.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("6f4136d6-cd69-47a3-a5e2-f8cb9097b064")]
        internal class MaybeValuesCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 4;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The execution type selecting how the database command is run.
            /// </summary>
            private DbExecuteType executeType;
            /// <summary>
            /// The sub-command executed when values are present.
            /// </summary>
            private string withValues;
            /// <summary>
            /// The sub-command executed when values are absent.
            /// </summary>
            private string withoutValues;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="MaybeValuesCommand" /> with the
            /// with-values and without-values sub-commands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="executeType">
            /// The execution type for the database command.
            /// </param>
            /// <param name="withValues">
            /// The sub-command executed when values are present.
            /// </param>
            /// <param name="withoutValues">
            /// The sub-command executed when values are absent.
            /// </param>
            public MaybeValuesCommand(
                Interpreter interpreter,   /* in */
                DbExecuteType executeType, /* in */
                string withValues,         /* in */
                string withoutValues       /* in */
                )
                : base(interpreter)
            {
                this.executeType = executeType;
                this.withValues = withValues;
                this.withoutValues = withoutValues;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the with-values or without-values sub-command
            /// depending on whether parameter values are present.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if ((withValues != null) || (withoutValues != null))
                {
                    Result error = null;

                    using (IDbCommand command = SetupDbCommand(
                            connection, withValues, withoutValues,
                            noCase, parameterNames, parameterValues,
                            ref error))
                    {
                        if (command == null)
                        {
                            result = error;
                            return ReturnCode.Error;
                        }

                        Result localResult = null;

                        if (ExecuteDbCommand(base.Interpreter,
                                command, format, executeType,
                                ref localResult) == ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }
                }
                else if (errorOnNop)
                {
                    result = "invalid command texts";
                    return ReturnCode.Error;
                }
                else
                {
                    result = null;
                    return ReturnCode.Ok;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this conditional-values
            /// command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this conditional-values command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (executeType != DbExecuteType.None))
                    list.Add("executeType", executeType.ToString());

                if (full || (withValues != null))
                    list.Add("withValues", withValues);

                if (full || (withoutValues != null))
                    list.Add("withoutValues", withoutValues);

                return list;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region MaybeWriteCommand Helper Class
        /// <summary>
        /// Implements a storage command that performs a write using one of two
        /// sub-commands depending on whether values are present.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("52e49005-96f2-43da-89d6-8ea079357ba1")]
        internal sealed class MaybeWriteCommand : MaybeValuesCommand
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="MaybeWriteCommand" /> with the
            /// with-values and without-values sub-commands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="executeType">
            /// The execution type for the database command.
            /// </param>
            /// <param name="withValues">
            /// The sub-command executed when values are present.
            /// </param>
            /// <param name="withoutValues">
            /// The sub-command executed when values are absent.
            /// </param>
            public MaybeWriteCommand(
                Interpreter interpreter,   /* in */
                DbExecuteType executeType, /* in */
                string withValues,         /* in */
                string withoutValues       /* in */
                )
                : base(interpreter, executeType, withValues, withoutValues)
            {
                // do nothing.
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the appropriate write sub-command depending on whether
            /// parameter values are present.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                //
                // HACK: Attempt to verify that something was actually
                //       written.
                //
                int intValue = 0;

                if (Value.GetInteger2(
                        (IGetValue)result, ValueFlags.AnyInteger, null,
                        ref intValue, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (intValue > 0)
                    return ReturnCode.Ok;

                result = "cannot verify data was successfully written";
                return ReturnCode.Error;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IfThenElseCommand Helper Class
        /// <summary>
        /// Implements a conditional storage command that executes a then- or
        /// else-command based on the success of an if-command.
        /// </summary>
#if OBFUSCATION
        [Obfuscation(Feature = "renaming")]
#endif
        [ObjectId("e5361538-9baf-4767-941e-21236430a64e")]
        internal sealed class IfThenElseCommand : TraceCommand
        {
            #region Public Constants
            /// <summary>
            /// The number of parameters this command consumes.
            /// </summary>
            public const int ParameterCount = 4;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The condition command.
            /// </summary>
            private IStorageCommand ifCommand;
            /// <summary>
            /// The command executed when the condition succeeds.
            /// </summary>
            private IStorageCommand thenCommand;
            /// <summary>
            /// The command executed when the condition fails.
            /// </summary>
            private IStorageCommand elseCommand;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="IfThenElseCommand" /> with the
            /// condition, then, and else commands.
            /// </summary>
            /// <param name="interpreter">
            /// The interpreter associated with the command.
            /// </param>
            /// <param name="ifCommand">
            /// The condition command.
            /// </param>
            /// <param name="thenCommand">
            /// The command executed when the condition succeeds.
            /// </param>
            /// <param name="elseCommand">
            /// The command executed when the condition fails.
            /// </param>
            public IfThenElseCommand(
                Interpreter interpreter,     /* in */
                IStorageCommand ifCommand,   /* in */
                IStorageCommand thenCommand, /* in */
                IStorageCommand elseCommand  /* in */
                )
                : base(interpreter)
            {
                this.ifCommand = ifCommand;
                this.thenCommand = thenCommand;
                this.elseCommand = elseCommand;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IStorageCommand Members
            /// <summary>
            /// Executes the condition command and then the then- or
            /// else-command based on its success.
            /// </summary>
            /// <param name="connection">
            /// The database connection to operate on.
            /// </param>
            /// <param name="format">
            /// The storage format used to encode and decode values.
            /// </param>
            /// <param name="parameterNames">
            /// The parameter names to bind.
            /// </param>
            /// <param name="parameterValues">
            /// The parameter values to bind.
            /// </param>
            /// <param name="method">
            /// The variable method (operation) to perform.
            /// </param>
            /// <param name="noCase">
            /// Non-zero for case-insensitive name matching.
            /// </param>
            /// <param name="errorOnNop">
            /// Non-zero to treat a no-op as an error.
            /// </param>
            /// <param name="result">
            /// Upon return, receives the result of the operation, or an error
            /// message describing why it failed.
            /// </param>
            /// <returns>
            /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
            /// <see cref="ReturnCode" /> value that indicates the type of
            /// failure.
            /// </returns>
            public override ReturnCode Execute(
                IDbConnection connection, /* in */
                IStorageFormat format,    /* in */
                string[] parameterNames,  /* in */
                string[] parameterValues, /* in */
                VariableMethod method,    /* in */
                bool noCase,              /* in */
                bool errorOnNop,          /* in */
                ref Result result         /* out */
                )
            {
                if (base.Execute(connection, format,
                        parameterNames, parameterValues,
                        method, noCase, errorOnNop,
                        ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                Result localResult; /* REUSED */

                if (ifCommand != null)
                {
                    localResult = null;

                    if (ifCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    string text = localResult;
                    bool boolValue = false;

                    localResult = null;

                    if (Value.GetBoolean2(
                            text, ValueFlags.AnyBoolean,
                            null, ref boolValue,
                            ref localResult) != ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }

                    if (boolValue)
                        goto thenCase;
                    else
                        goto elseCase;
                }

            thenCase:

                if (thenCommand != null)
                {
                    localResult = null;

                    if (thenCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) == ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Ok;
                    }
                    else
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }
                }
                else
                {
                    result = null;
                    return ReturnCode.Ok;
                }

            elseCase:

                if (elseCommand != null)
                {
                    localResult = null;

                    if (elseCommand.Execute(connection, format,
                            parameterNames, parameterValues,
                            method, noCase, errorOnNop,
                            ref localResult) == ReturnCode.Ok)
                    {
                        result = localResult;
                        return ReturnCode.Ok;
                    }
                    else
                    {
                        result = localResult;
                        return ReturnCode.Error;
                    }
                }
                else
                {
                    result = null;
                    return ReturnCode.Ok;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Produces a list representation of this conditional command.
            /// </summary>
            /// <param name="full">
            /// Non-zero to include all fields; zero for the summary set.
            /// </param>
            /// <returns>
            /// A list describing this conditional command.
            /// </returns>
            public override IStringList ToList(
                bool full /* in */
                )
            {
                StringPairList list = new StringPairList();

                if (full || (ifCommand != null))
                    list.Add("ifCommand", ifCommand.ToString());

                if (full || (thenCommand != null))
                    list.Add("thenCommand", thenCommand.ToString());

                if (full || (elseCommand != null))
                    list.Add("elseCommand", elseCommand.ToString());

                return list;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region DbConnectionParameters Helper Class
        /// <summary>
        /// Holds the parameters used to open a database connection (the file
        /// name, read-only flag, provider type, and derived connection
        /// string).
        /// </summary>
        [ObjectId("52a56a4f-563a-4455-ab88-827aa36e8a19")]
        internal sealed class DbConnectionParameters :
                IDbConnectionParameters
        {
            #region Private Constants
            //
            // HACK: This is purposely not read-only.
            //
            /// <summary>
            /// The format string used to build the connection string.
            /// </summary>
            private static string ConnectionStringFormat =
                "Data Source={0};Read Only={1};";
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// The database file name.
            /// </summary>
            private string fileName;
            /// <summary>
            /// Non-zero to open the connection read-only.
            /// </summary>
            private bool readOnly;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="DbConnectionParameters" /> for the
            /// specified file.
            /// </summary>
            /// <param name="fileName">
            /// The database file name.
            /// </param>
            /// <param name="readOnly">
            /// Non-zero to open the connection read-only.
            /// </param>
            public DbConnectionParameters(
                string fileName, /* in */
                bool readOnly    /* in */
                )
            {
                this.fileName = fileName;
                this.readOnly = readOnly;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region ITypeAndName Members
            /// <summary>
            /// Gets the simple name of the database connection type.
            /// </summary>
            public string TypeName
            {
                get { return null; }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the database connection type.
            /// </summary>
            public Type Type
            {
                get { return null; }
                set { throw new NotImplementedException(); }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region ITypeAndFullName Members
            /// <summary>
            /// Gets the full name of the database connection type.
            /// </summary>
            public string TypeFullName
            {
                get { return null; }
                set { throw new NotImplementedException(); }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IDbConnectionParameters Members
            /// <summary>
            /// Gets the primary candidate database connection type name.
            /// </summary>
            public DbConnectionType DbConnectionType1
            {
                get { return DbConnectionType.SQLiteEnterprise; }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the secondary candidate database connection type name.
            /// </summary>
            public DbConnectionType DbConnectionType2
            {
                get { return DbConnectionType.SQLite; }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the primary candidate public key token.
            /// </summary>
            public byte[] PublicKeyToken1
            {
                get
                {
                    byte[] publicKeyToken = null;
                    Result error = null;

                    if (Value.GetPublicKeyToken(String.Format(
                            "0x{0}", PublicKeyToken.SQLiteEnterprise),
                            null, ref publicKeyToken,
                            ref error) != ReturnCode.Ok)
                    {
                        throw new ScriptException(error);
                    }

                    return publicKeyToken;
                }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the secondary candidate public key token.
            /// </summary>
            public byte[] PublicKeyToken2
            {
                get
                {
                    byte[] publicKeyToken = null;
                    Result error = null;

                    if (Value.GetPublicKeyToken(String.Format(
                            "0x{0}", PublicKeyToken.SQLite),
                            null, ref publicKeyToken,
                            ref error) != ReturnCode.Ok)
                    {
                        throw new ScriptException(error);
                    }

                    return publicKeyToken;
                }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the connection string built from these parameters.
            /// </summary>
            public string ConnectionString
            {
                get
                {
                    return String.Format(
                        ConnectionStringFormat, fileName, readOnly);
                }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the assembly file name of the database provider.
            /// </summary>
            public string AssemblyFileName
            {
                get { return null; }
                set { throw new NotImplementedException(); }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the value flags used when parsing connection values.
            /// </summary>
            public ValueFlags ValueFlags
            {
                get { return ValueFlags.None; }
                set { throw new NotImplementedException(); }
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// The format string used to build an API-key access query.
        /// </summary>
        private static readonly string ApiKeyFormat = "N";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The token replaced to request case-insensitive matching.
        /// </summary>
        private static readonly string NoCaseToken = "{NoCase}";
        /// <summary>
        /// The replacement applied when case-insensitive matching is
        /// requested.
        /// </summary>
        private static readonly string NoCaseReplacement = " COLLATE NOCASE";

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The trace priority used for successful operations.
        /// </summary>
        private static TracePriority OkPriority = TracePriority.MediumLow; /* EXEMPT */
        /// <summary>
        /// The trace priority used for errors.
        /// </summary>
        private static TracePriority ErrorPriority = TracePriority.MediumHigh; /* EXEMPT */
        /// <summary>
        /// The trace priority used for command text.
        /// </summary>
        private static TracePriority CommandPriority = TracePriority.Low; /* EXEMPT */
        /// <summary>
        /// The trace priority used for operation detail.
        /// </summary>
        private static TracePriority DetailPriority = TracePriority.Lower; /* EXEMPT */
        /// <summary>
        /// The trace priority used for failures.
        /// </summary>
        private static TracePriority FailPriority = TracePriority.Highest; /* EXEMPT */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the command and format
        /// tables.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The registered storage commands, keyed by variable method.
        /// </summary>
        private static MethodStorageCommandDictionary commands;
        /// <summary>
        /// The registered storage formats, keyed by variable method.
        /// </summary>
        private static MethodStorageFormatDictionary formats;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Initialization Support Methods
        /// <summary>
        /// Initializes the storage commands and formats.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to create the commands and formats.
        /// </param>
        /// <param name="force">
        /// Non-zero to force re-initialization.
        /// </param>
        private static void Initialize(
            Interpreter interpreter, /* in */
            bool force               /* in */
            )
        {
            InitializeCommands(interpreter, force);
            InitializeStorageFormats(interpreter, force);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the variable methods supported by the storage subsystem.
        /// </summary>
        /// <returns>
        /// The supported variable methods.
        /// </returns>
        private static IEnumerable<VariableMethod> GetVariableMethods()
        {
            return new VariableMethod[] {
                VariableMethod.Access, VariableMethod.Exist,
                VariableMethod.Count, VariableMethod.Names,
                VariableMethod.Values, VariableMethod.All,
                VariableMethod.Get, VariableMethod.Set,
                VariableMethod.Unset, VariableMethod.Purge
            };
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Storage Support Methods
        /// <summary>
        /// Converts an empty value to null.
        /// </summary>
        /// <param name="value">
        /// The value to convert.
        /// </param>
        /// <returns>
        /// Null when the value is empty; otherwise, the value.
        /// </returns>
        private static string ChangeEmptyToNull(
            string value /* in */
            )
        {
            if (String.IsNullOrEmpty(value))
                return null;

            return value;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the error message for a command that received too few
        /// parameters.
        /// </summary>
        /// <param name="type">
        /// The command type.
        /// </param>
        /// <param name="needCount">
        /// The required parameter count.
        /// </param>
        /// <param name="haveCount">
        /// The supplied parameter count.
        /// </param>
        /// <returns>
        /// The error message.
        /// </returns>
        private static string TooFewParameters(
            Type type,     /* in */
            int needCount, /* in */
            int haveCount  /* in */
            )
        {
            return String.Format(
                "{0} requires at least {1} parameters, have {2}",
                Utility.FormatWrapOrNull(type), needCount, haveCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the error message for a command that received too many
        /// parameters.
        /// </summary>
        /// <param name="type">
        /// The command type.
        /// </param>
        /// <param name="needCount">
        /// The required parameter count.
        /// </param>
        /// <param name="haveCount">
        /// The supplied parameter count.
        /// </param>
        /// <returns>
        /// The error message.
        /// </returns>
        private static string TooManyParameters(
            Type type,     /* in */
            int needCount, /* in */
            int haveCount  /* in */
            )
        {
            return String.Format(
                "{0} requires at most {1} parameters, have {2}",
                Utility.FormatWrapOrNull(type), needCount, haveCount);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStorageCommand Support Methods
        /// <summary>
        /// Parses a storage command from its textual representation, building
        /// the corresponding command tree.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse and create the command.
        /// </param>
        /// <param name="value">
        /// The textual command to parse.
        /// </param>
        /// <param name="command">
        /// On success, receives the created command.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode CreateCommand( /* RECURSIVE */
            Interpreter interpreter,     /* in */
            string value,                /* in */
            ref IStorageCommand command, /* out */
            ref Result error             /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;

            try
            {
                if (value == null)
                {
                    error = "invalid storage command value";
                    code = ReturnCode.Error;

                    return code;
                }

                if (value.Length == 0)
                {
                    command = null;
                    return code;
                }

                StringList list = null;

                if (Parser.SplitList(
                        interpreter, value, 0, Length.Invalid, true,
                        ref list, ref error) != ReturnCode.Ok)
                {
                    code = ReturnCode.Error;
                    return code;
                }

                if (list.Count == 0)
                {
                    error = "missing type name for storage command";
                    code = ReturnCode.Error;

                    return code;
                }

                string typeName = list[0];

                if (Utility.SystemStringEquals(
                        typeName, typeof(NopCommand).Name))
                {
                    if (list.Count < NopCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(NopCommand),
                            NopCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > NopCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(NopCommand),
                            NopCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    object enumValue = Utility.TryParseEnum(
                        typeof(ReturnCode), list[1],
                        true, true, ref error);

                    if (!(enumValue is ReturnCode))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    command = new NopCommand(
                        interpreter, (ReturnCode)enumValue,
                        ChangeEmptyToNull(list[2]));
                }
#if TEST
                else if (Utility.SystemStringEquals(
                        typeName, typeof(ScriptCommand).Name))
                {
                    if (list.Count < ScriptCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(ScriptCommand),
                            ScriptCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > ScriptCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(ScriptCommand),
                            ScriptCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    Interpreter childInterpreter = null;

                    if (Value.GetInterpreter(
                            interpreter, list[1],
                            InterpreterType.Default,
                            ref childInterpreter,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    command = new ScriptCommand(
                        interpreter, childInterpreter, list[2]);
                }
#endif
                else if (Utility.SystemStringEquals(
                        typeName, typeof(MaybeValuesCommand).Name))
                {
                    if (list.Count < MaybeValuesCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(MaybeValuesCommand),
                            MaybeValuesCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > MaybeValuesCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(MaybeValuesCommand),
                            MaybeValuesCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    object enumValue = Utility.TryParseFlagsEnum(
                        null, typeof(DbExecuteType), null, list[1],
                        null, true, true, true, ref error);

                    if (!(enumValue is DbExecuteType))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    command = new MaybeValuesCommand(
                        interpreter, (DbExecuteType)enumValue,
                        ChangeEmptyToNull(list[2]),
                        ChangeEmptyToNull(list[3]));
                }
                else if (Utility.SystemStringEquals(
                        typeName, typeof(MaybeWriteCommand).Name))
                {
                    if (list.Count < MaybeWriteCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(MaybeWriteCommand),
                            MaybeWriteCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > MaybeWriteCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(MaybeWriteCommand),
                            MaybeWriteCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    object enumValue = Utility.TryParseFlagsEnum(
                        null, typeof(DbExecuteType), null, list[1],
                        null, true, true, true, ref error);

                    if (!(enumValue is DbExecuteType))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    command = new MaybeWriteCommand(
                        interpreter, (DbExecuteType)enumValue,
                        ChangeEmptyToNull(list[2]),
                        ChangeEmptyToNull(list[3]));
                }
                else if (Utility.SystemStringEquals(
                        typeName, typeof(IfThenElseCommand).Name))
                {
                    if (list.Count < IfThenElseCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(IfThenElseCommand),
                            IfThenElseCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > IfThenElseCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(IfThenElseCommand),
                            IfThenElseCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    IStorageCommand ifCommand = null;

                    if (!String.IsNullOrEmpty(list[1]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[1],
                                ref ifCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'if' command for {0}: {1}",
                                typeof(IfThenElseCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    IStorageCommand thenCommand = null;

                    if (!String.IsNullOrEmpty(list[2]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[2],
                                ref thenCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'then' command for {0}: {1}",
                                typeof(IfThenElseCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    IStorageCommand elseCommand = null;

                    if (!String.IsNullOrEmpty(list[3]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[3],
                                ref elseCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'else' command for {0}: {1}",
                                typeof(IfThenElseCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    command = new IfThenElseCommand(
                        interpreter, ifCommand, thenCommand,
                        elseCommand);
                }
                else if (Utility.SystemStringEquals(
                        typeName, typeof(NotCommand).Name))
                {
                    if (list.Count < NotCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(NotCommand),
                            NotCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > NotCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(NotCommand),
                            NotCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    IStorageCommand valueCommand = null;

                    if (!String.IsNullOrEmpty(list[1]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[1],
                                ref valueCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'value' command for {0}: {1}",
                                typeof(NotCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    command = new NotCommand(
                        interpreter, valueCommand);
                }
                else if (Utility.SystemStringEquals(
                        typeName, typeof(AndCommand).Name))
                {
                    if (list.Count < AndCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(AndCommand),
                            AndCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > AndCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(AndCommand),
                            AndCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    bool shortCircuit = false;

                    if (Value.GetBoolean2(
                            list[1], ValueFlags.AnyBoolean,
                            null, ref shortCircuit,
                            ref error) != ReturnCode.Ok)
                    {
                        error = String.Format(
                            "bad 'shortCircuit' value for {0}: {1}",
                            typeof(AndCommand),
                            Utility.FormatWrapOrNull(error));

                        code = ReturnCode.Error;
                        return code;
                    }

                    IStorageCommand leftCommand = null;

                    if (!String.IsNullOrEmpty(list[2]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[2], ref leftCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'left' command for {0}: {1}",
                                typeof(AndCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    IStorageCommand rightCommand = null;

                    if (!String.IsNullOrEmpty(list[3]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[3], ref rightCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'right' command for {0}: {1}",
                                typeof(AndCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    command = new AndCommand(
                        interpreter, shortCircuit, leftCommand,
                        rightCommand);
                }
                else if (Utility.SystemStringEquals(
                        typeName, typeof(OrCommand).Name))
                {
                    if (list.Count < OrCommand.ParameterCount)
                    {
                        error = TooFewParameters(typeof(OrCommand),
                            OrCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > OrCommand.ParameterCount)
                    {
                        error = TooManyParameters(typeof(OrCommand),
                            OrCommand.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    bool shortCircuit = false;

                    if (Value.GetBoolean2(
                            list[1], ValueFlags.AnyBoolean,
                            null, ref shortCircuit,
                            ref error) != ReturnCode.Ok)
                    {
                        error = String.Format(
                            "bad 'shortCircuit' value for {0}: {1}",
                            typeof(OrCommand),
                            Utility.FormatWrapOrNull(error));

                        code = ReturnCode.Error;
                        return code;
                    }

                    IStorageCommand leftCommand = null;

                    if (!String.IsNullOrEmpty(list[2]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[2],
                                ref leftCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'left' command for {0}: {1}",
                                typeof(OrCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    IStorageCommand rightCommand = null;

                    if (!String.IsNullOrEmpty(list[3]))
                    {
                        if (CreateCommand( /* RECURSIVE */
                                interpreter, list[3],
                                ref rightCommand,
                                ref error) != ReturnCode.Ok)
                        {
                            error = String.Format(
                                "bad 'right' command for {0}: {1}",
                                typeof(OrCommand),
                                Utility.FormatWrapOrNull(error));

                            code = ReturnCode.Error;
                            return code;
                        }
                    }

                    command = new OrCommand(
                        interpreter, shortCircuit, leftCommand,
                        rightCommand);
                }
                else if (Utility.SystemStringEquals(typeName, "Resource"))
                {
                    if (list.Count < 2)
                    {
                        error = "Resource requires at least 1 parameter";
                        code = ReturnCode.Error;

                        return code;
                    }
                    else if (list.Count > 2)
                    {
                        error = String.Format(
                            "too many parameters for \"Resource\", have {0}, need 2",
                            list.Count);
                    }

                    string text = Utility.GetResourceStreamData(
                        Assembly.GetExecutingAssembly(), /* Kapok */
                        Utility.ExpandEnvironmentVariables(list[1]),
                        null, false, ref error) as string;

                    if (text == null)
                    {
                        error = String.Format(
                            "command resource {0} does not exist",
                            Utility.FormatWrapOrNull(list[1]));

                        code = ReturnCode.Error;
                        return code;
                    }

                    code = CreateCommand( /* RECURSIVE */
                        interpreter, text, ref command, ref error);
                }
                else if (Utility.SystemStringEquals(typeName, "FileName"))
                {
                    if (list.Count < 2)
                    {
                        error = "FileName requires at least 1 parameter";
                        code = ReturnCode.Error;

                        return code;
                    }
                    else if (list.Count > 2)
                    {
                        error = String.Format(
                            "too many parameters for \"FileName\", have {0}, need 2",
                            list.Count);
                    }

                    if (!File.Exists(list[1]))
                    {
                        error = String.Format(
                            "command file {0} does not exist",
                            Utility.FormatWrapOrNull(list[1]));

                        code = ReturnCode.Error;
                        return code;
                    }

                    string text;

                    try
                    {
                        text = File.ReadAllText(
                            Utility.ExpandEnvironmentVariables(
                                list[1])); /* throw */
                    }
                    catch (Exception e)
                    {
                        error = e;
                        code = ReturnCode.Error;

                        return code;
                    }

                    code = CreateCommand( /* RECURSIVE */
                        interpreter, text, ref command, ref error);
                }
                else
                {
                    error = String.Format(
                        "unrecognized command type name {0}",
                        Utility.FormatWrapOrNull(typeName));

                    code = ReturnCode.Error;
                }

                return code;
            }
            finally
            {
                TracePriority priority = (code == ReturnCode.Ok) ?
                    OkPriority : ErrorPriority;

                priority |= TracePriority.FromPlugin;

                Utility.DebugTrace(String.Format(
                    "CreateCommand: value = {0}, " +
                    "command = {1}, code = {2}, error = {3}",
                    Utility.FormatWrapOrNull(value),
                    Utility.FormatWrapOrNull(command),
                    code, Utility.FormatWrapOrNull(error)),
                    typeof(StorageOps).Name, priority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens the database, executes the supplied command, and returns its
        /// result.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to open the database read-only.
        /// </param>
        /// <param name="command">
        /// The command to execute.
        /// </param>
        /// <param name="format">
        /// The storage format used to encode and decode values.
        /// </param>
        /// <param name="parameterNames">
        /// The parameter names to bind.
        /// </param>
        /// <param name="parameterValues">
        /// The parameter values to bind.
        /// </param>
        /// <param name="method">
        /// The variable method (operation) being performed.
        /// </param>
        /// <param name="noCase">
        /// Non-zero for case-insensitive name matching.
        /// </param>
        /// <param name="errorOnNop">
        /// Non-zero to treat a no-op as an error.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, or an error
        /// message describing why it failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ExecuteCommand(
            string fileName,          /* in */
            bool readOnly,            /* in */
            IStorageCommand command,  /* in */
            IStorageFormat format,    /* in */
            string[] parameterNames,  /* in */
            string[] parameterValues, /* in */
            VariableMethod method,    /* in */
            bool noCase,              /* in */
            bool errorOnNop,          /* in */
            ref Result result         /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;
            Result localResult = null;

            try
            {
                if (command == null)
                {
                    localResult = "invalid storage command";
                    code = ReturnCode.Error;

                    return code;
                }

                localResult = null;

                using (IDbConnection connection = SetupDbConnection(
                        fileName, readOnly, ref localResult))
                {
                    if (connection != null)
                    {
                        localResult = null;

                        code = command.Execute(
                            connection, format, parameterNames,
                            parameterValues, method, noCase,
                            errorOnNop, ref localResult);
                    }
                    else
                    {
                        code = ReturnCode.Error;
                    }

                    return code;
                }
            }
            finally
            {
                TracePriority priority = (code == ReturnCode.Ok) ?
                    OkPriority : ErrorPriority;

                priority |= TracePriority.FromPlugin;

                Utility.DebugTrace(String.Format(
                    "ExecuteCommand: fileName = {0}, " +
                    "readOnly = {1}, command = {2}, " +
                    "format = {3}, parameterNames = {4}, " +
                    "parameterValues = {5}, method = {6}, " +
                    "noCase = {7}, errorOnNop = {8}, " +
                    "code = {9}, result = {10}",
                    Utility.FormatWrapOrNull(fileName), readOnly,
                    Utility.FormatWrapOrNull(command),
                    Utility.FormatWrapOrNull(format),
                    Utility.FormatWrapOrNull(parameterNames),
                    Utility.FormatWrapOrNull(parameterValues),
                    Utility.FormatWrapOrNull(method), noCase,
                    errorOnNop, code,
                    Utility.FormatWrapOrNull(localResult)),
                    typeof(StorageOps).Name, priority);

                result = localResult;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the registered storage commands.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to create the commands.
        /// </param>
        /// <param name="force">
        /// Non-zero to force re-initialization.
        /// </param>
        private static void InitializeCommands(
            Interpreter interpreter, /* in */
            bool force               /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || (commands == null))
                {
                    if (commands == null)
                        commands = new MethodStorageCommandDictionary();

                    foreach (VariableMethod method in GetVariableMethods())
                    {
                        string value = WebStorageOps.GetCommand(method);

                        if (value == null)
                            continue;

                        IStorageCommand command = null;
                        Result error = null;

                        if (CreateCommand(
                                interpreter, value, ref command,
                                ref error) != ReturnCode.Ok)
                        {
                            Utility.DebugTrace(String.Format(
                                "InitializeCommands: method = {0}, " +
                                "force = {1}, error = {2}",
                                Utility.FormatWrapOrNull(method),
                                Utility.FormatWrapOrNull(force),
                                Utility.FormatWrapOrNull(error)),
                                typeof(StorageOps).Name,
                                ErrorPriority |
                                    TracePriority.FromPlugin);

                            continue;
                        }

                        Utility.DebugTrace(String.Format(
                            "InitializeCommands: method = {0}, " +
                            "force = {1}, command = {2}",
                            Utility.FormatWrapOrNull(method),
                            Utility.FormatWrapOrNull(force),
                            Utility.FormatWrapOrNull(command)),
                            typeof(StorageOps).Name, OkPriority |
                                TracePriority.FromPlugin);

                        commands[method] = command;
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the storage command registered for the specified variable
        /// method.
        /// </summary>
        /// <param name="method">
        /// The variable method.
        /// </param>
        /// <returns>
        /// The storage command, or null when none is registered.
        /// </returns>
        private static IStorageCommand GetCommand(
            VariableMethod method /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (commands != null)
                {
                    IStorageCommand command;

                    if (commands.TryGetValue(method, out command))
                        return command;
                }

                return null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStorageFormat Support Methods
        /// <summary>
        /// Parses a storage format from its textual representation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse and create the format.
        /// </param>
        /// <param name="value">
        /// The textual format to parse.
        /// </param>
        /// <param name="format">
        /// On success, receives the created format.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode CreateFormat(
            Interpreter interpreter,   /* in */
            string value,              /* in */
            ref IStorageFormat format, /* out */
            ref Result error           /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;

            try
            {
                if (value == null)
                {
                    error = "invalid storage format value";
                    code = ReturnCode.Error;

                    return code;
                }

                if (value.Length == 0)
                {
                    format = null;
                    return code;
                }

                StringList list = null;

                if (Parser.SplitList(
                        null, value, 0, Length.Invalid, true,
                        ref list, ref error) != ReturnCode.Ok)
                {
                    code = ReturnCode.Error;
                    return code;
                }

                if (list.Count == 0)
                {
                    error = "missing type name for storage format";
                    code = ReturnCode.Error;

                    return code;
                }

                string typeName = list[0];

                if (Utility.SystemStringEquals(
                        typeName, typeof(DefaultFormat).Name))
                {
                    if (list.Count < DefaultFormat.ParameterCount)
                    {
                        error = TooFewParameters(typeof(DefaultFormat),
                            DefaultFormat.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }
                    else if (list.Count > DefaultFormat.ParameterCount)
                    {
                        error = TooManyParameters(typeof(DefaultFormat),
                            DefaultFormat.ParameterCount, list.Count);

                        code = ReturnCode.Error;
                        return code;
                    }

                    CultureInfo cultureInfo = null;

                    if (!String.IsNullOrEmpty(list[1]))
                    {
                        try
                        {
                            cultureInfo = CultureInfo.GetCultureInfo(
                                list[1]); /* throw */
                        }
                        catch (Exception e)
                        {
                            error = e;
                            code = ReturnCode.Error;

                            return code;
                        }
                    }

                    object enumValue1 = Utility.TryParseEnum(
                        typeof(BlobBehavior), list[2],
                        true, true, ref error);

                    if (!(enumValue1 is BlobBehavior))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    object enumValue2 = Utility.TryParseEnum(
                        typeof(DateTimeBehavior), list[3],
                        true, true, ref error);

                    if (!(enumValue2 is DateTimeBehavior))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    object enumValue3 = Utility.TryParseEnum(
                        typeof(DateTimeKind), list[4],
                        true, true, ref error);

                    if (!(enumValue3 is DateTimeKind))
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    int intValue = 0;

                    if (Value.GetInteger2(
                            list[10], ValueFlags.AnyInteger,
                            null, ref intValue,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue1 = false;

                    if (Value.GetBoolean2(
                            list[11], ValueFlags.AnyBoolean,
                            null, ref boolValue1,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue2 = false;

                    if (Value.GetBoolean2(
                            list[12], ValueFlags.AnyBoolean,
                            null, ref boolValue2,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue3 = false;

                    if (Value.GetBoolean2(
                            list[13], ValueFlags.AnyBoolean,
                            null, ref boolValue3,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue4 = false;

                    if (Value.GetBoolean2(
                            list[14], ValueFlags.AnyBoolean,
                            null, ref boolValue4,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue5 = false;

                    if (Value.GetBoolean2(
                            list[15], ValueFlags.AnyBoolean,
                            null, ref boolValue5,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    bool boolValue6 = false;

                    if (Value.GetBoolean2(
                            list[16], ValueFlags.AnyBoolean,
                            null, ref boolValue6,
                            ref error) != ReturnCode.Ok)
                    {
                        code = ReturnCode.Error;
                        return code;
                    }

                    format = new DefaultFormat(
                        interpreter, cultureInfo,
                        (BlobBehavior)enumValue1,
                        (DateTimeBehavior)enumValue2,
                        (DateTimeKind)enumValue3,
                        ChangeEmptyToNull(list[5]),
                        ChangeEmptyToNull(list[6]),
                        ChangeEmptyToNull(list[7]),
                        ChangeEmptyToNull(list[8]),
                        ChangeEmptyToNull(list[9]),
                        intValue, boolValue1, boolValue2,
                        boolValue3, boolValue4, boolValue5,
                        boolValue6);
                }
                else
                {
                    error = String.Format(
                        "unrecognized format type name {0}",
                        Utility.FormatWrapOrNull(typeName));

                    code = ReturnCode.Error;
                }

                return code;
            }
            finally
            {
                TracePriority priority = (code == ReturnCode.Ok) ?
                    OkPriority : ErrorPriority;

                priority |= TracePriority.FromPlugin;

                Utility.DebugTrace(String.Format(
                    "CreateFormat: value = {0}, " +
                    "format = {1}, code = {2}, error = {3}",
                    Utility.FormatWrapOrNull(value),
                    Utility.FormatWrapOrNull(format),
                    code, Utility.FormatWrapOrNull(error)),
                    typeof(StorageOps).Name, priority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the registered storage formats.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to create the formats.
        /// </param>
        /// <param name="force">
        /// Non-zero to force re-initialization.
        /// </param>
        private static void InitializeStorageFormats(
            Interpreter interpreter, /* in */
            bool force               /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || (formats == null))
                {
                    if (formats == null)
                        formats = new MethodStorageFormatDictionary();

                    foreach (VariableMethod method in GetVariableMethods())
                    {
                        string value = WebStorageOps.GetFormat(method);

                        if (value == null)
                            continue;

                        IStorageFormat format = null;
                        Result error = null;

                        if (CreateFormat(
                                interpreter, value, ref format,
                                ref error) != ReturnCode.Ok)
                        {
                            Utility.DebugTrace(String.Format(
                                "InitializeStorageFormats: " +
                                "method = {0}, force = {1}, error = {2}",
                                Utility.FormatWrapOrNull(method),
                                Utility.FormatWrapOrNull(force),
                                Utility.FormatWrapOrNull(error)),
                                typeof(StorageOps).Name,
                                ErrorPriority |
                                    TracePriority.FromPlugin);

                            continue;
                        }

                        Utility.DebugTrace(String.Format(
                            "InitializeStorageFormats: " +
                            "method = {0}, force = {1}, format = {2}",
                            Utility.FormatWrapOrNull(method),
                            Utility.FormatWrapOrNull(force),
                            Utility.FormatWrapOrNull(format)),
                            typeof(StorageOps).Name, OkPriority |
                                TracePriority.FromPlugin);

                        formats[method] = format;
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the storage format registered for the specified variable
        /// method.
        /// </summary>
        /// <param name="method">
        /// The variable method.
        /// </param>
        /// <returns>
        /// The storage format, or null when none is registered.
        /// </returns>
        private static IStorageFormat GetFormat(
            VariableMethod method /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (formats != null)
                {
                    IStorageFormat format;

                    if (formats.TryGetValue(method, out format))
                        return format;
                }

                return null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region VariableMethod Support Methods
        /// <summary>
        /// Determines whether the specified variable method is read-only.
        /// </summary>
        /// <param name="method">
        /// The variable method.
        /// </param>
        /// <returns>
        /// Non-zero when the method is read-only; otherwise, zero.
        /// </returns>
        private static bool IsReadOnly(
            VariableMethod method /* in */
            )
        {
            switch (method)
            {
                case VariableMethod.Set:
                case VariableMethod.Unset:
                case VariableMethod.Purge:
                    return false;
                default:
                    return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the database command parameter names and values for the
        /// specified operation.
        /// </summary>
        /// <param name="method">
        /// The variable method (operation).
        /// </param>
        /// <param name="varName">
        /// The variable name.
        /// </param>
        /// <param name="varValue">
        /// The variable value.
        /// </param>
        /// <param name="pattern">
        /// The glob pattern for name matching.
        /// </param>
        /// <param name="noCase">
        /// Non-zero for case-insensitive matching.
        /// </param>
        /// <param name="parameterNames">
        /// On output, receives the parameter names.
        /// </param>
        /// <param name="parameterValues">
        /// On output, receives the parameter values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode GetDbCommandParameterNamesAndValues(
            VariableMethod method,        /* in */
            string varName,               /* in */
            string varValue,              /* in */
            string pattern,               /* in */
            bool noCase,                  /* in */
            ref string[] parameterNames,  /* out */
            ref string[] parameterValues, /* out */
            ref Result error              /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;

            try
            {
                string localVarName = varName;
                string localPattern = pattern;

                if (noCase)
                {
                    if (localVarName != null)
                        localVarName = localVarName.ToLowerInvariant();

                    if (localPattern != null)
                        localPattern = localPattern.ToLowerInvariant();
                }

                switch (method)
                {
                    case VariableMethod.Exist:
                    case VariableMethod.Get:
                    case VariableMethod.Unset:
                    case VariableMethod.Purge:
                        {
                            parameterValues = new string[] {
                                localVarName
                            };

                            break;
                        }
                    case VariableMethod.Count:
                    case VariableMethod.Names:
                    case VariableMethod.Values:
                    case VariableMethod.All:
                        {
                            parameterValues = new string[] {
                                localPattern
                            };

                            break;
                        }
                    case VariableMethod.Set:
                        {
                            parameterNames = new string[] {
                                "name", "value"
                            };

                            parameterValues = new string[] {
                                localVarName, varValue
                            };

                            break;
                        }
                    default:
                        {
                            error = String.Format(
                                "unsupported variable method {0}",
                                Utility.FormatWrapOrNull(method));

                            code = ReturnCode.Error;
                            break;
                        }
                }
            }
            finally
            {
                Utility.DebugTrace(String.Format(
                    "GetDbCommandParameterNamesAndValues: " +
                    "parameterNames = {0}, parameterValues = {1}, " +
                    "code = {2}, error = {3}",
                    Utility.FormatWrapOrNull(parameterNames),
                    Utility.FormatWrapOrNull(parameterValues),
                    code, Utility.FormatWrapOrNull(error)),
                    typeof(StorageOps).Name, DetailPriority |
                        TracePriority.FromPlugin);
            }

            return code;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Data Support Methods
        /// <summary>
        /// Opens a database connection for the specified file.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to open the connection read-only.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The opened connection, or null on failure.
        /// </returns>
        private static IDbConnection SetupDbConnection(
            string fileName, /* in */
            bool readOnly,   /* in */
            ref Result error /* out */
            ) /* throw */
        {
            IDbConnection connection = null;
            DbConnectionType dbConnectionType = DbConnectionType.None;
            byte[] publicKeyToken = null;

            if (Utility.CreateDbConnection(
                    null, new DbConnectionParameters(fileName,
                    readOnly), ref connection, ref dbConnectionType,
                    ref publicKeyToken, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            if (connection != null)
                connection.Open(); /* throw */
            else
                error = "could not create database connection";

            return connection;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a database command bound with the supplied parameters,
        /// selecting the with-values or without-values command text.
        /// </summary>
        /// <param name="connection">
        /// The database connection.
        /// </param>
        /// <param name="withValuesCommandText">
        /// The command text used when values are present.
        /// </param>
        /// <param name="withoutValuesCommandText">
        /// The command text used when values are absent.
        /// </param>
        /// <param name="noCase">
        /// Non-zero for case-insensitive matching.
        /// </param>
        /// <param name="parameterNames">
        /// The parameter names to bind.
        /// </param>
        /// <param name="parameterValues">
        /// The parameter values to bind.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created database command, or null on failure.
        /// </returns>
        private static IDbCommand SetupDbCommand(
            IDbConnection connection,        /* in */
            string withValuesCommandText,    /* in */
            string withoutValuesCommandText, /* in */
            bool noCase,                     /* in */
            string[] parameterNames,         /* in */
            string[] parameterValues,        /* in */
            ref Result error                 /* out */
            ) /* throw */
        {
            if (connection == null)
            {
                error = "invalid connection";
                return null;
            }

            IDbCommand command = connection.CreateCommand();

            if (command == null) /* IMPOSSIBLE? */
            {
                error = "could not create database command";
                return null;
            }

            IDataParameterCollection parameters = command.Parameters;

            if (parameters == null) /* IMPOSSIBLE? */
            {
                command.Dispose();

                error = "database command does not support parameters";
                return null;
            }

            int count = 0;

            if (parameterValues != null)
            {
                int valueLength = parameterValues.Length;
                int nameLength;

                if (parameterNames != null)
                    nameLength = parameterNames.Length;
                else
                    nameLength = Count.Invalid;

                for (int index = 0; index < valueLength; index++)
                {
                    string parameterValue = parameterValues[index];

                    if (parameterValue == null)
                        continue;

                    string parameterName;

                    if (index < nameLength)
                        parameterName = parameterNames[index];
                    else
                        parameterName = null;

                    IDbDataParameter parameter = command.CreateParameter();

                    parameter.DbType = DbType.String;
                    parameter.Value = parameterValue;

                    if (parameterName != null)
                        parameter.ParameterName = parameterName;

                    parameters.Add(parameter);
                    count++;
                }
            }

            string commandText = (count > 0) ?
                withValuesCommandText : withoutValuesCommandText;

            if (commandText != null)
            {
                commandText = commandText.Replace(NoCaseToken,
                    noCase ? NoCaseReplacement : String.Empty);
            }

            command.CommandText = commandText;

            return command;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Executes a database command and formats its result.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to format the result.
        /// </param>
        /// <param name="command">
        /// The database command to execute.
        /// </param>
        /// <param name="format">
        /// The storage format used to format the result.
        /// </param>
        /// <param name="executeType">
        /// The execution type selecting how the command is run.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, or an error
        /// message describing why it failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode ExecuteDbCommand(
            Interpreter interpreter,   /* in */
            IDbCommand command,        /* in */
            IStorageFormat format,     /* in */
            DbExecuteType executeType, /* in */
            ref Result result          /* out */
            )
        {
            if (command == null)
            {
                result = "invalid database command";
                return ReturnCode.Error;
            }

            if (format == null)
            {
                result = "invalid storage format";
                return ReturnCode.Error;
            }

            ReturnCode code = ReturnCode.Ok;

            try
            {
                switch (executeType)
                {
                    case DbExecuteType.NonQuery:
                        {
                            result = command.ExecuteNonQuery();
                            break;
                        }
                    case DbExecuteType.Scalar:
                        {
                            result = Utility.FixupDataValue(
                                interpreter, command.ExecuteScalar(), format);

                            break;
                        }
                    case DbExecuteType.Reader:
                        {
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                StringList list = null;
                                int count = 0;
                                Result error = null;

                                if (Utility.DataReaderToList(
                                        interpreter, reader, format, ref list,
                                        ref count, ref error) == ReturnCode.Ok)
                                {
                                    result = list;
                                }
                                else
                                {
                                    result = error;
                                    code = ReturnCode.Error;
                                }
                            }
                            break;
                        }
#if XML
                    case DbExecuteType.DataTable:
                        {
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                IDataTable dataTable = null;
                                Result error = null;

                                if (Utility.DataReaderToDataTable(
                                        interpreter, reader, format,
                                        ref dataTable,
                                        ref error) == ReturnCode.Ok)
                                {
                                    code = Utility.FixupReturnValue(
                                        interpreter, null,
                                        ObjectFlags.None, null,
                                        ObjectOptionType.Invoke, null,
                                        dataTable, true, false,
                                        ref result);
                                }
                                else
                                {
                                    result = error;
                                    code = ReturnCode.Error;
                                }
                            }
                            break;
                        }
#endif
                    default:
                        {
                            result = String.Format(
                                "unsupported execution type {0}",
                                Utility.FormatWrapOrNull(executeType));

                            code = ReturnCode.Error;
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            finally
            {
                TracePriority priority = (code == ReturnCode.Ok) ?
                    OkPriority : ErrorPriority;

                priority |= TracePriority.FromPlugin;

                Utility.DebugTrace(String.Format(
                    "ExecuteDbCommand: command = {0}, format = {1}, " +
                    "code = {2}, result = {3}", Utility.FormatWrapOrNull(
                        (command != null) ? command.CommandText : null),
                    Utility.FormatWrapOrNull(format),
                    code, Utility.FormatWrapOrNull(result)),
                    typeof(StorageOps).Name, priority);
            }

            return code;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Security Helper Methods
        /// <summary>
        /// Determines whether the supplied API key has access to the database
        /// file.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="apiKey">
        /// The API key requesting access.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when access is allowed; otherwise, zero.
        /// </returns>
        private static bool CheckAccess(
            string fileName, /* in */
            Guid apiKey,     /* in */
            ref Result error /* out */
            )
        {
            try
            {
                VariableMethod method = VariableMethod.Access;

                IStorageCommand storageCommand = GetCommand(
                    method);

                if (storageCommand == null)
                {
                    error = String.Format(
                        "bad method {0} storage command",
                        Utility.FormatWrapOrNull(method));

                    return false;
                }

                IStorageFormat storageFormat = new DefaultFormat();
                Result localError = null;

                using (IDbConnection connection = SetupDbConnection(
                        fileName, IsReadOnly(method), ref localError))
                {
                    if (connection == null)
                    {
                        error = localError;
                        return false;
                    }

                    string[] parameterValues = {
                        apiKey.ToString(ApiKeyFormat)
                    };

                    Result result = null;

                    if (storageCommand.Execute(
                            connection, storageFormat, null,
                            parameterValues, method, false,
                            true, ref result) != ReturnCode.Ok)
                    {
                        return false;
                    }

                    //
                    // HACK: The expected result of the access check
                    //       SQL is hard-coded here.
                    //
                    return Utility.SystemStringEquals(result, "1");
                }
            }
            catch (Exception e)
            {
                //
                // HACK: Avoid doing this because it may provide too
                //       much information to an attacker.
                //
                // error = e;

                Utility.DebugTrace(
                    e, typeof(StorageOps).Name,
                    FailPriority |
                        TracePriority.FromPlugin);
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the storage subsystem and checks API-key access in one
        /// step.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to initialize the subsystem.
        /// </param>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="apiKey">
        /// The API key requesting access.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when access is allowed; otherwise, zero.
        /// </returns>
        public static bool InitializeAndCheckAccess(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            Guid apiKey,             /* in */
            ref Result error         /* out */
            )
        {
            Initialize(interpreter, false);

            Result localError = null;

            if (CheckAccess(fileName, apiKey, ref localError))
                return true;

            error = (localError != null) ?
                localError : (Result)"access denied";

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Script Command Support Methods (NOT USED)
        /// <summary>
        /// Creates a typed value from its textual representation for the
        /// storage subsystem.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to create the value.
        /// </param>
        /// <param name="type">
        /// The target type.
        /// </param>
        /// <param name="value">
        /// The textual value.
        /// </param>
        /// <param name="object">
        /// On success, receives the created object.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode CreateType( /* RECURSIVE */
            Interpreter interpreter, /* in */
            StorageType type,        /* in */
            string value,            /* in */
            ref object @object,      /* out */
            ref Result error         /* out */
            )
        {
            switch (type)
            {
                case StorageType.None:
                    {
                        @object = null;
                        return ReturnCode.Ok;
                    }
                case StorageType.Command:
                    {
                        IStorageCommand command = null;

                        if (CreateCommand(
                                interpreter, value, ref command,
                                ref error) == ReturnCode.Ok)
                        {
                            @object = command;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
                    }
                case StorageType.Format:
                    {
                        IStorageFormat format = null;

                        if (CreateFormat(
                                interpreter, value, ref format,
                                ref error) == ReturnCode.Ok)
                        {
                            @object = format;
                            return ReturnCode.Ok;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
                    }
                default:
                    {
                        error = String.Format(
                            "unsupported storage type {0}",
                            Utility.FormatWrapOrNull(type));

                        return ReturnCode.Error;
                    }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Page Entry Point
        /// <summary>
        /// Processes a variable-storage request end to end: checking access,
        /// resolving the command and format, executing it against the
        /// database, and formatting the result.  This is the top-level storage
        /// entry point.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to process the request.
        /// </param>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="apiKey">
        /// The API key requesting the operation.
        /// </param>
        /// <param name="method">
        /// The variable method (operation) to perform.
        /// </param>
        /// <param name="varName">
        /// The variable name.
        /// </param>
        /// <param name="varValue">
        /// The variable value.
        /// </param>
        /// <param name="pattern">
        /// The glob pattern for name matching.
        /// </param>
        /// <param name="noCase">
        /// Non-zero for case-insensitive matching.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, or an error
        /// message describing why it failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode Process(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            Guid apiKey,             /* in */
            VariableMethod method,   /* in */
            string varName,          /* in */
            string varValue,         /* in */
            string pattern,          /* in */
            bool noCase,             /* in */
            ref Result result        /* out */
            )
        {
            Initialize(interpreter, false);

            Result error = null;

            if (!CheckAccess(fileName, apiKey, ref error))
            {
                result = (error != null) ?
                    error : (Result)"access denied";

                return ReturnCode.Error;
            }

            string[] parameterNames = null;
            string[] parameterValues = null;

            if (GetDbCommandParameterNamesAndValues(
                    method, varName, varValue, pattern, noCase,
                    ref parameterNames, ref parameterValues,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IStorageCommand storageCommand = GetCommand(
                method);

            if (storageCommand == null)
            {
                result = String.Format(
                    "bad method {0} storage command",
                    Utility.FormatWrapOrNull(method));

                return ReturnCode.Error;
            }

            IStorageFormat storageFormat = GetFormat(
                method);

            if (storageFormat == null)
            {
                result = String.Format(
                    "bad method {0} storage format",
                    Utility.FormatWrapOrNull(method));

                return ReturnCode.Error;
            }

            return ExecuteCommand(
                fileName, IsReadOnly(method), storageCommand,
                storageFormat, parameterNames, parameterValues,
                method, noCase, true, ref result);
        }
        #endregion
    }
}
