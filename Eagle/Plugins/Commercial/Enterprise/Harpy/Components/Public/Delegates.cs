/*
 * Delegates.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Public;

namespace Licensing.Components.Public.Delegates
{
    /// <summary>
    /// Represents a method used to renew the licensing certificate
    /// associated with an <see cref="Assembly" /> or <see cref="IPlugin" />.
    /// An implementation typically locates the existing certificate, re-signs
    /// or re-issues it using the supplied cryptographic material (hash
    /// parameters and key pairs), optionally validates the result, and writes
    /// the updated <see cref="ICertificate" /> and its associated file name
    /// back to the caller through the <paramref name="fileName" /> and
    /// <paramref name="certificate" /> reference parameters.  Diagnostic and
    /// error information is returned via the <paramref name="result" />
    /// parameter.
    /// </summary>
    /// <param name="interpreter">
    /// The <see cref="Interpreter" /> providing the context for the renewal
    /// operation.  This parameter is optional and may be null.
    /// </param>
    /// <param name="assembly">
    /// The <see cref="Assembly" /> whose certificate is being renewed.  This
    /// parameter is optional and may be null.
    /// </param>
    /// <param name="assemblyName">
    /// The <see cref="AssemblyName" /> of the assembly.  This parameter is
    /// not used.
    /// </param>
    /// <param name="plugin">
    /// The <see cref="IPlugin" /> whose certificate is being renewed.  This
    /// parameter is optional and may be null.
    /// </param>
    /// <param name="hashAlgorithmName">
    /// The name of the hash algorithm to use when signing the renewed
    /// certificate.  This parameter is optional.
    /// </param>
    /// <param name="hashKey">
    /// The key bytes used when hashing.  This parameter is optional.
    /// </param>
    /// <param name="hashValue">
    /// The precomputed hash value bytes.  This parameter is optional.
    /// </param>
    /// <param name="encoding">
    /// The <see cref="Encoding" /> to use when converting between text and
    /// bytes during the renewal operation.
    /// </param>
    /// <param name="keyPairs">
    /// The cryptographic key pair (or collection of key pairs) used to sign
    /// the renewed certificate.
    /// </param>
    /// <param name="anyClientData">
    /// Caller-supplied <see cref="IAnyClientData" /> for the operation.  This
    /// parameter is not used.
    /// </param>
    /// <param name="features">
    /// The features to be associated with the renewed certificate.  This
    /// parameter is optional.
    /// </param>
    /// <param name="restrictions">
    /// The restrictions to be associated with the renewed certificate.  This
    /// parameter is optional.
    /// </param>
    /// <param name="policy">
    /// The <see cref="ExecutionPolicy" /> to apply during renewal.  This
    /// parameter is optional.
    /// </param>
    /// <param name="policyType">
    /// The <see cref="PolicyType" /> that categorizes the execution policy to
    /// use.  This parameter is optional.
    /// </param>
    /// <param name="keyName">
    /// The name of the key to use.  This parameter is not used.
    /// </param>
    /// <param name="keyRingName">
    /// The name of the key ring containing the key.  This parameter is not
    /// used.
    /// </param>
    /// <param name="timeout">
    /// The timeout, in milliseconds, for the operation; a null value
    /// indicates that no explicit timeout is imposed.  This parameter is
    /// optional.
    /// </param>
    /// <param name="embedded">
    /// Non-zero if the certificate is embedded.  This parameter is not used.
    /// </param>
    /// <param name="validate">
    /// Non-zero if the renewed certificate should be validated before being
    /// returned to the caller.
    /// </param>
    /// <param name="fileName">
    /// On input, the file name associated with the existing certificate; on
    /// output, the file name of the renewed certificate.
    /// </param>
    /// <param name="certificate">
    /// On input, the existing <see cref="ICertificate" />; on output, the
    /// renewed certificate.
    /// </param>
    /// <param name="result">
    /// Upon return, receives the <see cref="Result" /> of the operation,
    /// including any error information.
    /// </param>
    /// <returns>
    /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code
    /// indicating why the renewal operation failed.
    /// </returns>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("749f8566-e4bd-4392-96ef-bb5ae85feb56")]
    public delegate ReturnCode RenewCallback(
        Interpreter interpreter,      /* in: OPTIONAL, May be null. */
        Assembly assembly,            /* in: OPTIONAL, May be null. */
        AssemblyName assemblyName,    /* in: NOT USED */
        IPlugin plugin,               /* in: OPTIONAL, May be null. */
        string hashAlgorithmName,     /* in: OPTIONAL */
        byte[] hashKey,               /* in: OPTIONAL */
        byte[] hashValue,             /* in: OPTIONAL */
        Encoding encoding,            /* in */
        object keyPairs,              /* in */
        IAnyClientData anyClientData, /* in: NOT USED */
        string features,              /* in: OPTIONAL */
        string restrictions,          /* in: OPTIONAL */
        ExecutionPolicy? policy,      /* in: OPTIONAL */
        PolicyType? policyType,       /* in: OPTIONAL */
        string keyName,               /* in: NOT USED */
        string keyRingName,           /* in: NOT USED */
        int? timeout,                 /* in: OPTIONAL */
        bool embedded,                /* in: NOT USED */
        bool validate,                /* in */
        ref string fileName,          /* in, out */
        ref ICertificate certificate, /* in, out */
        ref Result result             /* out */
    );
}
