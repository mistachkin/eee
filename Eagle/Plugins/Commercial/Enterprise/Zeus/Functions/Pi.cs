/*
 * Pi.cs --
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
using _Functions = Eagle._Functions;
using BBP = BaileyBorweinPlouffe;

namespace Zeus
{
    /// <summary>
    /// Implements an enhanced <c>pi</c> math function for the Zeus plugin.
    /// With no argument it delegates to the original core <c>pi</c> function
    /// (returning the constant); with a single argument it returns the
    /// hexadecimal digit of pi at that one-based position, computed by the
    /// Bailey-Borwein-Plouffe algorithm.  Installing this function temporarily
    /// renames the original so it can be restored later.
    /// </summary>
    [ObjectId("5e6e4b04-e0c8-4ad6-8ede-6efa524e20fa")]
    [FunctionFlags(FunctionFlags.Unsafe)]
    [Arguments(Arity.NullaryAndUnary)]
    [ObjectGroup("constant")]
    internal sealed class Pi : _Functions.Default
    {
        #region Public Constants
        //
        // NOTE: This is constant because the core library will almost
        //       always use the actual type name when adding it to the
        //       interpreter.
        //
        /// <summary>
        /// The base name of this math function (the lowercase type name), used
        /// when adding it to the interpreter.
        /// </summary>
        public static readonly string BaseFunctionName =
            typeof(Pi).Name.ToLowerInvariant();

        //
        // NOTE: This is the name to be used when temporarily renaming
        //       the core library pi() function to make room for this
        //       one.
        //
        /// <summary>
        /// The name under which the original core <c>pi</c> function is saved
        /// while this function takes its place.
        /// </summary>
        public static readonly string SavedFunctionName =
            String.Format("saved_{0}", BaseFunctionName);
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // HACK: This is the original (zero argument) function used to
        //       implement the "pi" math function in the interpreter
        //       associated with this function instance.  It will also
        //       be called by our IExecuteArgument implementation when
        //       zero arguments have been specified.
        //
        /// <summary>
        /// The original core <c>pi</c> function, delegated to when this
        /// function is invoked with no argument.
        /// </summary>
        private IFunction originalFunction;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
        /// <summary>
        /// Creates the function data describing this function, used when
        /// constructing and adding it to an interpreter.
        /// </summary>
        /// <param name="plugin">
        /// The plugin that owns the function.
        /// </param>
        /// <param name="clientData">
        /// The extra data to associate with the function, if any.
        /// </param>
        /// <returns>
        /// The created function data.
        /// </returns>
        private static IFunctionData CreateData(
            IPlugin plugin,        /* in */
            IClientData clientData /* in */
            )
        {
            return new FunctionData(
                BaseFunctionName, null, null, clientData,
                null, null, (int)Arity.NullaryAndUnary,
                null, FunctionFlags.None, plugin, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates this function (capturing the previously saved original
        /// function) and adds it to the interpreter, returning its token.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to add the function to.
        /// </param>
        /// <param name="plugin">
        /// The plugin that owns the function.
        /// </param>
        /// <param name="clientData">
        /// The extra data to associate with the function, if any.
        /// </param>
        /// <param name="token">
        /// On input, must be zero; on success, receives the token of the added
        /// function.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode CreateAndAddFunction(
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            IClientData clientData,  /* in */
            ref long token,          /* out */
            ref Result result        /* out */
            )
        {
            if (token != 0)
            {
                result = "cannot overwrite function token";
                return ReturnCode.Error;
            }

            long originalToken = 0;
            IFunction originalFunction = null;

            if (interpreter.GetFunction(
                    Pi.SavedFunctionName, LookupFlags.Default,
                    ref originalToken, ref originalFunction,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (interpreter.AddFunction(
                    new Pi(CreateData(plugin, clientData),
                    originalFunction), clientData, ref token,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Saves the original core <c>pi</c> function by renaming it to the
        /// saved name, making room for this function.  This is a no-op when
        /// the base function does not exist or the saved name is already in
        /// use.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose function is renamed.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode SaveOriginalFunction(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if ((interpreter.DoesFunctionExist(
                    BaseFunctionName) == ReturnCode.Ok) &&
                (interpreter.DoesFunctionExist(
                    SavedFunctionName) != ReturnCode.Ok))
            {
                if (interpreter.RenameFunction(
                        BaseFunctionName, SavedFunctionName,
                        false, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the original core <c>pi</c> function by renaming it back
        /// from the saved name.  This is a no-op when the base function still
        /// exists or the saved name is not present.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose function is renamed.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode RestoreOriginalFunction(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if ((interpreter.DoesFunctionExist(
                    BaseFunctionName) != ReturnCode.Ok) &&
                (interpreter.DoesFunctionExist(
                    SavedFunctionName) == ReturnCode.Ok))
            {
                if (interpreter.RenameFunction(
                        SavedFunctionName, BaseFunctionName,
                        false, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes this function from the interpreter and resets the supplied
        /// token to zero.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to remove the function from.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="token">
        /// On input, the token of the function to remove; on success, reset to
        /// zero.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode RemoveFunction(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref long token,          /* in, out */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (interpreter.RemoveFunction(
                    token, clientData, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            token = 0;
            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructor
        /// <summary>
        /// Constructs a new <see cref="Pi" /> function instance, capturing the
        /// original core <c>pi</c> function to delegate to for the no-argument
        /// case.
        /// </summary>
        /// <param name="functionData">
        /// The data used to create and configure the function.
        /// </param>
        /// <param name="originalFunction">
        /// The original core <c>pi</c> function to delegate to when invoked
        /// with no argument.
        /// </param>
        public Pi(
            IFunctionData functionData, /* in */
            IFunction originalFunction  /* in */
            )
            : base(functionData)
        {
            this.Flags |= Utility.GetFunctionFlags(GetType().BaseType) |
                Utility.GetFunctionFlags(this); /* HIGHLY RECOMMENDED */

            this.originalFunction = originalFunction;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IState Members
        /// <summary>
        /// Terminates this function, releasing its reference to the original
        /// function before delegating to the base implementation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the function is being terminated.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Terminate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            this.originalFunction = null;

            return base.Terminate(interpreter, clientData, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecuteArgument Members
        /// <summary>
        /// Evaluates the function.  With no argument (arity zero or one) it
        /// delegates to the original core <c>pi</c> function; with a single
        /// digit-position argument it returns the hexadecimal digit of pi at
        /// that one-based position.  Supplying more than one argument is an
        /// error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the function is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the function.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the computed function value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Execute(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Argument value,      /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (arguments == null)
            {
                error = "invalid argument list";
                return ReturnCode.Error;
            }

            int argumentCount = arguments.Count;

            if (argumentCount <= 1)
            {
                if (originalFunction == null)
                {
                    error = "missing original function";
                    return ReturnCode.Error;
                }

                return originalFunction.Execute(
                    interpreter, clientData, arguments,
                    ref value, ref error);
            }

            if (argumentCount > 2)
            {
                error = String.Format(
                    "too many arguments for math function {0}",
                    Utility.FormatWrapOrNull(base.Name));

                return ReturnCode.Error;
            }

            ReturnCode code;
            long longValue = 0;

            code = Value.GetWideInteger2(
                (IGetValue)arguments[1], ValueFlags.AnyWideInteger,
                interpreter.CultureInfo, ref longValue, ref error);

            if (code != ReturnCode.Ok)
                return code;

            try
            {
                value = BBP.GetDigit(interpreter, longValue);
            }
            catch (Exception e)
            {
                error = String.Format("caught math exception: {0}", e);
                code = ReturnCode.Error;
            }

            return code;
        }
        #endregion
    }
}
