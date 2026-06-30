/*
 * LicenseManager.cs --
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
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Interfaces.Public;
using Licensing.Components.Public.Delegates;

using CertificateDictionary = System.Collections.Generic.IDictionary<
    string, string>;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Defines the public surface of the license manager, providing the
    /// methods used to create, renew, verify, and inspect the
    /// <see cref="ICertificate" /> license certificates associated with
    /// <see cref="IPlugin" /> plugins and other managed assemblies.  This
    /// interface is the contract underlying the <c>[certificate]</c> script
    /// command and extends <see cref="ILicenseManagerData" /> with the
    /// behavioral (as opposed to purely data) members of the license
    /// manager.  Most methods accept an <see cref="IPlugin" /> argument that
    /// is entirely optional and may be null; in that case the license
    /// manager operates on an arbitrary managed assembly that is not an
    /// Eagle plugin.  Operations that can fail report success with
    /// <see cref="ReturnCode.Ok" /> and surface diagnostic information via a
    /// <c>ref</c> <see cref="Result" /> parameter.
    /// </summary>
    [ObjectId("10430d1c-f3be-43b8-8fe2-232e1e5ec91b")]
    public interface ILicenseManager : ILicenseManagerData
    {
        ///////////////////////////////////////////////////////////////////////
        //
        // NOTE: For these methods, the "plugin" argument is entirely optional
        //       and may be null (i.e. you can use the LicenseManager and
        //       related functionality for managed assemblies that are not
        //       Eagle plugins).
        //
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the file-system directory used to store the license
        /// certificates for the specified <see cref="IPluginData" />,
        /// optionally creating that directory when it does not already
        /// exist.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="pluginData">
        /// The plugin whose certificate directory is being resolved.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="anyClientData">
        /// Optional caller-supplied <see cref="IAnyClientData" /> used to
        /// influence the operation.  This value may be null.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the directory when it does not already exist;
        /// otherwise, the directory is only resolved.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The fully qualified certificate directory, or null if it could
        /// not be resolved.
        /// </returns>
        string GetCertificateDirectory(
            Interpreter interpreter,
            IPluginData pluginData,
            IAnyClientData anyClientData,
            bool create,
            ref Result error
        );

        /// <summary>
        /// Selects a single certificate file name from the supplied sequence
        /// of candidate file names, used when more than one certificate file
        /// is available for a given assembly or plugin.
        /// </summary>
        /// <param name="fileNames">
        /// The sequence of candidate certificate file names to choose from.
        /// </param>
        /// <param name="anyClientData">
        /// Optional caller-supplied <see cref="IAnyClientData" /> used to
        /// influence the selection.  This value may be null.
        /// </param>
        /// <returns>
        /// The selected certificate file name, or null if none was chosen.
        /// </returns>
        string SelectCertificateFileName(
            IEnumerable<string> fileNames,
            IAnyClientData anyClientData
        );

        /// <summary>
        /// Produces human-readable information describing the certificate
        /// represented by the specified dictionary of name and value pairs,
        /// as exposed by the <c>[certificate about]</c> script command.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary of certificate name and value pairs to describe.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the descriptive information; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode AboutCertificate(
            Interpreter interpreter,
            IPlugin plugin,
            CertificateDictionary dictionary,
            ref Result result
        );

        /// <summary>
        /// Produces human-readable information describing the specified
        /// <see cref="ICertificate" />.  This is the strongly typed overload
        /// of <see cref="AboutCertificate(Interpreter, IPlugin, CertificateDictionary, ref Result)" />.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="certificate">
        /// The <see cref="ICertificate" /> to describe.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the descriptive information; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode AboutCertificate(
            Interpreter interpreter,
            IPlugin plugin,
            ICertificate certificate,
            ref Result result
        );

        /// <summary>
        /// Retrieves the <see cref="ICertificate" /> identified by the
        /// specified globally unique <see cref="Guid" /> identifier.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="id">
        /// The <see cref="Guid" /> uniquely identifying the certificate to
        /// retrieve.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the retrieved <see cref="ICertificate" />.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode GetCertificate(
            Interpreter interpreter,
            IPlugin plugin,
            Guid id,
            ref ICertificate certificate,
            ref Result result
        );

        /// <summary>
        /// Creates or renews the <see cref="ICertificate" /> license
        /// certificate for the specified assembly or
        /// <see cref="IPlugin" />, signing it with the provided cryptographic
        /// material and populating it according to the supplied licensing
        /// parameters.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="assembly">
        /// The <see cref="Assembly" /> the certificate is being created or
        /// renewed for.
        /// </param>
        /// <param name="assemblyName">
        /// The <see cref="AssemblyName" /> of the assembly the certificate is
        /// being created or renewed for.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used when signing the certificate.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm.  This value may be null.
        /// </param>
        /// <param name="hashValue">
        /// The expected hash value for the certificate.  This value may be
        /// null.
        /// </param>
        /// <param name="encoding">
        /// The <see cref="Encoding" /> used when hashing or serializing data.
        /// </param>
        /// <param name="keyPairs">
        /// The cryptographic key pair (or pairs) used to sign the
        /// certificate.
        /// </param>
        /// <param name="anyClientData">
        /// Optional caller-supplied <see cref="IAnyClientData" /> used to
        /// influence the operation.  This value may be null.
        /// </param>
        /// <param name="features">
        /// The set of features granted by the certificate.
        /// </param>
        /// <param name="restrictions">
        /// The set of restrictions imposed by the certificate.
        /// </param>
        /// <param name="policy">
        /// The <see cref="ExecutionPolicy" /> associated with the
        /// certificate, or null for none.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> of the execution policy associated
        /// with the certificate, or null for none.
        /// </param>
        /// <param name="keyName">
        /// The name of the key used to sign the certificate.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring containing the signing key.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, for the operation, or null to use
        /// the default.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded within the assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to validate the certificate after it is created or
        /// renewed.
        /// </param>
        /// <param name="fileName">
        /// On input, the certificate file name to use; on output, receives
        /// the file name of the created or renewed certificate.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the created or renewed
        /// <see cref="ICertificate" />.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode RenewCertificate(
            Interpreter interpreter,
            Assembly assembly,
            AssemblyName assemblyName,
            IPlugin plugin,
            string hashAlgorithmName,
            byte[] hashKey,
            byte[] hashValue,
            Encoding encoding,
            object keyPairs,
            IAnyClientData anyClientData,
            string features,
            string restrictions,
            ExecutionPolicy? policy,
            PolicyType? policyType,
            string keyName,
            string keyRingName,
            int? timeout,
            bool embedded,
            bool validate,
            ref string fileName,
            ref ICertificate certificate,
            ref Result result
        );

        /// <summary>
        /// Verifies the <see cref="ICertificate" /> license certificate
        /// associated with the specified assembly or <see cref="IPlugin" />,
        /// optionally renewing it (via <paramref name="renewCallback" />)
        /// when it is missing or invalid.  This method backs the
        /// <c>[certificate verify]</c> script command.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation.
        /// </param>
        /// <param name="assembly">
        /// The <see cref="Assembly" /> whose certificate is being verified.
        /// </param>
        /// <param name="assemblyName">
        /// The <see cref="AssemblyName" /> of the assembly whose certificate
        /// is being verified.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used when verifying the
        /// certificate.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm.  This value may be null.
        /// </param>
        /// <param name="encoding">
        /// The <see cref="Encoding" /> used when hashing or serializing data.
        /// </param>
        /// <param name="keyPairs">
        /// The cryptographic key pair (or pairs) used to verify the
        /// certificate signature.
        /// </param>
        /// <param name="features">
        /// The set of features that must be granted by the certificate.
        /// </param>
        /// <param name="restrictions">
        /// The set of restrictions that must be honored by the certificate.
        /// </param>
        /// <param name="policy">
        /// The <see cref="ExecutionPolicy" /> that the certificate must
        /// satisfy, or null for none.
        /// </param>
        /// <param name="keyName">
        /// The name of the key used to verify the certificate.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring containing the verification key.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, for the operation, or null to use
        /// the default.
        /// </param>
        /// <param name="force">
        /// Non-zero to force verification even when it would otherwise be
        /// skipped.
        /// </param>
        /// <param name="embedded">
        /// Non-zero if the certificate is embedded within the assembly.
        /// </param>
        /// <param name="validate">
        /// Non-zero to perform additional validation of the certificate.
        /// </param>
        /// <param name="fileNameCallback">
        /// The <see cref="ElementSelectionCallback" /> used to select a
        /// certificate file name when more than one candidate is available.
        /// This value may be null.
        /// </param>
        /// <param name="renewCallback">
        /// The <see cref="RenewCallback" /> used to renew the certificate
        /// when it is missing or invalid.  This value may be null.
        /// </param>
        /// <param name="anyClientData">
        /// Optional caller-supplied <see cref="IAnyClientData" /> used to
        /// influence the operation.  This value may be null.
        /// </param>
        /// <param name="fileName">
        /// On input, the certificate file name to use; on output, receives
        /// the file name of the verified certificate.
        /// </param>
        /// <param name="certificate">
        /// Upon success, receives the verified <see cref="ICertificate" />.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode VerifyCertificate(
            Interpreter interpreter,
            Assembly assembly,
            AssemblyName assemblyName,
            IPlugin plugin,
            string hashAlgorithmName,
            byte[] hashKey,
            Encoding encoding,
            object keyPairs,
            string features,
            string restrictions,
            ExecutionPolicy? policy,
            string keyName,
            string keyRingName,
            int? timeout,
            bool force,
            bool embedded,
            bool validate,
            ElementSelectionCallback fileNameCallback,
            RenewCallback renewCallback,
            IAnyClientData anyClientData,
            ref string fileName,
            ref ICertificate certificate,
            ref Result result
        );

        /// <summary>
        /// Determines whether the certificate represented by the specified
        /// dictionary of name and value pairs matches the requested flag
        /// criteria, that is, whether it possesses the required flags while
        /// lacking the prohibited ones.
        /// </summary>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary of certificate name and value pairs to match
        /// against.
        /// </param>
        /// <param name="type">
        /// The <c>FlagType</c> that selects which set of flags is being
        /// matched.
        /// </param>
        /// <param name="key">
        /// The key identifying the specific flags to match.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must be absent.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// are present; otherwise, any one is sufficient.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require that all of the
        /// <paramref name="notHasFlags" /> are absent; otherwise, any one
        /// being present causes the match to fail.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enforce strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of the match; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode MatchCertificateFlags(
            IPlugin plugin,
            CertificateDictionary dictionary,
            int /* FlagType */ type,
            long key,
            string hasFlags,
            string notHasFlags,
            bool hasAll,
            bool notHasAll,
            bool strict,
            ref Result result
        );

        /// <summary>
        /// Determines whether the specified <see cref="ICertificate" />
        /// matches the requested flag criteria.  This is the strongly typed
        /// overload of
        /// <see cref="MatchCertificateFlags(IPlugin, CertificateDictionary, int, long, string, string, bool, bool, bool, ref Result)" />.
        /// </summary>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="certificate">
        /// The <see cref="ICertificate" /> to match against.
        /// </param>
        /// <param name="type">
        /// The <c>FlagType</c> that selects which set of flags is being
        /// matched.
        /// </param>
        /// <param name="key">
        /// The key identifying the specific flags to match.
        /// </param>
        /// <param name="hasFlags">
        /// The flags that must be present.
        /// </param>
        /// <param name="notHasFlags">
        /// The flags that must be absent.
        /// </param>
        /// <param name="hasAll">
        /// Non-zero to require that all of the <paramref name="hasFlags" />
        /// are present; otherwise, any one is sufficient.
        /// </param>
        /// <param name="notHasAll">
        /// Non-zero to require that all of the
        /// <paramref name="notHasFlags" /> are absent; otherwise, any one
        /// being present causes the match to fail.
        /// </param>
        /// <param name="strict">
        /// Non-zero to enforce strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of the match; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode MatchCertificateFlags(
            IPlugin plugin,
            ICertificate certificate,
            int /* FlagType */ type,
            long key,
            string hasFlags,
            string notHasFlags,
            bool hasAll,
            bool notHasAll,
            bool strict,
            ref Result result
        );

        /// <summary>
        /// Evaluates, as a script, the certificate file associated with the
        /// specified <see cref="IPlugin" /> and named license variant within
        /// the supplied <see cref="Interpreter" />.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> that provides the context for the
        /// operation and in which the certificate file is evaluated.
        /// </param>
        /// <param name="plugin">
        /// The <see cref="IPlugin" /> associated with the certificate.  This
        /// value may be null for managed assemblies that are not Eagle
        /// plugins.
        /// </param>
        /// <param name="variantName">
        /// The name of the license variant to evaluate.
        /// </param>
        /// <param name="anyClientData">
        /// Optional caller-supplied <see cref="IAnyClientData" /> used to
        /// influence the operation.  This value may be null.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the result of the evaluation; upon failure,
        /// receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a
        /// <see cref="ReturnCode" /> value indicating the type of failure.
        /// </returns>
        ReturnCode EvaluateFile(
            Interpreter interpreter,
            IPlugin plugin,
            string variantName,
            IAnyClientData anyClientData,
            ref Result result
        );
    }
}
