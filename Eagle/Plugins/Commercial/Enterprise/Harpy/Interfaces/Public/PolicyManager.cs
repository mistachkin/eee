/*
 * PolicyManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Provides methods for evaluating licensing policies against various
    /// kinds of input (e.g. scripts, files, streams, licenses, and key
    /// pairs) and for performing related policy checks.
    /// </summary>
    [ObjectId("5fb535a9-569f-487e-a3b9-deb180fcef86")]
    public interface IPolicyManager : IPolicyManagerData
    {
        ///////////////////////////////////////////////////////////////////////
        //
        // NOTE: For these methods, the "interpreter" and "arguments"
        //       arguments are entirely optional and may be null (i.e.
        //       you can use the PolicyManager and related functionality
        //       for managed assemblies that are not Eagle plugins).
        //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the policy of the specified type against the supplied
        /// context.
        /// </summary>
        /// <param name="policyType">
        /// The kind of policy to evaluate.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode Check(
            PolicyType policyType, Interpreter interpreter,
            IClientData clientData, ArgumentList arguments,
            ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a script against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckScript(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a file against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckFile(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a stream against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckStream(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a license against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckLicense(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a key pair against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckKeyPair(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates the policy applicable to a trace against the supplied
        /// context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckTrace(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);

        /// <summary>
        /// Evaluates any other policy not covered by the more specific check
        /// methods against the supplied context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.  This value
        /// is optional and may be null.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-specific data to be made available during the
        /// policy evaluation.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the request, if any.  This value is
        /// optional and may be null.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the policy evaluation.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the outcome of the policy
        /// evaluation.
        /// </returns>
        ReturnCode CheckOther(
            Interpreter interpreter, IClientData clientData,
            ArgumentList arguments, ref Result result);
    }
}
